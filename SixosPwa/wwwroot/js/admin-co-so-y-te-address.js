document.addEventListener('DOMContentLoaded', function () {
    const provinceSelect = document.querySelector('[data-address-province]');
    const wardSelect = document.querySelector('[data-address-ward]');
    const buildingInput = document.querySelector('[data-address-building]');
    const addressResult = document.querySelector('[data-address-result]');
    const addressStatus = document.querySelector('[data-address-status]');

    if (!provinceSelect || !wardSelect || !buildingInput || !addressResult) return;

    const selectedProvince = provinceSelect.dataset.selectedValue || '';
    const selectedWard = wardSelect.dataset.selectedValue || '';
    const existingAddress = addressResult.value.trim();
    let wardRequestId = 0;

    // Đợt A bỏ cột DM_CSKCB.SoToaNha ⇒ ô "Số nhà / tên đường" không còn bind vào model,
    // nên lúc nạp trang nó LUÔN RỖNG. Trước đây vòng ghép vẫn chạy vô điều kiện sau khi
    // nạp xong phường/xã ⇒ sửa mỗi số điện thoại cũng ghi đè DiaChi thành "Phường X, TP.HCM",
    // NUỐT MẤT số nhà vĩnh viễn (ô kết quả readonly, admin không gõ lại được). Hai lớp chống:
    //   (1) Nạp lại số nhà từ chính DiaChi đang lưu (bóc đuôi "phường, tỉnh").
    //   (2) CHỈ ghi đè DiaChi khi người dùng thực sự đụng vào 1 trong 3 ô địa chỉ — nếu bóc
    //       không ra (dữ liệu cũ gõ tay, tên phường không khớp API) thì DiaChi đứng yên.
    let nguoiDungDaSuaDiaChi = false;

    const chuanHoa = (s) => (s || '').trim().toLowerCase();

    /// Bóc phần "số nhà / tên đường" ra khỏi DiaChi đang lưu, để vòng ghép dựng lại đủ.
    const napLaiSoNhaTuDiaChi = () => {
        if (!existingAddress || buildingInput.value.trim()) return;

        const ward = getSelectedOptionText(wardSelect);
        const province = getSelectedOptionText(provinceSelect);
        const duoi = [ward, province].filter(Boolean).join(', ');

        // Cách 1 — chắc nhất: DiaChi kết thúc đúng bằng "phường, tỉnh" thì phần đầu là số nhà.
        if (duoi && chuanHoa(existingAddress).endsWith(chuanHoa(duoi))) {
            const soNha = existingAddress
                .slice(0, existingAddress.length - duoi.length)
                .replace(/[\s,]+$/, '')
                .trim();
            if (soNha) buildingInput.value = soNha;
            return;
        }

        // Cách 2 — dự phòng: lấy đoạn trước dấu phẩy ĐẦU TIÊN, nhưng chỉ khi nó không
        // phải chính tên phường/tỉnh — tránh ca DiaChi vốn không có số nhà ("Phường X, TP.HCM")
        // bị nhân đôi thành "Phường X, Phường X, TP.HCM".
        const viTri = existingAddress.indexOf(',');
        if (viTri <= 0) return;
        const dau = existingAddress.slice(0, viTri).trim();
        if (!dau || chuanHoa(dau) === chuanHoa(ward) || chuanHoa(dau) === chuanHoa(province)) return;
        buildingInput.value = dau;
    };

    const setStatus = (message) => {
        if (addressStatus) addressStatus.textContent = message;
    };

    const getSelectedOptionText = (select) => {
        if (!select) return '';
        if (select.tomselect) {
            const val = select.tomselect.getValue();
            const option = select.tomselect.options[val];
            return option && option.text ? option.text.trim() : '';
        }
        const option = select.options[select.selectedIndex];
        return option && option.value ? option.textContent.trim() : '';
    };

    const updateAddress = () => {
        // Chưa ai đụng vào 3 ô địa chỉ thì KHÔNG được ghi đè DiaChi đang lưu.
        if (!nguoiDungDaSuaDiaChi) return;

        const building = buildingInput.value.trim();
        const ward = getSelectedOptionText(wardSelect);
        const province = getSelectedOptionText(provinceSelect);

        const parts = [building, ward, province].filter(Boolean);
        addressResult.value = parts.join(', ');
    };

    const requestJson = (url, timeout = 10000) => {
        const request = fetch(url, { headers: { Accept: 'application/json' } })
            .then((response) => {
                if (!response.ok) throw new Error(`HTTP ${response.status}`);
                return response.json();
            });
        const timeoutRequest = new Promise((_, reject) => {
            window.setTimeout(() => reject(new Error('Request timeout')), timeout);
        });
        return Promise.race([request, timeoutRequest]);
    };

    const syncSelect = (selectElement, items, selectedValue, placeholder, isEnabled) => {
        selectElement.innerHTML = '';
        selectElement.add(new Option(placeholder, ''));
        items.forEach((item) => {
            selectElement.add(new Option(item.text, String(item.value)));
        });
        if (selectedValue) {
            selectElement.value = String(selectedValue);
        }
        selectElement.disabled = !isEnabled;

        if (selectElement.tomselect) {
            const ts = selectElement.tomselect;
            ts.clear(true);
            ts.clearOptions();
            items.forEach((item) => {
                ts.addOption({ value: String(item.value), text: item.text });
            });
            ts.settings.placeholder = placeholder;
            if (ts.control_input) {
                ts.control_input.placeholder = placeholder;
            }
            ts.refreshOptions(false);
            if (selectedValue) {
                ts.setValue(String(selectedValue), true);
            }
            if (isEnabled) {
                ts.enable();
            } else {
                ts.disable();
            }
        } else if (window.TomSelect) {
            const ts = new TomSelect(selectElement, {
                create: false,
                allowEmptyOption: true,
                maxOptions: 500,
                placeholder: placeholder,
                sortField: { field: 'text', direction: 'asc' },
                onChange: function () {
                    selectElement.dispatchEvent(new Event('change', { bubbles: true }));
                }
            });
            if (!isEnabled) {
                ts.disable();
            }
        }
    };

    const resetWards = (placeholder = 'Chọn tỉnh/thành phố trước') => {
        syncSelect(wardSelect, [], '', placeholder, false);
    };

    const loadWards = async (provinceCode, wardCode = '') => {
        const requestId = ++wardRequestId;
        if (!provinceCode) {
            resetWards();
            updateAddress();
            return;
        }

        resetWards('Đang tải phường/xã...');
        setStatus('Đang tải danh sách phường/xã...');

        try {
            const provinceData = await requestJson(
                `https://provinces.open-api.vn/api/v2/p/${encodeURIComponent(provinceCode)}?depth=2`
            );
            if (requestId !== wardRequestId) return;

            const wards = Array.isArray(provinceData?.wards) ? provinceData.wards : [];
            const items = wards.map((w) => ({ value: w.code, text: w.name }));

            syncSelect(
                wardSelect,
                items,
                wardCode,
                items.length ? 'Chọn phường/xã' : 'Không có dữ liệu phường/xã',
                items.length > 0
            );

            updateAddress();
            setStatus(items.length ? '' : 'Tỉnh/thành này chưa có dữ liệu phường/xã.');
        } catch (error) {
            if (requestId !== wardRequestId) return;
            console.error('Không tải được phường/xã từ API v2:', error);
            resetWards('Không tải được phường/xã');
            setStatus('Không thể tải danh sách phường/xã. Kiểm tra kết nối mạng.');
        }
    };

    const loadProvinces = async () => {
        syncSelect(provinceSelect, [], '', 'Đang tải tỉnh/thành...', false);
        resetWards();
        setStatus('Đang tải danh sách tỉnh/thành...');

        try {
            const data = await requestJson('https://provinces.open-api.vn/api/v2/p/');
            const provinces = Array.isArray(data) ? data : [];
            if (!provinces.length) throw new Error('API v2 trả danh sách rỗng.');

            const items = provinces.map((p) => ({ value: p.code, text: p.name }));
            const matchedProvince = selectedProvince && provinces.some((p) => String(p.code) === String(selectedProvince));

            syncSelect(
                provinceSelect,
                items,
                matchedProvince ? selectedProvince : '',
                'Chọn tỉnh/thành phố',
                true
            );

            if (matchedProvince) {
                await loadWards(selectedProvince, selectedWard);
            }

            // Nạp xong tỉnh + phường mới bóc được số nhà (cần biết tên để cắt đuôi).
            napLaiSoNhaTuDiaChi();

            setStatus('');
        } catch (error) {
            console.error('Không tải được tỉnh/thành từ API v2:', error);
            syncSelect(provinceSelect, [], '', 'Không tải được tỉnh/thành', false);
            resetWards();
            setStatus('Không thể tải danh sách tỉnh/thành. Kiểm tra kết nối mạng.');
        }
    };

    provinceSelect.addEventListener('change', async () => {
        nguoiDungDaSuaDiaChi = true;
        const provinceCode = provinceSelect.value;
        await loadWards(provinceCode);
        updateAddress();
    });

    wardSelect.addEventListener('change', () => {
        nguoiDungDaSuaDiaChi = true;
        updateAddress();
    });

    buildingInput.addEventListener('input', () => {
        nguoiDungDaSuaDiaChi = true;
        updateAddress();
    });

    loadProvinces();
});

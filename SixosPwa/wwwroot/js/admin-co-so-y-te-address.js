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
    const preserveExistingAddress = Boolean(existingAddress && !buildingInput.value.trim() && !selectedWard);
    let wardRequestId = 0;

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
            } else if (!preserveExistingAddress) {
                updateAddress();
            }

            if (preserveExistingAddress) {
                addressResult.value = existingAddress;
            }

            setStatus('');
        } catch (error) {
            console.error('Không tải được tỉnh/thành từ API v2:', error);
            syncSelect(provinceSelect, [], '', 'Không tải được tỉnh/thành', false);
            resetWards();
            setStatus('Không thể tải danh sách tỉnh/thành. Kiểm tra kết nối mạng.');
        }
    };

    provinceSelect.addEventListener('change', async () => {
        const provinceCode = provinceSelect.value;
        await loadWards(provinceCode);
        updateAddress();
    });

    wardSelect.addEventListener('change', () => {
        updateAddress();
    });

    buildingInput.addEventListener('input', () => {
        updateAddress();
    });

    loadProvinces();
});

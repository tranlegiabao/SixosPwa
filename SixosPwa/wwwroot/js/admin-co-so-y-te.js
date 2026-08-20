document.addEventListener('DOMContentLoaded', function () {

    // =========================================================
    // 1. XỬ LÝ ẢNH
    // =========================================================
    document.querySelectorAll('[data-image-source]').forEach(function (sourceControl) {

        const toggle = sourceControl.querySelector('[data-image-toggle]');
        const panel = sourceControl.querySelector('[data-image-panel]');
        const dropzone = sourceControl.querySelector('[data-image-dropzone]');
        const fileInput = sourceControl.querySelector('[data-image-file]');
        const urlInput = sourceControl.querySelector('[data-image-url]');
        const urlButton = sourceControl.querySelector('[data-image-url-button]');
        const preview = sourceControl.querySelector('[data-image-preview]');

        if (!toggle || !panel || !dropzone || !fileInput || !urlInput || !preview) {
            return;
        }

        let previewObjectUrl = null;

        const showPreview = (value) => {

            if (previewObjectUrl) {
                URL.revokeObjectURL(previewObjectUrl);
                previewObjectUrl = null;
            }

            if (!value) {
                preview.removeAttribute('src');
                preview.classList.add('d-none');
                return;
            }

            preview.src = value;
            preview.classList.remove('d-none');
        };

        toggle.addEventListener('click', function () {

            const isHidden = panel.classList.toggle('d-none');

            toggle.setAttribute(
                'aria-expanded',
                String(!isHidden)
            );
        });

        // Hiển thị ảnh URL hiện tại nếu có
        if (urlInput.value.trim()) {
            showPreview(urlInput.value.trim());
        }

        // Chọn file
        fileInput.addEventListener('change', function () {

            const file = fileInput.files?.[0];

            if (file) {

                previewObjectUrl = URL.createObjectURL(file);

                preview.src = previewObjectUrl;
                preview.classList.remove('d-none');

                return;
            }

            showPreview(urlInput.value.trim());
        });

        // Drag file
        ['dragenter', 'dragover'].forEach(function (eventName) {

            dropzone.addEventListener(eventName, function (event) {

                event.preventDefault();

                dropzone.classList.add('is-dragging');
            });
        });

        ['dragleave', 'drop'].forEach(function (eventName) {

            dropzone.addEventListener(eventName, function (event) {

                event.preventDefault();

                dropzone.classList.remove('is-dragging');
            });
        });

        // Drop file
        dropzone.addEventListener('drop', function (event) {

            const files = event.dataTransfer?.files;

            if (!files?.length) {
                return;
            }

            try {

                const dataTransfer = new DataTransfer();

                Array.from(files).forEach(function (file) {
                    dataTransfer.items.add(file);
                });

                fileInput.files = dataTransfer.files;

                fileInput.dispatchEvent(
                    new Event('change', {
                        bubbles: true
                    })
                );

            } catch (error) {

                console.error(
                    'Không thể gán file từ drag/drop:',
                    error
                );
            }
        });

        // Button xem URL
        urlButton?.addEventListener('click', function () {

            // Nếu đang chọn file thì ưu tiên file
            if (fileInput.files?.length) {
                return;
            }

            showPreview(
                urlInput.value.trim()
            );
        });

        // Nhập URL
        urlInput.addEventListener('input', function () {

            if (!fileInput.files?.length) {

                showPreview(
                    urlInput.value.trim()
                );
            }
        });
    });


    // =========================================================
    // 2. XỬ LÝ ĐỊA CHỈ
    // API V2:
    // Tỉnh / Thành phố
    //      ↓
    // Phường / Xã
    // =========================================================

    const provinceSelect =
        document.querySelector('[data-address-province]');

    const wardSelect =
        document.querySelector('[data-address-ward]');

    const buildingInput =
        document.querySelector('[data-address-building]');

    const addressResult =
        document.querySelector('[data-address-result]');

    const addressStatus =
        document.querySelector('[data-address-status]');


    // =========================================================
    // KIỂM TRA CONTROL
    // =========================================================

    if (
        !provinceSelect ||
        !wardSelect ||
        !buildingInput ||
        !addressResult
    ) {

        console.error('Thiếu control địa chỉ:', {
            provinceSelect,
            wardSelect,
            buildingInput,
            addressResult,
            addressStatus
        });

        return;
    }


    console.log('Address controls OK');


    // =========================================================
    // DATA EDIT CŨ
    // =========================================================

    const selectedProvince =
        provinceSelect.dataset.selectedValue || '';

    const selectedWard =
        wardSelect.dataset.selectedValue || '';

    const selectedBuilding =
        buildingInput.value.trim();

    const existingAddress =
        addressResult.value.trim();

    const preserveExistingAddress =
        Boolean(
            existingAddress &&
            !selectedBuilding &&
            !selectedWard
        );


    let provinces = [];

    let wardRequestId = 0;


    // =========================================================
    // STATUS
    // =========================================================

    const setAddressStatus = function (message) {

        if (addressStatus) {
            addressStatus.textContent = message;
        }
    };


    // =========================================================
    // TEXT OPTION ĐANG CHỌN
    // =========================================================

    const selectedOptionText = function (select) {

        if (!select) {
            return '';
        }

        const option =
            select.options[select.selectedIndex];

        if (!option || !option.value) {
            return '';
        }

        return option.textContent.trim();
    };


    // =========================================================
    // GHÉP ĐỊA CHỈ
    // =========================================================

    const updateAddress = function () {

        const parts = [
            buildingInput.value.trim(),
            selectedOptionText(wardSelect),
            selectedOptionText(provinceSelect)
        ].filter(Boolean);

        addressResult.value =
            parts.join(', ');
    };


    // =========================================================
    // FETCH CÓ TIMEOUT
    // =========================================================

    const fetchJsonWithTimeout =
        function (url, timeout = 10000) {
            const request = fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                return response.json();
            });

            const timeoutRequest = new Promise(function (_, reject) {
                window.setTimeout(function () {
                    reject(new Error('Request timeout'));
                }, timeout);
            });

            return Promise.race([request, timeoutRequest]);
        };


    // =========================================================
    // RESET PHƯỜNG/XÃ
    // =========================================================

    const resetWards = function () {

        wardSelect.innerHTML =
            '<option value="">Chọn phường/xã</option>';

        wardSelect.disabled = true;
    };


    // =========================================================
    // RENDER PHƯỜNG/XÃ
    // =========================================================

    const renderWards =
        function (wards, wardCode = '') {

            wardSelect.innerHTML =
                '<option value="">Chọn phường/xã</option>';

            if (
                !Array.isArray(wards) ||
                wards.length === 0
            ) {

                wardSelect.disabled = true;

                return;
            }

            wards.forEach(function (ward) {

                const option =
                    new Option(
                        ward.name,
                        String(ward.code)
                    );

                wardSelect.add(option);
            });

            wardSelect.disabled = false;


            // Set lại dữ liệu edit
            if (wardCode) {

                wardSelect.value =
                    String(wardCode);
            }

            console.log(
                'Đã render phường/xã:',
                wards.length
            );
        };


    // =========================================================
    // LOAD PHƯỜNG/XÃ THEO TỈNH
    // =========================================================

    const loadWards =
        async function (
            provinceCode,
            wardCode = ''
        ) {

            const requestId =
                ++wardRequestId;


            if (!provinceCode) {

                resetWards();

                updateAddress();

                return;
            }


            wardSelect.innerHTML =
                '<option value="">Đang tải phường/xã...</option>';

            wardSelect.disabled = true;


            setAddressStatus(
                'Đang tải danh sách phường/xã...'
            );


            try {

                // =============================================
                // API V2
                //
                // Lấy thông tin tỉnh + wards
                // =============================================

                const url =
                    `https://provinces.open-api.vn/api/v2/p/${provinceCode}?depth=2`;


                const data =
                    await fetchJsonWithTimeout(url);


                // Nếu user đổi tỉnh liên tục,
                // bỏ request cũ.
                if (requestId !== wardRequestId) {
                    return;
                }


                console.log(
                    'Province detail:',
                    data
                );


                const wards =
                    Array.isArray(data?.wards)
                        ? data.wards
                        : [];


                console.log(
                    'Danh sách wards:',
                    wards
                );


                renderWards(
                    wards,
                    wardCode
                );


                updateAddress();

                setAddressStatus('');


            } catch (error) {

                if (requestId !== wardRequestId) {
                    return;
                }


                console.error(
                    'Không load được phường/xã:',
                    error
                );


                wardSelect.innerHTML =
                    '<option value="">Không tải được phường/xã</option>';

                wardSelect.disabled = true;


                setAddressStatus(
                    'Không thể tải danh sách phường/xã.'
                );
            }
        };


    // =========================================================
    // LOAD TỈNH / THÀNH
    // =========================================================

    const loadProvinces =
        async function () {

            setAddressStatus(
                'Đang tải danh sách tỉnh/thành...'
            );


            provinceSelect.innerHTML =
                '<option value="">Đang tải tỉnh/thành...</option>';

            provinceSelect.disabled = true;


            resetWards();


            try {

                const url =
                    'https://provinces.open-api.vn/api/v2/p/';


                const data =
                    await fetchJsonWithTimeout(url);


                provinces =
                    Array.isArray(data)
                        ? data
                        : [];


                console.log(
                    'Tổng tỉnh/thành:',
                    provinces.length
                );


                provinceSelect.innerHTML =
                    '<option value="">Chọn tỉnh/thành phố</option>';


                provinces.forEach(function (province) {

                    const option =
                        new Option(
                            province.name,
                            String(province.code)
                        );

                    provinceSelect.add(option);
                });


                provinceSelect.disabled = false;


                // =============================================
                // EDIT
                // =============================================

                if (selectedProvince) {

                    provinceSelect.value =
                        String(selectedProvince);


                    // Kiểm tra code cũ còn tồn tại không
                    const provinceExists =
                        provinces.some(function (item) {

                            return String(item.code) ===
                                String(selectedProvince);
                        });


                    if (provinceExists) {

                        await loadWards(
                            selectedProvince,
                            selectedWard
                        );

                    } else {

                        console.warn(
                            'Mã tỉnh cũ không tồn tại trong API v2:',
                            selectedProvince
                        );
                    }
                }


                if (!preserveExistingAddress) {
                    updateAddress();
                }


                setAddressStatus('');


            } catch (error) {

                console.error(
                    'Không load được tỉnh/thành:',
                    error
                );


                provinceSelect.innerHTML =
                    '<option value="">Không tải được tỉnh/thành</option>';

                provinceSelect.disabled = true;


                resetWards();


                setAddressStatus(
                    'Không thể tải danh sách tỉnh/thành. Mở F12 → Console để xem lỗi.'
                );
            }
        };


    // =========================================================
    // EVENT: ĐỔI TỈNH
    // =========================================================

    provinceSelect.addEventListener(
        'change',
        async function () {

            const provinceCode =
                provinceSelect.value;


            // Reset phường cũ trước
            resetWards();


            updateAddress();


            if (!provinceCode) {

                setAddressStatus('');

                return;
            }


            await loadWards(
                provinceCode
            );
        }
    );


    // =========================================================
    // EVENT: ĐỔI PHƯỜNG/XÃ
    // =========================================================

    wardSelect.addEventListener(
        'change',
        function () {

            updateAddress();
        }
    );


    // =========================================================
    // EVENT: NHẬP SỐ NHÀ / ĐƯỜNG
    // =========================================================

    buildingInput.addEventListener(
        'input',
        function () {

            updateAddress();
        }
    );


    // =========================================================
    // BẮT ĐẦU LOAD
    // =========================================================

    loadProvinces();

});

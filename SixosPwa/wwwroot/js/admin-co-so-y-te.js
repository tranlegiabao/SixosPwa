document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-image-source]').forEach(function (sourceControl) {
        const toggle = sourceControl.querySelector('[data-image-toggle]');
        const panel = sourceControl.querySelector('[data-image-panel]');
        const dropzone = sourceControl.querySelector('[data-image-dropzone]');
        const fileInput = sourceControl.querySelector('[data-image-file]');
        const urlInput = sourceControl.querySelector('[data-image-url]');
        const urlButton = sourceControl.querySelector('[data-image-url-button]');
        const preview = sourceControl.querySelector('[data-image-preview]');

        if (!toggle || !panel || !dropzone || !fileInput || !urlInput || !preview) return;

        const showPreview = (value) => {
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
            toggle.setAttribute('aria-expanded', String(!isHidden));
        });

        if (urlInput.value) showPreview(urlInput.value);

        fileInput.addEventListener('change', function () {
            const file = fileInput.files?.[0];
            showPreview(file ? URL.createObjectURL(file) : urlInput.value.trim());
        });

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

        dropzone.addEventListener('drop', function (event) {
            const files = event.dataTransfer?.files;
            if (!files?.length) return;

            try {
                fileInput.files = files;
                fileInput.dispatchEvent(new Event('change', { bubbles: true }));
            } catch (_) {
                // Một số trình duyệt không cho gán FileList trực tiếp.
            }
        });

        urlButton?.addEventListener('click', function () {
            showPreview(urlInput.value.trim());
        });

        urlInput.addEventListener('input', function () {
            if (!fileInput.files?.length) showPreview(urlInput.value.trim());
        });
    });

    const provinceSelect = document.querySelector('[data-address-province]');
    const districtSelect = document.querySelector('[data-address-district]');
    const addressStatus = document.querySelector('[data-address-status]');

    if (!provinceSelect || !districtSelect) return;

    const selectedProvince = provinceSelect.dataset.selectedValue || '';
    const selectedDistrict = districtSelect.dataset.selectedValue || '';
    let provinces = [];

    const setAddressStatus = (message) => {
        if (addressStatus) addressStatus.textContent = message;
    };

    const renderDistricts = (province, districtCode = '') => {
        districtSelect.innerHTML = '<option value="">Chọn quận/huyện</option>';
        if (!province) {
            districtSelect.disabled = true;
            return;
        }

        (province.districts || []).forEach((district) => {
            const option = new Option(district.name, district.code);
            districtSelect.add(option);
        });
        districtSelect.disabled = false;
        if (districtCode) districtSelect.value = String(districtCode);
    };

    provinceSelect.addEventListener('change', function () {
        const province = provinces.find((item) => String(item.code) === provinceSelect.value);
        renderDistricts(province);
        setAddressStatus('');
    });

    const provinceRequestController = new AbortController();
    const provinceRequestTimeout = window.setTimeout(() => provinceRequestController.abort(), 10000);
    setAddressStatus('Đang tải danh sách tỉnh/thành...');

    fetch('https://provinces.open-api.vn/api/v1/?depth=2', {
        signal: provinceRequestController.signal,
        mode: 'cors'
    })
        .then((response) => {
            if (!response.ok) throw new Error('Không thể tải danh sách tỉnh/thành.');
            return response.json();
        })
        .then((data) => {
            provinces = Array.isArray(data) ? data : [];
            provinceSelect.innerHTML = '<option value="">Chọn tỉnh/thành phố</option>';
            provinces.forEach((province) => {
                provinceSelect.add(new Option(province.name, province.code));
            });
            provinceSelect.disabled = false;

            if (selectedProvince) {
                provinceSelect.value = String(selectedProvince);
                const province = provinces.find((item) => String(item.code) === provinceSelect.value);
                renderDistricts(province, selectedDistrict);
            }
            setAddressStatus('');
        })
        .catch(() => {
            provinceSelect.innerHTML = '<option value="">Không tải được tỉnh/thành</option>';
            provinceSelect.disabled = true;
            districtSelect.innerHTML = '<option value="">Không tải được quận/huyện</option>';
            districtSelect.disabled = true;
            setAddressStatus('Không thể tải danh sách tỉnh/thành sau 10 giây. Kiểm tra kết nối hoặc CORS.');
        })
        .finally(() => window.clearTimeout(provinceRequestTimeout));
});

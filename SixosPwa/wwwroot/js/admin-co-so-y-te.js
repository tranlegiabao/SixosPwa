document.addEventListener('DOMContentLoaded', function () {
    const toggle = document.getElementById('moKhungAnh');
    const panel = document.getElementById('khungNguonAnh');
    const dropzone = document.getElementById('vungThaAnh');
    const fileInput = document.getElementById('ImageFile');
    const urlInput = document.getElementById('Img');
    const urlButton = document.getElementById('timAnhTheoUrl');
    const preview = document.getElementById('anhXemTruoc');

    if (!toggle || !panel || !fileInput || !preview) return;

    const showPreview = (source) => {
        if (!source) {
            preview.removeAttribute('src');
            preview.classList.add('d-none');
            return;
        }

        preview.src = source;
        preview.classList.remove('d-none');
    };

    toggle.addEventListener('click', function () {
        const isHidden = panel.classList.toggle('d-none');
        toggle.setAttribute('aria-expanded', String(!isHidden));
    });

    if (urlInput?.value) showPreview(urlInput.value);

    fileInput.addEventListener('change', function () {
        const file = fileInput.files?.[0];
        if (!file) {
            showPreview(urlInput?.value);
            return;
        }

        showPreview(URL.createObjectURL(file));
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
        showPreview(urlInput?.value.trim());
    });

    urlInput?.addEventListener('input', function () {
        if (!fileInput.files?.length) showPreview(urlInput.value.trim());
    });
});

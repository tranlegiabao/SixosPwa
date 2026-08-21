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
});


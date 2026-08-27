document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-image-source]').forEach(function (sourceControl) {
        const toggle = sourceControl.querySelector('[data-image-toggle]');
        const panel = sourceControl.querySelector('[data-image-panel]');
        const dropzone = sourceControl.querySelector('[data-image-dropzone]');
        const fileInput = sourceControl.querySelector('[data-image-file]');
        const urlInput = sourceControl.querySelector('[data-image-url]');
        const urlButton = sourceControl.querySelector('[data-image-url-button]');
        const preview = sourceControl.querySelector('[data-image-preview]');
        const compactPreview = sourceControl.querySelector('[data-image-toggle-preview]');
        const compactPreviewImage = compactPreview?.querySelector('img');
        const toggleLabel = sourceControl.querySelector('[data-image-toggle-label]');
        const imageKind = sourceControl.dataset.imageKind || 'image';
        const logoRemovedInput = sourceControl.querySelector('[data-logo-removed]');
        let existingImage = sourceControl.dataset.existingImage?.trim() || '';
        let previewObjectUrl = null;

        if (!toggle || !panel || !dropzone || !fileInput || !urlInput || !preview) return;

        const releaseObjectUrl = function () {
            if (previewObjectUrl) {
                URL.revokeObjectURL(previewObjectUrl);
                previewObjectUrl = null;
            }
        };

        const setCompactPreview = function (value) {
            if (!compactPreview || !compactPreviewImage) return;
            if (!value) {
                compactPreview.classList.add('d-none');
                compactPreviewImage.removeAttribute('src');
                if (toggleLabel) toggleLabel.textContent = imageKind === 'logo' ? 'Chọn logo' : 'Chọn ảnh';
                return;
            }

            compactPreviewImage.src = value;
            compactPreview.classList.remove('d-none');
            if (toggleLabel) toggleLabel.textContent = 'Đổi ảnh';
        };

        const setRemovedState = function (removed) {
            if (logoRemovedInput) logoRemovedInput.value = removed ? 'true' : 'false';
        };

        const announceSelection = function (value) {
            if (!value) return;
            setRemovedState(false);
            sourceControl.dispatchEvent(new CustomEvent('cskcb:image-selected', {
                bubbles: true,
                detail: { kind: imageKind, url: value }
            }));
        };

        const setPreview = function (value) {
            if (!value) {
                preview.removeAttribute('src');
                preview.classList.add('d-none');
                return;
            }

            preview.src = value;
            preview.classList.remove('d-none');
        };

        const collapse = function () {
            panel.classList.add('d-none');
            toggle.setAttribute('aria-expanded', 'false');
        };

        toggle.addEventListener('click', function () {
            const isHidden = panel.classList.toggle('d-none');
            toggle.setAttribute('aria-expanded', String(!isHidden));
            if (!isHidden) setPreview(urlInput.value.trim() || existingImage);
        });

        if (existingImage) {
            setCompactPreview(existingImage);
            setPreview(existingImage);
        }

        fileInput.addEventListener('change', function () {
            const file = fileInput.files?.[0];
            if (!file) {
                setPreview(urlInput.value.trim() || existingImage);
                return;
            }

            releaseObjectUrl();
            previewObjectUrl = URL.createObjectURL(file);
            setPreview(previewObjectUrl);
            setCompactPreview(previewObjectUrl);
            announceSelection(previewObjectUrl);
            collapse();
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
                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(files[0]);
                fileInput.files = dataTransfer.files;
                fileInput.dispatchEvent(new Event('change', { bubbles: true }));
            } catch {
                if (typeof showToast === 'function') showToast('Không thể chọn ảnh kéo thả.', 'error');
            }
        });

        urlButton?.addEventListener('click', function () {
            if (fileInput.files?.length) return;
            const value = urlInput.value.trim();
            setPreview(value || existingImage);
            setCompactPreview(value || existingImage);
            if (value) {
                announceSelection(value);
                collapse();
            }
        });

        urlInput.addEventListener('input', function () {
            if (fileInput.files?.length) return;
            const value = urlInput.value.trim();
            setPreview(value || existingImage);
            if (value) setCompactPreview(value);
        });

        preview.addEventListener('error', function () {
            preview.classList.add('d-none');
            if (typeof showToast === 'function') showToast('Không thể tải ảnh đã chọn.', 'error');
        });

        document.addEventListener('cskcb:clear-image-source', function (event) {
            if (event.detail?.kind !== imageKind) return;

            releaseObjectUrl();
            fileInput.value = '';
            urlInput.value = '';
            existingImage = '';
            sourceControl.dataset.existingImage = '';
            setPreview('');
            setCompactPreview('');
            setRemovedState(true);
            collapse();
        });
    });

    const amountInput = document.querySelector('[data-advertising-amount]');
    const advertisingDetails = document.querySelector('[data-advertising-details]');
    const advertisingContent = document.querySelector('[data-advertising-content]');
    if (amountInput && advertisingDetails && advertisingContent) {
        const updateAdvertisingVisibility = function () {
            const amount = Number.parseFloat(amountInput.value || '0');
            const isEnabled = Number.isFinite(amount) && amount > 0;
            advertisingDetails.classList.toggle('d-none', !isEnabled);
            advertisingContent.classList.toggle('d-none', !isEnabled);
        };

        amountInput.addEventListener('input', updateAdvertisingVisibility);
        amountInput.addEventListener('change', updateAdvertisingVisibility);
        updateAdvertisingVisibility();
    }
});

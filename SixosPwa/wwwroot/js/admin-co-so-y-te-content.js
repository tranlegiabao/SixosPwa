(function () {
    var contentDrafts = Object.create(null);
    var contentLoadSequence = 0;

    function ensureDefaultBlackHtml(content) {
        var html = (content || '').trim();
        if (!html) return '';

        var parsed = document.createElement('div');
        parsed.innerHTML = html;
        var firstElement = parsed.firstElementChild;
        var firstStyle = firstElement ? (firstElement.getAttribute('style') || '') : '';
        if (parsed.children.length === 1
            && firstElement
            && firstElement.tagName.toLowerCase() === 'div'
            && /(^|;)\s*color\s*:/i.test(firstStyle)) {
            return html;
        }

        var wrapper = document.createElement('div');
        wrapper.style.color = '#000000';
        wrapper.innerHTML = html;
        return wrapper.outerHTML;
    }

    function setEditorContent(selector, content) {
        var editor = document.querySelector(selector);
        if (!editor) return;

        var id = selector.replace('#', '');
        var ed = typeof tinymce !== 'undefined' ? tinymce.get(id) : null;
        if (ed) {
            ed.setContent(content || '');
        } else {
            editor.value = content || '';
        }
    }

    function cacheCurrentTopicContent() {
        var topic = document.getElementById('cboChuDe');
        var editor = typeof tinymce !== 'undefined' ? tinymce.get('summernote') : null;
        if (!topic || !topic.value || !editor) return;

        contentDrafts[topic.value] = ensureDefaultBlackHtml(editor.getContent({ format: 'raw' }));
    }

    function syncTopicContents() {
        cacheCurrentTopicContent();
        var hidden = document.getElementById('topicContentsJson');
        if (hidden) hidden.value = JSON.stringify(contentDrafts);
    }

    function getFormValue(name, fallback) {
        var field = document.querySelector('[name="' + name + '"]');
        return field && field.value ? field.value.trim() : (fallback || '');
    }

    function escapePreviewText(value) {
        return String(value || '').replace(/[&<>"']/g, function (character) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[character];
        });
    }

    function getPreviewImageUrl(value) {
        var imageUrl = (value || '').trim();
        if (/^(https?:\/\/|\/|blob:|data:image\/)/i.test(imageUrl)) return imageUrl;
        return '/static/icon-512.png';
    }

    function getPreviewLogoUrl() {
        var logoFieldValue = getFormValue('Logo');
        if (logoFieldValue) return getPreviewImageUrl(logoFieldValue);

        var logoControl = document.querySelector('[data-image-source]');
        var selectedLogo = logoControl?.querySelector('[data-image-toggle-preview] img')
            || logoControl?.querySelector('[data-image-preview]');
        return getPreviewImageUrl(selectedLogo?.getAttribute('src') || logoControl?.dataset.existingImage);
    }

    function buildResponsivePreviewDocument(content) {
        var tenCoSo = escapePreviewText(getFormValue('TenCoSo', 'Cơ sở y tế'));
        var diaChi = escapePreviewText(getFormValue('DiaChi', 'Địa chỉ cơ sở y tế'));
        var ngayLamViec = escapePreviewText(getFormValue('NgayLamViec', 'Thứ 2 - Chủ nhật'));
        var gioMoCua = escapePreviewText(getFormValue('GioMoCua', '07:00'));
        var gioDongCua = escapePreviewText(getFormValue('GioDongCua', '17:00'));
        var logoUrl = escapePreviewText(getPreviewLogoUrl());

        return `<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        * { box-sizing: border-box; }
        html, body { margin: 0; min-height: 100%; background: #fff; color: #0f172a; }
        body { font: 15px/1.5 Arial, sans-serif; overflow-wrap: anywhere; }
        button, a { font: inherit; }
        .preview-page { min-height: 100vh; background: #fff; }
        .ytv-header { display: flex; align-items: center; gap: 18px; min-height: 76px; padding: 12px 28px; color: #0f172a; background: linear-gradient(135deg, #60a5fa, #4285f4); }
        .ytv-logo-area { display: flex; align-items: center; gap: 10px; color: inherit; text-decoration: none; flex: 0 0 auto; }
        .ytv-logo-area img { width: 54px; height: 54px; object-fit: cover; border-radius: 50%; background: #fff; border: 3px solid rgba(255,255,255,.9); }
        .ytv-logo-text { font: 700 22px/1.05 Georgia, serif; letter-spacing: 1px; }
        .ytv-header-title { min-width: 0; flex: 1; }
        .ytv-header-title h1 { margin: 0; font-size: 20px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .ytv-header-title p { margin: 2px 0 0; font-size: 12px; }
        .ytv-header-menu { display: flex; align-items: center; gap: 8px; }
        .ytv-header-menu span { padding: 7px 10px; border-radius: 8px; color: #123b82; background: rgba(255,255,255,.55); font-size: 13px; }
        .ytv-main { display: flex; min-height: 560px; padding-bottom: 88px; }
        .ytv-sidebar { width: 250px; flex: 0 0 250px; padding: 22px 20px; background: #fff; border-right: 1px solid #dbe4ef; }
        .sidebar-logo-container { display: flex; justify-content: center; margin-bottom: 22px; }
        .sidebar-logo-container img { width: 112px; height: 112px; object-fit: contain; border-radius: 50%; box-shadow: 0 8px 20px rgba(15,23,42,.12); background: #fff; }
        .sidebar-menu { display: grid; gap: 6px; }
        .sidebar-menu .preview-menu-item { display: block; padding: 10px 12px; border-radius: 12px; color: #183b65; }
        .sidebar-menu .preview-menu-item.active { color: #1d4ed8; background: #eff6ff; font-weight: 700; }
        .sidebar-actions { display: grid; gap: 8px; margin-top: 18px; }
        .sidebar-actions button { border: 1px solid #1d4ed8; border-radius: 12px; padding: 10px 8px; color: #1d4ed8; background: #fff; font-weight: 700; }
        .sidebar-actions button:first-child { color: #fff; background: #2563eb; }
        .operating-hours-box { margin-top: 22px; padding: 14px; border: 1px solid #dbe4ef; border-radius: 16px; background: #f8fafc; }
        .operating-hours-box .heading { color: #64748b; font-weight: 700; letter-spacing: .4px; }
        .operating-hours-box .days { margin-top: 4px; }
        .operating-hours-box strong { display: block; margin-top: 4px; font-size: 18px; }
        .open-status { display: inline-block; margin-top: 8px; padding: 4px 9px; border: 1px solid #86efac; border-radius: 999px; color: #16a34a; background: #f0fdf4; font-size: 12px; font-weight: 700; }
        .ytv-content { flex: 1; min-width: 0; padding: 30px 34px; background: #fff; }
        .intro-section { max-width: 1100px; margin: 0 auto; padding: 28px 34px 36px; border-radius: 18px; background: #fff; box-shadow: 0 10px 28px rgba(15,23,42,.08); }
        .intro-title { display: flex; align-items: center; gap: 10px; margin: 0 0 24px; color: #38aef2; font-size: 27px; }
        .intro-title .icon { display: inline-grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; color: #fff; background: #38aef2; font-size: 18px; }
        .nd-cskcb-text { color: #000; font-size: 16px; }
        .nd-cskcb-text img { max-width: 100%; height: auto; }
        .nd-cskcb-text table { width: 100%; max-width: 100%; border-collapse: collapse; margin: 12px 0 18px; table-layout: auto; }
        .nd-cskcb-text th, .nd-cskcb-text td { border: 1px solid #94a3b8; padding: 8px 10px; vertical-align: top; min-width: 48px; }
        .nd-cskcb-text th { background: #f1f5f9; font-weight: 700; }
        .stats-footer-bar { position: sticky; bottom: 0; z-index: 2; display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; padding: 12px 28px; border-top: 1px solid #dbe4ef; background: rgba(255,255,255,.97); }
        .stat-card { display: flex; align-items: center; gap: 10px; min-width: 0; padding: 9px 12px; border: 1px solid #e2e8f0; border-radius: 12px; background: #f8fafc; }
        .stat-icon { display: grid; place-items: center; width: 32px; height: 32px; flex: 0 0 32px; border-radius: 50%; color: #2563eb; background: #dbeafe; font-size: 17px; }
        .stat-card:nth-child(2) .stat-icon { color: #16a34a; background: #dcfce7; }
        .stat-card:nth-child(3) .stat-icon { color: #9333ea; background: #f3e8ff; }
        .stat-card:nth-child(4) .stat-icon { color: #ea580c; background: #ffedd5; }
        .stat-card strong { display: block; color: #1d4ed8; font-size: 17px; line-height: 1.1; }
        .stat-card span { display: block; color: #64748b; font-size: 11px; line-height: 1.2; }
        @media (max-width: 768px) {
            .ytv-header { min-height: 66px; padding: 8px 14px; gap: 10px; }
            .ytv-logo-area img { width: 42px; height: 42px; }
            .ytv-logo-text { display: none; }
            .ytv-header-title h1 { font-size: 16px; }
            .ytv-header-menu span { display: none; }
            .ytv-main { display: block; padding-bottom: 0; }
            .ytv-sidebar { width: 100%; padding: 16px; border-right: 0; border-bottom: 1px solid #dbe4ef; }
            .sidebar-logo-container { margin-bottom: 14px; }
            .sidebar-logo-container img { width: 88px; height: 88px; }
            .sidebar-menu { grid-template-columns: repeat(2, minmax(0, 1fr)); }
            .sidebar-actions { grid-template-columns: repeat(2, minmax(0, 1fr)); margin-top: 12px; }
            .operating-hours-box { margin-top: 14px; }
            .ytv-content { padding: 16px; }
            .intro-section { padding: 20px 18px 26px; }
            .intro-title { font-size: 23px; }
            .stats-footer-bar { position: relative; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; padding: 10px 12px; }
            .stat-card { padding: 8px; }
        }
        @media (max-width: 420px) {
            .sidebar-menu, .sidebar-actions, .stats-footer-bar { grid-template-columns: 1fr; }
            .ytv-content { padding: 12px; }
            .intro-section { padding: 18px 14px 22px; }
            .nd-cskcb-text { font-size: 15px; }
        }
    </style>
</head>
<body>
    <div class="preview-page">
        <header class="ytv-header">
            <div class="ytv-logo-area">
                <img src="/static/icon-512.png" alt="Y tế Việt">
                <span class="ytv-logo-text">Y TẾ<br>VIỆT</span>
            </div>
            <div class="ytv-header-title">
                <h1>${tenCoSo}</h1>
                <p>${diaChi} &nbsp; | &nbsp; Hotline: 19008198</p>
            </div>
            <div class="ytv-header-menu"><span>Trang chủ</span><span>Đăng nhập</span></div>
        </header>
        <main class="ytv-main">
            <aside class="ytv-sidebar">
                <div class="sidebar-logo-container"><img src="${logoUrl}" alt="Logo cơ sở"></div>
                <nav class="sidebar-menu" aria-label="Menu cơ sở">
                    <span class="preview-menu-item active">&#9432;&nbsp; Giới thiệu</span>
                    <span class="preview-menu-item">&#9829;&nbsp; Dịch vụ</span>
                    <span class="preview-menu-item">&#9813;&nbsp; Đội ngũ y bác sĩ</span>
                    <span class="preview-menu-item">&#9881;&nbsp; Các trang thiết bị</span>
                    <span class="preview-menu-item">&#9742;&nbsp; Liên hệ</span>
                </nav>
                <div class="sidebar-actions"><button type="button">Đăng ký khám</button><button type="button">Đăng nhập</button></div>
                <div class="operating-hours-box">
                    <div class="heading">&#9711;&nbsp; GIỜ HOẠT ĐỘNG</div>
                    <div class="days">${ngayLamViec}</div>
                    <strong>${gioMoCua} - ${gioDongCua}</strong>
                    <span class="open-status">&#9679; Đang mở cửa</span>
                </div>
            </aside>
            <section class="ytv-content">
                <article class="intro-section">
                    <h2 class="intro-title"><span class="icon">&#9432;</span> Giới thiệu</h2>
                    <div class="nd-cskcb-text">${content || '<p>Chưa có nội dung để xem trước.</p>'}</div>
                </article>
            </section>
        </main>
        <footer class="stats-footer-bar">
            <div class="stat-card"><span class="stat-icon">&#9742;</span><div><strong>24/7</strong><span>Hỗ trợ<br>Luôn sẵn sàng</span></div></div>
            <div class="stat-card"><span class="stat-icon">&#9733;</span><div><strong>4.8/5</strong><span>Đánh giá<br>Từ 1.248 khách hàng</span></div></div>
            <div class="stat-card"><span class="stat-icon">&#9822;</span><div><strong>50+</strong><span>Bác sĩ<br>Giàu kinh nghiệm</span></div></div>
            <div class="stat-card"><span class="stat-icon">&#10010;</span><div><strong>20+</strong><span>Dịch vụ<br>Đa dạng chuyên khoa</span></div></div>
        </footer>
    </div>
</body>
</html>`;
    }

    function preventPreviewButtonSubmit(editor) {
        var container = editor.getContainer && editor.getContainer();
        var previewButton = container?.querySelector('[data-mce-name="responsivepreview"]');
        if (!previewButton) return;

        previewButton.setAttribute('type', 'button');
        previewButton.addEventListener('click', function (event) {
            event.preventDefault();
        }, true);
    }

    function openResponsivePreview(editor) {
        var content = ensureDefaultBlackHtml(editor.getContent({ format: 'raw' }));
        var overlay = document.createElement('div');
        overlay.className = 'cskcb-responsive-preview';
        overlay.innerHTML = `
            <div class="cskcb-preview-dialog" role="dialog" aria-modal="true" aria-label="Xem trước nội dung">
                <div class="cskcb-preview-header">
                    <strong>Xem trước nội dung</strong>
                    <div class="cskcb-preview-actions">
                        <button type="button" class="cskcb-preview-mode active" data-mode="desktop">Desktop</button>
                        <button type="button" class="cskcb-preview-mode" data-mode="mobile">Mobile</button>
                        <button type="button" class="cskcb-preview-close" aria-label="Đóng xem trước">&times;</button>
                    </div>
                </div>
                <div class="cskcb-preview-stage">
                    <iframe class="cskcb-preview-frame desktop" title="Bản xem trước nội dung"></iframe>
                </div>
            </div>`;

        if (!document.getElementById('cskcb-responsive-preview-style')) {
            var style = document.createElement('style');
            style.id = 'cskcb-responsive-preview-style';
            style.textContent = `
                .cskcb-responsive-preview { position: fixed; inset: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; padding: 16px; background: rgba(15, 23, 42, .62); }
                .cskcb-preview-dialog { display: flex; flex-direction: column; width: min(1180px, 100%); height: min(860px, 94vh); overflow: hidden; background: #e2e8f0; border-radius: 12px; box-shadow: 0 24px 60px rgba(15, 23, 42, .3); }
                .cskcb-preview-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px; background: #fff; border-bottom: 1px solid #cbd5e1; color: #0f172a; }
                .cskcb-preview-actions { display: flex; align-items: center; gap: 8px; }
                .cskcb-preview-mode, .cskcb-preview-close { border: 1px solid #cbd5e1; border-radius: 6px; padding: 6px 12px; background: #fff; color: #334155; cursor: pointer; }
                .cskcb-preview-mode.active { border-color: #2563eb; background: #2563eb; color: #fff; }
                .cskcb-preview-close { padding: 2px 10px; font-size: 24px; line-height: 1.2; }
                .cskcb-preview-stage { display: flex; flex: 1; min-height: 0; align-items: flex-start; justify-content: center; overflow: auto; padding: 24px; }
                .cskcb-preview-frame { width: 100%; height: 100%; min-height: 620px; border: 1px solid #94a3b8; border-radius: 6px; background: #fff; box-shadow: 0 8px 20px rgba(15, 23, 42, .12); transition: width .2s ease; }
                .cskcb-preview-frame.mobile { width: 390px; max-width: 100%; }
                @media (max-width: 576px) {
                    .cskcb-responsive-preview { padding: 0; }
                    .cskcb-preview-dialog { width: 100%; height: 100%; border-radius: 0; }
                    .cskcb-preview-header { padding: 10px; }
                    .cskcb-preview-header strong { font-size: 14px; }
                    .cskcb-preview-actions { gap: 4px; }
                    .cskcb-preview-mode { padding: 5px 8px; font-size: 12px; }
                    .cskcb-preview-stage { padding: 12px; }
                }
            `;
            document.head.appendChild(style);
        }

        document.body.appendChild(overlay);
        var frame = overlay.querySelector('.cskcb-preview-frame');
        frame.setAttribute('sandbox', 'allow-same-origin');
        frame.srcdoc = buildResponsivePreviewDocument(content);

        function closePreview() {
            document.removeEventListener('keydown', onKeyDown);
            overlay.remove();
        }

        function onKeyDown(event) {
            if (event.key === 'Escape') closePreview();
        }

        overlay.querySelector('.cskcb-preview-close').addEventListener('click', closePreview);
        overlay.addEventListener('click', function (event) {
            if (event.target === overlay) closePreview();
        });
        overlay.querySelectorAll('[data-mode]').forEach(function (button) {
            button.addEventListener('click', function () {
                var isMobile = button.dataset.mode === 'mobile';
                frame.classList.toggle('mobile', isMobile);
                frame.classList.toggle('desktop', !isMobile);
                overlay.querySelectorAll('[data-mode]').forEach(function (item) {
                    item.classList.toggle('active', item === button);
                });
            });
        });
        document.addEventListener('keydown', onKeyDown);
    }

    async function loadContent() {
        var topicId = document.getElementById('cboChuDe')?.value;
        var currentId = document.querySelector('input[name="Id"]')?.value;
        var topicHidden = document.getElementById('contentTopicId');
        var requestSequence = ++contentLoadSequence;
        if (topicHidden) topicHidden.value = topicId || '';

        if (!currentId || !topicId) {
            setEditorContent('#summernote', '');
            return;
        }

        try {
            var response = await fetch('/Admin/CoSoYTe/GetContent?id=' + encodeURIComponent(currentId)
                + '&topicId=' + encodeURIComponent(topicId), {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
            if (!response.ok) throw new Error('Không tải được nội dung.');
            var data = await response.json();
            var currentTopicId = document.getElementById('cboChuDe')?.value;
            if (requestSequence !== contentLoadSequence || currentTopicId !== topicId) return;
            setEditorContent('#summernote', data.noiDung || '');
        } catch (error) {
            if (requestSequence !== contentLoadSequence) return;
            setEditorContent('#summernote', '');
            if (typeof showToast === 'function') showToast(error.message, 'error');
        }
    }

    function initializeEditor(selector, height) {
        var el = document.querySelector(selector);
        if (!el) return;

        var id = selector.replace('#', '');
        if (typeof tinymce !== 'undefined' && tinymce.get(id)) {
            return;
        }

        tinymce.init({
            selector: selector,
            height: height,
            menubar: false,
            branding: false,
            promotion: false,
            license_key: 'gpl',
            plugins: 'advlist autolink lists link image table code',
            font_size_formats: '8pt 10pt 12pt 14pt 16pt 18pt 24pt 36pt 48pt',
            toolbar: 'undo redo | responsivepreview | bold italic underline | fontsize | bullist numlist | ' +
                     'alignleft aligncenter alignright | table image link | ' +
                     'forecolor backcolor removeformat | code',
            mobile: {
                menubar: false,
                toolbar: 'undo redo | responsivepreview | bold italic underline | fontsize | ' +
                         'bullist numlist | table image link | forecolor backcolor'
            },
            paste_data_images: false,
            automatic_uploads: true,
            setup: function (editor) {
                editor.ui.registry.addButton('responsivepreview', {
                    icon: 'preview',
                    tooltip: 'Xem trước desktop/mobile',
                    onAction: function () { openResponsivePreview(editor); }
                });
                editor.on('init', function () {
                    preventPreviewButtonSubmit(editor);
                    if (selector === '#summernote') {
                        loadContent();
                    }
                });
            },
            images_upload_handler: function (blobInfo) {
                return new Promise(function (resolve, reject) {
                    var form = new FormData();
                    form.append('file', blobInfo.blob(), blobInfo.filename());
                    var token = document.querySelector('#coSoYTeForm input[name="__RequestVerificationToken"]');
                    if (token) form.append('__RequestVerificationToken', token.value);

                    fetch('/Admin/CoSoYTe/UploadImage', {
                        method: 'POST',
                        body: form
                    })
                    .then(function (res) {
                        if (!res.ok) {
                            return res.text().then(function (text) { throw new Error(text); });
                        }
                        return res.json();
                    })
                    .then(function (json) {
                        if (json && json.url) { resolve(json.url); }
                        else { reject({ message: 'Tải ảnh thất bại.', remove: true }); }
                    })
                    .catch(function (err) { reject({ message: err.message || 'Không kết nối được máy chủ.', remove: true }); });
                });
            }
        });
    }

    window.initializeEditor = initializeEditor;

    function initialize() {
        if (typeof tinymce === 'undefined') return;

        var selectorEl = document.getElementById('editSectionSelector');
        if (selectorEl) {
            if (selectorEl.value === 'noiDungChiTiet') {
                initializeEditor('#summernote', 450);
            } else if (selectorEl.value === 'quangCao') {
                initializeEditor('#advertisingContentEditor', 250);
            }
        } else {
            initializeEditor('#summernote', 450);
            initializeEditor('#advertisingContentEditor', 250);
        }
    }

    function syncEditorValue(selector) {
        var editor = document.querySelector(selector);
        if (!editor) return;

        var html = editor.value || '';
        var id = selector.replace('#', '');
        var ed = typeof tinymce !== 'undefined' ? tinymce.get(id) : null;
        if (ed) {
            html = ed.getContent();
        }
        editor.value = ensureDefaultBlackHtml(html);
    }

    document.addEventListener('DOMContentLoaded', function () {
        initialize();
        var topic = document.getElementById('cboChuDe');
        if (topic) {
            topic.addEventListener('change', function () {
                cacheCurrentTopicContent();
                ++contentLoadSequence;
                var topicHidden = document.getElementById('contentTopicId');
                if (topicHidden) topicHidden.value = topic.value || '';
                setEditorContent('#summernote', '');
            });
        }

        var form = document.getElementById('coSoYTeForm');
        if (form) {
            form.addEventListener('submit', function () {
                var topicHidden = document.getElementById('contentTopicId');
                if (topicHidden && topic) topicHidden.value = topic.value || '';
                syncEditorValue('#summernote');
                syncEditorValue('#advertisingContentEditor');
                syncTopicContents();
            });
        }

        loadContent();
    });
})();

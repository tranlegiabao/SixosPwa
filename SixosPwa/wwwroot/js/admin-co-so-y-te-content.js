(function () {
    var contentDrafts = Object.create(null);
    var contentLoadSequence = 0;
    var activeTopicId = '';
    var currentLogoUrl = '';
    var suppressLogoRemovalDetection = 0;
    var managedLogoWasPresent = false;
    var managedLogoSelector = '[data-cskcb-facility-logo="true"]';
    var managedLogoImageSelector = managedLogoSelector + ' img[data-cskcb-facility-logo-image="true"]';

    function buildManagedLogoHtml(logoUrl) {
        if (!logoUrl) return '';
        return '<div data-cskcb-facility-logo="true" style="text-align:center;margin:0 0 16px">'
            + '<img data-cskcb-facility-logo-image="true" src="' + escapePreviewText(logoUrl) + '" '
            + 'alt="Logo cơ sở" style="display:inline-block;max-width:180px;width:auto;height:auto;object-fit:contain">'
            + '</div>';
    }

    function stripManagedLogo(content) {
        var parsed = document.createElement('div');
        parsed.innerHTML = content || '';
        parsed.querySelectorAll(managedLogoSelector).forEach(function (element) { element.remove(); });
        return parsed.innerHTML;
    }

    function synchronizeManagedLogo(content, logoUrl) {
        var body = stripManagedLogo(content);
        return logoUrl ? buildManagedLogoHtml(logoUrl) + body : body;
    }

    function hasManagedLogo(content) {
        var parsed = document.createElement('div');
        parsed.innerHTML = content || '';
        return Boolean(parsed.querySelector(managedLogoImageSelector));
    }

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
            suppressLogoRemovalDetection++;
            try {
                ed.setContent(content || '');
                if (id === 'summernote') managedLogoWasPresent = hasManagedLogo(ed.getContent({ format: 'raw' }));
            } finally {
                suppressLogoRemovalDetection--;
            }
        } else {
            editor.value = content || '';
        }
    }

    function cacheCurrentTopicContent(topicIdOverride) {
        var topic = document.getElementById('cboChuDe');
        var editor = typeof tinymce !== 'undefined' ? tinymce.get('summernote') : null;
        var topicId = topicIdOverride || activeTopicId || topic?.value;
        if (!topicId || !editor) return;

        var content = editor.getContent({ format: 'raw' });
        contentDrafts[topicId] = ensureDefaultBlackHtml(synchronizeManagedLogo(content, currentLogoUrl));
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

    function getSelectedLogoUrl() {
        var logoFieldValue = getFormValue('Logo');
        if (logoFieldValue) return logoFieldValue;

        var logoControl = document.querySelector('[data-image-source][data-image-kind="logo"]');
        var selectedLogo = logoControl?.querySelector('[data-image-toggle-preview] img')
            || logoControl?.querySelector('[data-image-preview]');
        return (selectedLogo?.getAttribute('src') || logoControl?.dataset.existingImage || '').trim();
    }

    function getPreviewLogoUrl() {
        return getPreviewImageUrl(getSelectedLogoUrl());
    }

    function applyLogoToDrafts(logoUrl) {
        currentLogoUrl = logoUrl || '';
        Object.keys(contentDrafts).forEach(function (topicId) {
            contentDrafts[topicId] = synchronizeManagedLogo(contentDrafts[topicId], currentLogoUrl);
        });

        var editor = typeof tinymce !== 'undefined' ? tinymce.get('summernote') : null;
        if (editor) {
            setEditorContent('#summernote', synchronizeManagedLogo(editor.getContent({ format: 'raw' }), currentLogoUrl));
        }
        syncTopicContents();
    }

    function clearLogoEverywhere() {
        if (!currentLogoUrl && !managedLogoWasPresent) return;
        applyLogoToDrafts('');
        document.dispatchEvent(new CustomEvent('cskcb:clear-image-source', {
            detail: { kind: 'logo' }
        }));
    }

    function detectManagedLogoRemoval(editor) {
        if (suppressLogoRemovalDetection > 0 || !editor || editor.id !== 'summernote') return;

        var logoExists = Boolean(editor.getBody()?.querySelector(managedLogoImageSelector));
        if (managedLogoWasPresent && !logoExists) {
            clearLogoEverywhere();
            managedLogoWasPresent = false;
            return;
        }
        managedLogoWasPresent = logoExists;
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

    function getStaticPreviewTopic() {
        var select = document.getElementById('cboChuDe');
        var label = (select?.selectedOptions?.[0]?.textContent || '').trim().toLowerCase();

        if (label.indexOf('dịch vụ') >= 0) return 'dichvu';
        if (label.indexOf('đội ngũ') >= 0 || label.indexOf('bác sĩ') >= 0) return 'doingu';
        if (label.indexOf('trang thiết bị') >= 0 || label.indexOf('thiết bị') >= 0) return 'trangthietbi';
        if (label.indexOf('liên hệ') >= 0 || label.indexOf('liên lạc') >= 0) return 'lienhe';
        return 'gioithieu';
    }

    function buildStaticDetailPreviewDocument(content) {
        var tenCoSo = escapePreviewText(getFormValue('TenCoSo', 'Cơ sở y tế'));
        var ngayLamViec = escapePreviewText(getFormValue('NgayLamViec', 'Thứ 2 - Chủ nhật'));
        var gioMoCua = escapePreviewText(getFormValue('GioMoCua', '07:00'));
        var gioDongCua = escapePreviewText(getFormValue('GioDongCua', '17:00'));
        var activeTopic = getStaticPreviewTopic();
        var topics = [
            { key: 'gioithieu', id: 'gioi-thieu', icon: 'fa-info-circle', menu: 'Giới thiệu', title: 'Giới thiệu' },
            { key: 'dichvu', id: 'dich-vu', icon: 'fa-stethoscope', menu: 'Dịch vụ', title: 'Dịch vụ khám chữa bệnh' },
            { key: 'doingu', id: 'doi-ngu', icon: 'fa-user-md', menu: 'Đội ngũ y bác sĩ', title: 'Đội ngũ bác sĩ chuyên khoa' },
            { key: 'trangthietbi', id: 'trang-thiet-bi', icon: 'fa-tools', menu: 'Các trang thiết bị', title: 'Trang thiết bị hiện đại' },
            { key: 'lienhe', id: 'lien-he', icon: 'fa-headset', menu: 'Liên hệ', title: 'Chăm sóc & Hỗ trợ khách hàng' }
        ];

        var menuHtml = topics.map(function (topic) {
            return '<a class="ytv-menu-item ' + (topic.key === activeTopic ? 'active' : '') + '" href="#' + topic.id + '">'
                + '<i class="fas ' + topic.icon + '"></i> ' + topic.menu + '</a>';
        }).join('');

        var sectionsHtml = topics.map(function (topic) {
            var sectionContent = topic.key === activeTopic ? (content || '') : '';
            return '<div class="intro-section ' + (topic.key === activeTopic ? 'active-tab' : '') + '" id="' + topic.id + '">'
                + '<div class="intro-content">'
                + '<div class="intro-title"><i class="fas ' + topic.icon + '"></i> ' + topic.title + '</div>'
                + (sectionContent ? '<div class="nd-cskcb-text">' + sectionContent + '</div>' : '')
                + '</div></div>';
        }).join('');

        return `<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
    <style>
        * { box-sizing: border-box; }
        :root { --ytv-primary: #1d4ed8; --ytv-secondary: #3b82f6; }
        html, body { margin: 0; min-height: 100%; background: #fff; }
        body { padding-top: 98px; padding-bottom: 198px; color: #1e293b; font-family: Inter, system-ui, sans-serif; overflow-wrap: anywhere; }
        a { color: inherit; }
        .ytv-header {
            background: linear-gradient(135deg, #4285f4, #60a5fa); color: #fff; padding: 15px 30px;
            display: flex; align-items: center; justify-content: space-between; box-shadow: 0 4px 15px rgba(0,0,0,.1);
            border-bottom: 3px solid var(--ytv-secondary); position: fixed; top: 0; left: 0; width: 100%; z-index: 1100;
        }
        .ytv-logo-area { display: flex; align-items: center; gap: 15px; text-decoration: none; color: #fff; }
        .ytv-logo-area > img { width: 65px; height: 65px; object-fit: cover; background: #fff; border-radius: 50%; padding: 0; }
        .ytv-logo-text { font-family: 'Times New Roman', serif; font-size: 34px; font-weight: 900; line-height: 1.05; letter-spacing: 1px; }
        .ytv-header-title { text-align: center; flex-grow: 1; min-width: 0; }
        .ytv-header-title h1 { margin: 0 0 5px; font-size: 26px; font-weight: 700; color: #fff; overflow-wrap: anywhere; }
        .ytv-header-title p { margin: 0; font-size: 20px; opacity: .9; color: #fff; font-style: italic; }
        .ytv-header-menu { padding: 10px; font-size: 32px; color: #fff; position: relative; }
        .ytv-main { display: flex; width: 100%; min-height: calc(100vh - 184px); align-items: flex-start; padding-left: 250px; }
        .ytv-sidebar {
            width: 250px; flex-shrink: 0; background: #fff; border-right: 1px solid #e2e8f0; display: flex;
            flex-direction: column; padding: 12px 0 0; position: fixed; top: 98px; left: 0; height: auto; overflow: visible; z-index: 100;
        }
        .sidebar-scroll-area { flex: none; overflow: visible; }
        .ytv-menu-item {
            display: flex; align-items: center; padding: 12px 16px; margin: 2px 16px; gap: 12px; color: #475569;
            font-size: 18px; font-weight: 600; line-height: 1.3; text-decoration: none; border-radius: 10px;
        }
        .ytv-menu-item i { font-size: 20px; width: 24px; text-align: center; color: #64748b; }
        .ytv-menu-item.active { background: #eff6ff; color: #1d4ed8; }
        .ytv-menu-item.active i { color: #1d4ed8; }
        .hospital-actions { padding: 4px 16px; display: flex; flex-direction: column; gap: 6px; flex: 0 0 auto; }
        .btn-outline {
            background: #fff; color: #1d4ed8; padding: 6px 10px; border-radius: 8px; font-weight: 600; font-size: 12px;
            display: flex; align-items: center; justify-content: center; gap: 8px; text-decoration: none; border: 1.5px solid #1d4ed8;
        }
        .btn-outline.primary { background: #1d4ed8; color: aliceblue; }
        .btn-outline.primary i { color: aliceblue; }
        .operating-hours-box {
            flex-shrink: 0; background: #f8fafc; border-radius: 12px; padding: 10px 12px; margin: 8px 16px 12px;
            border: 1px solid #e2e8f0; box-shadow: inset 0 1px 2px rgba(0,0,0,.02);
        }
        .oh-header { display: flex; align-items: center; gap: 8px; color: #64748b; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: .5px; }
        .oh-header i { font-size: 14px; color: #94a3b8; }
        .oh-days { font-size: 16px; color: #334155; font-weight: 600; white-space: nowrap; padding-left: 22px; }
        .oh-time { font-size: 16px; color: #0f172a; font-weight: 700; white-space: nowrap; padding-left: 22px; }
        .oh-status { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; color: #16a34a; font-weight: 700; background: #f0fdf4; padding: 4px 8px 4px 22px; border-radius: 20px; border: 1px solid #bbf7d0; }
        .status-dot { width: 6px; height: 6px; background: #16a34a; border-radius: 50%; display: inline-block; }
        .ytv-content { flex: 1; background: transparent; padding: 30px; display: flex; flex-direction: column; gap: 24px; min-width: 0; max-width: none; margin: 0; }
        .intro-section {
            width: 100%; min-width: 0; background: transparent; border-radius: 16px; padding: 32px; display: none;
            justify-content: space-between; align-items: center; border: 1px solid rgba(255,255,255,.2); box-shadow: 0 4px 20px rgba(0,0,0,.08);
        }
        .intro-section.active-tab { display: flex; }
        .intro-content { flex: 1; width: 100%; max-width: 100%; min-width: 0; }
        .intro-title { display: flex; align-items: center; gap: 10px; font-size: 24px; font-weight: 700; color: #38bdf8; margin-bottom: 20px; }
        .intro-content p { color: #334155; line-height: 1.8; font-size: 17px; margin-bottom: 16px; }
        .nd-cskcb-text { color: #000; line-height: 1.8; font-size: 17px; width: 100%; max-width: 100%; overflow-x: auto; overflow-wrap: anywhere; word-break: break-word; }
        .nd-cskcb-text img { max-width: 100%; height: auto; }
        .nd-cskcb-text table { width: 100%; max-width: 100%; border-collapse: collapse; margin: 12px 0 18px; table-layout: auto; }
        .nd-cskcb-text th, .nd-cskcb-text td { border: 1px solid #94a3b8; padding: 8px 10px; vertical-align: top; min-width: 48px; }
        .nd-cskcb-text th { background: #f1f5f9; font-weight: 700; }
        .stats-footer-bar {
            background: #fff; border: 0; position: fixed; bottom: 82px; left: 250px; width: calc(100% - 250px); z-index: 999;
            padding: 6px 30px; box-sizing: border-box; pointer-events: none;
        }
        .stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; max-width: 1200px; margin: 0 auto; }
        .stat-card { background: #f8fafc; border-radius: 10px; padding: 2px 12px; display: flex; align-items: center; gap: 10px; border: 1px solid #f1f5f9; }
        .stat-icon { width: 36px; height: 36px; font-size: 16px; border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
        .stat-icon.blue { background: #eff6ff; color: #3b82f6; } .stat-icon.green { background: #f0fdf4; color: #22c55e; }
        .stat-icon.purple { background: #faf5ff; color: #a855f7; } .stat-icon.orange { background: #fff7ed; color: #f97316; }
        .stat-card-blue { background: #eff6ff; border-color: #dbeafe; } .stat-card-green { background: #f0fdf4; border-color: #dcfce7; }
        .stat-card-purple { background: #faf5ff; border-color: #f3e8ff; } .stat-card-orange { background: #fff7ed; border-color: #ffedd5; }
        .stat-info { display: flex; flex-direction: column; line-height: 1.1; }
        .stat-info .value { margin: 0; font-size: 15px; font-weight: 700; color: #1d4ed8; }
        .stat-info .title { color: #334155; font-size: 12px; font-weight: 600; margin: 2px 0 1px; }
        .stat-info .desc { color: #64748b; font-size: 10px; margin: 0; }
        .stat-card-green .value { color: #16a34a; } .stat-card-purple .value { color: #9333ea; } .stat-card-orange .value { color: #ea580c; }
        .ytv-footer.ctc, .ytv-footer.ctc * { box-sizing: border-box; }
        .ytv-footer.ctc {
            position: fixed; left: 0; right: 0; bottom: 0; z-index: 1000; display: flex; flex-direction: column; gap: 6px;
            margin: 0; background: #e0f2fe; color: #334155; border-top: 4px solid #1d4ed8; padding: 14px 30px; max-width: none;
            align-items: center;
        }
        .ytv-footer.ctc a { color: inherit; text-decoration: none; }
        .fb2-top { display: flex; align-items: center; justify-content: center; gap: 26px; flex-wrap: wrap; }
        .fb2-call { display: flex; align-items: center; gap: 9px; font-size: 17px; font-weight: 800; color: #0b4ea2; white-space: nowrap; }
        .fb2-call i { font-size: 20px; }
        .fb2-ic { display: inline-flex; align-items: center; gap: 8px; font-size: 15px; font-weight: 600; color: #0b4ea2; }
        .fb2-pol { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; width: auto; font-size: 14px; gap: 6px 12px; }
        .fb2-pol a { color: #0f172a; }
        .fb2-pol .sep { color: #94a3b8; }
        @media (max-width: 992px) {
            body { padding-top: 62px; padding-bottom: 104px; overflow-x: hidden; }
            .ytv-header { min-height: 62px; padding: 8px 16px; }
            .ytv-logo-area { gap: 8px; min-width: 0; } .ytv-logo-area > img { width: 40px; height: 40px; }
            .ytv-logo-text { font-size: 20px; line-height: 1; }
            .ytv-header-title { min-width: 0; padding: 0 8px; }
            .ytv-header-title h1 { font-size: clamp(14px, 3.5vw, 18px); line-height: 1.2; }
            .ytv-header-title p { font-size: 11px; margin-top: 2px; }
            .ytv-header-menu { flex-shrink: 0; font-size: 22px; }
            .ytv-main { flex-direction: column; padding-left: 0; min-height: auto; }
            .ytv-sidebar { position: relative; top: 0; left: 0; width: 100%; padding: 10px 16px; border-right: 0; border-bottom: 1px solid #e2e8f0; }
            .sidebar-scroll-area { max-height: none; overflow-y: visible; }
            .ytv-menu-item { margin: 0; padding: 12px 16px; border-radius: 8px; white-space: nowrap; }
            .hospital-actions { padding: 0; margin: 0; }
            .hospital-actions .btn-outline { width: 100%; }
            .operating-hours-box { width: 100%; margin: 0; padding: 10px 16px; }
            .oh-header { margin-bottom: 4px; } .oh-days, .oh-time { font-size: 14px; } .oh-time { margin-bottom: 4px; }
            .ytv-content { width: 100%; max-width: none; min-height: calc(100vh - 98px); padding: 16px; gap: 16px; }
            .intro-section { padding: 20px; } .intro-title { font-size: 20px; line-height: 1.3; margin-bottom: 14px; }
            .intro-content .nd-cskcb-text { font-size: 15px; line-height: 1.65; }
            .stats-footer-bar { position: relative; inset: auto; width: 100%; padding: 0 16px 12px; background: transparent; pointer-events: auto; }
            .stats-grid { width: 100%; max-width: none; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
            .stat-card { min-width: 0; padding: 10px; gap: 8px; background: #f8fafc; border: 1px solid #e2e8f0; }
        }
        @media (max-width: 576px) {
            .ytv-header { padding-left: 10px; padding-right: 10px; }
            .ytv-logo-text { font-size: 17px; } .ytv-header-title h1 { font-size: 14px; } .ytv-header-title p { display: none; }
            .ytv-content { padding: 12px; } .intro-section { padding: 16px; }
            .stats-footer-bar { padding-left: 12px; padding-right: 12px; } .stats-grid { grid-template-columns: 1fr; }
            .stat-card { padding: 8px; } .stat-icon { width: 30px; height: 30px; font-size: 14px; }
            .stat-info .value { font-size: 14px; } .stat-info .title { font-size: 11px; } .stat-info .desc { font-size: 9px; }
        }
        @media (max-width: 767.98px) {
            .ytv-footer.ctc { align-items: stretch; gap: 6px; padding: 9px 12px; }
            .fb2-top { gap: 0; }
            .fb2-call { justify-content: center; width: 100%; font-size: 16px; gap: 7px; }
            .fb2-call i { font-size: 17px; }
            .fb2-ic, .fb2-pol .sep { display: none; }
            .fb2-pol { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 10px; width: 100%; font-size: 13px; line-height: 1.25; text-align: center; }
        }
    </style>
</head>
<body>
    <div class="ytv-header">
        <div class="ytv-logo-area"><img src="/static/icon-512.png" alt="Logo"><div class="ytv-logo-text">Y TẾ<br>VIỆT</div></div>
        <div class="ytv-header-title"><h1>${tenCoSo}</h1><p>Hotline: 19008198</p></div>
        <div class="ytv-header-menu"><i class="fas fa-bars"></i></div>
    </div>
    <div class="ytv-main">
        <div class="ytv-sidebar">
            <div class="sidebar-scroll-area">${menuHtml}</div>
            <div class="hospital-actions">
                <span class="btn-outline primary"><i class="fas fa-sign-in-alt"></i> Đăng ký khám</span>
                <span class="btn-outline"><i class="fas fa-sign-in-alt"></i> Đăng nhập</span>
            </div>
            <div class="operating-hours-box">
                <div class="oh-header"><i class="far fa-clock"></i> Giờ hoạt động</div>
                <div class="oh-days">${ngayLamViec}</div>
                <div class="oh-time">${gioMoCua} - ${gioDongCua}</div>
                <div class="oh-status"><span class="status-dot"></span> Đang mở cửa</div>
            </div>
        </div>
        <div class="ytv-content">${sectionsHtml}</div>
    </div>
    <div class="stats-footer-bar"><div class="stats-grid">
        <div class="stat-card stat-card-blue"><div class="stat-icon blue"><i class="fas fa-headset"></i></div><div class="stat-info"><div class="value">24/7</div><div class="title">Hỗ trợ</div><div class="desc">Luôn sẵn sàng</div></div></div>
        <div class="stat-card stat-card-green"><div class="stat-icon green"><i class="far fa-star"></i></div><div class="stat-info"><div class="value">4.8/5</div><div class="title">Đánh giá</div><div class="desc">Từ 1.248 khách hàng</div></div></div>
        <div class="stat-card stat-card-purple"><div class="stat-icon purple"><i class="fas fa-user-friends"></i></div><div class="stat-info"><div class="value">50+</div><div class="title">Bác sĩ</div><div class="desc">Giàu kinh nghiệm</div></div></div>
        <div class="stat-card stat-card-orange"><div class="stat-icon orange"><i class="fas fa-briefcase-medical"></i></div><div class="stat-info"><div class="value">20+</div><div class="title">Dịch vụ</div><div class="desc">Đa dạng chuyên khoa</div></div></div>
    </div></div>
    <div class="ytv-footer ctc">
        <div class="fb2-top">
            <span class="fb2-call"><i class="fas fa-headset"></i><b>0901 87 88 96</b><span class="fb2-h2">&nbsp;-&nbsp;0364 956 007</span></span>
            <span class="fb2-ic"><i class="fas fa-envelope"></i><span class="fb2-lb">sixossoft@gmail.com</span></span>
            <span class="fb2-ic"><i class="fas fa-globe"></i><span class="fb2-lb">sixossoft.com</span></span>
        </div>
        <div class="fb2-pol"><span>Chính sách bảo mật</span><span class="sep">·</span><span>Điều khoản sử dụng</span><span class="sep">·</span><span>Giải quyết khiếu nại</span><span class="sep">·</span><span>Chính sách bảo hành</span></div>
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

    async function renderStaticPreview(frame, editor) {
        var form = document.getElementById('coSoYTeForm');
        if (!form) throw new Error('Không tìm thấy biểu mẫu cơ sở y tế.');

        var isDetailEditor = editor.id === 'summernote';
        var currentContent = ensureDefaultBlackHtml(editor.getContent({ format: 'raw' }));

        // Preview chỉ dựng HTML tĩnh trong iframe. Không POST/fetch trang thật,
        // nên lỗi 500 hoặc redirect đăng nhập của endpoint không còn ảnh hưởng.
        if (isDetailEditor) {
            syncEditorValue('#summernote');
            syncTopicContents();
            frame.srcdoc = buildStaticDetailPreviewDocument(currentContent);
            return;
        }

        syncEditorValue('#advertisingContentEditor');
        frame.srcdoc = buildResponsivePreviewDocument(currentContent);
    }

    function openResponsivePreview(editor) {
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
                .cskcb-preview-dialog { display: flex; flex-direction: column; width: min(1600px, calc(100vw - 32px)); height: min(900px, 94vh); overflow: hidden; background: #e2e8f0; border-radius: 12px; box-shadow: 0 24px 60px rgba(15, 23, 42, .3); }
                .cskcb-preview-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 12px 16px; background: #fff; border-bottom: 1px solid #cbd5e1; color: #0f172a; }
                .cskcb-preview-actions { display: flex; align-items: center; gap: 8px; }
                .cskcb-preview-mode, .cskcb-preview-close { border: 1px solid #cbd5e1; border-radius: 6px; padding: 6px 12px; background: #fff; color: #334155; cursor: pointer; }
                .cskcb-preview-mode.active { border-color: #2563eb; background: #2563eb; color: #fff; }
                .cskcb-preview-close { padding: 2px 10px; font-size: 24px; line-height: 1.2; }
                .cskcb-preview-stage { display: flex; flex: 1; min-height: 0; align-items: flex-start; justify-content: flex-start; overflow: auto; padding: 24px; }
                .cskcb-preview-frame { height: 100%; min-height: 620px; margin: 0 auto; border: 1px solid #94a3b8; border-radius: 6px; background: #fff; box-shadow: 0 8px 20px rgba(15, 23, 42, .12); transition: width .2s ease; }
                .cskcb-preview-frame.desktop { width: 1440px; max-width: none; flex: 0 0 1440px; }
                .cskcb-preview-frame.mobile { width: 390px; max-width: 390px; flex: 0 0 390px; }
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
        frame.srcdoc = '<!doctype html><html lang="vi"><body style="font-family:system-ui;padding:24px">Đang tải bản xem trước...</body></html>';
        renderStaticPreview(frame, editor).catch(function (error) {
            var previewPageName = editor.id === 'summernote' ? 'trang chi tiết cơ sở' : 'trang Home';
            var previewWarning = 'Không thể dựng bản xem trước tĩnh ' + previewPageName + '.';
            frame.srcdoc = '<!doctype html><html lang="vi"><body style="font-family:system-ui;padding:24px">'
                + escapePreviewText(previewWarning) + '</body></html>';
            if (typeof showToast === 'function') showToast(previewWarning, 'warning');
            console.warn(previewWarning, error);
        });

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

    async function loadContent(topicIdOverride) {
        var topicId = topicIdOverride || document.getElementById('cboChuDe')?.value;
        var currentId = document.querySelector('input[name="Id"]')?.value;
        var topicHidden = document.getElementById('contentTopicId');
        var requestSequence = ++contentLoadSequence;
        activeTopicId = topicId || '';
        if (topicHidden) topicHidden.value = topicId || '';

        if (!currentId || !topicId) {
            setEditorContent('#summernote', synchronizeManagedLogo('', currentLogoUrl));
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
            contentDrafts[topicId] = ensureDefaultBlackHtml(synchronizeManagedLogo(data.noiDung || '', currentLogoUrl));
            setEditorContent('#summernote', contentDrafts[topicId]);
            syncTopicContents();
        } catch (error) {
            if (requestSequence !== contentLoadSequence) return;
            setEditorContent('#summernote', synchronizeManagedLogo('', currentLogoUrl));
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
        if (id === 'summernote') currentLogoUrl = getSelectedLogoUrl();

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
                    if (selector === '#advertisingContentEditor') {
                        var advertisingTextarea = document.querySelector(selector);
                        editor.setContent(advertisingTextarea?.value || '');
                    }
                    if (selector === '#summernote') {
                        loadContent();
                    }
                });
                if (selector === '#summernote') {
                    editor.on('input change undo redo SetContent', function () {
                        detectManagedLogoRemoval(editor);
                    });
                }
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

    document.addEventListener('cskcb:image-selected', function (event) {
        if (event.detail?.kind !== 'logo') return;
        applyLogoToDrafts(event.detail.url || '');
    });

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
        if (id === 'summernote') html = synchronizeManagedLogo(html, currentLogoUrl);
        editor.value = ensureDefaultBlackHtml(html);
    }

    function renderValidationErrors(form, errors) {
        form.querySelectorAll('[data-valmsg-for]').forEach(function (element) {
            element.textContent = '';
            element.classList.remove('field-validation-error');
            element.classList.add('field-validation-valid');
        });

        Object.entries(errors || {}).forEach(function (entry) {
            var fieldName = entry[0];
            var messages = Array.isArray(entry[1]) ? entry[1] : [entry[1]];
            var target = Array.from(form.querySelectorAll('[data-valmsg-for]'))
                .find(function (element) { return element.getAttribute('data-valmsg-for') === fieldName; });
            if (!target) return;

            target.textContent = messages.filter(Boolean).join(' ');
            target.classList.remove('field-validation-valid');
            target.classList.add('field-validation-error');
        });
    }

    function setSaveButtonState(form, isSaving) {
        var button = form.querySelector('button[type="submit"]');
        if (!button) return;

        if (isSaving) {
            button.dataset.originalText = button.textContent;
            button.disabled = true;
            button.textContent = 'Đang lưu...';
            return;
        }

        button.disabled = false;
        if (button.dataset.originalText) button.textContent = button.dataset.originalText;
    }

    async function submitCoSoYTeForm(event, form, topic) {
        event.preventDefault();
        if (form.dataset.saving === 'true') return;

        form.dataset.saving = 'true';
        setSaveButtonState(form, true);
        renderValidationErrors(form, {});

        try {
            var topicHidden = document.getElementById('contentTopicId');
            if (topicHidden && topic) topicHidden.value = topic.value || '';
            syncEditorValue('#summernote');
            syncEditorValue('#advertisingContentEditor');
            syncTopicContents();

            var response = await fetch(form.action || window.location.href, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: {
                    Accept: 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            var result = await response.json();

            if (!response.ok || !result.success) {
                renderValidationErrors(form, result.errors);
                throw new Error(result.message || 'Không thể lưu cơ sở y tế.');
            }

            if (result.id > 0 && result.editUrl) {
                form.action = result.editUrl;
                var idInput = form.querySelector('input[name="Id"]');
                if (!idInput) {
                    idInput = document.createElement('input');
                    idInput.type = 'hidden';
                    idInput.name = 'Id';
                    form.appendChild(idInput);
                }
                idInput.value = result.id;
                window.history.replaceState({}, '', result.editUrl);
            }

            if (typeof showToast === 'function') showToast(result.message, 'success');
        } catch (error) {
            if (typeof showToast === 'function') {
                var message = error instanceof Error
                    && error.name !== 'SyntaxError'
                    && error.message !== 'Failed to fetch'
                    ? error.message
                    : 'Không thể lưu cơ sở y tế.';
                showToast(message, 'error');
            }
        } finally {
            form.dataset.saving = 'false';
            setSaveButtonState(form, false);
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        initialize();
        var topic = document.getElementById('cboChuDe');
        if (topic) {
            topic.addEventListener('change', function () {
                cacheCurrentTopicContent(activeTopicId);
                activeTopicId = topic.value || '';
                ++contentLoadSequence;
                var topicHidden = document.getElementById('contentTopicId');
                if (topicHidden) topicHidden.value = topic.value || '';
                if (Object.prototype.hasOwnProperty.call(contentDrafts, topic.value)) {
                    setEditorContent('#summernote', contentDrafts[topic.value]);
                } else {
                    setEditorContent('#summernote', '');
                    loadContent(topic.value);
                }
            });
        }

        var form = document.getElementById('coSoYTeForm');
        if (form) {
            form.addEventListener('submit', function (event) {
                submitCoSoYTeForm(event, form, topic);
            });
        }

        loadContent();
    });
})();

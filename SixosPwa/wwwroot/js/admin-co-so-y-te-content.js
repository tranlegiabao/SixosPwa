(function () {
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

    async function loadContent() {
        var topicId = document.getElementById('cboChuDe')?.value;
        var currentId = document.querySelector('input[name="Id"]')?.value;
        var topicHidden = document.getElementById('contentTopicId');
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
            setEditorContent('#summernote', data.noiDung || '');
        } catch (error) {
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
            toolbar: 'undo redo | bold italic underline | fontsize | bullist numlist | ' +
                     'alignleft aligncenter alignright | table image link | ' +
                     'forecolor backcolor removeformat | code',
            paste_data_images: false,
            automatic_uploads: true,
            setup: function (editor) {
                editor.on('init', function () {
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
        if (topic) topic.addEventListener('change', loadContent);

        var form = document.getElementById('coSoYTeForm');
        if (form) {
            form.addEventListener('submit', function () {
                var topicHidden = document.getElementById('contentTopicId');
                if (topicHidden && topic) topicHidden.value = topic.value || '';
                syncEditorValue('#summernote');
                syncEditorValue('#advertisingContentEditor');
            });
        }

        loadContent();
    });
})();

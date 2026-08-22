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

        if (window.jQuery && jQuery.fn.summernote && jQuery(selector).next('.note-editor').length) {
            jQuery(selector).summernote('code', content || '');
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

    function uploadImage(file, editor) {
        var data = new FormData();
        data.append('file', file);
        jQuery.ajax({
            url: '/Admin/CoSoYTe/UploadImage',
            cache: false,
            contentType: false,
            processData: false,
            data: data,
            type: 'post',
            success: function (response) {
                jQuery(editor).summernote('insertImage', response.url);
            },
            error: function (xhr) {
                if (typeof showToast === 'function') showToast(xhr.responseText || 'Lỗi upload ảnh.', 'error');
            }
        });
    }

    function initializeEditor(selector, height) {
        if (!jQuery(selector).length) return;

        jQuery(selector).summernote({
            tabsize: 2,
            height: height,
            toolbar: [
                ['para', ['ul', 'ol', 'paragraph']],
                ['style', ['bold', 'underline', 'italic']],
                ['fontsize', ['fontsize']],
                ['color', ['color']],
                ['history', ['undo', 'redo']],
                ['height', ['height']],
                ['table', ['table']],
                ['insert', ['link', 'picture', 'video']]
            ],
            fontSizes: ['8', '9', '10', '11', '12', '14', '16', '18', '24', '36', '48', '64'],
            callbacks: {
                onImageUpload: function (files) {
                    for (var i = 0; i < files.length; i++) uploadImage(files[i], this);
                }
            }
        });
    }

    function initialize() {
        if (!window.jQuery || !jQuery.fn.summernote) return;

        initializeEditor('#summernote', 450);
        initializeEditor('#advertisingContentEditor', 250);
    }

    function syncEditorValue(selector) {
        var editor = document.querySelector(selector);
        if (!editor) return;

        var html = editor.value || '';
        if (window.jQuery && jQuery.fn.summernote && jQuery(selector).next('.note-editor').length) {
            html = jQuery(selector).summernote('code');
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

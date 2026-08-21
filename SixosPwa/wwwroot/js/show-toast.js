(function () {
    'use strict';

    const variantMap = { success: 'success', danger: 'error', error: 'error', warning: 'warning', info: 'info' };
    const iconMap = { success: '✓', error: '!', warning: '!', info: 'i' };

    function getContainer() {
        let container = document.getElementById('appToastContainer');
        if (container) return container;

        container = document.createElement('div');
        container.id = 'appToastContainer';
        container.className = 'app-toast-container';
        container.setAttribute('aria-live', 'polite');
        container.setAttribute('aria-atomic', 'true');
        document.body.appendChild(container);
        return container;
    }

    window.showToast = function (message, type = 'info', duration = 4500) {
        const variant = variantMap[type] || 'info';
        const toast = document.createElement('div');
        toast.className = `app-toast app-toast-${variant}`;
        toast.setAttribute('role', 'alert');

        const icon = document.createElement('span');
        icon.className = 'app-toast-icon';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = iconMap[variant];

        const content = document.createElement('div');
        content.className = 'app-toast-content';
        content.textContent = message == null ? '' : String(message);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'app-toast-close';
        close.setAttribute('aria-label', 'Đóng thông báo');
        close.textContent = '×';

        const dismiss = () => {
            toast.classList.remove('is-visible');
            window.setTimeout(() => toast.remove(), 180);
        };

        close.addEventListener('click', dismiss);
        toast.append(icon, content, close);
        getContainer().appendChild(toast);
        window.requestAnimationFrame(() => toast.classList.add('is-visible'));
        if (duration > 0) window.setTimeout(dismiss, duration);
    };
})();

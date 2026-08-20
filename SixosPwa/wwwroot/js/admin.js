(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-confirm]').forEach((form) => {
            form.addEventListener('submit', (event) => {
                const message = form.getAttribute('data-confirm');
                if (message && !window.confirm(message)) event.preventDefault();
            });
        });

        const firstInput = document.querySelector('.admin-form input:not([type="hidden"]):not([readonly])');
        if (firstInput && window.matchMedia('(min-width: 768px)').matches) firstInput.focus();
    });
})();

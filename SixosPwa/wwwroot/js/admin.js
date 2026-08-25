(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        if (window.TomSelect) {
            document.querySelectorAll('select.form-select:not([data-address-province]):not([data-address-ward])').forEach((select) => {
                if (select.tomselect) return;

                new TomSelect(select, {
                    create: false,
                    allowEmptyOption: true,
                    maxOptions: 100,
                    sortField: { field: 'text', direction: 'asc' }
                });
            });

            document.querySelectorAll('select.admin-filter-select').forEach((select) => {
                const form = select.closest('form');
                if (!form || select.dataset.autoSubmitBound === 'true') return;

                select.dataset.autoSubmitBound = 'true';
                select.addEventListener('change', () => form.requestSubmit());
            });
        }

        document.querySelectorAll('[data-confirm]').forEach((form) => {
            form.addEventListener('submit', (event) => {
                const message = form.getAttribute('data-confirm');
                if (message && !window.confirm(message)) event.preventDefault();
            });
        });

        document.querySelectorAll('[data-confirm-facility-delete]').forEach((form) => {
            form.addEventListener('submit', (event) => {
                const message = form.getAttribute('data-confirm-facility-delete');
                if (message && !window.confirm(message)) {
                    event.preventDefault();
                    return;
                }

                const confirmed = form.querySelector('input[name="confirmed"]');
                if (confirmed) confirmed.value = 'true';
            });
        });

        const firstInput = document.querySelector('.admin-form input:not([type="hidden"]):not([readonly])');
        if (firstInput && window.matchMedia('(min-width: 768px)').matches) firstInput.focus();
    });
})();

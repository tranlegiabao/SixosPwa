(() => {
    'use strict';

    document.addEventListener('DOMContentLoaded', () => {
        if (window.TomSelect) {
            document.querySelectorAll('select.form-select:not(.admin-query-select)').forEach((select) => {
                if (select.tomselect) return;

                new TomSelect(select, {
                    create: false,
                    allowEmptyOption: true,
                    maxOptions: 100,
                    sortField: { field: 'text', direction: 'asc' }
                });
            });

        document.querySelectorAll('select.admin-query-select').forEach((select) => {
            const form = select.closest('form');
            const queryName = select.dataset.queryName;
            const hiddenInput = form?.querySelector(`input[type="hidden"][name="${queryName}"]`);
            const initialValue = select.dataset.initialValue || '';
            if (!form || !hiddenInput || select.tomselect) return;

            let submitTimer;
            const submitSearch = (value) => {
                hiddenInput.value = value.trim();
                window.clearTimeout(submitTimer);
                submitTimer = window.setTimeout(() => form.requestSubmit(), 450);
            };

            const search = new TomSelect(select, {
                create: false,
                allowEmptyOption: true,
                maxItems: 1,
                closeAfterSelect: true,
                openOnFocus: false,
                onType: submitSearch,
                onClear: () => submitSearch('')
            });

            search.setTextboxValue(initialValue);
            search.control_input.addEventListener('input', () => {
                submitSearch(search.control_input.value);
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

        const firstInput = document.querySelector('.admin-form input:not([type="hidden"]):not([readonly])');
        if (firstInput && window.matchMedia('(min-width: 768px)').matches) firstInput.focus();
    });
})();

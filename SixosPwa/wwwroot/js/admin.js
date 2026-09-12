(() => {
    'use strict';

    /**
     * Hop xac nhan trong app, thay cho window.confirm cua trinh duyet.
     *
     * Vi sao khong dung window.confirm: no hien hop den mang ten mien
     * (".trycloudflare.com says") nam ngoai giao dien, khong theo bang mau nao
     * cua minh, va khong the doi chu tren nut cho ro dang lam gi.
     *
     * Hop duoc chen thang vao <body>: .admin-panel co overflow: hidden nen moi
     * position: fixed long ben trong no deu bi ghim lai trong khung panel.
     */
    function hoiXacNhan({ tieuDe, noiDung, nutXacNhan = 'Đồng ý', nutHuy = 'Hủy', nguyHiem = true }) {
        return new Promise((traLoi) => {
            const nen = document.createElement('div');
            nen.className = 'ytv-nen-mo';
            nen.style.zIndex = '1100'; // Hiển thị trên các modal khác
            nen.innerHTML = `
                <div class="ytv-hop-xac-nhan" role="alertdialog" aria-modal="true"
                     aria-labelledby="ytvHopTieuDe" aria-describedby="ytvHopNoiDung" style="max-width: 440px;">
                    <div class="ytv-hop-dau">
                        <span class="ytv-hop-bieu-tuong" style="${nguyHiem ? 'color: #d53f52; background: #fff1f3;' : 'color: #d97706; background: #fef3c7;'}">
                            <svg viewBox="0 0 24 24" aria-hidden="true">
                                <path d="M12 9v4"></path><path d="M12 17h.01"></path>
                                <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"></path>
                            </svg>
                        </span>
                        <h2 class="ytv-hop-tieu-de" id="ytvHopTieuDe"></h2>
                    </div>
                    <p class="ytv-hop-noi-dung" id="ytvHopNoiDung" style="white-space: pre-line; word-break: break-word; font-size: 13.5px;"></p>
                    <div class="ytv-hop-nut">
                        <button type="button" class="btn admin-btn-secondary" data-ytv-huy></button>
                        <button type="button" class="btn ${nguyHiem ? 'ytv-nut-nguy-hiem' : 'btn-warning text-white fw-bold'}" data-ytv-dong-y></button>
                    </div>
                </div>`;

            // Dat bang textContent chu khong noi vao chuoi HTML: noi dung den tu
            // thuoc tinh tren form, coi nhu du lieu chu khong phai ma.
            nen.querySelector('#ytvHopTieuDe').textContent = tieuDe || 'Xác nhận';
            nen.querySelector('#ytvHopNoiDung').textContent = noiDung || '';
            nen.querySelector('[data-ytv-huy]').textContent = nutHuy || 'Hủy';
            nen.querySelector('[data-ytv-dong-y]').textContent = nutXacNhan || 'Đồng ý';

            const dong = (ketQua) => {
                document.removeEventListener('keydown', khiGoPhim);
                nen.remove();
                document.body.classList.remove('ytv-khoa-cuon');
                traLoi(ketQua);
            };
            const khiGoPhim = (e) => { if (e.key === 'Escape') dong(false); };

            nen.querySelector('[data-ytv-huy]').addEventListener('click', () => dong(false));
            nen.querySelector('[data-ytv-dong-y]').addEventListener('click', () => dong(true));
            nen.addEventListener('click', (e) => { if (e.target === nen) dong(false); });
            document.addEventListener('keydown', khiGoPhim);

            document.body.classList.add('ytv-khoa-cuon');
            document.body.appendChild(nen);
            requestAnimationFrame(() => nen.classList.add('hien'));
            // Chon san nut Huy — go Enter theo quan tinh se KHONG xoa nham.
            nen.querySelector('[data-ytv-huy]').focus();
        });
    }

    window.hoiXacNhan = hoiXacNhan;
    window.showConfirm = function (noiDung, tieuDe = 'Xác nhận', nutXacNhan = 'Đồng ý', nguyHiem = true) {
        return hoiXacNhan({ tieuDe, noiDung, nutXacNhan, nguyHiem });
    };

    /**
     * Hop canh bao (modal alert) trong app: hien thong bao canh bao dang modal dep mat.
     */
    window.showModal = function (noiDung, tieuDe = 'Cảnh báo') {
        return new Promise((resolve) => {
            const old = document.querySelector('.ytv-nen-mo-canh-bao');
            if (old) old.remove();

            const nen = document.createElement('div');
            nen.className = 'ytv-nen-mo ytv-nen-mo-canh-bao';
            nen.style.zIndex = '1100'; // Noi tren cac modal khac
            nen.innerHTML = `
                <div class="ytv-hop-xac-nhan" role="alertdialog" aria-modal="true" style="max-width: 440px;">
                    <div class="ytv-hop-dau">
                        <span class="ytv-hop-bieu-tuong" style="color: #d97706; background: #fef3c7;">
                            <svg viewBox="0 0 24 24" aria-hidden="true">
                                <path d="M12 9v4"></path><path d="M12 17h.01"></path>
                                <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"></path>
                            </svg>
                        </span>
                        <h2 class="ytv-hop-tieu-de" id="ytvCanhBaoTieuDe"></h2>
                    </div>
                    <p class="ytv-hop-noi-dung" id="ytvCanhBaoNoiDung" style="white-space: pre-line; word-break: break-word; font-size: 13.5px;"></p>
                    <div class="ytv-hop-nut">
                        <button type="button" class="btn btn-warning text-white fw-bold px-4" style="background: #f59e0b; border-color: #f59e0b;" data-ytv-dong>Đồng ý</button>
                    </div>
                </div>`;

            nen.querySelector('#ytvCanhBaoTieuDe').textContent = tieuDe || 'Cảnh báo';
            nen.querySelector('#ytvCanhBaoNoiDung').textContent = noiDung || '';

            const dong = () => {
                document.removeEventListener('keydown', khiGoPhim);
                nen.remove();
                document.body.classList.remove('ytv-khoa-cuon');
                resolve();
            };
            const khiGoPhim = (e) => { if (e.key === 'Escape' || e.key === 'Enter') dong(); };

            nen.querySelector('[data-ytv-dong]').addEventListener('click', dong);
            nen.addEventListener('click', (e) => { if (e.target === nen) dong(); });
            document.addEventListener('keydown', khiGoPhim);

            document.body.classList.add('ytv-khoa-cuon');
            document.body.appendChild(nen);
            requestAnimationFrame(() => nen.classList.add('hien'));
            nen.querySelector('[data-ytv-dong]').focus();
        });
    };

    function noiHopXacNhan(form, noiDung, tieuDe, nutXacNhan, truocKhiGui) {
        if (!noiDung) return;

        form.addEventListener('submit', async (event) => {
            if (form.dataset.ytvDaXacNhan === 'true') return;   // luot gui that
            event.preventDefault();

            if (!await hoiXacNhan({ tieuDe, noiDung, nutXacNhan })) return;

            if (truocKhiGui) truocKhiGui();
            form.dataset.ytvDaXacNhan = 'true';
            // requestSubmit (khong phai submit) de con chay qua cac listener khac
            // dang gan tren form nay; co dataset o tren chan lap vo han.
            if (form.requestSubmit) form.requestSubmit(); else form.submit();
        });
    }

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
            noiHopXacNhan(form, form.getAttribute('data-confirm'), 'Xác nhận', 'Tiếp tục');
        });

        document.querySelectorAll('[data-confirm-facility-delete]').forEach((form) => {
            noiHopXacNhan(
                form,
                form.getAttribute('data-confirm-facility-delete'),
                'Xóa cơ sở y tế?',
                'Xóa cơ sở',
                () => {
                    const confirmed = form.querySelector('input[name="confirmed"]');
                    if (confirmed) confirmed.value = 'true';
                });
        });

        const firstInput = document.querySelector('.admin-form input:not([type="hidden"]):not([readonly])');
        if (firstInput && window.matchMedia('(min-width: 768px)').matches) firstInput.focus();
    });

    /**
     * Page Loader theo chuẩn HisSoft / loadingpage
     */
    function showPageloader() {
        const el = document.getElementById('page-loader-body');
        if (!el) return;
        el.style.display = 'flex';
        el.style.zIndex = '99999';
        requestAnimationFrame(() => {
            el.classList.add('show');
        });
    }

    function hidePageloader() {
        const el = document.getElementById('page-loader-body');
        if (!el) return;
        el.classList.remove('show');
        setTimeout(() => {
            if (!el.classList.contains('show')) {
                el.style.display = 'none';
                el.style.zIndex = '-1';
            }
        }, 180);
    }

    window.showPageloader = window.showPageLoader = showPageloader;
    window.hidePageloader = window.hidePageLoader = hidePageloader;
    window.loadingpage = function (show = true) {
        if (show === false || show === 'hide' || show === 0) {
            hidePageloader();
        } else {
            showPageloader();
        }
    };
    window.loadingPage = window.loadingpage;
    window.showLoadingPage = showPageloader;
    window.hideLoadingPage = hidePageloader;
})();

/*
 * pwa-install.js - dang ky service worker va dieu khien NUT CAI DAT.
 *
 * File nay ASCII-only co chu dich: moi cau chu tieng Viet nam trong Razor
 * (Views/Shared/_PwaInstall.cshtml), o day chi bat/tat phan tu.
 *
 * ---------------------------------------------------------------------------
 *  Vi sao phai co logic "nhan dien nen tang"?
 *
 *  Chrome / Edge (may tinh + Android): ban su kien `beforeinstallprompt`.
 *      Ta chan no lai, cat di, khi nguoi dung bam nut moi goi ra -> cai 1 cham.
 *
 *  Safari tren iPhone / iPad: Apple KHONG co su kien do va KHONG cho web tu goi
 *      cai dat. Cach duy nhat la nguoi dung tu bam Chia se > Them vao MH chinh.
 *      -> tren iOS, nut bam se mo BANG HUONG DAN chu khong the cai duoc.
 * ---------------------------------------------------------------------------
 */
(function () {
    'use strict';

    // -----------------------------------------------------------------------
    // 1. Dang ky service worker.
    //    Bat buoc phai co thi trinh duyet moi coi trang la "cai dat duoc".
    //    Chi chay duoc tren HTTPS hoac localhost (secure context).
    // -----------------------------------------------------------------------
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').catch(function (err) {
                console.warn('[PWA] Dang ky service worker that bai:', err);
            });
        });
    }

    // -----------------------------------------------------------------------
    // 2. Nhan dien moi truong.
    // -----------------------------------------------------------------------

    /** Dang chay o che do da cai (khong con thanh dia chi)? */
    function isStandalone() {
        var byMedia = window.matchMedia
            && window.matchMedia('(display-mode: standalone)').matches;

        // navigator.standalone la cach RIENG cua Safari iOS, khong theo chuan.
        var byIos = window.navigator.standalone === true;

        return Boolean(byMedia || byIos);
    }

    /** May iPhone / iPad? */
    function isIos() {
        var ua = window.navigator.userAgent;

        if (/iPhone|iPad|iPod/i.test(ua)) {
            return true;
        }

        // Bay: iPadOS 13 tro len tu khai minh la "Macintosh" de xin giao dien may
        // tinh. Phai kiem them cam ung moi phan biet duoc iPad voi macOS that.
        return /Macintosh/.test(ua) && navigator.maxTouchPoints > 1;
    }

    // -----------------------------------------------------------------------
    // 3. Hien trang thai "che do dang chay" o trang chao (neu trang do co).
    // -----------------------------------------------------------------------
    function showRunMode() {
        var el = document.getElementById(isStandalone() ? 'modeStandalone' : 'modeBrowser');
        if (el) {
            el.classList.remove('d-none');
        }
    }

    // -----------------------------------------------------------------------
    // 4. Nut cai dat.
    // -----------------------------------------------------------------------
    var deferredPrompt = null;

    window.addEventListener('beforeinstallprompt', function (e) {
        // Chan loi moi mac dinh cua trinh duyet de tu chon thoi diem hien.
        e.preventDefault();
        deferredPrompt = e;
    });

    function setupInstallButton() {
        var block = document.getElementById('pwaInstall');
        var button = document.getElementById('btnPwaInstall');
        if (!block || !button) {
            return; // trang nay khong co khoi cai dat -> khong lam gi
        }

        // Da cai roi thi giu nguyen an: bam nua cung khong ra gi, de lai chi lam
        // nguoi dung tuong phan mem loi.
        if (isStandalone()) {
            return;
        }

        block.classList.remove('d-none');

        button.addEventListener('click', function () {
            if (deferredPrompt) {
                // Duong sung suong: Chrome / Edge tren may tinh va Android.
                deferredPrompt.prompt();
                deferredPrompt.userChoice.then(function () {
                    // Loi moi chi dung duoc MOT lan, dung xong phai bo di.
                    deferredPrompt = null;
                });
                return;
            }

            // Khong cai 1 cham duoc -> mo bang huong dan dung nhanh.
            openGuide(isIos() ? 'ios' : 'other');
        });

        // Cai xong thi an nut ngay, khong cho tai lai trang.
        window.addEventListener('appinstalled', function () {
            deferredPrompt = null;
            block.classList.add('d-none');
        });
    }

    function openGuide(which) {
        var modalEl = document.getElementById('pwaGuideModal');
        if (!modalEl) {
            return;
        }

        // Chi bat dung mot nhanh huong dan.
        var sections = modalEl.querySelectorAll('[data-pwa-guide]');
        for (var i = 0; i < sections.length; i++) {
            var s = sections[i];
            s.classList.toggle('d-none', s.getAttribute('data-pwa-guide') !== which);
        }

        if (window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
        }
    }

    // -----------------------------------------------------------------------
    document.addEventListener('DOMContentLoaded', function () {
        showRunMode();
        setupInstallButton();
    });
})();

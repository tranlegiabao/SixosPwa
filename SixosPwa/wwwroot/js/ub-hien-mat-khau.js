// Nut con mat de xem lai mat khau vua go, cho bo man cua doi tac.
//
// Ca khuon cua Ung Buou lan man dang nhap cua SixosPwa deu KHONG co nut nay —
// ma benh nhan go mat khau tren dien thoai thi rat de sai, nhat la o man Dang ky
// co toi hai o phai khop nhau.
//
// Gan bang JS thay vi sua markup: ba o mat khau nam o hai view, moi o mot kieu
// boc khac nhau (o man Dang ky con co san icon khien ben trai). Lam o day thi
// markup cua khach giu nguyen, va them o mat khau moi cung tu co nut.
//
// Chu tieng Viet nam trong data-* cua the <script> chu khong nam trong tep .js —
// tranh rui ro sai bang ma.
(function () {
    'use strict';

    var MAT = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24"'
        + ' fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"'
        + ' stroke-linejoin="round" aria-hidden="true">'
        + '<path d="M10 12a2 2 0 1 0 4 0a2 2 0 0 0 -4 0" />'
        + '<path d="M21 12c-2.4 4 -5.4 6 -9 6c-3.6 0 -6.6 -2 -9 -6c2.4 -4 5.4 -6 9 -6c3.6 0 6.6 2 9 6" />'
        + '</svg>';

    var MAT_GACH = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24"'
        + ' fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"'
        + ' stroke-linejoin="round" aria-hidden="true">'
        + '<path d="M10.585 10.587a2 2 0 0 0 2.829 2.828" />'
        + '<path d="M16.681 16.673a8.717 8.717 0 0 1 -4.681 1.327c-3.6 0 -6.6 -2 -9 -6c1.272 -2.12 2.712 -3.678 4.32 -4.674m2.86 -1.146a9.055 9.055 0 0 1 1.82 -.18c3.6 0 6.6 2 9 6c-.666 1.11 -1.379 2.067 -2.138 2.87" />'
        + '<path d="M3 3l18 18" />'
        + '</svg>';

    function gan(o, nhan) {
        // Da gan roi thi thoi (view co the goi lai sau khi them o dong).
        if (o.dataset.coNutMat === '1') return;
        o.dataset.coNutMat = '1';

        var boc = document.createElement('div');
        boc.className = 'ub-mk';
        o.parentNode.insertBefore(boc, o);
        boc.appendChild(o);

        var nut = document.createElement('button');
        nut.type = 'button';
        nut.className = 'ub-mk__nut';
        nut.innerHTML = MAT;
        nut.setAttribute('aria-label', nhan.hien);
        nut.title = nhan.hien;

        nut.addEventListener('click', function () {
            var dangAn = o.type === 'password';
            o.type = dangAn ? 'text' : 'password';
            nut.innerHTML = dangAn ? MAT_GACH : MAT;
            var chu = dangAn ? nhan.an : nhan.hien;
            nut.setAttribute('aria-label', chu);
            nut.title = chu;
            o.focus();
        });

        boc.appendChild(nut);
    }

    // Phai doc NGAY o day: trong callback thi document.currentScript da la null.
    var the = document.currentScript || document.querySelector('script[data-nhan-hien]');
    var nhan = {
        hien: (the && the.dataset.nhanHien) || 'Hien mat khau',
        an: (the && the.dataset.nhanAn) || 'An mat khau'
    };

    document.addEventListener('DOMContentLoaded', function () {
        var cac = document.querySelectorAll('input[type="password"]');
        for (var i = 0; i < cac.length; i++) {
            gan(cac[i], nhan);
        }
    });
})();

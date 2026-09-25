function showTab(tabId, el, event) {
    // Chi doi noi dung tab; khong de lien ket hash tu dong nhay/cuon trang.
    if (event) {
        event.preventDefault();
    }

    // Handle active state in sidebar
    document.querySelectorAll('.ytv-menu-item').forEach(m => m.classList.remove('active'));
    el.classList.add('active');

    // Handle active content tab
    document.querySelectorAll('.intro-section').forEach(sec => sec.classList.remove('active-tab'));
    var targetSec = document.getElementById(tabId);
    if (targetSec) {
        targetSec.classList.add('active-tab');

        // Nam sua 2026-08-26: doi tab xong phai dua khung nhin ve dau muc.
        // Truoc day chi doi noi dung nen dang doc cuoi mot muc dai roi bam
        // sang muc khac la van dung nguyen cho cu - tieu de muc vua bam nam
        // ngoai man hinh, nhin nhu trang bi treo.
        // Chieu cao header doc DONG (offsetHeight) chu khong ghim 98, khoi
        // lech khi header doi chieu cao o kho man khac.
        var header = document.querySelector('.ytv-header');
        var caoHeader = header ? header.offsetHeight : 0;
        var dich = targetSec.getBoundingClientRect().top + window.scrollY - caoHeader - 12;
        var itDong = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        window.scrollTo({ top: Math.max(0, dich), behavior: itDong ? 'auto' : 'smooth' });
    }
}

function toggleMenu() {
    var menu = document.getElementById("dropdownMenu");
    menu.classList.toggle("open");
}

document.addEventListener("DOMContentLoaded", function() {
    var menuBtn = document.getElementById("menuBtn");
    if (menuBtn) {
        menuBtn.addEventListener("mouseleave", function() {
            var menu = document.getElementById("dropdownMenu");
            if (menu) menu.classList.remove("open");
        });
    }
});

window.onclick = function(event) {
    if (!event.target.closest('#menuBtn')) {
        var dropdowns = document.getElementsByClassName("ytv-dropdown");
        for (var i = 0; i < dropdowns.length; i++) {
            if (dropdowns[i].classList.contains('open')) {
                dropdowns[i].classList.remove('open');
            }
        }
    }
}

// JS cua man dang nhap noi trang da di theo modal (2026-08-24). Dung tim
// openLoginModal/closeLoginModal nua — hai nut deu la the <a> tro thang
// sang /DangNhap/Login?coSo={slug}&returnUrl=...

// ===== Modal chan dang nhap cheo co so (ADR 0006) =====

function ccsMoModal(returnUrl) {
    var lop = document.getElementById('ccsLopPhu');
    if (!lop) return;

    var nut = document.getElementById('ccsNutDangXuat');
    if (nut) {
        nut.href = '/DangNhap/DangXuat?denCoSo=' + encodeURIComponent(ccsSlugTrang || '') +
            '&returnUrl=' + encodeURIComponent(returnUrl || '/');
    }

    lop.hidden = false;
}

function ccsDongModal() {
    var lop = document.getElementById('ccsLopPhu');
    if (lop) lop.hidden = true;
}

// Goi tu onclick cua hai nut hanh dong khi phien dang o co so khac.
function ccsChanNeuCheoCoSo(event, linkEl) {
    event.preventDefault();
    ccsMoModal(linkEl.getAttribute('data-returnurl'));
    return false;
}

document.addEventListener('DOMContentLoaded', function () {
    var lop = document.getElementById('ccsLopPhu');
    if (lop) {
        // Bam ra ngoai hop (khong phai ban than hop) cung dong modal.
        lop.addEventListener('click', function (e) {
            if (e.target === lop) ccsDongModal();
        });
    }

    // Go thang /DangNhap/Login?coSo=... bi lech co so: server da doi ve day
    // kem ?canhBao=1 — tu mo dung modal nay, khong bat benh nhan bam lai nut.
    if (ccsTuDongMo && ccsPhienCoSoKhac) {
        var thamSo = new URLSearchParams(window.location.search);
        ccsMoModal(thamSo.get('returnUrl') || '/');
    }
});

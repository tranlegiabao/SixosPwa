    function toggleMenu() {
        var menu = document.getElementById("dropdownMenu");
        var btn = document.getElementById("menuBtn");
        var mo = menu.classList.toggle("open");
        if (btn) btn.setAttribute("aria-expanded", mo ? "true" : "false");
    }

    // Auto-close menu when mouse leaves menu area
    document.addEventListener("DOMContentLoaded", function() {
        // Slider: nhip 7s (cu 3s - khong kip doc het ten co so), dung khi re chuot
        // hoac khi ban phim di vao; dung han neu nguoi dung bat prefers-reduced-motion.
        // KHONG dat nut Tam dung tren man - user chot bo, xem PROGRESS.md muc 2.
        var mainSlider = document.getElementById('mainSlider');
        if (mainSlider && typeof bootstrap !== 'undefined') {
            var itHieuUng = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            var slider = new bootstrap.Carousel(mainSlider, {
                interval: itHieuUng ? false : 7000,
                pause: 'hover'
            });
            // Bootstrap 5.1: constructor KHONG tu chay - viec do la cua data-api khi thay
            // data-bs-ride tren the. Da bo thuoc tinh do (de JS quyet dinh theo
            // prefers-reduced-motion) nen phai goi cycle() tuong minh, khong co dong nay
            // la slider dung im. Da can that.
            if (!itHieuUng) { slider.cycle(); }
            mainSlider.addEventListener('focusin', function () { slider.pause(); });
        }

        var menuBtn = document.getElementById("menuBtn");
        if (menuBtn) {
            menuBtn.addEventListener("mouseleave", function() {
                var menu = document.getElementById("dropdownMenu");
                if (menu) menu.classList.remove("open");
            });
        }

    });

    // Close the dropdown menu if the user clicks outside of it
    window.onclick = function(event) {
        if (!event.target.closest('#menuBtn')) {
            var dropdowns = document.getElementsByClassName("ytv-dropdown");
            for (var i = 0; i < dropdowns.length; i++) {
                var openDropdown = dropdowns[i];
                if (openDropdown.classList.contains('open')) {
                    openDropdown.classList.remove('open');
                }
            }
        }
    }


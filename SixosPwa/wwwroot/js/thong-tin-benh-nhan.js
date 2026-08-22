    function toggleMenu() {
        var menu = document.getElementById("dropdownMenu");
        var btn = document.getElementById("menuBtn");
        var mo = menu.classList.toggle("open");
        if (btn) btn.setAttribute("aria-expanded", mo ? "true" : "false");
    }

    // Auto-close menu when mouse leaves menu area
    document.addEventListener("DOMContentLoaded", function() {
        // Slider: nguoi dung phai dung duoc, va ton trong prefers-reduced-motion
        var mainSlider = document.getElementById('mainSlider');
        if (mainSlider && typeof bootstrap !== 'undefined') {
            var itHieuUng = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            var slider = new bootstrap.Carousel(mainSlider, {
                interval: itHieuUng ? false : 5000,
                ride: itHieuUng ? false : 'carousel',
                pause: 'hover'
            });

            var dangChay = !itHieuUng;
            var nut = document.createElement('button');
            nut.type = 'button';
            nut.className = 'ytv-slider-dung';
            function veNut() {
                nut.innerHTML = dangChay
                    ? '<i class="fas fa-pause" aria-hidden="true"></i>'
                    : '<i class="fas fa-play" aria-hidden="true"></i>';
                nut.setAttribute('aria-label', dangChay ? 'Tạm dừng trình chiếu' : 'Chạy trình chiếu');
            }
            veNut();
            nut.addEventListener('click', function () {
                dangChay = !dangChay;
                if (dangChay) { slider.cycle(); } else { slider.pause(); }
                veNut();
            });
            mainSlider.appendChild(nut);

            // dung khi ban phim di vao slider
            mainSlider.addEventListener('focusin', function () { slider.pause(); });
        }

        var menuBtn = document.getElementById("menuBtn");
        if (menuBtn) {
            menuBtn.addEventListener("mouseleave", function() {
                var menu = document.getElementById("dropdownMenu");
                if (menu) menu.classList.remove("open");
            });
        }

        var serviceItems = document.querySelectorAll('.ytv-company-service');
        if (serviceItems.length > 1) {
            var activeServiceIndex = 0;
            window.setInterval(function() {
                serviceItems[activeServiceIndex].classList.remove('is-active');
                serviceItems[activeServiceIndex].setAttribute('aria-hidden', 'true');
                serviceItems[activeServiceIndex].setAttribute('tabindex', '-1');
                activeServiceIndex = (activeServiceIndex + 1) % serviceItems.length;
                serviceItems[activeServiceIndex].classList.add('is-active');
                serviceItems[activeServiceIndex].setAttribute('aria-hidden', 'false');
                serviceItems[activeServiceIndex].setAttribute('tabindex', '0');
            }, 5000);
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


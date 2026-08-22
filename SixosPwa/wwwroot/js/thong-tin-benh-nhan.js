    function toggleMenu() {
        var menu = document.getElementById("dropdownMenu");
        menu.classList.toggle("open");
    }

    // Auto-close menu when mouse leaves menu area
    document.addEventListener("DOMContentLoaded", function() {
        var mainSlider = document.getElementById('mainSlider');
        if (mainSlider && typeof bootstrap !== 'undefined') {
            new bootstrap.Carousel(mainSlider, {
                interval: 3000,
                ride: 'carousel'
            });
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


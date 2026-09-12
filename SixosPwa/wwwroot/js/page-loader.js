/**
 * Page Loader chuan HisSoft
 */
function showPageloader() {
    var el = document.getElementById("page-loader-body");
    if (el) {
        el.classList.add("show");
        el.style.zIndex = "1056";
    }
    if (window.jQuery) {
        window.jQuery("#page-loader-body").addClass("show").css("z-index", "1056");
    }
}

function hidePageloader() {
    var el = document.getElementById("page-loader-body");
    if (el) {
        el.classList.remove("show");
        el.style.zIndex = "-1";
    }
    if (window.jQuery) {
        window.jQuery("#page-loader-body").removeClass("show").css("z-index", "-1");
    }
}

window.showPageloader = showPageloader;
window.hidePageloader = hidePageloader;
window.showPageLoader = showPageloader;
window.hidePageLoader = hidePageloader;

window.loadingpage = function (show) {
    if (show === false || show === 'hide' || show === 0) {
        hidePageloader();
    } else {
        showPageloader();
    }
};
window.loadingPage = window.loadingpage;
window.showLoadingPage = showPageloader;
window.hideLoadingPage = hidePageloader;

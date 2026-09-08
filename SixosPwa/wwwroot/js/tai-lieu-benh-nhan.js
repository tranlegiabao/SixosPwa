/**
 * TAI-LIEU-BENH-NHAN.JS (Phiên bản v3)
 * Hiển thị dạng Grid Card Google Drive, bỏ tabs bộ lọc ngoài,
 * khi phóng to PDF có nút chuyển qua lại giữa các tài liệu CÙNG LOẠI,
 * chỉ giữ nút TẢI ẢNH (đã bỏ nút tải PDF).
 */

(function () {
    "use strict";

    // Cấu hình PDF.js worker
    if (window.pdfjsLib) {
        window.pdfjsLib.GlobalWorkerOptions.workerSrc = "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";
    }

    // Danh sách toàn bộ tài liệu được trích xuất từ DOM
    let allDocuments = [];
    let currentSameTypeDocs = []; // Danh sách các tài liệu CÙNG LOẠI với tài liệu đang mở

    // Vị trí tài liệu đang xem trong mảng currentSameTypeDocs
    let currentDocIndexInType = -1;

    // State quản lý xem PDF trang hiện tại
    let currentPdfDoc = null;
    let currentPageNum = 1;
    let totalPageCount = 1;
    let currentDocName = "TaiLieu";
    let currentPdfUrl = "";

    // Quản lý lazy render và cuộn liên tục nhiều trang
    let pageObserver = null;
    let pageRenderedMap = {};
    let isRenderingPageMap = {};

    // DOM Elements
    let modalOverlay = null;
    let viewerContainer = null;
    let pagesContainer = null;
    let pdfCanvas = null;
    let canvasCtx = null;
    let loaderEl = null;
    let docTitleEl = null;
    let docSubtitleEl = null;
    let pageInfoEl = null;
    let btnPrevPage = null;
    let btnNextPage = null;
    let btnDownloadPng = null;
    let toastEl = null;

    // Document switcher elements
    let currentDocIndexEl = null;
    let totalDocsEl = null;
    let btnPrevDocTop = null;
    let btnNextDocTop = null;
    let btnFloatPrev = null;
    let btnFloatNext = null;
    let groupLabelEl = null;

    // Dropdown menu 3 chấm
    let dropdownMenu = null;
    let currentSelectedDoc = null;

    // Khởi tạo khi DOM ready
    document.addEventListener("DOMContentLoaded", function () {
        initDOMElements();
        collectDocumentsFromDOM();
        initModalEvents();
        initDropdownEvents();
        loadAllCardThumbnails();
    });

    function initDOMElements() {
        modalOverlay = document.getElementById("tlModalViewer");
        viewerContainer = document.getElementById("tlViewerContainer");
        pagesContainer = document.getElementById("tlPagesContainer");
        pdfCanvas = document.getElementById("tlPdfCanvas");
        if (pdfCanvas) {
            canvasCtx = pdfCanvas.getContext("2d");
        }
        loaderEl = document.getElementById("tlViewerLoader");
        docTitleEl = document.getElementById("tlModalDocTitle");
        docSubtitleEl = document.getElementById("tlModalDocSubtitle");
        pageInfoEl = document.getElementById("tlPageInfo");
        btnPrevPage = document.getElementById("tlBtnPrevPage");
        btnNextPage = document.getElementById("tlBtnNextPage");
        btnDownloadPng = document.getElementById("tlBtnDownloadPng");
        toastEl = document.getElementById("tlToast");

        currentDocIndexEl = document.getElementById("tlCurrentDocIndex");
        totalDocsEl = document.getElementById("tlTotalDocs");
        btnPrevDocTop = document.getElementById("tlBtnPrevDocTop");
        btnNextDocTop = document.getElementById("tlBtnNextDocTop");
        btnFloatPrev = document.getElementById("tlBtnFloatPrev");
        btnFloatNext = document.getElementById("tlBtnFloatNext");
        groupLabelEl = document.getElementById("tlCurrentGroupLabel");

        dropdownMenu = document.getElementById("tlDropdownMenu");
    }

    /**
     * Thu thập danh sách các card tài liệu trên màn hình
     */
    function collectDocumentsFromDOM() {
        const cards = document.querySelectorAll(".tl-drive-card");
        allDocuments = [];
        cards.forEach((card, idx) => {
            allDocuments.push({
                id: card.getAttribute("data-id"),
                nhom: card.getAttribute("data-nhom"),
                loai: card.getAttribute("data-loai") || card.getAttribute("data-nhom"),
                loaiText: card.getAttribute("data-loai-text") || "Tài liệu y tế",
                name: card.getAttribute("data-name"),
                url: card.getAttribute("data-url"),
                date: card.getAttribute("data-date"),
                element: card,
                globalIndex: idx
            });
        });
    }

    /**
     * Tự động đọc file PDF và hiển thị hình ảnh trang đầu tiên thật của tài liệu vào khung thẻ (Google Drive style)
     */
    function loadAllCardThumbnails() {
        if (!window.pdfjsLib) return;

        const cards = document.querySelectorAll(".tl-drive-card");
        cards.forEach((card) => {
            const idx = card.getAttribute("data-index");
            const fileUrl = card.getAttribute("data-url");
            if (!fileUrl) return;

            const canvas = document.getElementById(`tlThumbCanvas_${idx}`);
            const loadingEl = document.getElementById(`tlThumbLoading_${idx}`);
            const fallbackEl = document.getElementById(`tlThumbFallback_${idx}`);
            const paperEl = document.getElementById(`tlThumbPaper_${idx}`);
            if (!canvas) return;

            const loadingTask = window.pdfjsLib.getDocument(fileUrl);
            loadingTask.promise.then(function (pdfDoc) {
                return pdfDoc.getPage(1);
            }).then(function (page) {
                const targetWidth = (paperEl ? paperEl.clientWidth : 180) || 180;
                const unscaledViewport = page.getViewport({ scale: 1 });

                // Phóng to vừa khít 100% chiều ngang khung thẻ để chữ to rõ ràng, thấy ngay nội dung
                const scale = targetWidth / unscaledViewport.width;
                const dpr = Math.max(window.devicePixelRatio || 1, 2.2);
                const viewport = page.getViewport({ scale: scale * dpr });

                canvas.height = viewport.height;
                canvas.width = viewport.width;
                canvas.style.width = "100%";
                canvas.style.height = "auto";

                const ctx = canvas.getContext("2d");
                const renderContext = {
                    canvasContext: ctx,
                    viewport: viewport
                };

                return page.render(renderContext).promise;
            }).then(function () {
                // Render thành công: Ẩn loading và fallback, hiện canvas trang 1 thật
                if (loadingEl) loadingEl.style.display = "none";
                if (fallbackEl) fallbackEl.style.display = "none";
                canvas.style.display = "block";
            }).catch(function (err) {
                console.warn(`Không thể tạo thumbnail trang 1 cho tài liệu ${idx}:`, err);
                if (loadingEl) loadingEl.style.display = "none";
            });
        });
    }

    /* ==========================================================
       IN-APP PDF VIEWER & CHUYỂN QUA LẠI GIỮA CÁC TÀI LIỆU CÙNG LOẠI
       ========================================================== */
    function initModalEvents() {
        // Nút trang nội bộ trong PDF
        if (btnPrevPage) btnPrevPage.addEventListener("click", onPrevPage);
        if (btnNextPage) btnNextPage.addEventListener("click", onNextPage);
        if (btnDownloadPng) btnDownloadPng.addEventListener("click", taiAnhPngHienTai);

        // Nút đóng modal
        const btnClose = document.getElementById("tlBtnCloseModal");
        if (btnClose) btnClose.addEventListener("click", dongModalXemTaiLieu);

        // Nút chuyển tài liệu ở Toolbar
        if (btnPrevDocTop) btnPrevDocTop.addEventListener("click", () => chuyenTaiLieuCungLoai(-1));
        if (btnNextDocTop) btnNextDocTop.addEventListener("click", () => chuyenTaiLieuCungLoai(1));

        // Nút chuyển tài liệu dạng Floating Navigation Arrows 2 bên mép
        if (btnFloatPrev) btnFloatPrev.addEventListener("click", () => chuyenTaiLieuCungLoai(-1));
        if (btnFloatNext) btnFloatNext.addEventListener("click", () => chuyenTaiLieuCungLoai(1));

        // Bắt sự kiện bàn phím (Phím mũi tên ← → chuyển tài liệu)
        document.addEventListener("keydown", function (e) {
            if (!modalOverlay || !modalOverlay.classList.contains("active")) return;

            if (e.key === "Escape") {
                dongModalXemTaiLieu();
            } else if (e.key === "ArrowLeft") {
                e.preventDefault();
                chuyenTaiLieuCungLoai(-1);
            } else if (e.key === "ArrowRight") {
                e.preventDefault();
                chuyenTaiLieuCungLoai(1);
            }
        });
    }

    /**
     * Mở viewer từ click card trên màn hình
     */
    window.moViewerTheoIndex = function (globalIdx) {
        const docObj = allDocuments[globalIdx];
        if (!docObj) return;

        // Lọc danh sách các tài liệu CÙNG LOẠI với tài liệu vừa bấm
        currentSameTypeDocs = allDocuments.filter(d => d.loai === docObj.loai);

        // Nếu chỉ có 1 tài liệu loại đó hoặc muốn duyệt mở rộng, giữ nhóm cùng loại
        let indexInType = currentSameTypeDocs.findIndex(d => d.id === docObj.id);
        if (indexInType === -1) {
            currentSameTypeDocs = [docObj];
            indexInType = 0;
        }

        moTaiLieuTheoIndexTrongLoai(indexInType);
    };

    /**
     * Mở tài liệu theo index trong danh sách cùng loại
     */
    function moTaiLieuTheoIndexTrongLoai(indexInType) {
        if (!modalOverlay || indexInType < 0 || indexInType >= currentSameTypeDocs.length) return;

        currentDocIndexInType = indexInType;
        const doc = currentSameTypeDocs[currentDocIndexInType];

        currentDocName = doc.name || "TaiLieu";
        currentPdfUrl = doc.url;
        currentPageNum = 1;
        totalPageCount = 1;

        // Cập nhật tiêu đề tài liệu
        if (docTitleEl) docTitleEl.textContent = currentDocName;
        if (docSubtitleEl) {
            docSubtitleEl.textContent = `${doc.loaiText} • ${doc.date}`;
        }

        if (groupLabelEl) {
            groupLabelEl.textContent = doc.loaiText;
        }

        // Cập nhật bộ đếm tài liệu cùng loại (ví dụ 1 / 3)
        updateDocSwitcherUI();

        // Mở modal
        modalOverlay.classList.add("active");
        document.body.style.overflow = "hidden";

        const viewerContainer = document.getElementById("tlViewerContainer");
        if (viewerContainer) {
            viewerContainer.scrollTop = 0;
        }

        // Tải và hiển thị PDF
        taiVaRenderPdf(currentPdfUrl);
    }

    /**
     * Chuyển sang tài liệu trước (-1) hoặc tài liệu sau (+1) CÙNG LOẠI
     */
    function chuyenTaiLieuCungLoai(step) {
        if (currentSameTypeDocs.length <= 1) {
            hienToast("Chỉ có 1 tài liệu thuộc loại này.");
            return;
        }

        const newIdx = currentDocIndexInType + step;
        if (newIdx < 0 || newIdx >= currentSameTypeDocs.length) return;

        moTaiLieuTheoIndexTrongLoai(newIdx);

        const doc = currentSameTypeDocs[newIdx];
        hienToast(`Đang xem: ${doc.name} (${newIdx + 1}/${currentSameTypeDocs.length})`);
    }

    /**
     * Cập nhật trạng thái các nút chuyển tài liệu
     */
    function updateDocSwitcherUI() {
        const total = currentSameTypeDocs.length;
        const currentNum = currentDocIndexInType + 1;

        if (currentDocIndexEl) currentDocIndexEl.textContent = currentNum;
        if (totalDocsEl) totalDocsEl.textContent = total;

        const isFirst = (currentDocIndexInType <= 0);
        const isLast = (currentDocIndexInType >= total - 1);

        if (btnPrevDocTop) btnPrevDocTop.disabled = isFirst;
        if (btnNextDocTop) btnNextDocTop.disabled = isLast;

        if (btnFloatPrev) btnFloatPrev.disabled = isFirst;
        if (btnFloatNext) btnFloatNext.disabled = isLast;
    }

    function taiVaRenderPdf(fileUrl) {
        if (loaderEl) {
            loaderEl.style.display = "flex";
            loaderEl.innerHTML = '<div class="tl-spinner"></div><div>Đang tải tài liệu...</div>';
        }

        if (pagesContainer) {
            pagesContainer.innerHTML = "";
        }
        pageRenderedMap = {};
        isRenderingPageMap = {};

        if (pageObserver) {
            if (typeof pageObserver.disconnect === "function") pageObserver.disconnect();
            pageObserver = null;
        }

        if (!window.pdfjsLib) {
            console.error("PDF.js library is not loaded.");
            if (loaderEl) loaderEl.innerHTML = '<span style="color:#ef4444;">Không thể tải trình đọc PDF.</span>';
            return;
        }

        const loadingTask = window.pdfjsLib.getDocument(fileUrl);
        loadingTask.promise.then(function (pdfDoc) {
            currentPdfDoc = pdfDoc;
            totalPageCount = pdfDoc.numPages;
            currentPageNum = 1;
            updatePageNavUI();

            // Khởi tạo danh sách các trang cuộn dọc liên tục
            khoiTaoDanhSachTrangCuonDoc(pdfDoc);
        }).catch(function (error) {
            console.error("Lỗi đọc PDF:", error);
            if (loaderEl) {
                loaderEl.innerHTML = '<span style="color:#ef4444;">Lỗi đọc tài liệu hoặc tệp không tồn tại.</span>';
            }
        });
    }

    /**
     * Dựng khung toàn bộ các trang để người dùng cuộn dọc liên tục (Continuous Scroll)
     */
    function khoiTaoDanhSachTrangCuonDoc(pdfDoc) {
        if (!pagesContainer) return;
        pagesContainer.innerHTML = "";

        // Lấy trang 1 để đo tỉ lệ chuẩn
        pdfDoc.getPage(1).then(function (firstPage) {
            const sidePadding = (window.innerWidth <= 600 ? 16 : 100);
            const containerWidth = Math.max((viewerContainer ? viewerContainer.clientWidth - sidePadding : 640), 280);

            const unscaledViewport = firstPage.getViewport({ scale: 1 });
            let scale = containerWidth / unscaledViewport.width;
            if (scale > 2.0) scale = 2.0;
            if (scale < 0.65) scale = 0.65;

            const dpr = window.devicePixelRatio || 1;
            const viewport = firstPage.getViewport({ scale: scale * dpr });
            const pageCssWidth = (viewport.width / dpr) + "px";
            const pageCssHeight = (viewport.height / dpr) + "px";

            // Tạo khung trang cho toàn bộ các trang (từ 1 đến totalPageCount)
            for (let p = 1; p <= totalPageCount; p++) {
                const pageItem = document.createElement("div");
                pageItem.className = "tl-pdf-page-item" + (p === 1 ? " active-page" : "");
                pageItem.id = "tlPageItem_" + p;
                pageItem.setAttribute("data-page", p);
                pageItem.style.width = pageCssWidth;
                pageItem.style.minHeight = pageCssHeight;

                const canvas = document.createElement("canvas");
                canvas.className = "tl-pdf-page-canvas";
                canvas.id = "tlPageCanvas_" + p;
                canvas.style.width = pageCssWidth;
                canvas.style.height = pageCssHeight;
                canvas.style.display = "none"; // ẩn canvas cho đến khi render xong

                const skeleton = document.createElement("div");
                skeleton.className = "tl-page-skeleton";
                skeleton.id = "tlPageSkeleton_" + p;
                skeleton.innerHTML = `<div class="tl-spinner"></div><div>Đang tải trang ${p}/${totalPageCount}...</div>`;
                skeleton.style.height = pageCssHeight;

                pageItem.appendChild(skeleton);
                pageItem.appendChild(canvas);

                if (totalPageCount > 1) {
                    const badge = document.createElement("div");
                    badge.className = "tl-page-badge";
                    badge.textContent = `Trang ${p} / ${totalPageCount}`;
                    pageItem.appendChild(badge);
                }

                pagesContainer.appendChild(pageItem);
            }

            // Ẩn loader tổng sau khi đã dựng khung
            if (loaderEl) loaderEl.style.display = "none";

            // Luôn cuộn lên đỉnh đầu tiên
            if (viewerContainer) viewerContainer.scrollTop = 0;

            // Render ngay trang 1 và 2 để người dùng đọc ngay lập tức
            renderTrang(1, scale, dpr);
            if (totalPageCount >= 2) {
                renderTrang(2, scale, dpr);
            }

            // Thiết lập Observer tự động render khi cuộn đến và theo dõi vị trí trang
            thietLapPageObserver(scale, dpr);
        });
    }

    /**
     * Render 1 trang cụ thể lên canvas của trang đó
     */
    function renderTrang(p, scale, dpr) {
        if (!currentPdfDoc || pageRenderedMap[p] || isRenderingPageMap[p]) return;
        isRenderingPageMap[p] = true;

        currentPdfDoc.getPage(p).then(function (page) {
            const pageCanvas = document.getElementById("tlPageCanvas_" + p);
            const skeleton = document.getElementById("tlPageSkeleton_" + p);
            if (!pageCanvas) {
                delete isRenderingPageMap[p];
                return;
            }

            const viewport = page.getViewport({ scale: scale * dpr });
            pageCanvas.height = viewport.height;
            pageCanvas.width = viewport.width;
            pageCanvas.style.width = (viewport.width / dpr) + "px";
            pageCanvas.style.height = (viewport.height / dpr) + "px";

            const ctx = pageCanvas.getContext("2d");
            const renderContext = {
                canvasContext: ctx,
                viewport: viewport
            };

            const renderTask = page.render(renderContext);
            renderTask.promise.then(function () {
                pageRenderedMap[p] = true;
                delete isRenderingPageMap[p];
                if (skeleton) skeleton.style.display = "none";
                pageCanvas.style.display = "block";
            }).catch(function (err) {
                console.warn("Lỗi render trang " + p, err);
                delete isRenderingPageMap[p];
            });
        }).catch(function (err) {
            console.warn("Lỗi getPage " + p, err);
            delete isRenderingPageMap[p];
        });
    }

    /**
     * Theo dõi cuộn trang để tự động tải các trang kế cận và cập nhật số trang đang xem
     */
    function thietLapPageObserver(scale, dpr) {
        if (pageObserver && typeof pageObserver.disconnect === "function") {
            pageObserver.disconnect();
        }

        // 1. Observer lazy-render khi trang sắp cuộn vào tầm nhìn (margin 400px)
        const lazyObserver = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    const pageNum = parseInt(entry.target.getAttribute("data-page"), 10);
                    if (pageNum && !pageRenderedMap[pageNum]) {
                        renderTrang(pageNum, scale, dpr);
                    }
                    if (pageNum && pageNum + 1 <= totalPageCount && !pageRenderedMap[pageNum + 1]) {
                        renderTrang(pageNum + 1, scale, dpr);
                    }
                }
            });
        }, {
            root: viewerContainer,
            rootMargin: "450px 0px"
        });

        // 2. Observer xác định trang chính đang chiếm nhiều diện tích nhất trong màn hình
        const activeObserver = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    const pageNum = parseInt(entry.target.getAttribute("data-page"), 10);
                    if (pageNum) {
                        currentPageNum = pageNum;
                        updatePageNavUI();

                        document.querySelectorAll(".tl-pdf-page-item").forEach(item => {
                            const isCur = parseInt(item.getAttribute("data-page"), 10) === currentPageNum;
                            item.classList.toggle("active-page", isCur);
                        });
                    }
                }
            });
        }, {
            root: viewerContainer,
            threshold: 0.4
        });

        document.querySelectorAll(".tl-pdf-page-item").forEach(item => {
            lazyObserver.observe(item);
            activeObserver.observe(item);
        });

        pageObserver = {
            disconnect: () => {
                lazyObserver.disconnect();
                activeObserver.disconnect();
            }
        };
    }

    window.dongModalXemTaiLieu = function () {
        if (!modalOverlay) return;
        modalOverlay.classList.remove("active");
        document.body.style.overflow = "";

        if (pageObserver && typeof pageObserver.disconnect === "function") {
            pageObserver.disconnect();
            pageObserver = null;
        }
        if (pagesContainer) {
            pagesContainer.innerHTML = "";
        }
        currentPdfDoc = null;
        pageRenderedMap = {};
        isRenderingPageMap = {};
    };

    function onPrevPage() {
        if (currentPageNum <= 1) return;
        const targetPage = currentPageNum - 1;
        const targetEl = document.getElementById("tlPageItem_" + targetPage);
        if (targetEl) {
            targetEl.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }

    function onNextPage() {
        if (currentPageNum >= totalPageCount) return;
        const targetPage = currentPageNum + 1;
        const targetEl = document.getElementById("tlPageItem_" + targetPage);
        if (targetEl) {
            targetEl.scrollIntoView({ behavior: "smooth", block: "start" });
        }
    }

    function updatePageNavUI() {
        if (pageInfoEl) {
            pageInfoEl.textContent = `${currentPageNum} / ${totalPageCount}`;
        }
        if (btnPrevPage) btnPrevPage.disabled = (currentPageNum <= 1);
        if (btnNextPage) btnNextPage.disabled = (currentPageNum >= totalPageCount);
    }

    /* ==========================================================
       TẢI ẢNH PNG SẮC NÉT TỪ CANVAS TRANG ĐANG XEM
       ========================================================== */
    function taiAnhPngHienTai() {
        const activeCanvas = document.getElementById("tlPageCanvas_" + currentPageNum);
        if (!activeCanvas || !currentPdfDoc) {
            hienToast("Trang hiện tại chưa sẵn sàng để tải ảnh.");
            return;
        }

        try {
            const imgData = activeCanvas.toDataURL("image/png");
            const safeName = (currentDocName || "TaiLieu").replace(/[^a-zA-Z0-9_\u00C0-\u1EF9-]/g, "_");
            const fileName = `${safeName}_Trang${currentPageNum}.png`;

            const link = document.createElement("a");
            link.href = imgData;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            hienToast(`Đã tải ảnh trang ${currentPageNum}/${totalPageCount}`);
        } catch (err) {
            console.error("Lỗi khi tải ảnh:", err);
            hienToast("Không thể tải ảnh do chính sách bảo mật trình duyệt.");
        }
    }

    /* ==========================================================
       DROPDOWN MENU 3 CHẤM
       ========================================================== */
    function initDropdownEvents() {
        document.addEventListener("click", function (e) {
            if (dropdownMenu && dropdownMenu.classList.contains("show")) {
                if (!dropdownMenu.contains(e.target) && !e.target.closest(".tl-drive-btn-more")) {
                    dropdownMenu.classList.remove("show");
                }
            }
        });

        const menuBtnView = document.getElementById("tlMenuBtnView");
        if (menuBtnView) {
            menuBtnView.addEventListener("click", function () {
                if (currentSelectedDoc) {
                    window.moViewerTheoIndex(currentSelectedDoc.globalIndex);
                }
                if (dropdownMenu) dropdownMenu.classList.remove("show");
            });
        }

        const menuBtnDownloadPng = document.getElementById("tlMenuBtnDownloadPng");
        if (menuBtnDownloadPng) {
            menuBtnDownloadPng.addEventListener("click", function () {
                if (currentSelectedDoc) {
                    // Mở xem và tự động kích hoạt tải ảnh
                    window.moViewerTheoIndex(currentSelectedDoc.globalIndex);
                    setTimeout(taiAnhPngHienTai, 800);
                }
                if (dropdownMenu) dropdownMenu.classList.remove("show");
            });
        }
    }

    window.moMenuTuyChon = function (event, docId, docName, fileUrl) {
        event.stopPropagation();
        if (!dropdownMenu) return;

        const globalIdx = allDocuments.findIndex(d => d.id == docId);
        currentSelectedDoc = { id: docId, name: docName, url: fileUrl, globalIndex: globalIdx };

        // Định vị menu cạnh nút 3 chấm
        const rect = event.currentTarget.getBoundingClientRect();
        dropdownMenu.style.top = (rect.bottom + window.scrollY + 4) + "px";
        dropdownMenu.style.left = (Math.max(10, rect.right - 160)) + "px";
        dropdownMenu.classList.add("show");
    };

    /* ==========================================================
       TOAST THÔNG BÁO
       ========================================================== */
    function hienToast(msg) {
        if (!toastEl) return;
        toastEl.textContent = msg;
        toastEl.classList.add("show");
        setTimeout(function () {
            toastEl.classList.remove("show");
        }, 3000);
    }

})();

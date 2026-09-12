// Quet ma QR o man Dang nhap. Tach khoi Login.cshtml de `node --check` bat duoc loi
// cu phap — `dotnet build` KHONG kiem JS nam trong .cshtml.
// Phu thuoc ra ngoai: showAlert() va guiOtp() do khoi <script> noi tuyen cua Login.cshtml
// khai bao o cap cao nhat (nen la ham toan cuc).
/* ================= QUÉT MÃ QR CCCD (CHUẨN NGÂN HÀNG) ================= */
// Loai ma dang cho quet: 'his' = ma tren phieu kham (co MaBN), 'cccd' = CCCD gan chip.
// Hai khuon deu 7 manh ngan bang '|', chi khac o thu hai, va MaBN la nvarchar(50) nen
// KHONG doan duoc theo hinh dang => nguoi benh tu khai bang nut nao ho bam.
let loaiQrDangCho = 'cccd';

// Danh tinh doc duoc tu ma, de guiOtp()/xacNhanOtp() gui kem xuong may chu.
// 🔴 Chi la DU LIEU. Moi ket luan "khop / duoc noi" deu tinh lai o may chu.
window.danhTinhQuet = null;

let html5QrScanner = null;

// 🔴 Co nay la thu con thieu truoc day. Khong co no thi dongModalQuetQr() va doiCamera()
// goi stop() tren mot scanner CHUA BAO GIO chay duoc => thu vien nem loi => man bao
// "Khong the chuyen doi camera", che mat loi that la camera khong mo duoc ngay tu dau.
let dangChayCamera = false;
let isFlashlightOn = false;
let currentFacingMode = "environment"; // camera sau
let customScanAnimId = null;
let nativeBarcodeDetector = null;

if (typeof window.BarcodeDetector !== 'undefined') {
    try {
        nativeBarcodeDetector = new window.BarcodeDetector({ formats: ['qr_code'] });
    } catch (e) {
        console.warn("BarcodeDetector init:", e);
    }
}

function phatAmThanhTing() {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.setValueAtTime(880, ctx.currentTime); // 880Hz
        gain.gain.setValueAtTime(0.2, ctx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.18);
        osc.start(ctx.currentTime);
        osc.stop(ctx.currentTime + 0.18);
    } catch (e) { }
}

// Vòng lặp quét song song bằng phần cứng Native BarcodeDetector + jsQR siêu nhạy cho CCCD
function batDauVongLapQuetSieuNhanh() {
    if (customScanAnimId) cancelAnimationFrame(customScanAnimId);

    const checkAndScan = async () => {
        const video = document.querySelector('#bankQrReader video');
        if (video && video.readyState >= 2 && !video.paused) {
            // 1. Quét bằng Native Hardware BarcodeDetector (Google Play Services / iOS)
            if (nativeBarcodeDetector) {
                try {
                    const barcodes = await nativeBarcodeDetector.detect(video);
                    if (barcodes && barcodes.length > 0 && barcodes[0].rawValue) {
                        onQrCodeSuccess(barcodes[0].rawValue);
                        return;
                    }
                } catch (e) { }
            }

            // 2. Quét dự phòng bằng jsQR tốc độ cao
            if (typeof jsQR !== 'undefined') {
                try {
                    if (!window._scanCanvas) {
                        window._scanCanvas = document.createElement('canvas');
                        window._scanCtx = window._scanCanvas.getContext('2d', { willReadFrequently: true });
                    }
                    const vw = video.videoWidth || 640;
                    const vh = video.videoHeight || 480;
                    if (window._scanCanvas.width !== vw || window._scanCanvas.height !== vh) {
                        window._scanCanvas.width = vw;
                        window._scanCanvas.height = vh;
                    }
                    window._scanCtx.drawImage(video, 0, 0, vw, vh);
                    const imgData = window._scanCtx.getImageData(0, 0, vw, vh);
                    const code = jsQR(imgData.data, vw, vh, { inversionAttempts: 'dontInvert' });
                    if (code && code.data) {
                        onQrCodeSuccess(code.data);
                        return;
                    }
                } catch (e) { }
            }
        }
        customScanAnimId = requestAnimationFrame(checkAndScan);
    };

    customScanAnimId = requestAnimationFrame(checkAndScan);
}

async function moModalQuetQr(loai) {
    const modal = document.getElementById('bankQrModal');
    if (!modal) return;

    loaiQrDangCho = (loai === 'his') ? 'his' : 'cccd';
    const tieuDe = document.getElementById('tieuDeQuet');
    const moTa   = document.getElementById('moTaQuet');
    if (tieuDe) tieuDe.textContent = loaiQrDangCho === 'his'
        ? 'Quét mã trên phiếu khám'
        : 'Quét CCCD gắn chip';
    if (moTa) moTa.textContent = loaiQrDangCho === 'his'
        ? 'Đưa mã QR trên phiếu khám phòng khám đưa cho bạn vào khung'
        : 'Đưa mã QR ở mặt sau thẻ Căn cước công dân vào khung';
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    await moLaiCamera();
}

/** Hien bang bao loi nam lai trong khung den (toast bay mat, khong doc kip tren dien thoai). */
function hienLoiCamera(lyDo, goiY) {
    const hop  = document.getElementById('loiCamera');
    const oLyDo = document.getElementById('loiCameraLyDo');
    const oGoiY = document.getElementById('loiCameraGoiY');
    if (oLyDo) oLyDo.textContent = lyDo || '';
    if (oGoiY) oGoiY.textContent = goiY || '';
    if (hop) hop.classList.remove('d-none');
}

function anLoiCamera() {
    const hop = document.getElementById('loiCamera');
    if (hop) hop.classList.add('d-none');
}

/** Doi ten loi cua trinh duyet sang cau nguoi dung lam duoc gi. */
function goiYTheoLoi(ten) {
    switch (ten) {
        case 'NotAllowedError':
        case 'PermissionDeniedError':
            return 'Trình duyệt đang chặn quyền camera. Bấm vào biểu tượng ổ khoá trên thanh địa chỉ, '
                 + 'bật lại quyền Camera cho trang này rồi bấm Thử lại.';
        case 'NotFoundError':
        case 'DevicesNotFoundError':
            return 'Máy không tìm thấy camera nào.';
        case 'NotReadableError':
        case 'TrackStartError':
            return 'Camera đang bị ứng dụng khác chiếm. Đóng app máy ảnh hoặc app gọi video rồi Thử lại.';
        case 'OverconstrainedError':
        case 'ConstraintNotSatisfiedError':
            return 'Camera không đáp ứng được cấu hình yêu cầu.';
        case 'SecurityError':
            return 'Trang phải mở bằng HTTPS thì trình duyệt mới cho dùng camera.';
        case 'NotSupportedError':
            return 'Trình duyệt này không cho trang dùng camera. Nếu đang mở trong ứng dụng khác '
                 + '(Zalo, Facebook...), hãy chọn "Mở bằng trình duyệt" rồi thử lại.';
        default:
            return 'Bạn vẫn quét được bằng nút "Chọn ảnh mã QR" ở dưới.';
    }
}

/**
 * Mo camera theo THANG BAC: bac dau dep nhat, hong thi tut xuong bac de song hon.
 *
 * 🔴 Bac cu chi co MOT cau hinh, va no mang hai thu hay lam vo may Android:
 * `aspectRatio: 1.0` (rat de thanh OverconstrainedError) va `width/height` ep san.
 * Hong thi no thu lai DUNG cai cu, roi nuot loi vao console — tren dien thoai thanh
 * mot o den im lim.
 */
async function moLaiCamera() {
    anLoiCamera();

    if (typeof Html5Qrcode === 'undefined') {
        hienLoiCamera('Chưa tải được thư viện quét mã.',
                      'Kiểm tra lại mạng rồi bấm Thử lại.');
        return;
    }

    try {
        if (!html5QrScanner) {
            html5QrScanner = new Html5Qrcode("bankQrReader", {
                experimentalFeatures: { useBarCodeDetectorIfSupported: true },
                verbose: false
            });
        }
    } catch (e) {
        hienLoiCamera(String(e && e.message || e), 'Tải lại trang rồi thử lại.');
        return;
    }

    // Khung quet vuong theo canh ngan, KHONG ep aspectRatio cua camera.
    const oQuet = function (w, h) {
        const canh = Math.floor(Math.min(w, h) * 0.72);
        return { width: canh, height: canh };
    };

    // 🔴 html5-qrcode CHI nhan facingMode la CHUOI hoac { exact: ... } — dua
    // { ideal: 'environment' } vao la no nem thang "should be string or object with
    // exact as key" truoc khi cham toi camera. Da dinh mot lan khi tu do bang bo thu.
    const bac = [
        { cam: { facingMode: 'environment' },            cauHinh: { fps: 15, qrbox: oQuet } },
        { cam: { facingMode: 'environment' },            cauHinh: { fps: 10 } },
        { cam: { facingMode: { exact: 'environment' } }, cauHinh: { fps: 10 } },
        { cam: { facingMode: 'user' },                   cauHinh: { fps: 10 } }
    ];

    const loi = [];
    for (const b of bac) {
        try {
            await html5QrScanner.start(b.cam, b.cauHinh, onQrCodeSuccess, function () { });
            dangChayCamera = true;
            anLoiCamera();
            batDauVongLapQuetSieuNhanh();
            return;
        } catch (e) {
            loi.push((e && e.name ? e.name : 'Loi') + ': ' + (e && e.message ? e.message : String(e)));
            try { await html5QrScanner.stop(); } catch (_) { }   // don sach truoc khi tut bac
            dangChayCamera = false;
        }
    }

    // Bac cuoi: hoi thang danh sach camera roi mo theo dinh danh thiet bi.
    try {
        const ds = await Html5Qrcode.getCameras();
        if (ds && ds.length) {
            const chon = ds[ds.length - 1];               // thuong la camera sau
            await html5QrScanner.start(chon.id, { fps: 10, qrbox: oQuet }, onQrCodeSuccess, function () { });
            dangChayCamera = true;
            anLoiCamera();
            batDauVongLapQuetSieuNhanh();
            return;
        }
        loi.push('getCameras: khong thay camera nao');
    } catch (e) {
        loi.push((e && e.name ? e.name : 'Loi') + ': ' + (e && e.message ? e.message : String(e)));
    }

    // Het duong. Lay loi CUOI chu khong phai loi DAU: bac dau co the hong vi cau hinh
    // cua chinh minh, con bac cuoi la don gian nhat nen no moi noi dung ve thiet bi
    // (mat quyen / khong co camera / camera dang bi chiem).
    const cuoi = loi.length ? loi[loi.length - 1] : 'Khong ro nguyen nhan';
    const tenLoi = cuoi.split(':')[0];
    hienLoiCamera(cuoi + '  [' + loi.length + ' lan thu]', goiYTheoLoi(tenLoi));
    console.error('Cac lan thu mo camera:', loi);
}

async function dongModalQuetQr() {
    if (customScanAnimId) {
        cancelAnimationFrame(customScanAnimId);
        customScanAnimId = null;
    }

    const modal = document.getElementById('bankQrModal');
    if (modal) modal.classList.remove('active');
    document.body.style.overflow = '';
    isFlashlightOn = false;

    anLoiCamera();

    // Chi dung khi DANG chay. Goi stop() tren scanner chua chay la tu nem loi ra.
    if (html5QrScanner && dangChayCamera) {
        try {
            await html5QrScanner.stop();
        } catch (e) {
            console.warn("Lỗi dừng scanner:", e);
        }
    }
    dangChayCamera = false;
}

function onQrCodeSuccess(decodedText, decodedResult) {
    phatAmThanhTing();
    if (navigator.vibrate) navigator.vibrate([60, 40, 60]);

    dongModalQuetQr();
    const text = (decodedText || '').trim();
    if (!text) {
        showAlert('Không đọc được nội dung mã. Bạn quét lại giúp nhé.', 'danger');
        return;
    }

    // 1. Ma chua duong dan. 🔴 CHI nhan duong dan cua chinh cong nay: truoc day nhanh
    //    nay lai thang toi BAT KY dia chi nao doc duoc, nen chi can dan mot ma QR gia
    //    o ghe cho la benh nhan quet bang chinh app roi ha canh o trang dang nhap gia.
    if (/^https?:\/\//i.test(text)) {
        let cungNha = false;
        try { cungNha = new URL(text).origin === window.location.origin; } catch (e) { }
        if (!cungNha) {
            showAlert('Mã này dẫn ra một trang ngoài cổng bệnh nhân nên đã bị bỏ qua.', 'danger');
            return;
        }
        showAlert('Đang mở liên kết...', 'info');
        setTimeout(function () { window.location.href = text; }, 400);
        return;
    }

    // 2. Ca hai khuon that deu la 7 manh ngan bang '|'.
    const parts = text.split('|');
    if (parts.length < 7) {
        showAlert('Mã này không phải mã trên phiếu khám hay CCCD gắn chip.', 'danger');
        return;
    }

    const oThuHai = (parts[1] || '').trim();

    // Bam nham nut thi noi thang, dung doan bua. Hai phep duoi deu chac chan:
    //   - ma cua HIS LUON co MaBN o o thu hai;
    //   - CCCD gan chip o do la so CMND cu: rong hoac dung 9 chu so.
    // Dung 9 chu so thuan = so CMND cu cua CCCD gan chip. Do that tren Dev_Master3
    // (54.672 ho so) va DaoTaoHis (1.498): KHONG co mot MaBN nao dai dung 9 chu so thuan
    // — Dev_Master3 dai 0-8, DaoTaoHis 3-14 va cai dai la co chu. Nen dau hieu nay chac.
    const laCmndCu = /^[0-9]{9}$/.test(oThuHai);

    if (loaiQrDangCho === 'his' && (oThuHai === '' || laCmndCu)) {
        showAlert('Mã này không có Mã bệnh nhân. Nếu là thẻ căn cước, bạn bấm "Quét CCCD gắn chip" nhé.', 'warning');
        return;
    }
    if (loaiQrDangCho === 'cccd' && oThuHai !== '' && !laCmndCu) {
        showAlert('Đây có vẻ là mã trên phiếu khám. Bạn bấm "Quét mã trên phiếu khám" nhé.', 'warning');
        return;
    }

    const dt = {
        nguon:    loaiQrDangCho,
        cccd:     (parts[0] || '').trim(),
        maBN:     loaiQrDangCho === 'his' ? oThuHai : '',
        hoTen:    (parts[2] || '').trim(),
        ngaySinh: (parts[3] || '').trim(),   // ddMMyyyy
        gioiTinh: (parts[4] || '').trim(),
        diaChi:   (parts[5] || '').trim()
    };

    if (!dt.hoTen && !dt.cccd) {
        showAlert('Mã đọc được nhưng không có thông tin nào dùng được.', 'danger');
        return;
    }

    window.danhTinhQuet = dt;
    veTheQuet(dt);
}

/** ddMMyyyy -> dd/MM/yyyy. Tra chuoi rong neu khong phai ngay that.
 *  🔴 01/01/1900 la ngay sinh GIA cua HIS (chi biet nam) — khong hien ra man. */
function doiNgayQr(s) {
    if (!/^[0-9]{8}$/.test(s || '')) return '';
    if (s === '01011900') return '';
    return s.slice(0, 2) + '/' + s.slice(2, 4) + '/' + s.slice(4);
}

/** Che bot Ma BN: chi giu 3 ky tu dau. Du de nhan ra, khong du de doc trom qua vai. */
function cheBotMa(ma) {
    if (!ma) return '';
    return ma.length <= 3 ? ma : ma.slice(0, 3) + '\u2022\u2022\u2022';
}

function thoatHtml(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
}

/** Do the ket qua len man va khoa o Can cuoc. Dung lai tren man chu KHONG phai toast:
 *  toast bay mat sau vai giay, ma day la cho duy nhat benh nhan phat hien minh vua
 *  quet nham ma cua nguoi khac. */
function veTheQuet(dt) {
    const khung = document.getElementById('khungTheQuet');
    const oCccd = document.getElementById('cccd');
    const ghiO  = document.getElementById('ghiOCccd');
    if (!khung) return;

    const ngay  = doiNgayQr(dt.ngaySinh);
    const dong2 = [dt.gioiTinh, ngay].filter(Boolean).join(' \u00b7 ');
    const dong3 = dt.nguon === 'his'
        ? 'M\u00e3 BN <span class="the-quet__ma">' + thoatHtml(cheBotMa(dt.maBN)) + '</span>'
        : thoatHtml(dt.diaChi);

    khung.innerHTML =
        '<div class="the-quet">' +
          '<div class="the-quet__dau">' +
            '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>' +
            '<span>' + (dt.nguon === 'his' ? '\u0110\u00e3 qu\u00e9t m\u00e3 tr\u00ean phi\u1ebfu kh\u00e1m' : '\u0110\u00e3 qu\u00e9t CCCD g\u1eafn chip') + '</span>' +
            '<button type="button" class="the-quet__quetlai" onclick="xoaTheQuet()">Qu\u00e9t l\u1ea1i</button>' +
          '</div>' +
          '<div class="the-quet__ten">' + thoatHtml(dt.hoTen) + '</div>' +
          (dong2 ? '<div class="the-quet__phu">' + thoatHtml(dong2) + '</div>' : '') +
          (dong3 ? '<div class="the-quet__phu">' + dong3 + '</div>' : '') +
        '</div>';
    khung.classList.remove('d-none');

    // O Can cuoc: dien va khoa, de khong lech voi ma vua quet. Ma KHONG co can cuoc
    // (HIS de trong — do that 17% ho so) thi de trong cho benh nhan tu go.
    if (oCccd) {
        if (dt.cccd) {
            oCccd.value = dt.cccd;
            oCccd.readOnly = true;
            oCccd.classList.add('o-tu-qr');
            if (ghiO) ghiO.classList.remove('d-none');
        } else {
            oCccd.readOnly = false;
            oCccd.classList.remove('o-tu-qr');
            if (ghiO) ghiO.classList.add('d-none');
            showAlert('Mã này chưa có số căn cước. Bạn nhập giúp số căn cước nhé.', 'warning');
        }
    }

    const oSdt = document.getElementById('soDienThoai');
    if (oSdt && !oSdt.value) oSdt.focus();
}

function xoaTheQuet() {
    window.danhTinhQuet = null;
    const khung = document.getElementById('khungTheQuet');
    const oCccd = document.getElementById('cccd');
    const ghiO  = document.getElementById('ghiOCccd');
    if (khung) { khung.innerHTML = ''; khung.classList.add('d-none'); }
    if (oCccd) { oCccd.value = ''; oCccd.readOnly = false; oCccd.classList.remove('o-tu-qr'); }
    if (ghiO)  ghiO.classList.add('d-none');
}

async function toggleDenFlash() {
    if (!html5QrScanner) return;
    try {
        isFlashlightOn = !isFlashlightOn;
        await html5QrScanner.applyVideoConstraints({
            advanced: [{ torch: isFlashlightOn }]
        });
        const btnTorch = document.getElementById('btnTorch');
        if (btnTorch) {
            btnTorch.style.background = isFlashlightOn ? 'rgba(255, 215, 0, 0.4)' : '';
            btnTorch.style.color = isFlashlightOn ? '#fde047' : '#fff';
        }
    } catch (e) {
        showAlert('Thiết bị hoặc camera này không hỗ trợ bật đèn flash.', 'info');
    }
}

async function doiCamera() {
    currentFacingMode = (currentFacingMode === "environment") ? "user" : "environment";

    // Chua chay lan nao thi day khong phai "doi camera" — day la mo lan dau.
    if (!html5QrScanner || !dangChayCamera) {
        await moLaiCamera();
        return;
    }

    try {
        await html5QrScanner.stop();
        dangChayCamera = false;
        await html5QrScanner.start(
            { facingMode: currentFacingMode },
            {
                fps: 15,
                qrbox: function (w, h) {
                    const canh = Math.floor(Math.min(w, h) * 0.72);
                    return { width: canh, height: canh };
                }
            },
            onQrCodeSuccess,
            function () { });
        dangChayCamera = true;
        batDauVongLapQuetSieuNhanh();
    } catch (e) {
        console.error("Lỗi đổi camera:", e);
        dangChayCamera = false;
        hienLoiCamera((e && e.name ? e.name + ': ' : '') + (e && e.message ? e.message : String(e)),
                      'Bấm Thử lại để mở bằng camera mặc định.');
    }
}

async function xuLyAnhQrTuThuVien(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;

    showAlert('Đang xử lý ảnh QR...', 'info');

    try {
        // 1. Thử giải mã bằng Native BarcodeDetector (Phần cứng Android/iOS cực nhạy)
        if (nativeBarcodeDetector && typeof createImageBitmap !== 'undefined') {
            try {
                const bitmap = await createImageBitmap(file);
                const barcodes = await nativeBarcodeDetector.detect(bitmap);
                if (barcodes && barcodes.length > 0 && barcodes[0].rawValue) {
                    onQrCodeSuccess(barcodes[0].rawValue);
                    return;
                }
            } catch (e) {
                console.warn("Native file scan failed, trying fallback...", e);
            }
        }

        // 2. Thử giải mã bằng Html5Qrcode
        try {
            if (!html5QrScanner) {
                html5QrScanner = new Html5Qrcode("bankQrReader");
            }
            const decodedText = await html5QrScanner.scanFile(file, true);
            if (decodedText) {
                onQrCodeSuccess(decodedText);
                return;
            }
        } catch (e) { }

        // 3. Thử giải mã bằng jsQR qua Canvas
        if (typeof jsQR !== 'undefined') {
            const img = new Image();
            img.src = URL.createObjectURL(file);
            await new Promise(res => { img.onload = res; img.onerror = res; });
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            canvas.width = img.width;
            canvas.height = img.height;
            ctx.drawImage(img, 0, 0);
            const imgData = ctx.getImageData(0, 0, img.width, img.height);
            const code = jsQR(imgData.data, img.width, img.height, { inversionAttempts: 'both' });
            URL.revokeObjectURL(img.src);
            if (code && code.data) {
                onQrCodeSuccess(code.data);
                return;
            }
        }

        showAlert('Không nhận diện được mã QR trong ảnh. Vui lòng chụp rõ và gần mã QR hơn nhé.', 'danger');
    } catch (err) {
        console.error("Lỗi xử lý ảnh:", err);
        showAlert('Không nhận diện được mã QR trong ảnh.', 'danger');
    } finally {
        event.target.value = '';
    }
}

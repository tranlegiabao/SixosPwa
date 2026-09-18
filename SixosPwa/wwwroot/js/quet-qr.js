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

function chuanHoaKhoa(k) {
    if (!k) return '';
    return k.toLowerCase()
        .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
        .replace(/[^a-z0-9]/g, '');
}

function chuanHoaGioiTinh(gt) {
    if (!gt) return '';
    const s = gt.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim();
    if (['nam', 'male', '1', 'm'].includes(s)) return 'Nam';
    if (['nu', 'female', '2', 'f'].includes(s)) return 'Nữ';
    return gt;
}

/** Phân tích thông minh mọi định dạng mã QR: Key-Value, JSON, Pipe |, v.v. */
function phanTichMaQr(rawText, loaiGoiY) {
    const text = (rawText || '').trim();
    if (!text) return null;

    let dt = {
        nguon: loaiGoiY || 'cccd',
        cccd: '',
        maBN: '',
        hoTen: '',
        dienThoai: '',
        ngaySinh: '',
        gioiTinh: '',
        diaChi: ''
    };

    // 1. Thử giải mã định dạng JSON
    if ((text.startsWith('{') && text.endsWith('}')) || (text.startsWith('[') && text.endsWith(']'))) {
        try {
            const obj = JSON.parse(text);
            const target = Array.isArray(obj) ? obj[0] : obj;
            if (target && typeof target === 'object') {
                for (const [key, val] of Object.entries(target)) {
                    const k = chuanHoaKhoa(key);
                    const v = String(val == null ? '' : val).trim();
                    if (!v) continue;
                    if (['mabn', 'mabenhnhan', 'pid', 'patientid', 'idbenhnhan'].includes(k)) dt.maBN = v;
                    else if (['socccd', 'cccd', 'cmnd', 'socmnd', 'sodinhdanh', 'citizenid', 'idcard'].includes(k)) dt.cccd = v;
                    else if (['tenbn', 'hoten', 'tenbenhnhan', 'hovaten', 'fullname', 'name'].includes(k)) dt.hoTen = v;
                    else if (['dienthoai', 'sdt', 'sodienthoai', 'phone', 'phonenumber', 'mobile'].includes(k)) dt.dienThoai = v;
                    else if (['diachi', 'address', 'fulladdress'].includes(k)) dt.diaChi = v;
                    else if (['ngaysinh', 'dob', 'dateofbirth', 'birthdate', 'namsinh'].includes(k)) dt.ngaySinh = v;
                    else if (['gioitinh', 'gender', 'sex'].includes(k)) dt.gioiTinh = v;
                }
                if (dt.maBN) dt.nguon = 'his';
                if (dt.hoTen || dt.cccd || dt.maBN || dt.dienThoai) return dt;
            }
        } catch (e) { }
    }

    // 2. Thử định dạng Key-Value (Dòng chứa MaBN: ..., SoCCCD: ..., TenBN: ..., v.v.)
    const dongLines = text.split(/\r?\n|(?<=[^\s])(?=(?:MaBN|SoCCCD|TenBN|DienThoai|DiaChi|NgaySinh|GioiTinh|CCCD|Mã BN|Họ tên|SĐT|Địa chỉ|Ngày sinh|Giới tính)\s*[:=])/i);
    let coKeyVal = false;
    for (const rawLine of dongLines) {
        const line = rawLine.trim();
        if (!line) continue;
        const match = line.match(/^([^:=]+)[:=]\s*(.+)$/);
        if (match) {
            const k = chuanHoaKhoa(match[1]);
            const v = match[2].trim();
            if (['mabn', 'mabenhnhan', 'pid', 'patientid', 'idbenhnhan'].includes(k)) { dt.maBN = v; coKeyVal = true; }
            else if (['socccd', 'cccd', 'cmnd', 'socmnd', 'sodinhdanh', 'citizenid'].includes(k)) { dt.cccd = v; coKeyVal = true; }
            else if (['tenbn', 'hoten', 'tenbenhnhan', 'hovaten', 'name', 'fullname'].includes(k)) { dt.hoTen = v; coKeyVal = true; }
            else if (['dienthoai', 'sdt', 'sodienthoai', 'phone', 'phonenumber', 'mobile'].includes(k)) { dt.dienThoai = v; coKeyVal = true; }
            else if (['diachi', 'address'].includes(k)) { dt.diaChi = v; coKeyVal = true; }
            else if (['ngaysinh', 'dob', 'dateofbirth', 'birthdate', 'namsinh'].includes(k)) { dt.ngaySinh = v; coKeyVal = true; }
            else if (['gioitinh', 'gender', 'sex'].includes(k)) { dt.gioiTinh = v; coKeyVal = true; }
        }
    }
    if (coKeyVal && (dt.hoTen || dt.cccd || dt.maBN || dt.dienThoai)) {
        if (dt.maBN) dt.nguon = 'his';
        return dt;
    }

    // 3. Định dạng chuẩn phân cách bằng dấu gạch đứng '|' (CCCD gắn chip / HIS)
    const parts = text.split('|');
    if (parts.length >= 4) {
        const oThuHai = (parts[1] || '').trim();
        const laCmndCu = /^[0-9]{9}$/.test(oThuHai);

        dt.cccd = (parts[0] || '').trim();
        dt.maBN = (loaiGoiY === 'his' || (!laCmndCu && oThuHai !== '')) ? oThuHai : '';
        dt.hoTen = (parts[2] || '').trim();
        dt.ngaySinh = (parts[3] || '').trim();
        dt.gioiTinh = (parts[4] || '').trim();
        dt.diaChi = (parts[5] || '').trim();
        if (parts[6] && /^(0[35789]\d{8}|\+84\d{9})$/.test(parts[6].trim())) {
            dt.dienThoai = parts[6].trim();
        }

        if (dt.maBN) dt.nguon = 'his';
        if (dt.hoTen || dt.cccd || dt.maBN) return dt;
    }

    // 4. Nếu chỉ có chuỗi 12 số CCCD hoặc SĐT
    if (/^\d{12}$/.test(text)) {
        dt.cccd = text;
        return dt;
    }
    if (/^(0[35789]\d{8}|\+84\d{9})$/.test(text)) {
        dt.dienThoai = text;
        return dt;
    }

    return null;
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

    // 1. Mã chứa đường dẫn URL
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

    // 2. Phân tích mã QR
    const dt = phanTichMaQr(text, loaiQrDangCho);
    if (!dt || (!dt.hoTen && !dt.cccd && !dt.maBN && !dt.dienThoai)) {
        showAlert('Không nhận diện được định dạng thông tin trong mã QR này.', 'danger');
        return;
    }

    // 3. Bam nham nut thi noi thang, dung doan bua. Phep nay phai xet SAU khi phan
    //    tich, tren chinh MaBN doc duoc: khuon that KHONG phai luc nao cung du 7 manh
    //    '|' (phanTichMaQr nhan tu 4 manh tro len, lai con khuon JSON va key=value),
    //    nen dem so manh la lot — ma phieu kham it manh se chui qua cua CCCD.
    const maBnQuet = (dt.maBN || '').trim();
    // CCCD gan chip de so CMND CU o dung o cua MaBN: rong hoac dung 9 chu so. Do that
    // tren Dev_Master3 (54.672 ho so) va DaoTaoHis (1.498): KHONG mot MaBN nao dai
    // dung 9 chu so thuan, nen dau hieu nay chac.
    const laCmndCu = /^[0-9]{9}$/.test(maBnQuet);
    if (loaiQrDangCho === 'his' && (maBnQuet === '' || laCmndCu)) {
        showAlert('Mã này không có Mã bệnh nhân. Nếu là thẻ căn cước, bạn bấm "Quét CCCD gắn chip" nhé.', 'warning');
        return;
    }
    if (loaiQrDangCho === 'cccd' && maBnQuet !== '' && !laCmndCu) {
        showAlert('Đây có vẻ là mã trên phiếu khám. Bạn bấm "Quét mã trên phiếu khám" nhé.', 'warning');
        return;
    }

    window.danhTinhQuet = dt;
    if (typeof setCookie === 'function') {
        setCookie('qr_data', encodeURIComponent(JSON.stringify(dt)), 365);
    } else {
        try {
            document.cookie = 'qr_data=' + encodeURIComponent(JSON.stringify(dt)) + ';expires=' + new Date(Date.now() + 365*864e5).toUTCString() + ';path=/;SameSite=Lax';
        } catch (e) {}
    }
    veTheQuet(dt);

    // 4. CHI luong PHIEU KHAM HIS moi di thang vao man OTP (tai khoan lay theo MaBN).
    //    Luong CCCD thi DUNG LAI o buoc 1: ma QR khong bao gio mang so dien thoai,
    //    benh nhan phai tu nhap SDT that cua ho roi bam Tiep tuc.
    const laQrHis = dt.nguon === 'his' || !!dt.maBN;
    if (laQrHis) {
        if (typeof guiOtp === 'function') guiOtp();
        return;
    }

    const oSdtSauQuet = document.getElementById('soDienThoai');
    if (oSdtSauQuet) {
        if (!oSdtSauQuet.value.trim()) {
            showAlert('Đã đọc xong thẻ căn cước. Bạn nhập <strong>số điện thoại</strong> rồi bấm <strong>Tiếp tục</strong> để nhận mã xác thực nhé.', 'info');
        }
        oSdtSauQuet.focus();
    }
}

/** ddMMyyyy / yyyy-MM-dd / ISO -> dd/MM/yyyy */
function doiNgayQr(s) {
    if (!s) return '';
    s = String(s).trim();
    if (s === '01011900' || s.startsWith('1900-01-01')) return '';
    if (/^[0-9]{8}$/.test(s)) {
        return s.slice(0, 2) + '/' + s.slice(2, 4) + '/' + s.slice(4);
    }
    const isoMatch = s.match(/^(\d{4})[-/](\d{1,2})[-/](\d{1,2})/);
    if (isoMatch) {
        const y = isoMatch[1];
        const m = isoMatch[2].padStart(2, '0');
        const d = isoMatch[3].padStart(2, '0');
        if (y === '1900' && m === '01' && d === '01') return '';
        return `${d}/${m}/${y}`;
    }
    const dmyMatch = s.match(/^(\d{1,2})[-/](\d{1,2})[-/](\d{4})/);
    if (dmyMatch) {
        const d = dmyMatch[1].padStart(2, '0');
        const m = dmyMatch[2].padStart(2, '0');
        const y = dmyMatch[3];
        if (y === '1900' && m === '01' && d === '01') return '';
        return `${d}/${m}/${y}`;
    }
    return s;
}

/** Che bớt Mã BN nếu cần */
function cheBotMa(ma) {
    if (!ma) return '';
    return ma.length <= 3 ? ma : ma.slice(0, 3) + '\u2022\u2022\u2022';
}

function thoatHtml(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
}

/** Hiển thị thẻ kết quả lên màn hình */
function veTheQuet(dt) {
    const khung = document.getElementById('khungTheQuet');
    const oCccd = document.getElementById('cccd');
    const oSdt  = document.getElementById('soDienThoai');
    const ghiO  = document.getElementById('ghiOCccd');
    if (!khung) return;

    if (dt.gioiTinh) dt.gioiTinh = chuanHoaGioiTinh(dt.gioiTinh);

    const ngay  = doiNgayQr(dt.ngaySinh);
    const cacMucDong2 = [];
    if (dt.gioiTinh) cacMucDong2.push(dt.gioiTinh);
    if (ngay) cacMucDong2.push(ngay);
    if (dt.dienThoai) cacMucDong2.push('SĐT: ' + dt.dienThoai);
    const dong2 = cacMucDong2.join(' · ');

    const cacMucDong3 = [];
    if (dt.maBN) cacMucDong3.push('Mã BN: <span class="the-quet__ma">' + thoatHtml(dt.maBN) + '</span>');
    if (dt.diaChi) cacMucDong3.push(thoatHtml(dt.diaChi));
    const dong3 = cacMucDong3.join(' · ');

    const tieuDeDau = dt.nguon === 'his' || dt.maBN 
        ? 'Đã quét thông tin bệnh nhân' 
        : 'Đã quét CCCD gắn chip';

    khung.innerHTML =
        '<div class="the-quet">' +
          '<div class="the-quet__dau">' +
            '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>' +
            '<span>' + tieuDeDau + '</span>' +
            '<button type="button" class="the-quet__quetlai" onclick="xoaTheQuet()">Quét lại</button>' +
          '</div>' +
          '<div class="the-quet__ten">' + thoatHtml(dt.hoTen || (dt.cccd ? ('CCCD: ' + dt.cccd) : (dt.maBN ? ('Mã BN: ' + dt.maBN) : 'Đã quét mã'))) + '</div>' +
          (dong2 ? '<div class="the-quet__phu">' + dong2 + '</div>' : '') +
          (dong3 ? '<div class="the-quet__phu">' + dong3 + '</div>' : '') +
        '</div>';
    khung.classList.remove('d-none');

    // Tự động điền ô Căn cước công dân
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
        }
    }

    // Tu dong dien o So dien thoai — CHI khi ma QR that su mang so dien thoai.
    // TUYET DOI khong do CCCD/MaBN vao o nay: o SDT la TEN TAI KHOAN, do nham vao
    // la de ra tai khoan mang so can cuoc, con benh nhan thi mat duong nhap so that.
    if (oSdt && dt.dienThoai) {
        oSdt.value = dt.dienThoai;
    }
}

function xoaTheQuet() {
    const sdtTuQr = window.danhTinhQuet ? (window.danhTinhQuet.dienThoai || '') : '';
    window.danhTinhQuet = null;
    if (typeof deleteCookie === 'function') {
        deleteCookie('qr_data');
    } else {
        try {
            document.cookie = 'qr_data=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/; SameSite=Lax';
        } catch (e) {}
    }
    const khung = document.getElementById('khungTheQuet');
    const oCccd = document.getElementById('cccd');
    const oSdt  = document.getElementById('soDienThoai');
    const ghiO  = document.getElementById('ghiOCccd');
    if (khung) { khung.innerHTML = ''; khung.classList.add('d-none'); }
    if (oCccd) { oCccd.value = ''; oCccd.readOnly = false; oCccd.classList.remove('o-tu-qr'); }
    // Chi xoa o SDT khi chinh ma QR dien so do vao. So benh nhan TU GO thi giu lai
    // — bam "Quet lai" khong phai ly do bat ho go lai so dien thoai.
    if (oSdt && sdtTuQr && oSdt.value.trim() === sdtTuQr.trim()) { oSdt.value = ''; }
    if (ghiO)  ghiO.classList.add('d-none');
    if (typeof quayLaiStep1 === 'function') {
        quayLaiStep1();
    }
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

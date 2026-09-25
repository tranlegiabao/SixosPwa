// CẤU HÌNH FIREBASE PROJECT CỦA BẠN (Dự án: my-pwa-app-4eb1b)
const firebaseConfig = {
    apiKey: "AIzaSyCX9m0qimWQ5-4t5_U2vdbTQXqitrdioE8",
    authDomain: "my-pwa-app-4eb1b.firebaseapp.com",
    projectId: "my-pwa-app-4eb1b",
    storageBucket: "my-pwa-app-4eb1b.firebasestorage.app",
    messagingSenderId: "689094471128",
    appId: "1:689094471128:web:61653ca054732bb26916ad",
    measurementId: "G-55REDZ6C01"
};

// Chế độ OTP thử nghiệm trực tiếp trên giao diện (Không cần Firebase / Khỏi tốn phí)
let useRealFirebase = false;
let confirmationResultObj = null;
let recaptchaVerifier = null;
let currentSdt = '';
const rawReturnUrl = new URLSearchParams(window.location.search).get('returnUrl');
const adminReturnUrl = (rawReturnUrl && rawReturnUrl !== '/' && rawReturnUrl !== '/Home') ? rawReturnUrl : null;
let currentCccd = '';


function showAlert(msg, type = 'info') {
    const plainMessage = String(msg ?? '').replace(/<[^>]*>/g, '');
    showToast(plainMessage, type);
}

function hideAlert() {
    // Thông báo đăng nhập được hiển thị bằng showToast.
}

// Chuyển 0987654321 thành +84987654321 cho định dạng quốc tế của Firebase
function formatPhoneNumber(sdt) {
    sdt = sdt.replace(/\s+/g, '');
    if (sdt.startsWith('0')) {
        return '+84' + sdt.substring(1);
    }
    if (!sdt.startsWith('+')) {
        return '+84' + sdt;
    }
    return sdt;
}

async function guiOtp() {
    let sdtInput = document.getElementById('soDienThoai') ? document.getElementById('soDienThoai').value.trim() : '';
    const oCccd = document.getElementById('cccd');
    const cccdInput = oCccd ? oCccd.value.trim() : '';

    // Chi duoc lay thay so dien thoai trong HAI truong hop: ma QR that su mang
    // so dien thoai, hoac la QR PHIEU KHAM HIS (tai khoan lay theo MaBN).
    // KHONG lay so CCCD lam ten tai khoan — luong CCCD phai de benh nhan tu
    // nhap so dien thoai that cua ho.
    const laQrHis = !!(window.danhTinhQuet && (window.danhTinhQuet.nguon === 'his' || window.danhTinhQuet.maBN));
    if (!sdtInput && window.danhTinhQuet && window.danhTinhQuet.dienThoai) {
        sdtInput = window.danhTinhQuet.dienThoai;
        if (document.getElementById('soDienThoai')) {
            document.getElementById('soDienThoai').value = window.danhTinhQuet.dienThoai;
        }
    } else if (!sdtInput && laQrHis && window.danhTinhQuet.maBN) {
        sdtInput = window.danhTinhQuet.maBN;
    }

    if (!sdtInput) {
        showAlert('Bạn nhập <strong>số điện thoại</strong> rồi bấm Tiếp tục nhé.', 'danger');
        const oSdtTrong = document.getElementById('soDienThoai');
        if (oSdtTrong) oSdtTrong.focus();
        return;
    }

    currentCccd = cccdInput || (window.danhTinhQuet ? window.danhTinhQuet.cccd : '');

    const isEmail = sdtInput.indexOf('@') !== -1;
    if (isEmail) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(sdtInput)) {
            showAlert('Email không đúng định dạng!', 'danger');
            return;
        }
    } else {
        if (!laQrHis && sdtInput.length < 9) {
            showAlert('Số điện thoại hoặc Căn cước công dân không hợp lệ!', 'danger');
            return;
        }
    }

    currentSdt = sdtInput;
    const btnGui = document.getElementById('btnGuiOtp');
    if (btnGui) {
        btnGui.disabled = true;
        btnGui.innerText = 'Đang xử lý...';
    }

    if (useRealFirebase) {
        if (isEmail) {
            showAlert('Chế độ OTP qua SMS thật không hỗ trợ Email!', 'danger');
            if (btnGui) {
                btnGui.disabled = false;
                btnGui.innerText = 'Tiếp tục';
            }
            return;
        }
        // CHẾ ĐỘ 1: GỬI SMS THẬT QUA FIREBASE
        try {
            if (!recaptchaVerifier) {
                recaptchaVerifier = new firebase.auth.RecaptchaVerifier('recaptcha-container', {
                    'size': 'invisible'
                });
            }

            const formattedPhone = formatPhoneNumber(sdtInput);
            confirmationResultObj = await firebase.auth().signInWithPhoneNumber(formattedPhone, recaptchaVerifier);

            document.getElementById('lblSoDienThoai').innerText = formattedPhone;
            sessionStorage.setItem('on_otp_step', '1');
            try { history.pushState({ step: 'otp' }, document.title, window.location.href); } catch (e) {}
            document.querySelector('.step-1').style.display = 'none';
            document.querySelector('.step-2').style.display = 'block';

            showAlert(`📲 <strong>Mã xác thực thực tế đã được gửi qua SMS tới ${formattedPhone}!</strong><br/>Vui lòng kiểm tra điện thoại của bạn để dùng làm mật khẩu đăng nhập.`, 'success');
            document.getElementById('maOtp').focus();
        } catch (error) {
            console.error("Firebase Auth Error:", error);
            showAlert('Không thể gửi SMS OTP: ' + (error.message || 'Lỗi Firebase'), 'danger');
            if (recaptchaVerifier) recaptchaVerifier.render().then(widgetId => grecaptcha.reset(widgetId));
        } finally {
            if (btnGui) {
                btnGui.disabled = false;
                btnGui.innerText = 'Tiếp tục';
            }
        }
    } else {
        // CHẾ ĐỘ 2: DEMO MÃ THỬ NGHIỆM (NẾU CHƯA ĐIỀN API KEY FIREBASE)
        try {
            const response = await fetch('/DangNhap/GuiOtp', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    soDienThoai: sdtInput, 
                    maCoSo: maCoSoHienTai, 
                    cccd: currentCccd,
                    danhTinhQuet: window.danhTinhQuet
                })
            });

            const data = await response.json();
            if (data.success) {
                const hienThiTk = data.soDienThoai || currentSdt;
                document.getElementById('lblSoDienThoai').innerText = hienThiTk;
                sessionStorage.setItem('on_otp_step', '1');
                try { history.pushState({ step: 'otp' }, document.title, window.location.href); } catch (e) {}
                document.querySelector('.step-1').style.display = 'none';
                document.querySelector('.step-2').style.display = 'block';
                
                if (data.isPassword) {
                    document.getElementById('maOtp').value = '';
                    showAlert(data.message || 'Vui lòng nhập mật khẩu.', 'info');
                } else {
                    document.getElementById('maOtp').value = data.otpDemo;
                    showAlert(`🔑 <strong>OTP thử nghiệm của bạn là: <span style="font-size: 1.2em; color: #0d6efd;">${data.otpDemo}</span></strong><br/><small>Đã tự động điền sẵn mật khẩu cho bạn test nhanh!</small>`, 'success');
                }
            } else {
                showAlert(data.message, 'danger');
            }
        } catch (err) {
            showAlert('Lỗi kết nối máy chủ!', 'danger');
        } finally {
            if (btnGui) {
                btnGui.disabled = false;
                btnGui.innerText = 'Tiếp tục';
            }
        }
    }
}

function getCookie(name) {
    try {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
    } catch (e) {
        console.warn("Lỗi đọc cookie:", e);
    }
    return null;
}

function setCookie(name, value, days) {
    try {
        const d = new Date();
        d.setTime(d.getTime() + (days * 24 * 60 * 60 * 1000));
        const expires = "expires=" + d.toUTCString();
        document.cookie = name + "=" + value + ";" + expires + ";path=/;SameSite=Lax";
    } catch (e) {
        console.warn("Lỗi ghi cookie:", e);
    }
}

function deleteCookie(name) {
    try {
        const paths = ['/', window.location.pathname, ''];
        const host = window.location.hostname;
        const domains = ['', host, '.' + host];

        paths.forEach(function (path) {
            domains.forEach(function (domain) {
                const domainAttr = domain ? '; domain=' + domain : '';
                const pathAttr = path ? '; path=' + path : '; path=/';
                document.cookie = name + '=; expires=Thu, 01 Jan 1970 00:00:00 GMT; max-age=0' + pathAttr + domainAttr + '; SameSite=Lax';
                document.cookie = name + '=; expires=Thu, 01 Jan 1970 00:00:00 GMT; max-age=0' + pathAttr + domainAttr;
            });
        });
    } catch (e) {
        console.warn("Lỗi xóa cookie:", e);
    }
}

window.getCookie = getCookie;
window.setCookie = setCookie;
window.deleteCookie = deleteCookie;

// May chu tu choi kem mot dich den (vd bi chan vi chua biet co so - ADR 0027):
// bao loi roi TU DUA nguoi dung toi do. Khong co dichDen thi chi bao loi nhu cu.
function baoLoiVaDiTiep(data) {
    showAlert(data.message || 'Không đăng nhập được, bạn thử lại giúp.', 'danger');

    if (data.dichDen) {
        setTimeout(function () { window.location.href = data.dichDen; }, 4000);
    }
}

async function xacNhanOtp() {
    const otpInput = document.getElementById('maOtp').value.trim();
    if (!otpInput) {
        showAlert('Vui lòng nhập OTP!', 'danger');
        return;
    }

    const btnXacNhan = document.getElementById('btnXacNhan');
    btnXacNhan.disabled = true;
    btnXacNhan.innerText = 'Đang đăng nhập...';

    if (useRealFirebase && confirmationResultObj) {
        // XÁC THỰC MÃ OTP THẬT VỚI GOOGLE FIREBASE
        try {
            const result = await confirmationResultObj.confirm(otpInput);
            const userPhone = result.user.phoneNumber || currentSdt;

            // Gửi token xác thực thành công về Server C# để tạo Cookie 365 ngày
            const response = await fetch('/DangNhap/XacNhanFirebaseToken', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    soDienThoai: userPhone,
                    maCoSo: maCoSoHienTai,
                    cccd: currentCccd,
                    danhTinhQuet: window.danhTinhQuet,
                    returnUrl: adminReturnUrl
                })
            });

            const data = await response.json();
            if (data.success) {
                window._isSubmittingOtp = true;
                deleteCookie('qr_data');
                sessionStorage.removeItem('on_otp_step');
                showAlert('🎉 Đăng nhập thành công! Đang chuyển hướng...', 'success');
                let targetUrl = data.redirectUrl;
                if (window.danhTinhQuet && (window.danhTinhQuet.nguon === 'his' || window.danhTinhQuet.maBN)) {
                    if (!targetUrl || targetUrl === '/' || targetUrl === '/Home') targetUrl = '/benh-nhan';
                }
                window.location.href = targetUrl || '/benh-nhan';
            } else {
                baoLoiVaDiTiep(data);
            }
        } catch (error) {
            console.error("Lỗi xác thực OTP Firebase:", error);
            showAlert('OTP không đúng hoặc đã hết hạn!', 'danger');
        } finally {
            btnXacNhan.disabled = false;
            btnXacNhan.innerText = 'Đăng nhập';
        }
    } else {
        // DEMO MODE
        try {
            const response = await fetch('/DangNhap/XacNhanOtp', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    soDienThoai: currentSdt, 
                    otp: otpInput,
                    maCoSo: maCoSoHienTai,
                    cccd: currentCccd,
                    danhTinhQuet: window.danhTinhQuet,
                    returnUrl: adminReturnUrl
                })
            });

            const data = await response.json();
            if (data.success) {
                window._isSubmittingOtp = true;
                deleteCookie('qr_data');
                sessionStorage.removeItem('on_otp_step');
                showAlert('Đăng nhập thành công! Đang chuyển hướng...', 'success');
                let targetUrl = data.redirectUrl;
                if (window.danhTinhQuet && (window.danhTinhQuet.nguon === 'his' || window.danhTinhQuet.maBN)) {
                    if (!targetUrl || targetUrl === '/' || targetUrl === '/Home') targetUrl = '/benh-nhan';
                }
                window.location.href = targetUrl || '/benh-nhan';
            } else {
                baoLoiVaDiTiep(data);
            }
        } catch (err) {
            showAlert('Lỗi xác thực OTP!', 'danger');
        } finally {
            btnXacNhan.disabled = false;
            btnXacNhan.innerText = 'Đăng nhập';
        }
    }
}

function quayLaiStep1(dongBoHistory = true) {
    hideAlert();
    deleteCookie('qr_data');
    sessionStorage.removeItem('on_otp_step');

    // Gọi API hủy mã OTP cache trên server
    try {
        const sdt = currentSdt || (document.getElementById('soDienThoai') ? document.getElementById('soDienThoai').value.trim() : '');
        const cccd = currentCccd || (document.getElementById('cccd') ? document.getElementById('cccd').value.trim() : '');
        fetch('/DangNhap/HuyOtp', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ soDienThoai: sdt, cccd: cccd })
        }).catch(function (e) { console.warn("HuyOtp error:", e); });
    } catch (e) { }

    // Chỉ xóa ô Số điện thoại để người dùng đổi số khác, giữ lại CCCD và thẻ quét
    const oSdt = document.getElementById('soDienThoai');
    if (oSdt) {
        oSdt.value = '';
    }

    // Dọn sạch query params trên URL (hienOtp, sdt, phone, tuQr, fromQr)
    try {
        const url = new URL(window.location.href);
        url.searchParams.delete('hienOtp');
        url.searchParams.delete('sdt');
        url.searchParams.delete('phone');
        url.searchParams.delete('tuQr');
        url.searchParams.delete('fromQr');
        const cleanUrlStr = url.pathname + (url.search ? url.search : '');
        if (dongBoHistory && history.state && history.state.step === 'otp') {
            history.replaceState({ step: 'login' }, document.title, cleanUrlStr);
        } else if (dongBoHistory) {
            window.history.replaceState({}, document.title, cleanUrlStr);
        }
    } catch (e) { }

    document.querySelector('.step-2').style.display = 'none';
    document.querySelector('.step-1').style.display = 'block';
    if (oSdt) oSdt.focus();
}

// Bắt sự kiện phím Back của trình duyệt hoặc điện thoại khi đang ở màn OTP
window.addEventListener('popstate', function (e) {
    const step2 = document.querySelector('.step-2');
    if (step2 && getComputedStyle(step2).display !== 'none') {
        quayLaiStep1(false);
    }
});

// Bắt sự kiện người dùng bấm link điều hướng rời khỏi trang khi đang ở màn OTP
document.addEventListener('click', function (e) {
    const anchor = e.target.closest('a');
    if (anchor && anchor.getAttribute('href') && !anchor.getAttribute('href').startsWith('#')) {
        const step2 = document.querySelector('.step-2');
        if (step2 && getComputedStyle(step2).display !== 'none' && !window._isSubmittingOtp) {
            deleteCookie('qr_data');
            sessionStorage.removeItem('on_otp_step');
            try {
                const payload = JSON.stringify({ soDienThoai: currentSdt, cccd: currentCccd });
                const blob = new Blob([payload], { type: 'application/json' });
                navigator.sendBeacon('/DangNhap/HuyOtp', blob);
            } catch (err) { }
        }
    }
});

// 🔴 KHONG huy OTP o 'pagehide'. Nghiep vu "roi man OTP thi xoa dau vet quet"
// van giu nguyen, nhung phai bat dung luc NGUOI DUNG CHU DONG BO DI (khoi
// 'click' ben tren, nut Doi so dien thoai, dang nhap xong) — 'pagehide' tren
// Chrome Android con ban khi chi chuyen app hay tat man hinh, tuc ban NHAM
// giua luc nguoi ta dang doc tin nhan OTP.
//
// Do that tren may that (nhat ky 17:01:52 -> 17:01:54): '/qr-otp' dat cookie
// xong, hai giay sau cookie da bi xoa trong khi nguoi dung VAN o man OTP; toi
// luc bam Dang nhap thi khong con gi de biet vua quet phieu cua ai, nen ho bi
// nem vao ho so DAU TIEN cua tai khoan.
//
// Doi lai: dong tab dot ngot thi ma OTP con song trong cache toi 30 phut. Chap
// nhan duoc — OTP van phai go dung, va cookie qr_mabn cung het han cung luc.


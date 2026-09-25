// ================================================================
// HỆ THỐNG THÔNG BÁO – POLLING 15 GIÂY
// ================================================================
let _prevSoMoi = 0;
let _danhSachThongBao = [];

// Đóng dropdown khi click bên ngoài
document.addEventListener('click', function (e) {
    const wrapper = document.getElementById('notifBellWrapper');
    const dropdown = document.getElementById('notifDropdown');
    if (wrapper && !wrapper.contains(e.target)) {
        dropdown.classList.remove('open');
    }
});

// Toggle mở/đóng dropdown
function toggleNotifDropdown(e) {
    e.stopPropagation();
    const dropdown = document.getElementById('notifDropdown');
    const isOpen = dropdown.classList.contains('open');
    dropdown.classList.toggle('open');
    // Khi mở dropdown thì load ngay
    if (!isOpen) layThongBao();
}

// Render danh sách thông báo vào dropdown
function renderThongBao(danhSach) {
    const list = document.getElementById('notifList');
    if (!danhSach || danhSach.length === 0) {
        list.innerHTML = `
            <div class="notif-empty">
                <span class="notif-empty-icon">🔕</span>
                Chưa có thông báo nào
            </div>`;
        return;
    }

    list.innerHTML = danhSach.map(item => `
        <div class="notif-item ${item.daDoc ? 'read' : 'unread'}">
            <div class="notif-dot"></div>
            <div class="notif-content">
                <div class="notif-sender">Từ: <strong>${escHtml(item.nguoiGui)}</strong></div>
                <div class="notif-text">${escHtml(item.noiDung)}</div>
                <div class="notif-time d-flex justify-content-between align-items-center">
                    <span>🕐 ${escHtml(item.thoiGian)}</span>
                    <button class="btn btn-sm btn-link p-0 text-primary text-decoration-none" onclick="moFormTraLoi('${escHtml(item.nguoiGui)}')">Trả lời</button>
                </div>
            </div>
        </div>
    `).join('');
}

// Escape HTML để tránh XSS
function escHtml(str) {
    const d = document.createElement('div');
    d.appendChild(document.createTextNode(str || ''));
    return d.innerHTML;
}

// Cập nhật badge số trên chuông
function capNhatBadge(soMoi) {
    const badge = document.getElementById('notifBadge');
    const bell = document.getElementById('notifBellBtn');

    if (soMoi > 0) {
        badge.classList.remove('d-none');
        badge.textContent = soMoi > 99 ? '99+' : soMoi;

        // Rung chuông khi có thông báo mới
        if (soMoi > _prevSoMoi) {
            bell.classList.remove('has-new');
            void bell.offsetWidth; // reflow để reset animation
            bell.classList.add('has-new');
        }
    } else {
        badge.classList.add('d-none');
        bell.classList.remove('has-new');
    }
    _prevSoMoi = soMoi;
}

// Gọi API lấy thông báo
async function layThongBao() {
    try {
        const res = await fetch('/Home/LayThongBao', {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!res.ok) return;
        const data = await res.json();
        if (data.success) {
            _danhSachThongBao = data.danhSach || [];
            capNhatBadge(data.soMoi || 0);

            // Cập nhật list nếu dropdown đang mở
            if (document.getElementById('notifDropdown').classList.contains('open')) {
                renderThongBao(_danhSachThongBao);
            }

            // Cập nhật footer
            const footer = document.getElementById('notifFooter');
            const now = new Date();
            footer.textContent = `Cập nhật lúc ${now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })} • Tự động mỗi 15 giây`;
        }
    } catch (_) {
        // Bỏ qua lỗi mạng
    }
}

// Đánh dấu tất cả thông báo là đã đọc
async function danhDauDaDoc() {
    try {
        await fetch('/Home/DanhDauDaDoc', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            }
        });
        // Cập nhật UI ngay lập tức
        _danhSachThongBao.forEach(t => t.daDoc = true);
        capNhatBadge(0);
        renderThongBao(_danhSachThongBao);
    } catch (_) {}
}

// Trả lời thông báo -> Mở Chat Modal
async function moFormTraLoi(nguoiNhan) {
    // Đóng dropdown thông báo
    document.getElementById('notifDropdown').classList.remove('open');
    
    // Mở modal chat
    const chatModal = new bootstrap.Modal(document.getElementById('chatModal'));
    document.getElementById('chatModalLabel').innerText = nguoiNhan;
    document.getElementById('chatTargetUser').value = nguoiNhan;
    document.getElementById('chatInputMessage').value = '';
    
    const chatBody = document.getElementById('chatModalBody');
    chatBody.innerHTML = '<div class="text-center text-muted small my-auto">Đang tải tin nhắn...</div>';
    
    chatModal.show();

    // Lấy lịch sử chat
    try {
        const res = await fetch(`/Home/LayLichSuTinNhan?doiTac=${encodeURIComponent(nguoiNhan)}`);
        const data = await res.json();
        
        if (data.success && data.messages.length > 0) {
            chatBody.innerHTML = data.messages.map(m => {
                const isMe = m.nguoiGui !== nguoiNhan;
                return `
                    <div class="chat-bubble ${isMe ? 'me' : 'them'}">
                        <div>${escHtml(m.noiDung)}</div>
                        <span class="chat-time text-${isMe ? 'white-50' : 'muted'}">${escHtml(m.thoiGian)}</span>
                    </div>
                `;
            }).join('');
        } else {
            chatBody.innerHTML = '<div class="text-center text-muted small my-auto">Chưa có tin nhắn nào. Hãy gửi lời chào!</div>';
        }
        // Scroll to bottom
        chatBody.scrollTop = chatBody.scrollHeight;
    } catch(e) {
        chatBody.innerHTML = '<div class="text-center text-danger small my-auto">Lỗi khi tải tin nhắn.</div>';
    }
    
    setTimeout(() => document.getElementById('chatInputMessage').focus(), 500);
}

function handleChatKeyPress(e) {
    if (e.key === 'Enter') {
        e.preventDefault();
        guiTinNhanTuChat();
    }
}

async function guiTinNhanTuChat() {
    const input = document.getElementById('chatInputMessage');
    const nguoiNhan = document.getElementById('chatTargetUser').value;
    const message = input.value.trim();
    const btnSend = document.getElementById('btnSendChat');
    
    if (!message) return;
    
    input.disabled = true;
    btnSend.disabled = true;

    try {
        const res = await fetch('/Home/TraLoiTinNhan', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ nguoiNhan, message })
        });
        const data = await res.json();
        
        if (data.success && data.data) {
            const chatBody = document.getElementById('chatModalBody');
            // Nếu đang có thông báo rỗng thì xoá đi
            if (chatBody.innerHTML.includes('Chưa có tin nhắn nào')) {
                chatBody.innerHTML = '';
            }
            
            const m = data.data;
            chatBody.innerHTML += `
                <div class="chat-bubble me">
                    <div>${escHtml(m.noiDung)}</div>
                    <span class="chat-time text-white-50">${escHtml(m.thoiGian)}</span>
                </div>
            `;
            chatBody.scrollTop = chatBody.scrollHeight;
            input.value = '';
        } else {
            showToast(data.message || "Không thể gửi tin nhắn", 'error');
        }
    } catch (err) {
        showToast("Lỗi mạng, không thể gửi tin nhắn.", 'error');
    } finally {
        input.disabled = false;
        btnSend.disabled = false;
        input.focus();
    }
}

// Polling mỗi 15 giây
layThongBao(); // Gọi ngay khi load trang
setInterval(layThongBao, 15000);
// ================================================================
// WEB PUSH – Đăng ký nhận thông báo màn hình khoá
// ================================================================

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}

// Đăng ký push – chỉ gọi khi user đã click (bắt buộc với iOS)
async function dangKyWebPush() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        showToast('Trình duyệt của bạn chưa hỗ trợ thông báo đẩy.\nVui lòng dùng Chrome (Android) hoặc cài PWA lên màn hình chính (iOS).', 'warning');
        return;
    }

    try {
        // Xin quyền thông báo (phải do user click mới không bị block)
        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            showToast('Bạn đã từ chối quyền thông báo.\nVào Cài đặt trình duyệt để bật lại.', 'warning');
            return;
        }

        // Lấy VAPID public key
        const vapidRes = await fetch('/Home/VapidPublicKey', { credentials: 'same-origin' });
        const { publicKey } = await vapidRes.json();

        // Đăng ký push subscription
        const swReg = await navigator.serviceWorker.ready;
        const subscription = await swReg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(publicKey)
        });

        // Lấy keys
        const p256dh = btoa(String.fromCharCode(...new Uint8Array(subscription.getKey('p256dh'))));
        const auth   = btoa(String.fromCharCode(...new Uint8Array(subscription.getKey('auth'))));

        // Gửi subscription lên server
        let deviceId = localStorage.getItem('deviceId');
        const res = await fetch('/Home/DangKyPush', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify({ endpoint: subscription.endpoint, p256dh, auth, deviceId })
        });
        const result = await res.json();

        if (result.success) {
            // Ẩn banner, hiện thông báo thành công
            document.getElementById('push-banner')?.remove();
            showToast('Đã bật thông báo thành công!', 'success');
        }
    } catch (err) {
        console.warn('Lỗi đăng ký Web Push:', err);
        showToast('Lỗi khi đăng ký thông báo: ' + err.message, 'error');
    }
}

// Kiểm tra trạng thái và hiển thị banner nếu chưa bật
async function kiemTraTrangThaiPush() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;
    if (!('Notification' in window)) return;

    // Nếu đã granted thì tự đăng ký lại subscription (trường hợp clear cache)
    if (Notification.permission === 'granted') {
        try {
            const swReg = await navigator.serviceWorker.ready;
            const existing = await swReg.pushManager.getSubscription();
            if (!existing) {
                // Subscription bị mất, đăng ký lại tự động
                await dangKyWebPush();
            }
        } catch(e) {}
        return; // Đã có quyền rồi, không show banner
    }

    // Chưa có quyền → hiện banner bật thông báo
    if (Notification.permission === 'default') {
        const banner = document.createElement('div');
        banner.id = 'push-banner';
        banner.style.cssText = `
            position: fixed; bottom: 80px; left: 50%; transform: translateX(-50%);
            background: linear-gradient(135deg, #1e293b, #0f172a);
            border: 1px solid rgba(99,102,241,0.4);
            color: #fff; padding: 14px 20px; border-radius: 16px;
            font-size: 14px; z-index: 9999; max-width: 340px; width: 90%;
            box-shadow: 0 8px 32px rgba(0,0,0,0.4);
            display: flex; align-items: center; gap: 12px;
        `;
        banner.innerHTML = `
            <span style="font-size:24px">🔔</span>
            <div style="flex:1">
                <div style="font-weight:700;margin-bottom:2px">Bật thông báo</div>
                <div style="font-size:12px;opacity:0.7">Nhận tin nhắn từ bác sĩ ngay lập tức</div>
            </div>
            <button onclick="dangKyWebPush()" style="
                background:linear-gradient(135deg,#6366f1,#8b5cf6);
                color:#fff;border:none;padding:8px 16px;border-radius:10px;
                font-weight:700;cursor:pointer;white-space:nowrap;font-size:13px
            ">Bật ngay</button>
            <button onclick="document.getElementById('push-banner').remove()" style="
                background:transparent;border:none;color:#aaa;font-size:18px;
                cursor:pointer;padding:0 4px;line-height:1
            ">✕</button>
        `;
        document.body.appendChild(banner);
    }
}

// Chạy sau 1.5s để không cản render trang
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.ready.then(() => {
        setTimeout(kiemTraTrangThaiPush, 1500);
    });
}

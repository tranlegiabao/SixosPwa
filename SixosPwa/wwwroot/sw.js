/*
 * Service worker cua khuon mau SixosPwa.
 *
 * ==========================================================================
 *  NGUYEN TAC BAT BIEN: KHONG CACHE NOI DUNG UNG DUNG.
 *  Xem docs/adr/0002-service-worker-khong-cache.md truoc khi sua file nay.
 * ==========================================================================
 *
 * Ly do ton tai cua file nay co 3:
 *   1. Trinh duyet chi cho CAI DAT khi trang co service worker dang ky kem
 *      trinh xu ly `fetch` -> khong co file nay thi khong co nut cai.
 *   2. Chrome con doi hoi service worker phai TRA LOI DUOC luc mat mang.
 *      Nhanh `catch` tra ve /offline.html ben duoi chinh la thu thoa dieu do,
 *      no BAT BUOC chu khong phai trang tri.
 *   3. [MỚI] Xu ly Web Push Notification – hien thi thong bao tren man hinh khoa.
 */

var CACHE_NAME = 'sixospwa-offline-v1';
var OFFLINE_URL = '/offline.html';

// Chi 2 file duy nhat duoc phep nam trong cache: trang bao mat mang va logo cua no.
var PRECACHE = [OFFLINE_URL, '/static/icon-192.png'];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(function (cache) { return cache.addAll(PRECACHE); })
            .then(function () { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function (event) {
    // Xoa moi cache cua phien ban cu -> doi CACHE_NAME la don sach ban truoc.
    event.waitUntil(
        caches.keys()
            .then(function (keys) {
                return Promise.all(keys.map(function (key) {
                    return key === CACHE_NAME ? null : caches.delete(key);
                }));
            })
            .then(function () { return self.clients.claim(); })
    );
});

self.addEventListener('fetch', function (event) {
    // Chi can thiep vao request DIEU HUONG (mo trang). Anh, CSS, JS, goi API...
    // deu khong dung toi -> trinh duyet xu ly binh thuong, khong qua cache.
    if (event.request.mode !== 'navigate') {
        return;
    }

    event.respondWith(
        fetch(event.request).catch(function () {
            return caches.open(CACHE_NAME)
                .then(function (cache) { return cache.match(OFFLINE_URL); })
                .then(function (cached) {
                    if (cached) {
                        return cached;
                    }
                    // Luoi an toan cuoi cung, phong khi cache bi he dieu hanh don.
                    return new Response(
                        '<!doctype html><meta charset="utf-8"><title>Mat ket noi</title>' +
                        '<body style="font-family:sans-serif;text-align:center;padding:3rem">' +
                        '<h1>Mat ket noi mang</h1><p>Vui long kiem tra ket noi roi thu lai.</p>',
                        { headers: { 'Content-Type': 'text/html; charset=utf-8' } }
                    );
                });
        })
    );
});

// =============================================================================
// WEB PUSH – Hiển thị thông báo trên màn hình khoá
// =============================================================================

self.addEventListener('push', function (event) {
    if (!event.data) return;

    var data = {};
    try {
        data = event.data.json();
    } catch (e) {
        data = { title: 'HisSoft', body: event.data.text() };
    }

    var title   = data.title  || '💬 Tin nhắn mới – HisSoft';
    var body    = data.body   || 'Bạn có tin nhắn mới';
    var icon    = data.icon   || '/static/icon-192.png';
    var badge   = data.badge  || '/static/icon-192.png';
    var url     = data.url    || '/';
    var sender  = data.sender || 'HisSoft';

    var options = {
        body:      body,
        icon:      icon,
        badge:     badge,
        vibrate:   [200, 100, 200],          // Rung: 200ms ON – 100ms OFF – 200ms ON
        tag:       'hissoft-message',         // Gộp các thông báo cùng tag
        renotify:  true,                      // Luôn rung dù tag đã tồn tại
        requireInteraction: false,            // Tự đóng sau vài giây
        data: { url: url, sender: sender },
        actions: [
            { action: 'open',    title: '📖 Xem tin nhắn' },
            { action: 'dismiss', title: '✖ Bỏ qua'       }
        ]
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// Xử lý khi người dùng bấm vào thông báo
self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    if (event.action === 'dismiss') return;

    var targetUrl = (event.notification.data && event.notification.data.url) ? event.notification.data.url : '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then(function (windowClients) {
                // Nếu app đã mở sẵn thì focus vào tab đó
                for (var i = 0; i < windowClients.length; i++) {
                    var client = windowClients[i];
                    if (client.url.includes(self.location.origin) && 'focus' in client) {
                        client.focus();
                        return;
                    }
                }
                // Chưa mở thì mở tab mới
                if (clients.openWindow) {
                    return clients.openWindow(targetUrl);
                }
            })
    );
});

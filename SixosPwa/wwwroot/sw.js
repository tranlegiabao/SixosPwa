/*
 * Service worker cua khuon mau SixosPwa.
 *
 * ==========================================================================
 *  NGUYEN TAC BAT BIEN: KHONG CACHE NOI DUNG UNG DUNG.
 *  Xem docs/adr/0002-service-worker-khong-cache.md truoc khi sua file nay.
 * ==========================================================================
 *
 * Ly do ton tai cua file nay chi co 2:
 *   1. Trinh duyet chi cho CAI DAT khi trang co service worker dang ky kem
 *      trinh xu ly `fetch` -> khong co file nay thi khong co nut cai.
 *   2. Chrome con doi hoi service worker phai TRA LOI DUOC luc mat mang.
 *      Nhanh `catch` tra ve /offline.html ben duoi chinh la thu thoa dieu do,
 *      no BAT BUOC chu khong phai trang tri.
 *
 * Ngoai 2 viec do, moi request deu di thang ra server nhu khi khong co service
 * worker. Nho vay: khong bao gio hien du lieu cu, va deploy ban moi la an ngay.
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

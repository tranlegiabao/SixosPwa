# Hướng dẫn mở server để có link dùng trên điện thoại

## Bước 1: Cài cloudflared (chỉ làm 1 lần)

Chọn 1 trong 2 cách:

**Cách 1: Dùng winget**
```powershell
winget install --id Cloudflare.cloudflared
```

**Cách 2: Tải file**
1. Vào: https://github.com/cloudflare/cloudflared/releases
2. Tải file `cloudflared-windows-amd64.exe`
3. Đổi tên thành `cloudflared.exe`
4. Đưa vào thư mục có trong PATH (ví dụ: `C:\Windows\System32`)

## Bước 2: Chạy server

```powershell
.\start-tunnel.ps1
```

## Kết quả

Script sẽ:
1. ✓ Kiểm tra cloudflared đã cài chưa
2. 🚀 Khởi động ứng dụng trên https://localhost:7024
3. 🌐 Mở Cloudflare Tunnel
4. 📱 In ra link kiểu: `https://xxx-yyy-zzz.trycloudflare.com`

## Sử dụng

- **Mở link trên điện thoại** để xem và cài đặt PWA
- **Nhấn Ctrl+C** để tắt server
- Script tự động dọn dẹp khi tắt

## Nếu gặp lỗi

### Lỗi "cloudflared not found"
→ Chưa cài cloudflared, xem Bước 1

### Lỗi "App chưa khởi động được"
→ Thử chạy thủ công để xem lỗi chi tiết:
```powershell
dotnet run --project SixosPwa --launch-profile https
```

### Lỗi "port 7024 đã được dùng"
→ Tắt app đang chạy hoặc đổi port trong `launchSettings.json`

## Link tham khảo

- Cloudflared: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/do-more-with-tunnels/trycloudflare/
- DevTunnel (thay thế): https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started

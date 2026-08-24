# Dựng môi trường thử thật bằng Cloudflare tunnel thay vì mạng nội bộ

Luồng bàn giao sang Ung Bướu chỉ chứng minh được là chạy khi **ba** thành phần cùng nói chuyện với
nhau qua HTTPS công khai: SixosPwa, `DangKyOnlineUB`, và `SixOSDatKhamAPI`. Chạy tất cả trên
`localhost` thì không thử được, vì cookie `DKOnline_auth` của đối tác là `SameSite=Lax` — hành vi của
nó phụ thuộc vào việc hai bên có cùng site hay không, và điều đó chỉ lộ ra khi mỗi bên đứng ở một tên
miền thật. Bản thân điện thoại của người thử cũng phải vào được cả ba.

Ta chọn **ba quick tunnel Cloudflare**, mỗi tiến trình một tunnel, rồi trỏ cấu hình đối tác trong
`DM_DoiTacApi` vào các tên miền đó.

## Considered Options

- **Chạy hết trên `localhost` / mạng LAN.** Được: không phụ thuộc dịch vụ ngoài, không đổi cấu hình
  DB. Mất: không thử được `SameSite=Lax` xuyên site, không mở được trên điện thoại thật, và
  `SixOSDatKhamAPI` nằm ở `10.85.9.34:5000` — địa chỉ nội bộ, máy ngoài công ty không với tới.
- **Deploy lên máy chủ thử có tên miền cố định.** Được: địa chỉ bền, không phải sửa DB mỗi lần. Mất:
  phải xin hạ tầng và mở cổng cho một việc kéo dài vài giờ; vòng sửa–thử chậm hẳn vì mỗi lần đổi code
  là một lần deploy.
- **Ba quick tunnel Cloudflare (đã chọn).** Được: dựng trong vài giây, HTTPS thật, điện thoại vào
  được, sửa code xong chỉ cần khởi động lại tiến trình. Mất: tên miền **đổi mỗi lần** `cloudflared`
  khởi động lại, nên phải `UPDATE` lại `DM_DoiTacApi`.

## Cách dựng

| Thành phần | Cổng cục bộ | Vai trò trong luồng |
|---|---|---|
| SixosPwa | `https://localhost:7024`, `http://localhost:5015` | nơi bệnh nhân đứng |
| `DangKyOnlineUB` | `https://localhost:44366`, `http://localhost:5161` | đích bàn giao — ứng với `DM_DoiTacApi.TrangChu` |
| `SixOSDatKhamAPI` | `http://localhost:5069` | tra tài khoản + nhóm `forgot-password` — ứng với `DM_DoiTacApi.BaseUrl` |

Mỗi thành phần một tunnel:

```
cloudflared tunnel --url http://localhost:5015  --no-autoupdate                 # SixosPwa
cloudflared tunnel --url https://localhost:44366 --no-tls-verify                # UB  (HTTPS tự ký)
cloudflared tunnel --url http://localhost:5069  --no-autoupdate                 # DatKhamAPI
```

Rồi trỏ cấu hình của **riêng cơ sở đang thử** (`DM_DoiTacApi` `Id = 1`, `MaCoSo = 79423`) vào hai tên
miền tương ứng. Dòng `Id = 8` (Ung Bướu CS2) là cấu hình thật đang chạy — không đụng tới.

## Những chỗ đã vấp

**`BaseUrl` và `TrangChu` là hai địa chỉ khác nhau, của hai ứng dụng khác nhau.** `TrangChu` là trang
MVC của UB (đăng ký, đăng nhập, bàn giao); `BaseUrl` là `SixOSDatKhamAPI`. Trỏ nhầm cả hai vào một
tunnel thì `TinhTrangTaiKhoanAsync` trả 404 cho mọi CCCD, và bệnh nhân nào cũng bị đẩy sang màn Đăng
ký dù đã có tài khoản.

**Tên miền quick tunnel chết theo tiến trình.** Khi `cloudflared` tắt, tên miền biến mất khỏi DNS;
SixosPwa ném `HttpRequestException: No such host is known` và bệnh nhân thấy *"Không kết nối được tới
cơ sở"*. Nhìn vào log của UB sẽ thấy **không có request nào đi tới** — đó là dấu hiệu phân biệt với
lỗi trong code. Dựng lại tunnel là ra tên miền **mới**, phải `UPDATE` lại `DM_DoiTacApi` chứ không
dùng lại địa chỉ cũ được.

**Quên mất địa chỉ của một tunnel đang chạy thì không cần dựng cái mới.** `cloudflared` mở một cổng
metrics cục bộ; tìm cổng bằng `netstat -ano | findstr <PID>` rồi hỏi thẳng:

```
curl http://127.0.0.1:<cổng>/quicktunnel     →  {"hostname":"....trycloudflare.com"}
```

**`nslookup` báo `Non-existent domain` cho `*.trycloudflare.com` là bình thường** trên máy trong mạng
bệnh viện — DNS nội bộ không phân giải được tên miền ngoài, nhưng resolver của Windows và .NET vẫn đi
đường khác và tới được. Dùng `curl` để kết luận, đừng dùng `nslookup`.

**Tunnel trả 502 nghĩa là tiến trình phía sau đã chết**, không phải tunnel hỏng. Chạy lại ứng dụng là
xong, không cần đụng `cloudflared`.

**Đổi `DM_DoiTacApi` không cần khởi động lại SixosPwa.** `PartnerGatewayFactory.LayAsync` đọc tươi từ
DB mỗi lần gọi, `AsNoTracking()`, không cache.

**Khi hai bên cùng sửa, deploy UB trước rồi mới tới SixosPwa.** Ví dụ kênh `xacthuc = 4`: nếu
SixosPwa gửi `4` mà UB còn bản cũ thì `switch` của họ rơi vào nhánh mặc định, trả `statusCode 500` và
không kèm trường `code` — không bàn giao được.

## Consequences

Cấu hình đối tác trong `DM_DoiTacApi` trở thành **thứ phải bảo trì trong lúc thử**: mỗi lần
`cloudflared` khởi động lại là một lần `UPDATE`. Đổi lại, ta thử được đúng thứ không thể thử trên
`localhost` — hành vi `SameSite=Lax` xuyên site, bàn giao top-level thật, và trải nghiệm trên điện
thoại.

Các `UPDATE` này chỉ chạm dòng của cơ sở đang thử. Khi thử xong phải trả `BaseUrl` về
`http://10.85.9.34:5000` và `TrangChu` về địa chỉ thật — script trong
`.claude/prompts/2026-08-24_sixospwa-menu-chan-dangnhap-cheo/output/` có sẵn phần hoàn nguyên.

Với `SixOSDatKhamAPI` còn một lối gọn hơn tunnel: vì `TinhTrangTaiKhoanAsync` là lời gọi **máy chủ →
máy chủ** phát đi từ backend SixosPwa chứ không phải từ trình duyệt, để `BaseUrl = http://localhost:5069`
cũng chạy, và không chết theo `cloudflared`. Chỉ cần tunnel khi muốn gọi API đó từ thiết bị khác.

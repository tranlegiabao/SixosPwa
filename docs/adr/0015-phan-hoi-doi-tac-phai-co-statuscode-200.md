# 0015 — Phản hồi của đối tác chỉ tính là thành công khi mang `statusCode == 200`

- **Tác giả:** Nam · **Ngày:** 2026-08-27 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0014](0014-co-so-ub-dung-man-cua-khach.md) —
  quyết định này giữ cho bộ màn dựng lại ở 0014 không nói dối bệnh nhân.

## Bối cảnh

`UbGateway.GuiAsync` là chỗ duy nhất SixosPwa đọc phản hồi của hệ đối tác. Bản đầu phân loại
như sau:

```csharp
if (!phanHoi.IsSuccessStatusCode)                    { ... thất bại }
if (DocMaTrangThai(chuoi) is int ma && ma != 200)    { ... thất bại }
return new KetQuaThaoTac(true, ...);                 // còn lại: thành công
```

Cách viết đó để hở **hai lỗ ngược chiều nhau**, và ngày 26/08/2026 cả hai đều lộ ra trong cùng
một buổi thử luồng Đăng ký:

**Lỗ thứ nhất — nói sai nguyên nhân.** Instance DangKyOnlineUB chạy thử bị hỏng kết nối cơ sở dữ
liệu, nên trả về **trang HTML 500**. `DocThongBao` gọi `JsonDocument.Parse` trên HTML thì ném
`JsonException`, trả `null`, và toán tử `??` rơi về câu mặc định *"Không tạo được tài khoản tại
cơ sở"*. Bệnh nhân đọc câu đó sẽ tưởng **hồ sơ của mình bị từ chối** và ngồi sửa CCCD, đổi số điện
thoại, thử đi thử lại — trong khi sự thật là bên kia đang chết và chẳng có gì để sửa.

**Lỗ thứ hai, nặng hơn — nói sai kết quả.** `DocMaTrangThai` trả `null` cho *mọi* thân không phải
JSON, nên mẫu `is int ma` **trượt**, và luồng rơi thẳng xuống nhánh `return ... true`. Nghĩa là một
trang HTML trả kèm **HTTP 200** — ví dụ đối tác `302` dẫn về màn đăng nhập rồi `HttpClient` tự đi
theo — sẽ được tính là **THÀNH CÔNG**. Khi đó bệnh nhân thấy *"Đã gửi mã xác thực"* dù đối tác
không tạo gì, còn `LuongCongBenhNhan.DangKyDoiTacAsync` vẫn chạy tiếp `TaoHoSoNoiBoAsync` +
`LuuLienKetAsync`, tức **ghi hồ sơ nội bộ cho một tài khoản không tồn tại bên đối tác**. Lỗi này im
lặng; hôm 26/08 nó chỉ không nổ vì đối tác trả 500 chứ không phải 200.

Soát lại **cả bốn cửa** SixosPwa gọi thì thấy một sự thật đủ chắc để dựa vào: tất cả đều trả
`{ statusCode, message }`, **kể cả lúc thành công**.

| Cửa | Neo | Thành công trả |
|---|---|---|
| `POST /HeThong/HT_DangNhap/login` | `HtDangNhapServices.LoginAsync` | `statusCode = 200` |
| `POST /HeThong/HT_DangNhap/register` | `RegisterAsync` → `SendCode` | `statusCode = 200` |
| `POST /HeThong/HT_DangNhap/XacThucMaXacNhan` | `XacThucMaXacNhan` | `statusCode = 200` |
| `POST /HeThong/HT_QuenMatKhau/QuenMatKhau` | `HT_QuenMatKhauServices` | `statusCode = 200` |

## Quyết định

Một phản hồi của đối tác **chỉ** được coi là thành công khi hội đủ **cả ba** điều kiện:

1. thân trả về **parse được** thành JSON object, **và**
2. object đó **có** trường `statusCode`, **và**
3. `statusCode == 200` (kèm HTTP thuộc dải thành công).

Mọi trường hợp khác là thất bại. Trong đó, trường hợp **không đọc nổi `statusCode`** được tách
riêng thành một loại lỗi có câu báo riêng — *"Hệ thống của cơ sở đang gặp sự cố, vui lòng thử lại
sau"* (hằng `ThongBaoSuCo`) — **không bao giờ** mượn câu từ chối nghiệp vụ để báo.

Hệ quả kèm theo: `DocMaTrangThai` trả `null` **không còn là "không rõ"** mà là **dấu hiệu dương
tính "bên kia đang hỏng"**. Đổi ý nghĩa của hàm đó là đổi luôn luật phân loại này.

## Vì sao không chọn cách khác

- **Cứ tin HTTP status là đủ.** Không được: đối tác trả `HTTP 200` kèm
  `{ statusCode: 500, message: "..." }` ở **đường thất bại nghiệp vụ thường gặp nhất**
  (`Register` luôn `return Ok(...)` kể cả khi từ chối). Tin HTTP thôi là nhận nhầm mọi lời từ chối
  thành thành công.
- **Dung thứ: thiếu `statusCode` thì coi như thành công.** Đây chính là hành vi cũ, và nó đẻ ra lỗ
  thứ hai. Với một cổng bệnh nhân, *nói nhầm là đã đăng ký xong* tốn kém hơn nhiều so với *bắt thử lại*.
- **Đọc HTML để đoán xem đối tác định nói gì.** Giòn, và sai lệch âm thầm mỗi lần bên kia đổi giao diện.

## Đánh đổi đã biết

Luật này **khoá cứng SixosPwa vào quy ước `{ statusCode, message }` của DangKyOnlineUB**. Nếu sau
này đối tác thêm một cửa trả JSON trần không có `statusCode`, SixosPwa sẽ **từ chối một phản hồi
hợp lệ** và báo "cơ sở đang gặp sự cố". Đó là hướng sai an toàn hơn, nhưng phải nhớ: thêm cửa mới
bên đối tác thì kiểm lại hợp đồng này trước.

Luật chỉ áp cho `UbGateway`. Bản cài của đối tác khác tự quyết cách phân loại của mình —
`IPartnerGateway` không ép ai theo.

# Tài khoản Demo

## 📱 Cách đăng nhập

Ứng dụng dùng OTP qua SMS. Nhập số điện thoại → Nhận mã OTP → Nhập mã.

**Mã OTP demo luôn là: `123456`**

---

## 👤 Tài khoản Admin

**Số điện thoại:** `0999999999`  
**Loại:** Admin  
**Quyền:** Gửi tin nhắn SMS, quản lý bệnh nhân, xem lịch sử

### Sau khi đăng nhập:
→ Tự động chuyển đến `/Sms` (Trang gửi tin nhắn)

---

## 🏥 Tài khoản Đối tác

**Số điện thoại:** `0988888888`  
**Loại:** Đối tác (Phòng khám ABC)  
**Quyền:** Gửi tin nhắn SMS, quản lý bệnh nhân

### Sau khi đăng nhập:
→ Tự động chuyển đến `/Sms` (Trang gửi tin nhắn)

---

## 🧑‍⚕️ Tài khoản Bệnh nhân

### Bệnh nhân 1: Nguyễn Văn An
**Số điện thoại:** `0901234567`  
**Mã BN:** BN001  
**Quyền:** Xem tin nhắn đã nhận, xem hóa đơn, bật/tắt nhận tin

### Bệnh nhân 2: Trần Thị Bình
**Số điện thoại:** `0912345678`  
**Mã BN:** BN002  
**Quyền:** Xem tin nhắn đã nhận, xem hóa đơn, bật/tắt nhận tin

### Sau khi đăng nhập:
→ Tự động chuyển đến `/BenhNhan` (Dashboard bệnh nhân)

---

## 🔐 Phân quyền

| Loại tài khoản | Trang gửi SMS (`/Sms`) | Trang bệnh nhân (`/BenhNhan`) |
|---|---|---|
| **Admin** | ✅ Có quyền | ❌ Không vào được |
| **Đối tác** | ✅ Có quyền | ❌ Không vào được |
| **Bệnh nhân** | ❌ Không vào được | ✅ Có quyền |

---

## 🧪 Test các tài khoản

### 1. Test Admin
```
1. Vào https://localhost:7024
2. Nhập SĐT: 0999999999
3. Nhấn "Gửi mã OTP"
4. Nhập OTP: 123456
5. → Vào trang gửi SMS
6. Import bệnh nhân, chọn mẫu, gửi tin nhắn
```

### 2. Test Bệnh nhân
```
1. Vào https://localhost:7024
2. Nhập SĐT: 0901234567
3. Nhấn "Gửi mã OTP"
4. Nhập OTP: 123456
5. → Vào dashboard bệnh nhân
6. Xem lịch sử tin nhắn, hóa đơn, bật/tắt nhận tin
```

### 3. Test phân quyền
```
- Đăng nhập Admin → Thử vào /BenhNhan → Bị chặn
- Đăng nhập Bệnh nhân → Thử vào /Sms → Bị chặn
```

---

## ✏️ Thêm tài khoản mới

Sửa file `Services/ITaiKhoanService.cs`:

```csharp
new TaiKhoan 
{ 
    Id = 5, 
    TenDangNhap = "0977777777", // SĐT mới
    MatKhau = "",
    HoTen = "Tên người dùng",
    LoaiTaiKhoan = "Admin", // hoặc "DoiTac", "BenhNhan"
    BenhNhanId = null, // Chỉ cần nếu là BenhNhan
    KichHoat = true
}
```

Khởi động lại ứng dụng.

---

## 🔒 Production

Trong production nên:
1. ❌ Tắt mã OTP mặc định `123456`
2. ✅ Tích hợp SMS gateway thật (Twilio, VNPT, Viettel...)
3. ✅ Hash mật khẩu
4. ✅ Lưu tài khoản vào database
5. ✅ Thêm rate limiting cho OTP
6. ✅ Log lịch sử đăng nhập

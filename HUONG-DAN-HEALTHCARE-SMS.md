# Hướng dẫn Hệ thống Chăm sóc Bệnh nhân - SMS

## ✅ Đã tạo xong

### 1. Models (d:\web\SixosPwaTemplate\SixosPwaTemplate\SixosPwa\Models\BenhNhan.cs)
- `BenhNhan` - Thông tin bệnh nhân
- `LichSuTinNhan` - Lịch sử tin nhắn đã gửi
- `MauTinNhan` - Mẫu tin nhắn có sẵn
- `HoaDon` - Hóa đơn khám bệnh

### 2. Services
- `IBenhNhanService` + `InMemoryBenhNhanService` - Quản lý bệnh nhân
- `ILichSuTinNhanService` + `InMemoryLichSuTinNhanService` - Lưu lịch sử
- `IMauTinNhanService` + `InMemoryMauTinNhanService` - Quản lý mẫu
- `IHoaDonService` + `InMemoryHoaDonService` - Quản lý hóa đơn
- `ISmsService` + `TwilioSmsService` - Gửi SMS

### 3. Controllers
- `SmsController` - Quản lý tin nhắn (Admin/Đối tác)
  - Import danh sách bệnh nhân
  - Gửi SMS hàng loạt
  - Chọn mẫu tin nhắn
  - Lưu lịch sử
  
- `BenhNhanController` - Dành cho bệnh nhân
  - Xem lịch sử tin nhắn nhận được
  - Xem lịch sử hóa đơn
  - Bật/tắt nhận tin nhắn

### 4. CSS Healthcare Theme
- `wwwroot/css/healthcare.css` - Theme y tế chuyên nghiệp

### 5. Configuration
- `Program.cs` - Đã đăng ký các service
- `appsettings.json` - Đã thêm cấu hình Twilio

## 🎯 Tính năng chính

### Admin/Đối tác (Trang `/Sms`)
✅ Import danh sách bệnh nhân từ CSV  
✅ Chọn mẫu tin nhắn có sẵn  
✅ Chọn đối tác gửi  
✅ Gửi SMS hàng loạt cho nhiều bệnh nhân  
✅ Tự động thay thế {TenBenhNhan}, {MaBenhNhan}  
✅ Lưu lịch sử tin nhắn (không xóa)  
✅ Không gửi cho bệnh nhân đã tắt nhận tin  

### Bệnh nhân (Trang `/BenhNhan`)
✅ Xem lịch sử tin nhắn đã nhận  
✅ Xem trạng thái tin nhắn (Đã gửi, Thất bại)  
✅ Xem lịch sử hóa đơn  
✅ Bật/tắt nhận tin nhắn  

## 📱 Cách sử dụng

### 1. Cài package Twilio
```powershell
dotnet restore
```

### 2. Cấu hình Twilio (appsettings.json)
```json
"Twilio": {
  "AccountSid": "ACxxxxxxxx",
  "AuthToken": "your_token",
  "FromPhoneNumber": "+84xxxxxxxxx"
}
```

### 3. Chạy ứng dụng
```powershell
dotnet run --project SixosPwa --launch-profile https
```

### 4. Truy cập
- **Admin**: https://localhost:7024/Sms
- **Bệnh nhân**: https://localhost:7024/BenhNhan

## 📋 Format Import CSV

```csv
MaBenhNhan,HoTen,SoDienThoai,NgaySinh,GioiTinh,DiaChi,Email
BN001,Nguyễn Văn A,0901234567,1990-05-15,Nam,Hà Nội,email@example.com
BN002,Trần Thị B,0912345678,1985-03-20,Nữ,TP.HCM,email2@example.com
```

## 🎨 UI Healthcare Theme

Giao diện theo chuẩn y tế:
- ✅ Màu xanh y tế (#0066CC)
- ✅ Cards bo tròn hiện đại
- ✅ Status badges rõ ràng
- ✅ Timeline cho lịch sử
- ✅ Responsive mobile

## 🔄 Flow gửi tin nhắn

1. Admin chọn mẫu tin nhắn
2. Nhập đối tác (tùy chọn)
3. Chọn bệnh nhân từ danh sách
4. Hệ thống tự thay {TenBenhNhan}, {MaBenhNhan}
5. Gửi SMS qua Twilio
6. Lưu lịch sử (không xóa)
7. Bệnh nhân nhận SMS trên điện thoại thật

## ⚙️ Tùy chỉnh

### Thêm mẫu tin nhắn mới
Sửa file `InMemoryMauTinNhanService.cs`:
```csharp
new MauTinNhan { 
    Id = 5, 
    TenMau = "Mẫu mới", 
    LoaiTinNhan = "LoaiMoi",
    NoiDung = "Nội dung {TenBenhNhan}...",
    MoTa = "Mô tả",
    KichHoat = true 
}
```

### Dùng Database thật
Thay `InMemory*Service` bằng service kết nối SQL Server/PostgreSQL

## 🚀 Tiếp theo

Bạn cần tạo Views cho:
1. `/Views/Sms/Index.cshtml` - Trang admin gửi SMS
2. `/Views/Sms/LichSu.cshtml` - Lịch sử gửi tin
3. `/Views/BenhNhan/Index.cshtml` - Dashboard bệnh nhân  
4. `/Views/BenhNhan/LichSuTinNhan.cshtml` - Lịch sử tin nhận
5. `/Views/BenhNhan/LichSuHoaDon.cshtml` - Lịch sử hóa đơn
6. `/Views/BenhNhan/CaiDat.cshtml` - Cài đặt nhận tin

Tôi có thể tạo từng view chi tiết nếu bạn cần!

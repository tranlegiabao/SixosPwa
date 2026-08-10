# 🔔 Hướng dẫn Chuông Thông Báo & Trả Lời Tin Nhắn

## Tính năng

Hệ thống chuông thông báo cho phép bệnh nhân:
- **Nhận thông báo realtime** khi có tin nhắn mới từ admin/đối tác
- **Xem danh sách tin nhắn** với số lượng tin chưa đọc
- **Đọc chi tiết** từng tin nhắn
- **Trả lời** tin nhắn trực tiếp từ ứng dụng

---

## Giao diện

### 1. Chuông thông báo (Fixed Position)
- **Vị trí:** Góc phải phía trên màn hình
- **Biểu tượng:** 🔔 Font Awesome Bell icon
- **Badge đỏ:** Hiển thị số lượng tin nhắn chưa đọc
- **Animation:** 
  - Pulse khi có tin chưa đọc
  - Shake (rung) khi nhận tin mới

### 2. Popup tin nhắn
Khi bấm vào chuông → Hiện popup với 2 màn hình:

#### A. Danh sách tin nhắn
- Hiển thị tất cả tin nhắn theo thứ tự mới nhất
- Tin chưa đọc có:
  - Nền màu xanh nhạt
  - Label "MỚI" màu đỏ góc phải
  - Border trái màu đỏ
- Tin đã đọc: Nền trắng, border trái xanh lá
- Mỗi item hiển thị:
  - Tiêu đề (loại tin nhắn)
  - Thời gian (tương đối: "5 phút trước", "2 giờ trước"...)
  - Nội dung (rút gọn 80 ký tự)
  - Người gửi

#### B. Chi tiết tin nhắn
Khi bấm vào 1 tin nhắn → Hiện:
- Nút "Quay lại" ← 
- Tiêu đề đầy đủ
- Metadata: Người gửi • Thời gian
- Nội dung tin nhắn đầy đủ
- **Khu vực trả lời:**
  - Textarea nhập nội dung
  - Nút "📩 Gửi trả lời"
  - Nút "Hủy"

---

## Luồng hoạt động

### Khi Admin gửi tin nhắn:
1. Admin chọn bệnh nhân và gửi SMS từ trang `/Sms`
2. Server:
   - Gửi SMS qua Twilio
   - Lưu vào `LichSuTinNhan` với `DaDoc = false`
   - Gửi SignalR notification đến group `BenhNhan_{id}`

3. Bệnh nhân (nếu đang online):
   - Nhận SignalR event `"NhanTinNhanMoi"`
   - Banner thông báo xuất hiện ở đầu trang (10s)
   - Chuông rung + phát âm thanh
   - Badge số lượng tin chưa đọc tăng lên
   - Tin nhắn được thêm vào đầu danh sách

### Khi Bệnh nhân đọc tin nhắn:
1. Bấm vào chuông → Popup hiện ra
2. Bấm vào tin nhắn → Chuyển sang màn chi tiết
3. Tin nhắn được đánh dấu `DaDoc = true`
4. Badge số lượng giảm xuống

### Khi Bệnh nhân trả lời:
1. Nhập nội dung vào textarea
2. Bấm "Gửi trả lời"
3. POST đến `/BenhNhan/TraLoiTinNhan` với:
   ```json
   {
     "tinNhanGocId": 123,
     "benhNhanId": 1,
     "noiDung": "Cảm ơn bác sĩ..."
   }
   ```
4. Server lưu tin trả lời vào `LichSuTinNhan`:
   - `LoaiTinNhan = "Trả lời"`
   - `DoiTac = Tên bệnh nhân`
   - `TrangThai = "DaGui"`
5. Tin trả lời được thêm vào đầu danh sách
6. Quay về màn danh sách

---

## Code chính

### View: `Views/BenhNhan/Index.cshtml`

#### HTML Structure:
```html
<!-- Chuông thông báo -->
<div class="notification-bell" onclick="openMessagePopup()">
    <i class="fas fa-bell"></i>
    <span class="notification-badge" id="unreadCount">5</span>
</div>

<!-- Popup -->
<div class="message-popup" id="messagePopup">
    <div class="message-popup-content">
        <!-- Header -->
        <div class="message-popup-header">
            <h4>📩 Tin nhắn của bạn</h4>
            <button class="message-popup-close">&times;</button>
        </div>
        
        <!-- Danh sách -->
        <div class="message-list" id="messageListView">
            <!-- Dynamic content -->
        </div>

        <!-- Chi tiết -->
        <div class="message-detail" id="messageDetailView">
            <!-- Dynamic content -->
        </div>
    </div>
</div>
```

#### JavaScript Functions:
```javascript
// Khởi tạo danh sách tin nhắn từ server
let allMessages = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(lichSuTinNhan));

// Mở popup
function openMessagePopup()

// Đóng popup
function closeMessagePopup()

// Render danh sách
function renderMessageList()

// Mở chi tiết
function openMessageDetail(messageId)

// Quay lại danh sách
function backToMessageList()

// Gửi trả lời
async function sendReply()

// Cập nhật badge
function updateUnreadCount()

// Format thời gian tương đối
function formatDate(dateString)
```

#### SignalR Event Handler:
```javascript
connection.on("NhanTinNhanMoi", function (data) {
    // Thêm tin mới vào danh sách
    allMessages.unshift(newMessage);
    
    // Hiển thị banner
    // Rung chuông
    // Phát âm thanh
    // Cập nhật badge
});
```

### Controller: `BenhNhanController.cs`

#### API Trả lời tin nhắn:
```csharp
[HttpPost]
public async Task<IActionResult> TraLoiTinNhan([FromBody] TraLoiTinNhanRequest request)
{
    var tinNhanTraLoi = new LichSuTinNhan
    {
        BenhNhanId = request.BenhNhanId,
        DoiTac = benhNhan.HoTen,
        NoiDung = request.NoiDung,
        LoaiTinNhan = "Trả lời",
        TrangThai = "DaGui"
    };
    
    await _lichSuService.LuuLichSuAsync(tinNhanTraLoi);
    
    return Ok(new { thanhCong = true, tinNhanId = tinNhanTraLoi.Id });
}
```

### Model: `LichSuTinNhan`
```csharp
public class LichSuTinNhan
{
    public int Id { get; set; }
    public int BenhNhanId { get; set; }
    public string? DoiTac { get; set; }
    public string NoiDung { get; set; }
    public string LoaiTinNhan { get; set; }
    public DateTime ThoiGianGui { get; set; }
    public string TrangThai { get; set; }
    public bool DaDoc { get; set; } = false; // ✅ Mới thêm
}
```

---

## CSS Highlights

### Chuông thông báo:
- `position: fixed; top: 20px; right: 20px`
- `z-index: 9999`
- `box-shadow` + hover scale effect
- Badge: `position: absolute` với animation pulse

### Popup:
- Fullscreen overlay với `rgba(0,0,0,0.5)`
- Content: `max-width: 600px`, `max-height: 80vh`
- Border-radius 16px, shadow lớn

### Message item:
- Unread: `background: #e0f2f1`, border-left đỏ, có badge "MỚI"
- Read: `background: white`, border-left xanh
- Hover: `translateX(5px)` + shadow

---

## Test kịch bản

### Bước 1: Admin gửi tin nhắn
1. Đăng nhập Admin: SĐT `0999999999`, OTP `123456`
2. Vào `/Sms`
3. Chọn bệnh nhân "Nguyễn Văn An" (0901234567)
4. Nhập tên đối tác: "Phòng khám ABC"
5. Chọn mẫu hoặc nhập nội dung
6. Gửi tin nhắn

### Bước 2: Bệnh nhân nhận thông báo
1. Đăng nhập Bệnh nhân: SĐT `0901234567`, OTP `123456`
2. Đang ở trang `/BenhNhan`
3. Nhận được:
   - ✅ Banner thông báo ở đầu trang
   - ✅ Chuông rung
   - ✅ Âm thanh thông báo
   - ✅ Badge hiện số "1"

### Bước 3: Đọc tin nhắn
1. Bấm vào chuông → Popup hiện ra
2. Thấy tin nhắn mới có label "MỚI"
3. Bấm vào tin nhắn → Màn chi tiết hiện ra
4. Badge giảm xuống "0"

### Bước 4: Trả lời
1. Nhập vào textarea: "Cảm ơn bác sĩ, con đã nhận được!"
2. Bấm "Gửi trả lời"
3. Alert "✅ Đã gửi trả lời thành công!"
4. Quay về danh sách → Thấy tin trả lời ở đầu

---

## Lưu ý quan trọng

### 1. SignalR Connection
- Bệnh nhân chỉ nhận notification khi **đang online** và **đã kết nối SignalR**
- Nếu offline → SMS vẫn được gửi, nhưng không có realtime notification
- Khi bệnh nhân login lại → Xem tin trong danh sách lịch sử

### 2. Property `DaDoc`
- Chỉ đánh dấu khi bệnh nhân mở chi tiết (client-side)
- **Không persist vào database** trong demo này
- Production: Nên thêm API để cập nhật `DaDoc` vào DB

### 3. Format thời gian
- Dùng relative time: "5 phút trước", "2 giờ trước"
- Nếu > 7 ngày: Hiển thị ngày đầy đủ "10/08/2026 14:30"

### 4. Mobile responsive
- Popup: `width: 90%` trên mobile
- Message list: Scrollable với `overflow-y: auto`
- Textarea: `resize: vertical` để user có thể kéo dãn

### 5. Font Awesome
- Cần thêm CDN link:
  ```html
  <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
  ```

---

## Mở rộng tương lai

1. **Đánh dấu đã đọc persistent:**
   - Thêm API `PUT /BenhNhan/DanhDauDaDoc/{id}`
   - Cập nhật `DaDoc = true` vào DB

2. **Attach file:**
   - Thêm input file trong form trả lời
   - Upload lên server và lưu link

3. **Push notification:**
   - Tích hợp Firebase Cloud Messaging
   - Bệnh nhân nhận notification ngay cả khi không mở app

4. **Typing indicator:**
   - Hiển thị "Đang soạn tin..." khi bệnh nhân đang gõ

5. **Trả lời nhanh (Quick replies):**
   - Các button: "Cảm ơn", "Đã hiểu", "Cần hỗ trợ"

---

## Troubleshooting

### Chuông không hiện?
- Kiểm tra CSS: `.notification-bell { position: fixed; z-index: 9999 }`
- Kiểm tra Font Awesome CDN đã load chưa

### Badge không cập nhật?
- Check `updateUnreadCount()` được gọi sau khi render
- Check `allMessages.filter(m => !m.daDoc)` có đúng logic không

### SignalR không nhận được?
- Check console log: "✅ Đã kết nối SignalR"
- Check BenhNhanId claim có trong User không
- Check Hub đã map đúng: `app.MapHub<ThongBaoHub>("/thongBaoHub")`

### Gửi trả lời bị lỗi 500?
- Check model `TraLoiTinNhanRequest` có match với body không
- Check service `_lichSuService.LuuLichSuAsync()` có exception không
- Xem log trong Output console

---

**Hoàn thành! 🎉**

Bệnh nhân giờ đã có thể nhận thông báo realtime và trả lời tin nhắn ngay trong ứng dụng!

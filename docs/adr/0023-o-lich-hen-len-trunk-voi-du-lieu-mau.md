# 0023 — Ô Lịch hẹn lên trunk ở dạng vỏ giao diện, dữ liệu mẫu nằm trong view

- **Tác giả:** Nam · **Ngày:** 2026-09-09 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0017](0017-hai-chieu-theo-loai-du-lieu.md) — lịch hẹn thì *gọi thẳng* HIS,
  không giữ bản sao. ADR này **không thay thế** 0017; nó ghi lại một khoảng nợ có thời hạn nằm
  trước 0017.

## Bối cảnh

Đợt 3 Giai đoạn 2 khép lại với *Trang bệnh nhân nội bộ* đã có dữ liệu thật ở hai mảng **tài liệu** và
**đợt khám** (HIS đẩy lên). Mảng thứ ba — **lịch** — thì chưa: theo 0017 nó phải đi đường *gọi thẳng*
qua `DM_DoiTacApi.BaseUrl`, mà đường đó tới ngày chốt ADR này vẫn chưa có cửa nào bên HIS.

Trong lúc chờ, ô *Lịch hẹn* trên màn đã được dựng trước phần nhìn: một danh sách thông báo kiểu ngân
hàng, cộng một lịch tháng đầy đủ (điều hướng tháng, vuốt, ô ngày tô màu theo trạng thái hẹn, thanh
chi tiết dưới đáy). Toàn bộ phần nhìn đó chạy bằng **dữ liệu mẫu hardcode ngay trong view**:
`window.danhSachThongBaoLich` trong `SixosPwa/Views/Home/TrangBenhNhan.cshtml` — 7 mục, ngày neo vào
`new Date()` nên mục đầu **luôn hiện "Hôm nay"**, kèm huy hiệu số chưa đọc.

Hôm nay `master_1` nhận trọn Đợt 2 + Đợt 3, nghĩa là ô này **lên tới trunk** trong khi vẫn còn rỗng
ruột. Người đọc code về sau mở ra sẽ thấy một màn lịch chạy mượt mà không một lời gọi HIS nào, và rất
dễ kết luận nhầm rằng 0017 đã bị bỏ.

## Quyết định

**Đưa ô *Lịch hẹn* lên trunk nguyên trạng, không dán nhãn, không ẩn — và ghi nó ra đây là NỢ, không
phải kiến trúc.**

- **ADR 0017 vẫn là đích.** Lịch phải *gọi thẳng* HIS; cổng **không** giữ bản sao lịch. Dữ liệu mẫu
  hiện nay không phải một "bản sao" được chấp nhận — nó là giàn giáo.
- **Điều kiện đóng nợ:** nối cửa đọc lịch của HIS ở Đợt 4, **trước khi** bất kỳ cơ sở thật nào bật màn
  này cho bệnh nhân. Chừng nào chưa nối, ô này chỉ được sống trên môi trường thử.
- **Không cơ sở thật nào được bật *Trang bệnh nhân nội bộ* mà chưa qua điều kiện trên.** Đây là ràng
  buộc vận hành, không phải khuyến nghị: nội dung mẫu có cả lời dặn y tế (*"vui lòng nhịn ăn sáng để
  làm xét nghiệm máu"*) và tên bác sĩ, tức là thứ một bệnh nhân sẽ hành động theo.

## Hệ quả

- Trunk có một màn **chạy được nhưng không thật**. Ai đọc `TrangBenhNhan.cshtml` mà không đọc ADR này
  sẽ tưởng mảng lịch đã xong.
- Lối vào ô *Lịch hẹn* hiện **vô điều kiện** — không cổng theo `DM_DoiTacApi.KieuApi`, không cổng theo
  môi trường. Nghĩa là **không có cái van nào** để tắt riêng nó ngoài việc sửa code. Đây chính là chỗ
  phải đụng đầu tiên ở Đợt 4.
- Đổi lại, phần nhìn đã xong và đã render kiểm chứng, nên Đợt 4 chỉ còn việc thay nguồn dữ liệu —
  không phải dựng lại màn.

## Các lựa chọn đã cân nhắc

**Dán một dải băng "DỮ LIỆU MẪU — chưa nối HIS" trên đỉnh modal.** Rẻ và chặn đúng cái nguy hiểm nhất
là người dùng tưởng lịch có thật. Bỏ vì đợt này ràng buộc là *chỉ merge, không đổi gì khác*, và một
dải băng là thay đổi giao diện thật sự — nó sẽ phải gỡ ra ở Đợt 4, tức thêm một vòng sửa chỉ để rồi
xoá. Nếu Đợt 4 trượt lịch thì đây là việc **phải làm ngay**.

**Ẩn ô *Lịch hẹn* sau một cờ, như *Đăng ký khám theo gói* đã ẩn tới giai đoạn 3.** Có tiền lệ sẵn
trong chính glossary, và cho trunk sạch nghĩa. Bỏ vì ô này khác *Đăng ký khám theo gói* ở một điểm:
phần nhìn của nó **đã xong và cần được xem thử** trên nhiều khổ màn trước khi nối dữ liệu; ẩn đi thì
không ai bấm, và lỗi giao diện sẽ chỉ lộ ra vào đúng lúc nối HIS xong — chồng hai loại lỗi vào một
đợt.

**Đổi nhãn cho khớp glossary (`Lịch đặt` thay cho `Lịch hẹn`).** Hấp dẫn vì `CONTEXT.md` xếp
*"lịch hẹn (trần)"* vào mục `_Tránh_`. Bỏ vì đổi liều này **sai hơn cũ**: nội dung modal là *nhắc hẹn
/ tái khám đã xảy ra*, tức gần *Giấy hẹn tái khám* + *Đợt khám*, chứ **không** phải *Lịch đặt* (thứ
bệnh nhân đặt từ cổng). Chốt tên đúng là việc của Đợt 4, khi đã biết cửa HIS thật sự trả về cái gì.
Trong lúc chờ, `CONTEXT.md` được thêm mục *Ô Lịch hẹn* để không ai lẫn hai khái niệm.

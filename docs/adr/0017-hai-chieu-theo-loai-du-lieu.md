# 0017 — Hai chiều theo loại dữ liệu: tài liệu thì đẩy, lịch hẹn thì gọi thẳng

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-08
- **Bối cảnh liên quan:** [0014](0014-co-so-ub-dung-man-cua-khach.md) — bài học `BaseUrl` không với tới
  được. [0012](0012-anh-luu-tren-ftp-dung-chung.md) — kho FTP dùng chung.
  [0015](0015-phan-hoi-doi-tac-phai-co-statuscode-200.md) — đọc phản hồi đối tác.

## Bối cảnh

Giai đoạn 2 mở *Trang bệnh nhân nội bộ* cho nhóm **Cơ sở dùng HIS** (HisSoft `master_3`, `master_2`,
NhaKhoaDHYD…). Có ba loại dữ liệu phải chảy giữa hai hệ, và chúng **không giống nhau về bản chất**:

| Loại | Kích thước | Tần suất đọc | Chấp nhận cũ được không |
|---|---|---|---|
| Kết quả CLS, đơn thuốc (PDF) | nặng, cố định | thỉnh thoảng, đọc lại nhiều lần | được — đã in ra thì không đổi nữa |
| Tóm tắt đợt khám | nhẹ | mỗi lần mở trang | được |
| Lịch trống, lịch hẹn | nhẹ | mỗi lần đặt | **không** — lịch cũ là đặt trùng chỗ |

ADR 0014 đã trả giá đúng một lần cho việc phụ thuộc vào `BaseUrl`: cơ sở Ung Bướu khai
`http://10.85.9.34:5000` (IP nội bộ bệnh viện), SixosPwa chạy công khai gọi vào luôn là *connection
refused*, và **mọi bệnh nhân đã có tài khoản đều tắc hoàn toàn**. Bài học đó nói: đừng để đường sống
của trang bệnh nhân treo vào việc với tới được máy của khách.

Nhưng nhóm cơ sở của giai đoạn 2 khác hẳn Ung Bướu ở một điểm đo được: các DB khách (`118.69.34.247`,
`14.224.180.114`, `115.79.193.79`…) đều nằm trên **máy chủ công khai do chính công ty vận hành**, chứ
không chôn trong LAN bệnh viện.

## Quyết định

Chia theo **loại dữ liệu**, không theo cơ sở:

- **Tài liệu và tóm tắt đợt khám → HIS ĐẨY LÊN.** HIS là bên chủ động gọi; SixosPwa chỉ mở cửa nhận,
  ghi file xuống kho FTP của mình và ghi một dòng dữ liệu. File là **bản sao thật**, không phải liên
  kết trỏ về máy khách — production mỗi khách một FTP riêng nên trỏ link là mất khi khách đổi máy.
- **Lịch trống và lịch hẹn → SixosPwa GỌI THẲNG HIS** qua `DM_DoiTacApi.BaseUrl`. **Không** giữ bản
  sao lịch ở SixosPwa.

Kèm theo, `DM_DoiTacApi` phải **kiểm tra sức khỏe ngay lúc lưu cấu hình** — không cho một `BaseUrl`
không với tới được lọt vào dữ liệu rồi tới lúc bệnh nhân bấm mới vỡ. Đây chính là chỗ 0014 đã sập.

## Hệ quả

- Trang bệnh nhân **sống sót một phần** khi HIS chết: tài liệu và lịch sử khám vẫn hiện (đã là bản
  sao), chỉ mảng lịch hẹn báo lỗi. Đây là điều một cơ chế thuần "gọi thẳng" không làm được.
- Đổi lại có **hai cơ chế, hai kiểu lỗi, hai bộ khoá** phải nuôi: khoá SixosPwa cấp cho từng cơ sở
  (chiều đẩy) và khoá HIS cấp cho SixosPwa (chiều gọi thẳng).
- Lịch hẹn không có bản sao ⇒ không có bài toán lệch dữ liệu lịch, nhưng cũng **không xem lại được
  lịch cũ khi HIS ngừng phục vụ**. Chấp nhận.

## Các lựa chọn đã cân nhắc

**HIS đẩy tất cả, kể cả lịch (một chiều thuần).** Hấp dẫn vì chỉ một cơ chế để test và không đòi khách
mở cổng nào — đúng tinh thần 0014. Bỏ vì đặt lịch sẽ phải đi qua hàng đợi: bệnh nhân đặt xong chỉ nhận
được *"chờ cơ sở xác nhận"*, không biết còn chỗ hay không, và phải chờ HIS xuống lấy mới biết kết quả.
Với một cổng đặt lịch thì đó là mất chính cái giá trị người ta tới để lấy.

**SixosPwa gọi thẳng tất cả, kể cả tài liệu.** Gọn nhất về dữ liệu: không nhân bản file, không hàng
đợi, bỏ hẳn một mảng CSDL. Bỏ vì tài liệu y tế sẽ **biến mất khỏi tay bệnh nhân** mỗi khi khách đổi
máy chủ, đổi FTP, hay ngừng hợp đồng — trong khi kết quả xét nghiệm là thứ người ta cần tra lại sau
nhiều năm. Và mỗi lượt xem lại kéo một file nặng qua đường truyền của khách.

**Cấu hình được theo từng cơ sở (cơ sở nào mở cổng thì gọi thẳng, không thì đẩy).** Linh hoạt nhất và
không phải chốt sớm khi chưa khảo sát hạ tầng từng khách. Bỏ vì phải viết **cả hai đường ngay từ pilot**
mà Thiên Nam chỉ chạy được một nhánh — nhánh còn lại sẽ không có ai nghiệm thu, tức là code chết mang
tiếng là tính năng.

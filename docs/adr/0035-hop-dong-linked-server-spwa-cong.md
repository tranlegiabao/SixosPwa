# 0035 — Hợp đồng linked server `SPWA_CONG`: bảy đối tượng, chỉ được thêm tham số có mặc định

- **Tác giả:** Nam · **Ngày:** 2026-09-19 · **Trạng thái:** Đề xuất
- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0021](0021-tu-choi-tai-lieu-mo-coi.md) ·
  [0031](0031-his-tu-dung-ho-so-ben-cong.md) ·
  [0032](0032-mot-ho-so-giu-dung-mot-ma.md) ·
  [0040](0040-idtaikhoantheosdt-soi-guong-idbenhnhan.md)

## Bối cảnh

`Dev_Master3.dbo.S00_UploadOnline` bên HIS gọi thẳng vào CSDL cổng qua linked server `SPWA_CONG`.
Đây là một **hợp đồng thật** nhưng chưa ADR nào ghi, và nó có ba tính chất khiến nó nguy hiểm khác
thường:

1. **Không grep nào bên repo cổng nhìn thấy nó.** Người sửa cổng không có cách nào biết mình vừa đụng
   vào cái gì. Cả hai đầu dây nằm ở hai CSDL khác nhau, trên hai repo khác nhau.
2. **Build vẫn xanh khi đã vỡ.** Đây là lời gọi động qua chuỗi; trình biên dịch không thấy gì.
3. **Sửa đầu HIS = đi từng khách hàng.** Nên trên thực tế chỉ có đầu cổng được phép nhúc nhích.

Đợt A đã đạp một lần: bớt một tham số làm chết màn *Gửi cho bệnh nhân* bên HIS mà không ai phát hiện
được bằng cách đọc code cổng. Đợt 1B suýt đạp lần thứ hai ở một chỗ khác — xem ADR 0040.

## Quyết định

Hợp đồng gồm **bảy đối tượng**. Với cả bảy: **chỉ được THÊM tham số có giá trị mặc định. Không bao giờ
bớt tham số, không đổi tên tham số, không đổi tên hay thứ tự cột trả về, không đổi tên chính stored.**

| # | Đối tượng | Ràng buộc riêng |
|---|---|---|
| 1 | `DM_BenhNhan_Save` | giữ 12 tham số. **Không có `@IDCoSo`** — đây chính là lý do đợt 1B phải dùng dòng neo (PA-C1) |
| 2 | `HT_TaiKhoan_Save` | giữ 9 tham số, kể cả `@IDBenhNhan` đã *nhận-rồi-bỏ* từ đợt A |
| 3 | `DM_BenhNhan_NhanChuSoHuu` | giữ 4 tham số; thân thành no-op từ 1B |
| 4 | `DM_BenhNhanCoSo_Save` | **giữ nguyên tên** dù bảng `DM_BenhNhanCoSo` đã biến mất ở 1B; giữ `@IDBenhNhanCoSo OUTPUT` |
| 5 | `QL_DotKham_Save` | giữ tên tham số `@IDBenhNhanCoSo` dù **cột** trong bảng đã đổi thành `IDBenhNhan` |
| 6 | `QL_TaiLieuBenhNhan_Save` | như trên. Là đối tượng **duy nhất** HIS đọc được `ResultCode`, nhờ có `SELECT @rc, @rm;` ở lời gọi |
| 7 | `S00_SPWA_DoHienTrang` | hợp đồng **cột trả về**, không phải tham số: `IDCoSo, IDBenhNhan, IDBenhNhanCoSo, MaBNDangNoi, IDTaiKhoanTheoSdt, IDTaiLieuDaCo, DuongDanDaCo` — đúng tên, đúng thứ tự. 🔴 **KHÔNG được thêm cột, kể cả ở cuối** — xem dưới |

Kèm bốn luật vận hành:

- **Tham số `OUTPUT` không vượt được linked server.** Muốn HIS đọc được kết quả thì stored phải
  `SELECT` nó ra và HIS phải hứng bằng `INSERT ... EXEC`. Hôm nay chỉ đối tượng #6 làm vậy. Nghĩa là
  với sáu đối tượng còn lại, `ResultCode` **không ai đọc** — HIS chỉ biết hỏi lại `S00_SPWA_DoHienTrang`.
  ⇒ *Trả mã lỗi cho HIS* là một ảo tưởng; thứ duy nhất HIS cảm nhận được là **cột trả về của #7** và
  **việc stored có ném hay không**.
- **Không được ném.** Lỗi vượt linked server rơi vào `CATCH` của HIS và bị báo thành
  `KHONG_TOI_DUOC_CONG` — một mã nói **sai nguyên nhân**. Mọi ca từ chối phải là *từ chối mềm*.
- **`CREATE OR ALTER`, tuyệt đối không `DROP` + `CREATE`.** `DROP PROCEDURE` xoá sạch mọi `GRANT` trên
  object đó; login `spwa_his` mất `EXECUTE` ngay lập tức và hỏng theo kiểu không liên quan gì tới nội
  dung stored. Đã đạp thật ngày 16/09.
- **Không TVP.** Table-valued parameter không đi qua linked server được. Mọi tham số phải vô hướng.

🔴 **Đính chính (19-09): "thêm cột ở cuối" là SAI.** HIS hứng #7 bằng
`INSERT INTO @do EXEC ... AT SPWA_CONG`, mà `@do` khai báo **đúng bảy cột**. `INSERT ... EXEC` đòi số
cột khớp tuyệt đối, nên **cột thứ tám làm vỡ ngay**, dù đặt ở cuối. Chính chú thích trong stored đã
ghi *"Lệch một cột là INSERT ... EXEC vỡ"*. Hệ quả thật: **bảy cột này là trần cứng** — không có
đường nào báo thêm thông tin cho HIS mà không sửa HIS. Muốn nói thêm điều gì thì phải **nhét vào một
cột đã có** (ví dụ `MaBNDangNoi`), hoặc ghi log bên cổng để người vận hành tự tra.

## Hệ quả

Phép nghiệm thu duy nhất bắt được vi phạm hợp đồng này là **đẩy thật một bệnh nhân hoàn toàn mới** từ
`/QuanLy/SPWA_GuiChoBenhNhan` và đòi `soThanhCong > 0 AND soLoi = 0` (`PLAN-DOT-1B.md §10` mục 11).
Không có test đơn vị nào, không có build nào, không có phép grep nào thay được nó. Mọi đợt đụng tới
bảy đối tượng trên **bắt buộc** chạy phép này trước khi đóng đợt.

Bảng giao thức đầy đủ — bảy cột, năm chốt `RETURN` của HIS, năm cửa ghi — ở
`.claude/prompts/2026-09-18_so-do-uml-sixospwa/HOP-DONG-5-CUA.md`.

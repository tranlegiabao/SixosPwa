# 0031 — HIS tự dựng tài khoản và hồ sơ bên cổng (đảo một vế của 0021)

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-16
- **Bối cảnh liên quan:** [0005](0005-luu-mat-khau-khong-bam.md) ·
  [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0021](0021-tu-choi-tai-lieu-mo-coi.md) ·
  [0024](0024-noi-ho-so-tu-dong-hai-tang-va-go-noi.md) ·
  [0026](0026-nguon-stored-procedure-lay-tu-csdl-va-dua-vao-git.md) ·
  [0027](0027-dang-nhap-duoc-thi-phai-co-tai-khoan.md) ·
  [0028](0028-ho-so-khong-co-can-cuoc-thi-khong-noi-benh-an.md) ·
  [0030](0030-tai-lieu-nam-o-hai-kho.md)

> Số **0029** đang để dành cho mạch *Quét QR màn Đăng nhập*, **0030** là Đợt 2. ADR này lấy 0031.

## Bối cảnh

ADR 0030 mở *chế độ Trỏ đường*: HIS không gửi byte nữa, chỉ gửi **đường dẫn**, cổng tự kéo tệp từ
*Kho phiếu cơ sở*. Nhưng đường ấy vẫn tắc ở một chỗ khác: cổng **từ chối tài liệu mồ côi** — không
có hồ sơ nào nhận thì trả `ResultCode 5` (`QL_TaiLieuBenhNhan_Save`), và đường HTTP trả **409
`CHUA_CO_NGUOI_NHAN`**. Đo 12/09: bấm *Gửi* trên màn HIS rơi đúng vào lỗi đó.

Nghĩa là mọi tài liệu chỉ đi được **sau khi** bệnh nhân tự vào cổng, tự đăng ký, tự nối hồ sơ ở màn
*Nối hồ sơ*. Phòng khám không có cách nào đẩy trước.

## Quyết định

**HIS được phép tự dựng tài khoản, hồ sơ và nối mã bên cổng — nhưng chỉ qua đúng những cửa stored
mà cổng đã có, và chỉ khi ba điều kiện dữ liệu đạt.**

Cụ thể:

1. **Ghi thẳng vào `HIS_CSKH`, không qua HTTP** — nhưng bằng `EXEC` stored của cổng qua linked
   server `SPWA_CONG`, **không `INSERT` thô**. Cổng đã có đúng các cửa ghi, mỗi cửa mang luật riêng
   (chặn CCCD giả / ngày sinh 1900 · `DaMoTaiLieu` · từ chối mồ côi · `PhienBan`/`LaBanMoiNhat`).
   Gõ lại bốn khối luật ấy rồi đem đi 12 CSDL khách là cách chắc chắn nhất để chúng trôi khỏi nhau.
2. **Một cửa đọc mới, `S00_SPWA_DoHienTrang`** (file `Database/23_*.sql`) — chỉ đọc, trả 7 sự thật
   trong một lượt đi–về. Login của HIS trên `HIS_CSKH` chỉ có `EXECUTE` **7 stored**, **0
   `GRANT SELECT`**, **0 quyền ghi bảng** (file `Database/25_*.sql`). Lý do: `HIS_CSKH` dùng chung
   cho 12 cơ sở, `DM_BenhNhan` và `HT_TaiKhoan` là bảng toàn hệ thống — `GRANT SELECT` cho login của
   phòng khám A là A đọc được bệnh nhân và danh bạ số điện thoại của B, C, D. Ownership chaining lo
   phần ghi, nên không cần quyền bảng.
3. **Hàng rào "một hồ sơ giữ đúng một mã" đặt ở HIS**, trong `S00_UploadOnline`: đo trước rồi mới
   chọn cửa. Ba nhánh — chưa có dòng ⇒ `DM_BenhNhanCoSo_Save`; đang nối đúng mã ⇒ không gọi gì;
   **đang nối mã khác ⇒ tụt lại, trả `HO_SO_NOI_MA_KHAC`, dừng hẳn**. 🔴 Bắt buộc phải có, vì
   `DM_BenhNhanCoSo_Save` là cửa **duy nhất không mang hàng rào**: nhánh có-dòng-rồi của nó là
   `UPDATE SET MaBN = @MaBN` (`08_MO_CUA_TAI_LIEU.sql:72`). Hàng rào thật của hệ đang nằm ở C#
   (`NoiKhiLuuAsync:396-403`), mà đường này không đi qua C#.
4. **Không có transaction bao các cửa** — gọi chéo máy mà mở transaction là đòi MSDTC, môi trường
   không có. Bù bằng thứ tự cửa và tính idempotent của từng cửa; kết cục "xong cửa đầu, hỏng ở cửa
   cuối" có mã riêng `NUA_DUONG` và bấm lại là qua.

## Vế nào của 0021 bị đảo

ADR 0021 có **hai** vế. Chỉ vế thứ hai bị đảo:

- **(a) "cổng không giữ một byte bệnh án nào của người chưa phải người dùng" — VẪN ĐÚNG.**
  Chế độ Trỏ đường cổng chỉ giữ **con trỏ**; tệp nằm ở *Kho phiếu cơ sở*, cổng đọc tệp sống mỗi lần
  mở (ADR 0030). `BamNoiDung` để `NULL` vĩnh viễn, `DungLuongByte = 0`.
- **(b) "việc nối hồ sơ là của người dùng ở màn Nối hồ sơ — không phải của đường API" — ĐÂY là vế bị
  đảo.** HIS tự dựng tài khoản + hồ sơ + nối mã, không hỏi ai.

## Giá phải trả

Nói thẳng, không nói quá:

- **Cổng bắt đầu có bản ghi của người chưa bấm đồng ý gì.** Một người chưa từng nghe tới cổng vẫn có
  thể đã có `HT_TaiKhoan` + `DM_BenhNhan` + `DM_BenhNhanCoSo` ở đó, do phòng khám bấm *Gửi*.
- **Cửa tài liệu (`DaMoTaiLieu = 1`) mở dựa trên SỐ ĐIỆN THOẠI do phòng khám gõ vào HIS.** Gõ nhầm
  số là **người lạ nhận OTP** rồi đăng nhập vào xem bệnh án của người khác. Đây là rủi ro thật, không
  phải rủi ro lý thuyết.

Hàng rào đánh đổi (chốt 3, kiểm **trước** cửa đầu tiên, trượt là không gọi cửa nào):

| Điều kiện | Vì sao |
|---|---|
| CCCD **12 số thật** (không `11111111111`/`111111111111`) | Mã giả thì cổng dò theo nhân thân — dễ trộn hai người |
| Số điện thoại hợp lệ | Số rác thì OTP đi đâu không ai biết |
| Số **không gắn quá 10 người** | Số của phòng khám / số dùng chung sẽ mở cửa cho cả chục hồ sơ lạ |

Và: `DM_BenhNhanCoSo_Save` nhánh `UPDATE` cố ý không đụng `DaMoTaiLieu` ⇒ **HIS không thể mở một cửa
đã đóng**, chỉ đặt được giá trị lúc tạo dòng mới.

## Ba tiền đề triển khai — phải đọc trước khi bật ở bất kỳ cơ sở nào

🔴 Ghi ở đây vì chúng **không nằm trong `Database/` của repo nào**, người deploy sẽ không thấy script:

1. **Cơ sở phải có 4 cột ký số** — `CLS_PhieuCLS.URLKySo`, `CLS_PhieuXN.URLKySo`,
   `QL_GiayRaVien.URLKySo`, `QL_ToaThuoc_CT.URLKySoGop`. Chúng thuộc mạch ký số của đồng nghiệp
   (nhánh `Thinh_QuyHoachCKSM3`, chưa merge) và do `ALTER` tay, không script nào trong git tạo.
   Đo 16/09: `Dev_Master3` có 4/4; **mọi DB khách 0/4**. Thiếu cột thì chế độ này không có tài liệu
   nào để gửi.
2. **Phải có linked server `SPWA_CONG`** trỏ đúng host cổng và bật `rpc out`. Tạo bằng
   `Database/SPWA/10_TAO_LINKED_SERVER_SPWA_CONG.sql`, tay người quản trị CSDL — app không tạo được
   (`sixosdev`@14.224 không sysadmin). 🔴 Đừng dùng lại tên `LINKED_SERVER_ONLINE`: nó đã tồn tại
   trên 118 và trỏ về chính 118, còn ở UngBuou nó trỏ `UB_DangKyOnline` với 51 chỗ gọi.
3. **Login của HIS phải có `EXECUTE` đúng 7 stored và không hơn** — `Database/25_GRANT_LOGIN_HIS.sql`
   bên cổng. Thêm quyền bảng vào đó là gỡ mất hàng rào ở mục 2 phần Quyết định.

## Hệ quả

- Công tắc **`GUIBENHNHANTRODUONG`** (`HT_Config`, nhóm *8. Cổng bệnh nhân*), mặc định **tắt** —
  thiếu dòng hoặc tắt = chế độ API như cũ. Bật nó thì lưới **luôn** đọc stored bản ký số và công tắc
  `GUIBENHNHANBANKYSO` **không còn tác dụng**: tổ hợp "Trỏ đường + chưa ký" là ô vô nghĩa, T-SQL
  không render PDF được. Điều đó được nói ra ở **`Ghichu`** của chính `GUIBENHNHANBANKYSO` — màn
  *Tiện ích* không có cột tên tiếng Việt, `Ghichu` là ô giải thích duy nhất.
- Sổ nằm **ở HIS**, không ở cổng: `SPWA_GuiTaiLieu.CheDo` (`API`|`TRO_DUONG`) và các sự kiện dựng
  tài khoản / hồ sơ / nối mã ghi vào `SPWA_LogDuLieu` với `TenService = 'S00_UploadOnline'`.
  Không nhét mã chữ vào `MaKetQua` của dòng API — cột đó đang mang mã HTTP.
- `QL_TaiLieuBenhNhan_Save` nhận thêm `@NguonKho` (`Database/26_*.sql`), mặc định `N'CONG'` nên mọi
  chỗ gọi cũ của C# chạy y nguyên. Không có tham số này thì dòng do chế độ Trỏ đường sinh ra sẽ mang
  `NguonKho = 'CONG'` và cổng đi tìm tệp ở FTP của chính nó ⇒ 404 không ai hiểu vì sao.
- Nợ để lại: chuyển hàng rào "một hồ sơ giữ đúng một mã" **vào stored của cổng** ở Đợt 4, để đường
  nào đi vào cũng vấp phải nó chứ không riêng đường này.

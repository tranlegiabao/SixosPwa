# 0040 — `IDTaiKhoanTheoSdt` soi gương `IDBenhNhan`, không tra theo số điện thoại nữa

- **Tác giả:** Nam · **Ngày:** 2026-09-19 · **Trạng thái:** Đề xuất
- **Bối cảnh liên quan:** [0019](0019-mot-tai-khoan-nhieu-ho-so.md) ·
  [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) ·
  [0027](0027-dang-nhap-duoc-thi-phai-co-tai-khoan.md) ·
  [0031](0031-his-tu-dung-ho-so-ben-cong.md) ·
  [0032](0032-mot-ho-so-giu-dung-mot-ma.md)

## Bối cảnh

Đợt 1B bỏ `HT_TaiKhoan` khỏi đường bệnh nhân: từ nay chỉ Admin còn tài khoản, còn bệnh nhân đăng nhập
bằng chính dòng `DM_BenhNhan` tại cơ sở. Nhưng **HIS không biết chuyện đó**, và không được phép biết —
sửa `S00_UploadOnline` là phải đi từng khách hàng (cùng lý do C17a loại `SPWA_LogDuLieu` khỏi phạm vi).

HIS gọi `S00_SPWA_DoHienTrang` qua linked server `SPWA_CONG` và đọc 7 cột theo **đúng tên và đúng thứ
tự**. Một trong bảy cột là `IDTaiKhoanTheoSdt`, và HIS dùng nó ở **bước (5) CỬA 2**:

```sql
IF @IDTaiKhoan IS NULL
BEGIN
    EXEC dbo.HT_TaiKhoan_Save ... AT SPWA_CONG;   -- sau 1B là no-op
    ... hỏi lại DoHienTrang ...
    IF @IDTaiKhoan IS NULL
    BEGIN
        SET @MaLyDo = N'NUA_DUONG';   -- S00_UploadOnline:373
        RETURN;                       -- :375  DỪNG HẲN
    END
END
```

Kế hoạch đợt 1B ban đầu ghi: cột này trả `NULL`. Truy ra thì **trả `NULL` là giết toàn bộ đường đẩy
tài liệu** — cửa dựng tài khoản đã thành no-op nên lần hỏi thứ hai chắc chắn vẫn `NULL`, mọi lượt đẩy
của mọi cơ sở đều dừng ở `NUA_DUONG`. Đây đúng hạng lỗi mà phương án PA-C nguyên bản mắc ở cửa 1, chỉ
khác là lần này nằm ở cửa 2 và không ai soi tới.

Phương án hiển nhiên tiếp theo — **giữ nghĩa đen của tên cột**, tra `DM_BenhNhan WHERE SDT = @Sdt` —
cũng hỏng, và hỏng nặng hơn vì nó *im lặng*. Đo thật trên 10.987 hồ sơ Thiện Nam đối chiếu được với
`Dev_Master3`:

| SĐT cổng so với SĐT HIS | Số dòng |
|---|---|
| Khớp | **114** (1,0%) |
| Cả hai đều có, khác nhau | **9.934** (90,4%) |
| HIS rỗng | 831 |
| Cổng rỗng | 108 |

Hôm nay không ai chết vì `HT_TaiKhoan.SDT` được **chính cửa 2 ghi bằng đúng số HIS vừa gửi**, nên tra
theo SĐT luôn trúng. Còn `DM_BenhNhan.SDT` bên cổng thì đã trôi khỏi HIS từ lâu. Chuyển đích tra sang
`DM_BenhNhan` mà vẫn tra theo SĐT ⇒ **99% hồ sơ đang có sẽ chết**. Tệ hơn: đó là vòng lặp chết cứng —
cửa 1 (nơi duy nhất có câu `SDT = ISNULL(@SDT, SDT)`) bị bỏ qua khi hồ sơ đã tồn tại, nên SĐT **không
bao giờ** được sửa, và lượt sau chết y hệt.

## Quyết định

`S00_SPWA_DoHienTrang` trả `IDTaiKhoanTheoSdt` = **đúng giá trị mà cột `IDBenhNhan` trả** — dòng
`DM_BenhNhan` tại cơ sở đang hỏi, hoặc dòng neo (`IDCoSo IS NULL`) nếu chưa gắn cơ sở.

Tên cột giữ nguyên (hợp đồng). **Ý nghĩa** đổi, và đây là phần phải nói rõ:

> ~~"số điện thoại này đã có tài khoản chưa"~~ → **"cơ sở này đã có hồ sơ ăn được cho *người* này chưa"**

Danh tính do **CCCD** quyết định, đúng theo ADR 0020. Số điện thoại tụt xuống đúng vai trò của nó:
đường nhận OTP, không phải khoá danh tính.

Hệ quả bắt buộc đi kèm, **cùng một gốc**:

- `IDBenhNhan` cũng phải trả *dòng tại cơ sở này, chưa có thì trả dòng neo*. Trả dòng ở cơ sở khác là
  chết ở `MA_DA_CO_CHU` (:334).
- Cửa 1 `DM_BenhNhan_Save` **chỉ được dùng lại dòng neo**; không có dòng neo thì luôn dựng dòng neo
  mới. Tuyệt đối không trả về một dòng đã gắn cơ sở.

## Hệ quả

**Được:** cả 10.987 hồ sơ Thiện Nam đi được (100%), không cần đụng một dòng nào bên HIS, và cột này trở
thành thứ trả lời đúng luật **C7b** — cùng một câu hỏi mà màn đăng nhập bên cổng đang hỏi.

**Mất:** SĐT bên cổng vẫn trôi so với HIS. Bệnh nhân đổi số thì không nhận được OTP ở số mới cho tới
khi có ai đó sửa hồ sơ bên cổng. Đây là **bệnh có sẵn từ trước 1B**, không phải do 1B gây ra, và đợt
này cố ý không chữa: đường sửa duy nhất là thêm `@SDT` (có mặc định) vào cửa 4 rồi sửa `S00_UploadOnline`
truyền xuống — mà sửa HIS thì phải đi từng khách hàng. Ghi lại ở đây để đợt sau không phải truy lại.

**Quả mìn đã gỡ:** phương án "trả một hằng số khác `NULL` cho qua chốt" bị loại, vì `@IDTaiKhoan` lấy
từ cột này được HIS bơm thẳng vào cửa 3 `DM_BenhNhan_NhanChuSoHuu(@IDBenhNhan, @IDTaiKhoan)`. Hôm nay
cửa 3 là no-op nên hằng số bừa cũng không sao — nhưng ngày nào đó ai mở lại cửa 3, một hằng số `1` sẽ
gán **mọi** hồ sơ bệnh nhân cho tài khoản Admin.

**Nghiệm thu:** mục 11 của `PLAN-DOT-1B.md §10` là phép duy nhất bắt được lỗi này — đẩy một bệnh nhân
hoàn toàn mới từ `/QuanLy/SPWA_GuiChoBenhNhan`, đòi `soThanhCong > 0 AND soLoi = 0`. Build vẫn xanh dù
sai, và **không grep nào bên repo cổng nhìn thấy** `S00_UploadOnline`.

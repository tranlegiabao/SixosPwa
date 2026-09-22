# 0039 — Lỗi của từng cửa phải về tới người vận hành, không bị nuốt

- **Tác giả:** Nam · **Ngày:** 2026-09-19 · **Trạng thái:** Đề xuất
- **Bối cảnh liên quan:** [0035](0035-hop-dong-linked-server-spwa-cong.md) ·
  [0036](0036-ho-so-la-cap-nguoi-x-co-so.md) · [0032](0032-cua-doi-ma-benh-nhan.md)

## Bối cảnh

Ngày 19-09, màn *Gửi cho bệnh nhân* báo **Lỗi** cho bệnh nhân TRƯƠNG NGUYỄN NHẬT NAM
(mã `102483`, `VV2608210002`). Thông điệp duy nhất người vận hành nhận được:

> Đã dựng tài khoản nhưng chưa nối được mã bệnh nhân.

Câu đó **không đúng sự thật và không dùng được**. Lý do thật, truy ra bằng cách gọi tay cửa 4:
mã `102483` bên cổng **đang thuộc PHẠM THỊ LÀNH** (CCCD `030189009868`), nên `UNIQUE(IDCoSo, MaBN)`
chặn và cửa 4 trả `ResultCode = 2` kèm câu *"Mã bệnh nhân này đã được cơ sở cấp cho người khác."*

Câu đúng ấy **bị vứt đi**. Trong `S00_UploadOnline`, **năm trong sáu cửa** gọi stored qua linked
server rồi **bỏ `@ResultMessage`**, sau đó dò lại `S00_SPWA_DoHienTrang` và tự suy ra một câu chung:

| Cửa | Dòng | Stored | Cách kiểm hiện nay |
|---|---|---|---|
| 1 | `:305` | `DM_BenhNhan_Save` | dò lại `DoHienTrang`, câu chung |
| 2 | `:349` | `HT_TaiKhoan_Save` | dò lại `DoHienTrang`, câu chung |
| 3 | `:385` | `DM_BenhNhan_NhanChuSoHuu` | 🔴 **không kiểm gì cả** |
| 4 | `:403` | `DM_BenhNhanCoSo_Save` | dò lại `DoHienTrang`, câu chung |
| 5 | `:467` | `QL_DotKham_Save` | 🔴 **không kiểm gì cả** |
| 6 | `:156` | `QL_TaiLieuBenhNhan_Save` | ✅ hứng đúng `@rc` + `@rm` |

Suy-ra-từ-hiện-trạng **không thể** thay cho lý do thật: `DoHienTrang` chỉ tìm hồ sơ **theo CCCD**,
nên một mã do *người khác* giữ là vô hình với nó — `@IDBNCS` về `NULL` và HIS chỉ biết nói
*"chưa nối được"*.

## Quyết định

**Mọi lời gọi qua `SPWA_CONG` phải hứng `@ResultCode` + `@ResultMessage`, và thông điệp hiển thị cho
người vận hành phải là thông điệp của chính cửa đã từ chối.**

Khuôn đã có sẵn và đang chạy đúng ở cửa 6 — chép sang năm cửa còn lại:

```sql
DELETE FROM @kq;
INSERT INTO @kq
EXEC (@setOpt + N'DECLARE @rc int, @rm nvarchar(4000);
        EXEC dbo.<stored> ... , @ResultCode = @rc OUTPUT, @ResultMessage = @rm OUTPUT;
        SELECT @rc, @rm;', ...) AT SPWA_CONG;
SELECT TOP 1 @rc = ResultCode, @rm = ResultMessage FROM @kq;
```

Ba hệ quả kèm theo:

1. **Cổng tự tra tên người đang giữ mã.** `rc = 2` sinh trong `CATCH` từ vi phạm `UNIQUE`, lúc đó
   stored chưa biết ai giữ mã — nên nhánh `CATCH` của `DM_BenhNhanCoSo_Save` chạy thêm một `SELECT`
   để dựng câu *"Mã 102483 đã được cơ sở cấp cho PHẠM THỊ LÀNH (CCCD …)."*
   🔴 **Chỗ tra phải là cổng, không phải HIS.** HIS tra ngược có nghĩa HIS phải biết tên bảng/cột bên
   cổng — một ràng buộc ngầm **ngoài** bảy đối tượng hợp đồng, kiểu ràng buộc mà đổi một cột là vỡ
   trong im lặng còn `grep` repo cổng thì không thấy ai dùng.
2. **Mã lý do mới `MA_THUOC_NGUOI_KHAC`**, tách khỏi `NUA_DUONG`. `NUA_DUONG` nghĩa là *dở dang, bấm
   Gửi lại là xong*; ca này thì **gửi lại bao nhiêu lần cũng hỏng y hệt**. Gộp chung hai thứ trái
   ngược nhau vào một mã khiến người vận hành làm đúng cái việc vô ích.
3. **Cột *Kết quả* tô màu theo nhóm:** đỏ = gửi lại vô ích, phải sửa tay (`MA_DA_CO_CHU`,
   `MA_THUOC_NGUOI_KHAC`, `HO_SO_NOI_MA_KHAC`); cam = gửi lại được (`NUA_DUONG`, `THIEU_CCCD`,
   `SDT_KHONG_HOP_LE`, `DUONG_DAN_QUA_DAI`, `KHONG_TOI_DUOC_CONG`).

## Vì sao không chọn hướng khác

**Thêm cột vào `S00_SPWA_DoHienTrang`** — không làm được. HIS hứng bằng `INSERT INTO @do EXEC` với
`@do` khai **đúng bảy cột**, nên cột thứ tám vỡ ngay kể cả đặt ở cuối (ADR 0035, đính chính 19-09).
Đó chính là lý do phải đi đường `@ResultMessage`, vốn **không** vướng trần này.

**Thêm `TrangThaiGui = 3` (cần xử lý tay)** để dòng bị từ chối rời khỏi nhóm *Chưa gửi* và thôi bị
gửi lại — đúng về nguyên tắc, nhưng chạm **schema + 2 stored lưới + bộ lọc**, mà HIS triển khai theo
**từng khách hàng**. Nhãn màu đạt phần lớn lợi ích với rủi ro gần bằng không. **Ghi nợ**, không làm
trong đợt này.

**Nêu tên người đang giữ mã** có lộ danh tính người thứ ba cho nhân viên phòng khám. Chấp nhận, vì:
người xem là nhân viên **của chính cơ sở đã cấp mã đó** — dữ liệu vốn thuộc phạm vi họ; và không có
tên thì họ **không sửa được**, buộc phải mở một phiên hỗ trợ kỹ thuật cho mỗi ca.
Phạm vi lộ hẹp đúng bằng thế: `SaveBenhNhanCoSoAsync` **không còn nơi nào trong cổng gọi** từ đợt
V6b (`AdminStoredProcedureService.cs:327`), nên `@ResultMessage` của cửa 4 **chỉ HIS đọc** — không
màn nào của bệnh nhân hiện nó ra.

## Hệ quả

`ThongDiep` trong `SPWA_GuiTaiLieu` là `nvarchar(500)` còn `@rm` là `nvarchar(4000)` ⇒ phải
`LEFT(@rm, 500)` khi ghi sổ, nếu không là cụt giữa chừng.

🔴 **Việc đổi nội dung `@ResultMessage` của một đối tượng hợp đồng là hợp lệ, đổi *chữ ký* thì không.**
ADR 0035 khoá tên stored, tên/thứ tự tham số, tên/thứ tự cột trả về — **không** khoá nội dung chuỗi.
Nhưng vì lý do đó, **không được** để HIS *phân nhánh theo văn bản* của `@rm`: phân nhánh chỉ dựa vào
`@rc`, chuỗi chỉ để hiển thị. Nếu HIS đọc chữ trong chuỗi thì mọi lần sửa câu văn bên cổng lại thành
một thay đổi phá vỡ.

Ca này còn **sẽ gặp thật khi triển khai** dù dữ liệu Thiên Nam hiện tại là do máy sinh: HIS sửa CCCD
(gõ nhầm rồi sửa, đổi 9 số sang 12 số), HIS gộp hồ sơ trùng, hoặc bệnh nhân tự nối nhầm ở màn *Sửa
hồ sơ* — cả ba đều dẫn tới đúng tình huống này, và **không tự lành**.

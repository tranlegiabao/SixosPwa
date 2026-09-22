# 0011 — Cơ sở thuộc đối tác theo quan hệ một–nhiều

- **Tác giả:** Nam · **Ngày:** 2026-08-25 · **Trạng thái:** Đã bị thay thế bởi ADR 0036

> 🔴 **ĐÃ BỊ THAY THẾ bởi ADR 0036** (2026-09-19, đợt 1B).
> Bảng `DM_DoiTac` đã bị bỏ ở đợt 1B; khái niệm còn lại là cột phẳng `DM_CSKCB.TenCongTy`.
> Xem [0036](0036-ho-so-la-cap-nguoi-x-co-so.md). Giữ file này để đọc lại bối cảnh cũ.

- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0010](0010-doi-ten-lan-toi-javascript.md)

## Bối cảnh

Trước tái kiến trúc, `DMBenhNhan.MaDT` là cột nối duy nhất giữa bệnh nhân và "đối tác". Phát hiện
**C-01** của đợt audit cho thấy cột đó **lẫn hai không gian khoá**: có dòng chứa mã cơ sở (`79423`,
`CS4`, `75265`), có dòng chứa mã đối tác (`1`), cộng 11 dòng rác `DT001` do
`HomeController.cs:384` gán cứng.

Đợt 2 dọn chuyện đó bằng cách cho hồ sơ bệnh nhân treo vào **cơ sở** (`DM_BenhNhanCoSo.IDCoSo`) —
đúng, nhưng để lại một lỗ: `DM_DoiTac` và `DM_CSKCB` **không còn cột nào nối với nhau**. Đo thật trên
`HIS_CSKH`: `DM_DoiTac.MaDT` là `'1'`,`'2'` còn `DM_CSKCB.MaCoSo` là `'79423'`,`'CS2'`… — **0 dòng
trùng**.

Hệ quả: màn *Gửi tin nhắn* (`POST /Home/LocDanhSachBN`) xác thực đối tác xong thì buộc phải trả **mọi
cơ sở**. Bộ lọc theo đối tác chết **âm thầm** — không ném lỗi, không vỡ build, chỉ trả sai. Đây là loại
hỏng tệ nhất vì không ai biết.

Thêm một chuyện làm rối: chữ **"đối tác"** trong hệ này mang **hai nghĩa**. `DM_DoiTac` là *tổ chức*
(có `BrandName` để gửi SMS). Còn "đối tác" trong `DM_DoiTacApi.IDCoSo` và `HT_TaiKhoanDoiTac.IDCoSo`
thì thực chất **là cơ sở**. Hai nghĩa này phải tách bạch trước khi bàn quan hệ.

## Quyết định

Thêm cột **`DM_CSKCB.IDDoiTac`** (nullable) làm khoá ngoại sang `DM_DoiTac(ID)` — **một đối tác quản
nhiều cơ sở**. `DM_BenhNhan_Loc` nhận thêm tham số `@IDDoiTac`, đi đường: đối tác → các cơ sở của đối
tác → hồ sơ bệnh nhân.

Cột để **nullable** vì "cơ sở chưa thuộc đối tác nào" là trạng thái hợp lệ và đang đúng với 7/8 cơ sở.

Seed **chỉ một dòng**: `75265` "PKĐK Hoàng Dũng" → đối tác `Hoàng Dũng` (trùng tên nên chắc chắn).
Bảy cơ sở còn lại để NULL — không bịa dữ liệu nghiệp vụ, để người dùng tự gán qua màn Admin.

## Các lựa chọn đã cân nhắc

**Bảng nối `DM_DoiTac_CoSo` (N–N).** Đúng bài hơn nếu một cơ sở có thể thuộc nhiều đối tác cùng lúc.
Bỏ vì hiện chưa có bằng chứng nào cho thấy cần N–N — thực tế là 2 đối tác / 8 cơ sở — mà lại nặng hơn
hẳn (thêm bảng, thêm 2 khoá ngoại, thêm seed, thêm một tầng JOIN ở mọi câu lọc). Khi nào có ca thật cần
N–N thì nâng lên vẫn được, và nâng từ 1–N lên N–N rẻ hơn là gỡ N–N xuống.

**Bỏ hẳn trục đối tác, lọc theo cơ sở.** Thống nhất về một nghĩa duy nhất, khớp với hướng mà
`DM_DoiTacApi` và `HT_TaiKhoanDoiTac` đã đi, và **không đụng schema** chút nào. Bỏ vì màn *Gửi tin
nhắn* sẽ phải đổi hình dáng (ô "Đối tác + mật khẩu" thành danh sách thả Cơ sở) và **mất cổng mật khẩu
đối tác** — trong khi `BrandName` để gửi SMS vốn thuộc về *tổ chức* chứ không thuộc cơ sở, nên trục đối
tác vẫn có lý do tồn tại.

**Chặn cứng: chưa có trục thì trả rỗng.** Sửa được cái "im lặng" ngay mà không đụng schema, nhưng để
tính năng chết hẳn thay vì chữa nó. Chỉ hợp nếu phải hoãn sang đợt sau.

## Hệ quả

- Đo được ngay sau khi thi hành: lọc theo `Hoàng Dũng` trả **3 hồ sơ**, theo `Bảo Minh` trả **0**.
  Trước đó cả hai đều trả **12** — toàn bộ hồ sơ của mọi cơ sở.
- Bảy cơ sở `IDDoiTac IS NULL` sẽ **không** hiện với bất kỳ đối tác nào cho tới khi được gán. Đây là
  hành vi cố ý, nhưng phải nói cho người vận hành biết, nếu không họ tưởng mất dữ liệu.
- Màn Admin hiện **chưa có ô gán đối tác cho cơ sở**. Muốn gán phải chạy `UPDATE` tay. Việc còn treo.
- Khoá JSON `maDT` của `/Home/LocDanhSachBN` đổi thành **`maCoSo`** (kèm thêm `tenCoSo`). Đây là ngoại
  lệ có chủ ý của luật "giữ nguyên tên khoá" trong [0010](0010-doi-ten-lan-toi-javascript.md): giá trị
  trả về vốn là mã **cơ sở**, nên tên cũ đang nói dối về nghĩa. Đã kiểm `wwwroot/js/gui-tin-nhan.js`
  chỉ đọc `maBN`/`tenBN`/`sdt` nên không có gì gãy.
- Cùng lý do, tham số lọc của màn `Admin/BenhNhan` đổi `maDT` → `maCoSo` (thân hàm vốn đã lọc theo
  `DM_CSKCB.MaCoSo`).

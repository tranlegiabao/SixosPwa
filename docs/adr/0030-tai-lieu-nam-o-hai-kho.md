# 0030 — Tài liệu nằm ở hai kho, cổng giữ đường chứ không giữ byte

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-09-12
- **Bối cảnh liên quan:** [0005](0005-luu-mat-khau-khong-bam.md) ·
  [0008](0008-moi-duong-ghi-qua-stored-procedure.md) · [0012](0012-anh-luu-tren-ftp-dung-chung.md) ·
  [0017](0017-hai-chieu-theo-loai-du-lieu.md) · [0021](0021-tu-choi-tai-lieu-mo-coi.md) ·
  [0022](0022-khoa-api-bang-rieng-khong-dung-co-active.md)

> Số **0029** đã được mạch *Quét QR màn Đăng nhập* giữ chỗ trước nhưng chưa viết. ADR này lấy 0030
> để không đụng.

## Bối cảnh

Đường đang chạy: HIS gọi *khu API nhận*, gửi PDF dạng Base64; cổng giải mã, đẩy lên **kho ảnh** của
chính mình (`sixospwa/<MaCoSo>/tailieu/<MaBN>/…`) rồi lưu đường dẫn. Đo 12/09 trên `HIS_CSKH`:
**113/113 tài liệu** đều nằm ở kho cổng, 12 cơ sở.

Cách đó bắt cổng ôm bản sao của mọi phiếu — trong khi **bản gốc đã nằm sẵn trên FTP của chính phòng
khám**. Đo trên `Dev_Master3`: `URLKySo` của cả bốn bảng nguồn (`CLS_PhieuXN`, `CLS_PhieuCLS`,
`QL_GiayRaVien`, `QL_ToaThuoc_CT`) có **135/135 đường đúng một khuôn**

```
77121/CongVan/<MaBN>_<TênKhôngDấu>/<MaVaoVien>/<tên tệp>.pdf
```

dài 55–95 ký tự, **không** đường nào tuyệt đối, **không** đường nào mang `ftp://` hay `http`. Đoạn đầu
chính là `MaCSKCB`, bằng đúng `DM_CSKCB.MaCoSo` bên cổng. Đường này do `CaServices` →
`QlLuuTruCongVanServices.cs:122-167` ghi lúc ký số.

Từ đó sinh *chế độ trỏ đường*: HIS ghi thẳng một dòng vào `HIS_CSKH`, không gửi byte nào; cổng giữ
đường và tự kéo tệp lúc bệnh nhân bấm mở.

## Quyết định

**Một tài liệu bên cổng có thể nằm ở một trong hai kho, và dòng dữ liệu phải tự nói ra nó nằm ở kho
nào.**

- Thêm cột **`QL_TaiLieuBenhNhan.NguonKho nvarchar(20) NOT NULL DEFAULT N'CONG'`**, hai giá trị
  `CONG` (kho ảnh của cổng) và `COSO` (*kho phiếu cơ sở*). 113 dòng đang có đổi sang `CONG` bằng một
  câu `UPDATE`.
- **`DuongDanFtp` giữ nguyên văn `URLKySo`.** Không cắt đoạn `<MaCSKCB>` đầu. Nhờ vậy stored bên HIS
  chỉ việc `SELECT URLKySo`, chuỗi trong cổng đối chiếu thẳng được với chuỗi bên HIS, và hàng rào
  đường dẫn kiểm được ngay trên chuỗi.
- Cấu hình kho nằm ở bảng riêng **`HT_KhoFtpCoSo`** (khuôn `HT_KhoaApiCoSo`, ADR 0022 đã bác lối nhét
  bí mật vào `DM_CSKCB`), gồm `Host`, `TaiKhoan`, `MatKhau`, **`ThuMucGoc`** (mặc định rỗng, ghép
  trước đường tài liệu để lo ca tài khoản FTP bị chroot khác gốc) và `Active`. Ghi qua stored theo
  ADR 0008. Sửa ở **khối mới trong màn *Sửa cơ sở*** của khu Quản trị, kèm nút *Thử kết nối kho* —
  **chưa thử đạt thì không bật được** `Active`.
- **Mật khẩu FTP lưu thô**, theo đúng tiền lệ ADR 0005 dành cho bí mật phải phát lại nguyên văn. Tài
  khoản dùng là **chính tài khoản FTP mà HIS đang ghi** (`test`), tức tài khoản **ghi và xoá được**.
- Cổng đọc kho cơ sở bằng **một dịch vụ mới, chỉ-đọc** (`IKhoCoSoService`, đúng một phương thức
  `TaiVeAsync`). `FtpService` 635 dòng đang phục vụ bốn khu ảnh **không bị đụng một dòng nào**.
- **Không đệm ở đâu cả.** Mỗi lượt mở là một phiên FTP mới, đúng khuôn `AnhController` của ADR 0012;
  thêm `Cache-Control: no-store`. Cổng không giữ một byte nào của tài liệu ở chế độ trỏ đường.
- Đặt **timeout 10 giây** cho lời gọi FTP. `FtpService` hiện **không đặt timeout nào** ⇒ mặc định của
  `FtpWebRequest` là **100 giây**.
- Lúc mở hỏng, cổng **tách hai ca**: `550` (tệp không còn) trả **404**; không kết nối được trả
  **502**, và màn nói đúng *"Chưa lấy được tài liệu từ phòng khám — tài liệu vẫn còn nguyên"* kèm nút
  *Thử lại*. Câu cũ ở `tai-lieu-benh-nhan.js:345` — *"Lỗi đọc tài liệu hoặc tệp không tồn tại"* —
  gộp hai ca, đúng loại lỗi im lặng mà thuật ngữ *Chưa hỏi được cơ sở* sinh ra để dẹp.
- Hàng rào đường dẫn ở tầng code: từ chối mọi đường không quy ra được dưới `{MaCoSo}/CongVan/`, khuôn
  `DonAnhService` đang dùng cho kho ảnh.

**Chế độ trỏ đường chỉ phủ tài liệu đã ký số.** Tệp chỉ tồn tại trên FTP khi đi qua đường ký số; tài
liệu chưa ký được HIS dựng tại chỗ (`C0307_SPWA_GuiChoBenhNhanController.cs:787`) nên không có gì để
trỏ tới. Cơ sở chưa ký số vẫn đi *chế độ API* như cũ.

## Vì sao không chọn cách khác

**Suy kho từ tiền tố đường dẫn** (bắt đầu bằng `sixospwa/` là kho cổng) không phải đổi schema và không
phải migration. Bỏ qua vì luật nằm trong chuỗi: ai đổi `KhoAnh.GocFtp`, hoặc một cơ sở tình cờ có thư
mục tên `sixospwa` trên FTP của họ, là cổng đọc nhầm kho mà không báo gì. Một cột hai giá trị rẻ hơn
hẳn một luật ngầm.

**Mã hoá hai chiều mật khẩu FTP, khoá để ở biến môi trường** làm cho việc đọc được cơ sở dữ liệu
không còn đủ để vào FTP khách — đáng giá vì chuỗi kết nối `HIS_CSKH` nằm trong `appsettings.json`
đang được git theo dõi, tức ai có repo là đọc được cơ sở dữ liệu. Bỏ qua theo quyết định của người
dùng ngày 12/09: trong codebase **không có một hàm băm hay mã hoá nào** (ADR 0009, phần đính chính),
nên đây là một khái niệm mới phải dựng và một quy trình đặt biến môi trường phải giữ cho đúng ở mọi
lần deploy. Hệ quả được ghi rõ ở dưới thay vì giấu đi.

**Xin cơ sở một tài khoản FTP riêng, chỉ đọc, giới hạn thư mục** là hàng rào còn lại khi mật khẩu đã
lưu thô. Bỏ qua vì phải đi xin từng cơ sở và không phải máy chủ FTP nào cũng chroot được; người dùng
chốt dùng lại tài khoản `test` đang có.

**Đệm tệp phía máy chủ** làm lần mở thứ hai nhanh và còn xem được khi FTP khách chết. Bỏ qua vì nó
quay lại đúng thứ chế độ trỏ đường sinh ra để tránh — cổng lại ôm bệnh án — và đẻ ngay chùm câu
"đệm sống bao lâu, ai xoá, bản gốc đã sửa thì sao". Đo thật: PDF 232 KB kéo về mất **110–241 ms**, đủ
nhanh để không cần đệm. Đệm là thứ thêm vào sau được mà không phải đổi cơ sở dữ liệu.

**Dán nhãn "tạm chưa xem được" ngay ngoài lưới** để bệnh nhân khỏi chờ. Bỏ qua vì nó đòi cổng biết
kho sống hay chết **trước** lúc vẽ lưới, tức phải có cờ sức khoẻ của kho và người đo định kỳ — đúng
loại việc mà quyết định "không đệm" vừa tránh. Cờ đó đo lúc bấm thì nói dối ngay khi rời màn.

**Thêm overload nhận cấu hình vào `FtpService`**, hoặc factory sinh `FtpService` theo cơ sở, gọn hơn
về số khái niệm. Bỏ qua vì cả hai đều để lại `UploadBytesAsync` / `DeleteFileAsync` / `MoveFileAsync`
gọi được với kho của khách — mà mật khẩu đang lưu là mật khẩu **ghi được**. Dịch vụ chỉ-đọc làm việc
"cổng lỡ ghi vào kho khách" thành bất khả thi về mặt kiểu dữ liệu, không phải bằng kỷ luật người viết.

## Hệ quả

- 🔴 **Đọc được `HIS_CSKH` là cầm được FTP của mọi cơ sở đã cấu hình** — và FTP đó chứa **toàn bộ**
  công văn ký số của phòng khám, kể cả của người chưa bao giờ dùng cổng, chứ không riêng thứ đã lên
  cổng. Tài khoản lại **ghi và xoá được**. Đây là cái giá đã được nêu rõ và người dùng chọn ngày
  12/09. Khi nào đổi ý, đường sửa là mã hoá cột `MatKhau` — không phải đổi schema.
- **Ranh giới của ADR 0021 vẫn đứng, nhưng đứng ở chỗ khác.** Cổng vẫn không ôm bệnh án của người lạ,
  lần này vì nó **không giữ byte nào cả** chứ không phải vì nó từ chối nhận. Việc đảo luật từ chối
  `409 CHUA_CO_NGUOI_NHAN` là của đợt sau và chưa được ADR này chạm tới.
- **Cơ sở muốn dùng chế độ trỏ đường thì bắt buộc có quy trình ký số.** Đo trên `Dev_Master3` ngày
  10/09 cho thấy cơ sở ít ký số thì lưới gần như rỗng: CDHA 12/56.674 · KET_QUA_XN 4/190.628 ·
  GIAY_RA_VIEN 0/1.003. Đây là cơ sở dữ liệu dev không ai ký thật, nhưng con số nói đúng hình dạng
  của rủi ro.
- **FTP của phòng khám chết thì bệnh nhân không xem được, và bên HIS không thấy gì sai** — HIS đã ghi
  dòng thành công từ lâu. Triệu chứng chỉ lộ ra ở phía bệnh nhân. Nút *Thử kết nối kho* chặn được
  cấu hình sai lúc cài, **không** chặn được máy chủ chết sau đó.
- **Tên tệp có dấu tiếng Việt và khoảng trắng chạy được**, đã đo bằng đúng lớp `FtpWebRequest` mà cổng
  dùng: `…/Phiếu kết quả XN_326053-326054.pdf` tải về 232.473 byte, 241 ms nguội / 110 ms nóng. Không
  cần mã hoá tên đường thêm.
- **Bẫy đã gỡ:** ghi chú cũ dặn coi chừng `CommonServices.boDauChuoiVaKhoangTrang` trả TitleCase
  (`NGUYỄN THỊ KIM QUỲNH` → `NguyenThiKimQuynh`) **hết hiệu lực** — cổng không dựng đường nữa, cổng
  chép lại đường HIS đã ghi.
- `DuongDanFtp` khai `nvarchar(1000)` trong cơ sở dữ liệu nhưng `ApplicationDbContext.cs:241` và tham
  số stored đều chặn ở **500**. Đường dài nhất đo được là 95 ký tự nên chưa cắn, nhưng ba con số này
  đang lệch nhau và nên được thống nhất.

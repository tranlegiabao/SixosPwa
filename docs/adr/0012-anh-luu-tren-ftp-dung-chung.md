# 0012 — Ảnh lưu trên FTP dùng chung, đọc lại qua route proxy

- **Tác giả:** Nam · **Ngày:** 2026-08-25 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0007](0007-moi-truong-thu-that-qua-cloudflare-tunnel.md) ·
  [0008](0008-moi-duong-ghi-qua-stored-procedure.md)

## Bối cảnh

Mọi ảnh của SixosPwa — logo cơ sở, ảnh quảng cáo, ảnh nhúng trong bài viết — được ghi thẳng vào
`wwwroot/static/…` trên đĩa của máy đang chạy. Bốn thư mục: `logo_cs`, `img_qc_kcb`, `img_cs`,
`img_nd`.

Cách đó hỏng ở hai chỗ, cả hai đều **im lặng**:

1. **Deploy là mất ảnh.** Đo lúc quyết định: 24 tệp ảnh nằm trong `wwwroot/static`, **18 tệp không hề
   được git theo dõi**. Đổi máy hay dựng lại từ repo là mất trắng, trong khi DB vẫn trỏ tới chúng ⇒ thẻ
   `<img>` vỡ trên trang khách hàng mà không ai báo lỗi. Vết còn lại trong DB chứng minh điều này đã
   xảy ra rồi: `DM_CSKCB.ID = 7` và `DM_CSKCB_QuangCao.ID = 4` trỏ tới hai tệp **không còn tồn tại**.
2. **Kho phình mãi.** Thay logo thì tệp cũ nằm lại vĩnh viễn, không có đường nào dọn.

HisSoft (`master_3`) đã giải bài này từ lâu bằng một máy chủ FTP dùng chung ở `118.69.34.247`, kèm sẵn
một dịch vụ (`S0401_FtpService`) và một lối phục vụ ảnh (`HomeController.cs:123` — route `/HinhCLS/…`
tải bytes từ FTP rồi trả về).

## Quyết định

**Kho ảnh là FTP `118.69.34.247`, dưới đúng một thư mục gốc `/sixospwa/`**, bên trong giữ nguyên bốn
tên thư mục cũ. Cột DB không còn giữ đường dẫn tệp tĩnh mà giữ **đường đọc ảnh** `/anh/<thư mục>/<tên>`,
do `AnhController` phục vụ: mỗi lượt xin ảnh là **một phiên FTP mới**, không đệm.

Dịch vụ FTP **chép nguyên bản** `S0401_FtpService` của master_3 — đủ tám phương thức, và **giữ tên tệp
gốc** người dùng tải lên (trùng thì thêm `(1)`, `(2)`), thay vì lối đặt tên GUID mà SixosPwa đang dùng.

Khi ảnh bị thay hoặc bị gỡ khỏi bài viết, tệp cũ bị xoá trên FTP — nhưng chỉ sau ba hàng rào, xem
`DonAnhService`.

## Vì sao không chọn cách khác

**Đệm ra đĩa** (FTP là bản chính, `wwwroot` là bản đệm đọc-xuyên-qua) nhanh hơn hẳn và tên tệp là bất
biến nên không bao giờ cũ. Bỏ qua vì phải cân giữa tốc độ và số lượng thứ phải hiểu khi đọc code, và
lối proxy thuần **giống hệt** cái master_3 đang chạy — người quen HisSoft đọc là hiểu ngay. Đệm là thứ
thêm vào sau được mà không phải đổi gì trong DB.

**Giữ `wwwroot` làm nơi phục vụ, FTP chỉ là bản sao** thì nhanh nhất nhưng không giải quyết được gì:
ảnh vẫn mất khi deploy, và sinh ra hai nguồn sự thật lệch nhau mà không ai biết.

**Đặt tên tệp bằng GUID** an toàn hơn cho URL. Bỏ qua để bám sát bản master_3. Rủi ro tên tiếng Việt
có dấu đã **đo thật** trước khi chốt: máy chủ là Microsoft IIS FTP có công bố `UTF8`; tệp
`ảnh thử này.jpg` tải lên được, `LIST` hiện đúng dấu, đọc qua `/anh/…` trả 200 đúng số byte, xoá được,
đọc lại 404.

## Hệ quả

- **Đọc ảnh chậm hơn phục vụ tĩnh khoảng 150 lần.** Đo thật: một ảnh qua proxy **0,36–0,40 s** (lượt
  đầu nguội 1,94 s); chín logo song song **2,85 s**; cùng tệp đó phục vụ tĩnh là **0,002–0,006 s**.
  Đây là cái giá đã biết trước và chấp nhận. Khi nào thấy trang danh sách cơ sở chậm quá thì thêm tầng
  đệm — không phải đổi DB.
- **FTP chết thì ảnh 404, nhưng phần chữ vẫn sửa được.** Tải ảnh lên thất bại **không chặn** việc Lưu:
  cột ảnh giữ nguyên giá trị cũ và người dùng nhận một câu cảnh báo kèm thông báo thành công. Cách này
  cố ý khác với lỗi nhập liệu (sai kích thước, sai đuôi tệp) — những lỗi đó vẫn chặn Lưu như cũ.
- **Kho FTP dùng chung với HisSoft**, gốc của nó đang có ~70 mục (`ttpt_images`, `HinhAnh`, `79423`…).
  Vì vậy `DonAnhService` chỉ được xoá thứ quy ra được đường dẫn dưới `/sixospwa/` và thuộc đúng bốn thư
  mục — mọi thứ khác, kể cả link `http(s)` do admin dán vào, không thể chạm tới.
- **Xoá chỉ chạy SAU khi thủ tục lưu thành công.** Nếu xoá sớm thì lúc thủ tục lưu hỏng sẽ mất ảnh
  trong khi DB vẫn trỏ tới nó. Nhờ thứ tự này mà phép đo "còn ai dùng ảnh này không" khỏi phải trừ dòng
  vừa lưu — dòng đó trong DB đã mang giá trị mới.
- **Tài khoản FTP `test`/`test` nằm trong `appsettings.json`**, là tệp git đang theo dõi. Không làm tình
  hình xấu thêm (tệp này đã chứa mật khẩu `HIS_CSKH` thật và khoá riêng VAPID từ 06/08), nhưng đây vẫn
  là một mật khẩu nữa vào git.
- **Dùng `FtpWebRequest`** kéo theo cảnh báo biên dịch `SYSLIB0014` (lớp này đã bị đánh dấu lỗi thời từ
  .NET 6). Chấp nhận, vì đó là cái giá của việc chép nguyên bản master_3.
- Đường dẫn cũ trong DB được đổi một lần bằng `M02_doi_duong_dan_anh_sang_ftp.sql`; sau đó `/static/…`
  không còn ý nghĩa với ảnh. `ValidateImageUrl` vẫn chấp nhận tiền tố cũ để dữ liệu chưa đổi không bị
  chặn giữa chừng.

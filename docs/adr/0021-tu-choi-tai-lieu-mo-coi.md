# 0021 — Từ chối tài liệu mồ côi, bù bằng đường hỏi lại theo lô

- **Tác giả:** Nam · **Ngày:** 2026-09-08 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0008](0008-moi-duong-ghi-qua-stored-procedure.md) ·
  [0012](0012-anh-luu-tren-ftp-dung-chung.md) · [0017](0017-hai-chieu-theo-loai-du-lieu.md) ·
  [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) · [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md)

## Bối cảnh

HIS của cơ sở đẩy kết quả cận lâm sàng và đơn thuốc lên cổng, khoá theo *Mã BN* mà cơ sở đó cấp. Nhưng
bệnh nhân gần như luôn cài ứng dụng **sau** khi đã đi khám. Nên ở thời điểm HIS đẩy, phần lớn *Mã BN*
chưa ứng với hồ sơ nào bên cổng.

Bản đầu tiên dựng xong ngày 08/09 chọn hướng **nhận tuốt**: lưu bản ghi với hồ sơ để trống, và tự đoán
chủ nhân bằng CCCD rồi bằng số điện thoại, thậm chí tự tạo bản ghi con người mới từ chính gói tin. Ba
việc đó đều đi ngược ADR 0018: Thiên Nam có **345 nhóm cùng CCCD khác tên**, và có **một số điện thoại
gắn tới 876 người**. Đoán chủ nhân bằng những ô đó là con đường đưa bệnh án người này cho người kia.

Bỏ phần đoán đi thì còn lại câu hỏi thật: tài liệu của *Mã BN* chưa ai nhận thì cổng làm gì.

## Quyết định

**Cổng từ chối. Không lưu bản ghi, không đẩy tệp lên kho FTP.** Trả `409` kèm mã máy đọc được
`CHUA_CO_NGUOI_NHAN` — tách bạch với lỗi kỹ thuật, để hàng đợi bên HIS không phải đọc câu tiếng Việt mà
đoán. Luật này được chặn ở **cả hai tầng**: thủ tục `QL_TaiLieuBenhNhan_Save` trả `ResultCode = 5` khi hồ
sơ trống hoặc không thuộc cơ sở, nên nó đúng ngay cả khi tầng C# chưa kịp vá.

Cổng **không giữ một byte bệnh án nào của người chưa phải người dùng của nó**. Việc nối hồ sơ là của
người dùng ở màn *Nối hồ sơ*, theo luật gộp ba ô — không phải của đường API.

Cái giá của quyết định này là bộ đếm tồn bên HIS sẽ không tự về 0, và một bộ đếm không bao giờ về 0 thì
người ở quầy sẽ học cách phớt lờ nó — đúng cái bệnh mà *hàng đợi gửi* muốn tránh. Nên nó **đi kèm một
đường bù bắt buộc**: `POST /api/v1/ho-so/kiem-tra-nhan`, HIS gửi lô *Mã BN* đang tồn và nhận về những mã
đã có người nhận. Nhờ đó màn *Gửi cho bệnh nhân* tách được hai con số — **chờ người nhận** và **gửi được
ngay** — và con số thứ hai về 0 được. Không có đường bù này thì quyết định từ chối không đứng vững.

## Vì sao không chọn cách khác

**Nhận và ký gửi, có hạn giữ** là phương án được cân nhắc kỹ nhất, và nó có lợi thế thật: HIS đẩy đúng
một lần, không cần biết ai đã đăng ký, và tài liệu tự gắn vào hồ sơ ngay khi bệnh nhân nối. Không phải
dựng đường hỏi lại, cũng không ai phải bấm gửi lần hai. Bỏ qua vì nó buộc cổng phải giữ tệp PDF bệnh án
của những người **chưa hề là người dùng của nó** và chưa đồng ý gì cả — kể cả khi có hạn dọn 90 ngày, thì
trong 90 ngày đó cổng vẫn là một kho bệnh án của người ngoài. Ranh giới "cổng chỉ giữ dữ liệu của người
dùng của mình" đáng giá hơn sự tiện của một lần đẩy.

**Nhận vô hạn như bản đầu tiên, chỉ bỏ phần đoán chủ nhân** là ít việc nhất và đỡ đụng vào code đang chạy
nhất. Bỏ qua vì nó gộp cả hai điểm yếu: cổng vẫn tích dần bệnh án của người không bao giờ dùng cổng, mà
bên HIS lại **không phân biệt được "đã tới nơi" với "đã tới tay người bệnh"** — không có trạng thái nào
nói lên điều đó.

**Cổng gọi ngược vào HIS báo khi có người nối hồ sơ** thay cho đường hỏi lại theo lô. Bỏ qua vì nó đẻ
thêm một đường ghi từ ngoài vào HIS — loại việc rủi ro cao mà bản đồ tổng thể cố ý đẩy xuống đợt sau — và
vì cơ sở nào có đường hầm rớt đúng lúc đó là mất tín hiệu vĩnh viễn, không có gì bù lại. Kéo thì bên hỏi
tự chịu trách nhiệm hỏi lại; đẩy thì mất là mất.

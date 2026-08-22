# SixosPwaTemplate

Khuôn mẫu web cài đặt được của Sixos: một phần mềm ASP.NET Core tối giản mà khách hàng mở bằng đường
link, rồi tự cài thành biểu tượng trên điện thoại hoặc máy tính. Không chứa nghiệp vụ nào — tồn tại để
được nhân bản làm điểm khởi đầu cho các phần mềm sau.

## Language

**Khuôn mẫu**:
Chính project `SixosPwa` trong thư mục này. Nó không phải sản phẩm giao khách; nó là bản gốc để sao ra.
_Tránh_: bản demo, bản mẫu, POC

**Bản nhân**:
Một phần mềm thật được sao ra từ khuôn mẫu rồi phát triển tiếp. Mỗi bản nhân có tên, tên miền và vòng
đời riêng, không đồng bộ ngược về khuôn mẫu.
_Tránh_: bản sao, fork, clone

**Cài đặt**:
Việc khách bấm nút để đưa phần mềm thành biểu tượng ngoài màn hình chính. Không có gì được tải về máy
theo nghĩa tệp cài đặt — đây vẫn là web.
_Tránh_: tải app, tải phần mềm, download

**Chế độ ứng dụng**:
Trạng thái phần mềm mở ra từ biểu tượng đã cài: chiếm trọn cửa sổ, không có thanh địa chỉ. Trạng thái
còn lại là *chế độ trình duyệt*.
_Tránh_: standalone, fullscreen, toàn màn hình

**Nút cài thông minh**:
Nút "Cài HisSoft vào máy" dưới form đăng nhập. Gọi là thông minh vì trên Android và máy tính nó cài trực
tiếp, còn trên iPhone nó mở bảng hướng dẫn — do Apple không cho website tự gọi cài đặt.
_Tránh_: nút tải, nút download

**Trang mất kết nối**:
Trang tĩnh duy nhất được giữ sẵn trong máy khách, hiện lên thay cho trang lỗi của trình duyệt khi rớt
mạng. Đây là ngoại lệ duy nhất của nguyên tắc không cache.
_Tránh_: trang offline, trang lỗi

**Đường hầm tạm**:
Địa chỉ HTTPS công khai tạm thời (Dev Tunnels, ngrok) trỏ về máy đang chạy, dùng để thử cài đặt trên
điện thoại thật. Địa chỉ đổi mỗi lần chạy nên không giao cho khách.
_Tránh_: tunnel, ngrok link

### Cổng bệnh nhân (chốt 2026-08-22)

> Lưu ý: phần **Khuôn mẫu** ở trên mô tả bản gốc chưa có nghiệp vụ. Nhánh `19_Bao-Hieu` đã là một
> **bản nhân** thật, mang nghiệp vụ cổng bệnh nhân mô tả dưới đây.

**Cơ sở**:
Một dòng `DMCSKCB` — một địa điểm khám chữa bệnh có trang giới thiệu và URL cố định riêng.
_Tránh_: chi nhánh, phòng khám, bệnh viện

**Đối tác**:
Một dòng `DMDoiTac`. **Không** phải cơ sở, và hiện **không có cột nào nối** hai bảng với nhau. Mọi thứ
trong cổng bệnh nhân khoá theo *cơ sở*, không theo đối tác.
_Tránh_: khách hàng, tenant

**Chi nhánh**:
Khái niệm **của hệ đối tác** (claim `IdDT` bên Ung Bướu), không tồn tại trong SixosPwa. SixosPwa không
bao giờ hỏi chi nhánh — bên đối tác tự hỏi sau khi bàn giao.
_Tránh_: cơ sở (dễ lẫn), branch

**Slug**:
Đoạn chữ do quản trị viên đặt tay trong `DMCSKCB.Slug`, làm nên URL cố định `/pk/{slug}` của cơ sở.
Không tự sinh từ tên, nên đổi tên cơ sở không gãy URL.
_Tránh_: đường dẫn, alias, permalink

**Mã CSKCB**:
`DMCSKCB.MaCoSo` (`CS1`…`cs6`). Là khoá kỹ thuật nối cơ sở với bảng đăng ký API và với hồ sơ bệnh nhân;
hiện ở màn đăng nhập dưới dạng **chỉ để xem**.
_Tránh_: mã đối tác, mã phòng khám

**Bàn giao**:
Việc chuyển phiên đã xác thực của bệnh nhân sang trang web của đối tác, bằng cách để **chính hệ đối
tác đặt cookie của họ**. Không phải chuyển hướng suông, và cũng không phải đăng nhập lại.
_Tránh_: redirect, SSO, chuyển trang

**Cửa đối tác**:
Lớp `IPartnerGateway` — chỗ duy nhất trong SixosPwa biết một hệ đối tác cụ thể nói chuyện thế nào.
Màn hình gọi giao diện này chứ không gọi thẳng API của ai.
_Tránh_: adapter, connector, tích hợp

**Nhánh có API** / **nhánh nội bộ**:
Hai đường đi sau khi xác thực, chọn theo `DM_DoiTacApi.KieuApi`. *Có API* thì bàn giao sang đối tác;
*nội bộ* thì ở lại trang bệnh nhân của SixosPwa.
_Tránh_: mode, loại cơ sở

**Liên kết một lần**:
Màn chỉ hiện đúng một lần cho bệnh nhân đã có sẵn tài khoản bên đối tác mà SixosPwa chưa biết mật
khẩu. Sau khi qua, những lần sau đi thẳng.
_Tránh_: kết nối, đồng bộ tài khoản

**Trang bệnh nhân nội bộ**:
Trang chủ dành cho bệnh nhân của cơ sở **không có** API riêng. Đợt 2026-08 mới chỉ có giao diện.
_Tránh_: dashboard, trang chủ (chung chung)

## Quyết định

Xem [`docs/adr/`](docs/adr/). Hai quyết định định hình khuôn mẫu này:

- [0001](docs/adr/0001-chon-net7-du-het-ho-tro.md) — vì sao nền là .NET 7 dù đã hết hỗ trợ.
- [0002](docs/adr/0002-service-worker-khong-cache.md) — vì sao service worker cố ý không cache gì.
- [0003](docs/adr/0003-vao-ub-qua-cua-an-danh.md) — vì sao bàn giao bằng cửa ẩn danh sẵn có của đối tác thay vì mở SSO.
- [0004](docs/adr/0004-ban-giao-thay-vi-dung-lai-man.md) — vì sao không dựng lại màn của đối tác trong PWA.
- [0005](docs/adr/0005-luu-mat-khau-khong-bam.md) — vì sao mật khẩu lưu đọc lại được thay vì băm.

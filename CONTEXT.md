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

### Xác thực & vé bàn giao (chốt 2026-08-25)

**OTP**:
Mã 6 chữ số do **chính SixosPwa** sinh và giữ (`IMemoryCache`, khoá `OTP_{sđt}`, hạn 5 phút) để xác
thực **danh tính bệnh nhân**. Đây là trục xác thực duy nhất của cổng bệnh nhân. Hiện còn kê tạm một
giá trị cố định lúc phát triển.
_Tránh_: mã xác nhận, mã đối tác

**Vé bàn giao**:
Trường `code` do hệ đối tác trả về trong thân phản hồi lúc mở tài khoản, cất ở
`HT_TaiKhoanDoiTac.MaXacNhanTam`, dùng **đúng một lần** để POST kèm lúc bàn giao cho bên họ đặt cookie.
Bệnh nhân **không bao giờ nhìn thấy và không bao giờ gõ** nó — họ đã qua *OTP* từ trước. 🔴 Nó **không
phải** một bước xác thực danh tính; xếp nhầm nó thành "OTP thay thế" từng dẫn tới một kết luận sai về
lỗ hổng (2026-08-24).
_Tránh_: OTP của đối tác, mã xác thực, mã xác nhận trần

**Kênh**:
Tham số `xacthuc` gửi kèm khi nhờ đối tác mở tài khoản, quyết định họ gửi *Vé bàn giao* đi đường nào:
1 = Zalo, 2 = Email, 3 = SMS, 4 = **chỉ sinh vé, không gửi gì**. Cổng bệnh nhân luôn dùng kênh 4 —
bệnh nhân không cần nhận vé, nên gửi tin là vừa tốn tiền vừa làm họ hoang mang.
_Tránh_: phương thức xác thực, hình thức gửi OTP

### Nền dữ liệu (chốt 2026-08-24)

**Con người**:
Một dòng `DM_BenhNhan`, định danh bằng **CCCD** — khoá tự nhiên, tồn tại đúng một nơi trong cả cơ sở dữ
liệu. Một con người có thể khám ở nhiều cơ sở nhưng vẫn chỉ là một dòng.
_Tránh_: bệnh nhân (mơ hồ — xem *Hồ sơ tại cơ sở*), user, tài khoản

**Hồ sơ tại cơ sở**:
Một dòng `DM_BenhNhanCoSo` — việc một *con người* có mặt tại một *cơ sở*, mang mã bệnh nhân do chính cơ
sở đó cấp. Cùng một người ở hai cơ sở là **hai hồ sơ, một con người**. Thực đo 2026-08-24: 13 con người
ứng với 15 hồ sơ.
_Tránh_: bệnh nhân, bản ghi BN

**Mã BN**:
`DM_BenhNhanCoSo.MaBN` — do **từng cơ sở** cấp, nên chỉ duy nhất *trong phạm vi một cơ sở*. Hai cơ sở
khác nhau hoàn toàn có thể cấp trùng một chuỗi. Không bao giờ dùng `MaBN` trần làm khoá nối.
_Tránh_: mã bệnh nhân toàn hệ, ID bệnh nhân

**Mật khẩu nội bộ**:
`HT_TaiKhoan.MatKhauNoiBo` — để Admin và Đối tác đăng nhập vào SixosPwa. Bệnh nhân không dùng cột này
(họ đi bằng OTP). **Hiện CHƯA băm** — phần băm đã hoãn, xem mục Đính chính của ADR 0009; cột này đang
được đối chiếu bằng chuỗi thường.
_Tránh_: mật khẩu (chung chung — dễ lẫn với *Mật khẩu đối tác*)

**Mật khẩu đối tác**:
`HT_TaiKhoanDoiTac.MatKhau` — do máy sinh, dùng để POST nguyên văn sang hệ đối tác lúc bàn giao. **Cố ý
không băm**, bắt buộc theo ADR 0005. Không bao giờ dùng để đăng nhập vào SixosPwa.
_Tránh_: mật khẩu, mật khẩu UB

**Đối tác**:
Một dòng `DM_DoiTac` — một **tổ chức**, mang `BrandName` để gửi SMS. Từ 2026-08-25 một đối tác quản
nhiều *Cơ sở* qua `DM_CSKCB.IDDoiTac` (ADR 0011). 🔴 Đừng lẫn với chữ "đối tác" trong tên
`DM_DoiTacApi` và `HT_TaiKhoanDoiTac`: ở hai bảng đó nó khoá theo `IDCoSo`, tức là nói về một **Cơ sở**
chứ không phải tổ chức.
_Tránh_: dùng "đối tác" trần khi đang nói về `DM_DoiTacApi`/`HT_TaiKhoanDoiTac` — ở đó phải nói *Cơ sở*

**Cơ sở đang hiển thị**:
Cơ sở có `DM_CSKCB.Active = 1`. Đây là cổng **duy nhất** quyết định hai việc: cơ sở có lên cổng công
khai không, và cơ sở có nhận **đăng nhập / đăng ký mới** không. Ẩn một cơ sở là thôi quảng bá và thôi
nhận người mới — **không phải khoá tài khoản**: bệnh nhân đã đăng nhập vẫn dùng bình thường. Cột
`XacMinh` từng giữ vai này nhưng chưa bao giờ được đọc, đã xoá hẳn ở đợt 2026-08-26 (ADR 0013).
_Tránh_: đã xác minh, kích hoạt, đã duyệt, bật/tắt

**Cờ `Active`**:
🔴 Có **ba** cột mang tên này với **ba nghĩa khác nhau**, đừng lẫn. `DM_CSKCB.Active` là *Cơ sở đang
hiển thị* ở trên. `DM_DoiTacApi.Active` nói đăng ký API của một cơ sở còn hiệu lực không.
`DM_CSKCB_CapQuangCao.Active` nói một cấp quảng cáo còn hiệu lực không. Chỉ cái đầu là cổng hiển thị.
_Tránh_: nói "cờ Active" trần khi chưa nói rõ bảng nào

**Phòng khám**:
**Không tồn tại.** Bảng `PhongKham` từng có 3 dòng dữ liệu bịa, đã bỏ ở đợt 2026-08-24. Mọi "nơi khám"
đều là *Cơ sở*.
_Tránh_: dùng lại từ này dưới bất kỳ dạng nào

## Quyết định

Xem [`docs/adr/`](docs/adr/). Hai quyết định định hình khuôn mẫu này:

**Kho ảnh**:
Máy chủ FTP dùng chung với HisSoft, nơi đặt mọi ảnh của phần mềm. Không phải ổ đĩa của máy đang chạy —
đĩa máy chạy chỉ là chỗ tạm, mất khi dựng lại.
_Tránh_: thư mục ảnh, wwwroot, ổ đĩa

**Đường đọc ảnh**:
Địa chỉ mà trình duyệt dùng để xin một tấm ảnh trong *kho ảnh*. Đây là địa chỉ được lưu trong cơ sở dữ
liệu, không phải vị trí thật của tệp.
_Tránh_: link ảnh, url ảnh, đường dẫn tệp

**Ảnh mồ côi**:
Tấm ảnh còn nằm trong *kho ảnh* nhưng không còn chỗ nào trỏ tới. Là rác — chiếm chỗ và không ai đọc.
_Tránh_: ảnh thừa, ảnh rác, file cũ

- [0001](docs/adr/0001-chon-net7-du-het-ho-tro.md) — vì sao nền là .NET 7 dù đã hết hỗ trợ.
- [0002](docs/adr/0002-service-worker-khong-cache.md) — vì sao service worker cố ý không cache gì.
- [0003](docs/adr/0003-vao-ub-qua-cua-an-danh.md) — vì sao bàn giao bằng cửa ẩn danh sẵn có của đối tác thay vì mở SSO.
- [0004](docs/adr/0004-ban-giao-thay-vi-dung-lai-man.md) — vì sao không dựng lại màn của đối tác trong PWA.
- [0005](docs/adr/0005-luu-mat-khau-khong-bam.md) — vì sao mật khẩu lưu đọc lại được thay vì băm.
- [0006](docs/adr/0006-chan-dang-nhap-cheo-co-so.md) — vì sao chặn phiên đang đứng ở cơ sở khác ngay tại lối vào.
- [0007](docs/adr/0007-moi-truong-thu-that-qua-cloudflare-tunnel.md) — vì sao môi trường thử đi qua Cloudflare tunnel.
- [0008](docs/adr/0008-moi-duong-ghi-qua-stored-procedure.md) — vì sao mọi đường ghi đi qua stored procedure.
- [0009](docs/adr/0009-bam-mat-khau-noi-bo-tach-khoi-mat-khau-doi-tac.md) — vì sao tách mật khẩu nội bộ khỏi mật khẩu đối tác.
- [0010](docs/adr/0010-doi-ten-lan-toi-javascript.md) — vì sao đổi tên lan tới tận JavaScript dù ràng buộc ban đầu cấm.
- [0011](docs/adr/0011-co-so-thuoc-doi-tac-mot-nhieu.md) — vì sao cơ sở thuộc đối tác theo quan hệ một–nhiều thay vì bảng nối.
- [0012](docs/adr/0012-anh-luu-tren-ftp-dung-chung.md) — vì sao ảnh lưu trên FTP dùng chung và đọc lại qua route proxy.
- [0013](docs/adr/0013-active-la-cong-hien-thi-duy-nhat.md) — vì sao `Active` là cổng hiển thị duy nhất và `XacMinh` bị xoá.

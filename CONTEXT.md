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
Địa chỉ HTTPS công khai tạm thời (Dev Tunnels, ngrok, Cloudflare) trỏ về máy đang chạy, dùng để thử
cài đặt trên điện thoại thật. Địa chỉ đổi mỗi lần chạy nên không giao cho khách. 🔴 Cũng vì đổi mỗi
lần chạy, nó **không được nằm trong dữ liệu**: `DM_DoiTacApi.TrangChu` của cơ sở CS1 hiện vẫn đang
giữ một địa chỉ đường hầm, nên cứ dựng lại đường hầm là cơ sở đó chết tới khi có người sửa tay.
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

**Nhánh bàn giao** / **nhánh màn chung**:
Hai đường đi sau khi xác thực, chọn theo `DM_DoiTacApi.KieuApi`. *Bàn giao* thì chuyển phiên sang màn
của đối tác (`KieuApi = UB`); *màn chung* thì ở lại trang bệnh nhân của SixosPwa (`NONE`, `HIS`).
🔴 Cặp từ này trước 2026-09-08 gọi là *"nhánh có API" / "nhánh nội bộ"* — **đã bỏ**, vì từ giai đoạn 2
một *Cơ sở dùng HIS* **có API mà vẫn ở lại màn chung**. Nói "nhánh có API" trần từ nay là mơ hồ.
_Tránh_: nhánh có API, nhánh nội bộ, mode, loại cơ sở

**Liên kết một lần**:
Màn chỉ hiện đúng một lần cho bệnh nhân đã có sẵn tài khoản bên đối tác mà SixosPwa chưa biết mật
khẩu. Sau khi qua, những lần sau đi thẳng. 🔴 **Không phải** *Nối hồ sơ* — cái này nói về **mật khẩu
tài khoản đối tác** (`HT_TaiKhoanDoiTac.DaLienKet`), cái kia nói về **hồ sơ khám ở HIS**. Hai màn
khác nhau; ADR 0018 từng lỡ dùng chung tên này cho cả hai.
_Tránh_: kết nối, đồng bộ tài khoản, nối hồ sơ

**Trang bệnh nhân nội bộ**:
Trang chủ dành cho bệnh nhân của cơ sở đi theo *nhánh màn chung*. Đợt 2026-08 mới chỉ có giao diện;
giai đoạn 2 đổ dữ liệu thật vào. Ba ô: *Đăng ký khám theo gói* (ẩn tới giai đoạn 3) · *Lịch sử hẹn
khám* · *Tra cứu hồ sơ khám bệnh*.
_Tránh_: dashboard, trang chủ (chung chung)

### Xác thực & vé bàn giao (chốt 2026-08-25)

**OTP**:
Mã 6 chữ số do **chính SixosPwa** sinh và giữ (`IMemoryCache`, khoá `OTP_{sđt}`, hạn 5 phút) để xác
thực **danh tính bệnh nhân**. Là trục xác thực của cổng bệnh nhân **ở những cơ sở dùng màn của chính
SixosPwa**. 🔴 Cơ sở đi theo ADR 0014 (hiện là Ung Bướu) **không đi qua OTP này một bước nào** — bên
đó chính đối tác xác thực bệnh nhân. Hiện còn kê tạm một giá trị cố định lúc phát triển.
_Tránh_: mã xác nhận, mã đối tác

**Vé bàn giao**:
Trường `code` do hệ đối tác trả về trong thân phản hồi lúc mở tài khoản, cất ở
`HT_TaiKhoanDoiTac.MaXacNhanTam`, dùng **đúng một lần** để POST kèm lúc bàn giao cho bên họ đặt cookie.
🔴 Nó **không phải** một bước xác thực danh tính; xếp nhầm nó thành "OTP thay thế" từng dẫn tới một
kết luận sai về lỗ hổng (2026-08-24). Ai nhìn thấy vé thì **tuỳ đường đi**: ở cơ sở dùng màn của chính
SixosPwa, bệnh nhân không bao giờ thấy nó (họ đã qua *OTP*); ở cơ sở đi theo ADR 0014, chính vé này
được gửi tới điện thoại bệnh nhân và **họ tự gõ vào**.
_Tránh_: OTP của đối tác, mã xác thực, mã xác nhận trần

**Kênh**:
Tham số `xacthuc` gửi kèm khi nhờ đối tác mở tài khoản, quyết định họ gửi *Vé bàn giao* đi đường nào:
1 = Zalo, 2 = Email, 3 = SMS, 4 = **chỉ sinh vé, không gửi gì**. 🔴 Chốt hiện hành theo ADR 0014 là
**kênh 3 — SMS thật, bệnh nhân tự gõ vé**, và bệnh nhân được chọn giữa SMS với Zalo. Kênh 4 từng là
chốt cũ nhưng **không còn được gọi tới**; kênh 2 đóng vì nhánh Email bên đối tác hỏng sẵn. Cái giá đã
biết của chốt hiện hành: **mỗi lần đăng ký là một tin nhắn thật**.
_Tránh_: phương thức xác thực, hình thức gửi OTP

### Nền dữ liệu (chốt 2026-08-24)

**Con người**:
Một dòng `DM_BenhNhan`, định danh bằng **CCCD** — khoá tự nhiên, tồn tại đúng một nơi trong cả cơ sở dữ
liệu. Một con người có thể khám ở nhiều cơ sở nhưng vẫn chỉ là một dòng. 🔴 Từ 2026-09-08 một con người
còn **thuộc về đúng một *Tài khoản cổng*** (`DM_BenhNhan.IDTaiKhoan`) — nên *ai khai trước giữ CCCD*:
con khai hộ mẹ rồi thì mẹ tự đăng ký sẽ **bị chặn** cho tới khi con xoá hồ sơ đó. Xem ADR 0019.
_Tránh_: bệnh nhân (mơ hồ — xem *Hồ sơ tại cơ sở*), user, tài khoản

**Hồ sơ tại cơ sở**:
Một dòng `DM_BenhNhanCoSo` — việc một *con người* mang một mã bệnh nhân do một *cơ sở* cấp. Cùng một
người ở hai cơ sở là **hai hồ sơ, một con người**. Thực đo 2026-08-24: 13 con người ứng với 15 hồ sơ.
🔴 **Một người tại MỘT cơ sở vẫn có thể có NHIỀU hồ sơ** — bản trước 2026-09-08 ngầm hiểu là một, và
điều đó **sai**. Đo trên DB khách thật: `PKDK_ThienNam` **17,3%** người có ≥2 `MaBN` (8.454 người),
`PKDK_TamDuc_BL` 24,7%, `Chinh_PKDKNhanDuc` 12,7%. Vì vậy mọi màn hiển thị phải gom theo *Con người*,
không theo *Hồ sơ tại cơ sở*; và ràng buộc duy nhất đúng là `(IDCoSo, MaBN)`, không phải
`(IDBenhNhan, IDCoSo)`. Xem ADR 0018.
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
`HT_TaiKhoanDoiTac.MatKhau` — dùng để POST nguyên văn sang hệ đối tác lúc bàn giao. **Cố ý không băm**,
bắt buộc theo ADR 0005. Không bao giờ dùng để đăng nhập vào SixosPwa. 🔴 **Ai đặt ra nó thì tuỳ cơ sở,
đừng nói "do máy sinh"**: ở cơ sở đi theo ADR 0014 (hiện là Ung Bướu) cột này giữ **mật khẩu THẬT do
chính bệnh nhân gõ**; `SinhMatKhauChoDoiTac()` chỉ còn chạy ở nhánh đối tác-tương-lai, mà **hiện không
cơ sở nào đi qua nhánh đó**. Khác biệt này quyết định mức thiệt hại: mật khẩu máy sinh mà lộ thì mất
đúng một tài khoản, còn mật khẩu bệnh nhân tự đặt mà lộ thì mất cả những nơi họ dùng lại nó — đó là lý
do *Bàn giao* phải đòi *Phiên đã được cơ sở xác thực* (ADR 0016).
_Tránh_: mật khẩu, mật khẩu UB, "mật khẩu máy sinh"

**Phiên đã được cơ sở xác thực**:
Phiên SixosPwa mang claim `DoiTacXacThuc` — đóng khi và chỉ khi **chính đối tác** vừa phán mật khẩu thật
hoặc mã xác thực của họ là đúng. Phân biệt hẳn với **phiên đã đăng nhập** (chỉ cần qua `[Authorize]`,
và đúc được từ đường OTP). Chỉ phiên loại này mới được *Bàn giao* — vì bàn giao là đưa ra mật khẩu thật
của bệnh nhân. Xem ADR 0016.
_Tránh_: phiên hợp lệ, đã đăng nhập, đã xác thực (trần), authenticated

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
🔴 Có **bốn** cột mang tên này với **bốn nghĩa khác nhau**, đừng lẫn. `DM_CSKCB.Active` là *Cơ sở đang
hiển thị* ở trên. `DM_DoiTacApi.Active` nói đăng ký API của một cơ sở còn hiệu lực không.
`DM_CSKCB_CapQuangCao.Active` nói một cấp quảng cáo còn hiệu lực không. `HT_KhoaApiCoSo.Active` là
*Khoá cơ sở* còn dùng được không. Chỉ cái đầu là cổng hiển thị.
🔴 `DM_CSKCB.Active` **không** phải cổng đường API. Theo ADR 0013 nó quyết định đúng hai thứ: cơ sở có
hiện ở cổng công khai, và có nhận đăng nhập/đăng ký mới. Tắt một cơ sở khỏi trang quảng bá mà làm HIS
của họ ngừng đẩy được kết quả là lỗi im lặng. Cắt đường API thì tắt *Khoá cơ sở*.
_Tránh_: nói "cờ Active" trần khi chưa nói rõ bảng nào

**Phòng khám**:
**Không tồn tại.** Bảng `PhongKham` từng có 3 dòng dữ liệu bịa, đã bỏ ở đợt 2026-08-24. Mọi "nơi khám"
đều là *Cơ sở*.
_Tránh_: dùng lại từ này dưới bất kỳ dạng nào

### Đồng bộ với HIS (chốt 2026-09-08)

**Cơ sở dùng HIS**:
Một *Cơ sở* có `DM_DoiTacApi.KieuApi = 'HIS'` — dùng *Trang bệnh nhân nội bộ* của SixosPwa, **đồng
thời** có một bản HisSoft đứng sau cấp dữ liệu. Khác *Cơ sở bàn giao* (`UB`) ở chỗ đối tác không dựng
màn nào; khác cơ sở `NONE` ở chỗ có nguồn dữ liệu thật. Bước đệm hiện nay là **Thiên Nam**.
_Tránh_: cơ sở có API, cơ sở nội bộ, khách HisSoft

**Đẩy** / **Gọi thẳng**:
Hai chiều đi của dữ liệu, chia theo **loại dữ liệu** chứ không theo cơ sở. *Đẩy* = HIS chủ động gửi
lên SixosPwa (tài liệu, đợt khám). *Gọi thẳng* = SixosPwa chủ động hỏi HIS qua `DM_DoiTacApi.BaseUrl`
(lịch trống, đặt/huỷ lịch). Lịch hẹn **không** có bản sao ở SixosPwa. Xem ADR 0017.
_Tránh_: đồng bộ (chung chung — không nói được ai gọi ai), push/pull

**Hàng đợi gửi**:
Bảng nằm **bên HIS**, không phải bên SixosPwa: mỗi dòng là một tài liệu chờ đẩy, mang trạng thái và
số lần thử. Nó tồn tại vì cò đẩy là **người bấm**, nên phải có chỗ ghi nhớ cái gì đã gửi, cái gì còn
tồn, cái gì gửi hỏng cần gửi lại.
_Tránh_: queue, bảng log, bảng đồng bộ

**Luật gộp hồ sơ**:
Điều kiện để SixosPwa coi hai `MaBN` là cùng một *Con người*: khớp **cả ba** — CCCD hợp lệ, Họ tên
không dấu, Ngày sinh. Lệch bất kỳ ô nào thì **không tự gộp**, đẩy sang *Liên kết một lần* cho bệnh
nhân tự nhận. 🔴 **Không bao giờ gộp chỉ bằng CCCD**: đo thật ở Thiên Nam có **345 nhóm** cùng CCCD
mà khác tên. Xem ADR 0018.
_Tránh_: khớp bệnh nhân, matching, gộp theo CCCD

**CCCD rác**:
Giá trị nằm ở cột CCCD nhưng không định danh ai — `000000000000` (2.847 lần trên `Dev_Master3`),
`111111111111`, `012345678910`… Mọi câu đếm hay khớp theo CCCD **bắt buộc** loại nhóm này trước, nếu
không con số ra sai hoàn toàn.
_Tránh_: CCCD trống, dữ liệu bẩn

**Lịch đặt**:
Một lần bệnh nhân đặt lịch từ cổng, lưu ở **bảng riêng bên HIS**, mang trạng thái *chờ xác nhận /
đã xác nhận / đã huỷ*. **Không phải** *Giấy hẹn tái khám* (`QL_GiayHenTaiKham`) — cái đó do bác sĩ cấp
và gắn vào một đợt khám đã xảy ra.
_Tránh_: giấy hẹn, lịch hẹn (trần), appointment

**Khoá cơ sở**:
Một dòng `HT_KhoaApiCoSo` — chuỗi bí mật cấp cho **một cơ sở** để HIS của họ gọi vào *khu API nhận*.
Cơ sở giữ chuỗi thô trong cấu hình HIS; cổng chỉ giữ **bản băm**, nên đọc cơ sở dữ liệu không đọc ra
được khoá. Một cơ sở mang **nhiều khoá** cùng lúc được — đó là cách xoay khoá mà không có khoảng chết.
Cờ `Active` của nó là công tắc **duy nhất** của đường API, tách hẳn khỏi *Cờ `Active`* của cơ sở.
_Tránh_: API key (trần), token, mật khẩu đối tác

**Khu API nhận**:
Các đường dưới `api/v1` — chỗ **MÁY** gọi vào: HIS của cơ sở đẩy tài liệu, đẩy đợt khám, hỏi ai đã nối
hồ sơ. Vào bằng *Khoá cơ sở*, không bao giờ bằng cookie. Đối lại là các đường của **NGƯỜI** bệnh nhân
dưới `/benh-nhan`, vào bằng cookie. 🔴 Một đường phục vụ người mà nằm trong khu này là sai chỗ — đó
chính là cách đường đọc tài liệu từng hở cho cả thế giới.
_Tránh_: API (trần), endpoint, webhook

**Đợt khám**:
Một dòng `QL_DotKham` = **một lần đến khám**, do HIS đẩy lên theo lô. Không phải bảng đếm: bảng đếm cũ
`QL_LichSuKham` đã khai tử, các số *lần đầu / lần gần nhất / số lần* suy thẳng từ đây. Khoá nhận dạng
là cặp *(cơ sở, mã vào viện)*. Ô chẩn đoán **được phép trống** — 20,5% đợt khám ở Thiên Nam trống ô này.
_Tránh_: lịch sử khám (trần), lượt khám, phiên khám

**Tài liệu mồ côi**:
Tài liệu HIS đẩy lên cho một *Mã BN* mà cổng **chưa có hồ sơ nào** nối tới. Cổng **từ chối** chứ không
giữ lại: cổng không ôm một byte bệnh án nào của người chưa phải người dùng. Phiếu nằm lại hàng đợi bên
HIS, và HIS biết khi nào đẩy được nhờ hỏi lại theo lô. Xem ADR 0021.
_Tránh_: tài liệu treo, ký gửi, pending

### Tài khoản và hồ sơ (chốt 2026-09-08)

**Tài khoản cổng**:
Một dòng `HT_TaiKhoan`, khoá bằng **số điện thoại** (`UK_HT_TaiKhoan_SDT`) — thứ đi qua OTP. Từ
2026-09-08 một tài khoản quản **nhiều** *Con người* (ADR 0019), chứ không còn một-một. Vẫn phải phân
biệt với *Con người*: tài khoản là chỗ đăng nhập, con người là người đi khám.
🔴 Quan hệ nhiều-hồ-sơ này **chỉ áp cho nhánh màn chung**; cơ sở đi *nhánh bàn giao* (`KieuApi='UB'`)
giữ nguyên một tài khoản một người. Hai mô hình danh tính song song là **cố ý**, không phải bỏ sót.
_Tránh_: user, người dùng, tài khoản (trần — dễ lẫn với `HT_TaiKhoanDoiTac`)

**Hồ sơ tự khai**:
Một *Con người* do chính người dùng gõ tay trên màn *Hồ sơ của tôi*, khi cơ sở **chưa** có người đó.
Chưa mang `MaBN`, chưa có đợt khám nào, và **sửa được mọi ô**. Đối lập với *hồ sơ đã nối*, nơi họ tên
/ ngày sinh / CCCD đọc từ HIS và **khoá cứng** — vì đó đúng là ba ô của *Luật gộp hồ sơ*.
_Tránh_: hồ sơ tạm, hồ sơ nháp, hồ sơ trống

**Nối hồ sơ**:
Việc gắn một *Hồ sơ tự khai* với hồ sơ thật bên HIS. Tự động khi khớp cả ba ô của *Luật gộp hồ sơ*;
lệch thì bệnh nhân gõ **Mã BN** in trên phiếu. SixosPwa chỉ hỏi HIS **khi người dùng bấm**, không hỏi
mỗi lần mở màn — một tài khoản N hồ sơ thì mở màn một lần là N cuộc gọi sang máy khách.
_Tránh_: liên kết, đồng bộ, tra cứu (trần)

**Hồ sơ đang chọn**:
Claim trong phiên nói người dùng đang xem hồ sơ nào. Bám khuôn `DangKyOnlineUB`
(`QL_HoSoBenhNhanServices.ThemIdXemThongTinBenhNhan`). Khác claim `Cccd` — cái đó là **chính chủ tài
khoản**, đóng lúc đăng nhập và không đổi.
_Tránh_: bệnh nhân hiện tại, context, hồ sơ active

**Cửa tài liệu**:
Điều kiện để *kết quả cận lâm sàng* và *đơn thuốc* của một hồ sơ được mở ra: số điện thoại trên hồ sơ
HIS trùng số điện thoại của *Tài khoản cổng*, hoặc bệnh nhân đã gõ đúng **Mã BN** một lần. Đọc nó là
*"cơ sở đã ghi bạn là đầu mối liên lạc của người này"*, **không** phải *"bạn chính là người này"*.
Tóm tắt đợt khám thì **không** qua cửa này. Xem ADR 0020.
_Tránh_: phân quyền, khoá tài liệu, xác thực (trần)

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
- [0014](docs/adr/0014-co-so-ub-dung-man-cua-khach.md) — vì sao cơ sở Ung Bướu dùng bộ màn của khách thay vì OTP của SixosPwa.
- [0015](docs/adr/0015-phan-hoi-doi-tac-phai-co-statuscode-200.md) — vì sao phản hồi của đối tác chỉ tính là thành công khi mang `statusCode == 200`.
- [0016](docs/adr/0016-phien-con-song-di-thang-va-dau-an-doi-tac.md) — vì sao phiên còn sống thì đi thẳng, và vì sao bàn giao đòi dấu ấn của đối tác chứ không chỉ `[Authorize]`.
- [0017](docs/adr/0017-hai-chieu-theo-loai-du-lieu.md) — vì sao tài liệu thì HIS đẩy lên còn lịch hẹn thì SixosPwa gọi thẳng.
- [0018](docs/adr/0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) — vì sao gộp hồ sơ đòi cả CCCD + tên + ngày sinh chứ không chỉ CCCD.
- [0019](docs/adr/0019-mot-tai-khoan-nhieu-ho-so.md) — vì sao một tài khoản quản nhiều hồ sơ, và vì sao ai khai trước giữ CCCD.
- [0020](docs/adr/0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) — vì sao tin CCCD ở lối vào nhưng đặt cửa chắn ở tầng tài liệu.

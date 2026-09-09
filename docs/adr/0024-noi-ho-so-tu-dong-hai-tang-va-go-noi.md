# 0024 — Nối hồ sơ xảy ra khi LƯU, chia hai tầng, và bắt buộc có đường gỡ

- **Trạng thái:** Đã chấp nhận
- **Ngày:** 2026-09-09
- **Bối cảnh liên quan:** [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) — *Luật gộp hồ sơ*, bản
  bốn ô. [0019](0019-mot-tai-khoan-nhieu-ho-so.md) — một tài khoản nhiều hồ sơ.
  [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) — *Cửa tài liệu*.

## Bối cảnh

Đợt 3 khép lại với một khoảng trống không ai nhận ra cho tới khi rà cả hai kho: cửa đọc bên HIS
(`SPWA_TraCuuHoSo`, `SPWA_TraCuuTheoMaBN`) đã chạy và đã nghiệm thu thật, nhưng **không màn nào bên
cổng gọi nó** — chỗ duy nhất gọi là cửa kiểm tra sức khoẻ của quản trị viên. `HoSoController.Them`
chỉ tạo *Hồ sơ tự khai*: không hỏi `MaBN`, không gọi HIS. Hệ quả: tài liệu HIS đẩy lên chỉ về được
những hồ sơ **gắn tay bằng `.sql`**.

Đo trạng thái cổng ngày 2026-09-09: **19 người, 14 (74%) mang tên là SỐ ĐIỆN THOẠI**; 24 *Hồ sơ tại
cơ sở* thì **4 đã nối mã**, **20 tự khai**. Nhóm 74% ấy là *hồ sơ chính chủ* — sinh ra lúc đăng ký
bằng OTP, **không bao giờ đi qua màn *Thêm hồ sơ***. Bất kỳ phương án nào chỉ sửa màn *Thêm hồ sơ*
đều bỏ rơi đúng nhóm đông nhất. Và cổng khi đó **chưa có màn *Sửa hồ sơ*** — `HoSoController` chỉ có
`Index` / `Chon` / `Them` / `Xoa`.

## Quyết định

**Ba vế, phải đi cùng nhau.**

1. **Không có nút *Nối hồ sơ* trên giao diện.** Việc nối xảy ra ở **mỗi lần LƯU** một hồ sơ tự khai —
   *Thêm* hay *Sửa* đều thế. Kèm theo là **màn *Sửa hồ sơ* mới**, thứ duy nhất cứu được nhóm hồ sơ
   chính chủ. Ô *Mã bệnh nhân tại cơ sở* hiện trên form ở dạng **chỉ đọc**, chưa nối thì **để rỗng** —
   để bệnh nhân tự thấy hồ sơ của mình đã nối hay chưa mà không cần ai giải thích.

2. **Hai tầng, cắt theo mức chắc chắn.**
   - **Tầng 1** — khớp đủ bốn ô của *Luật gộp hồ sơ* với **CCCD hợp lệ** ⇒ **gán im lặng**, không hỏi.
   - **Tầng 2** — CCCD rác hoặc trống, hoặc chỉ khớp ba ô còn lại ⇒ **hiện danh sách, bắt bệnh nhân
     xác nhận tay**, không gán gì cho tới khi có người bấm.

   Ranh giới này đặt đúng chỗ **348 nhóm** trùng họ tên + ngày sinh + giới tính mà CCCD hợp lệ khác
   nhau (ADR 0018, phần sửa đổi): mọi ca ấy rơi vào tầng 2.

3. **Mỗi mã đã nối có nút *Gỡ nối* riêng, đặt TRONG màn *Sửa hồ sơ*.** Gỡ = xoá đúng dòng
   `DM_BenhNhanCoSo`, nhả mã ra cho tài khoản khác nhận; **không xoá hồ sơ, không đụng gì bên HIS**.
   Hết mã thì hồ sơ tụt về *Hồ sơ tự khai*. Nó **không** nằm ở *Hồ sơ của tôi*: màn đó để **chọn
   người**, không phải để sửa dữ liệu — trộn hai việc vào một danh sách là mời người dùng bấm nhầm
   một nút phá huỷ trong lúc chỉ định đổi hồ sơ đang xem.

   Kéo theo ở *Hồ sơ của tôi*: thẻ **bỏ ô avatar** để lấy chỗ cho **hai nút cạnh nhau, khác màu** —
   *Chọn* (xanh đặc) và *Sửa* (tím nhạt); thẻ đang xem chỉ còn *Sửa*. Số mã đã nối hiện ở dòng phụ.

## Hệ quả

- **CCCD hợp lệ phủ 79,1%** hồ sơ Thiên Nam ⇒ khoảng bốn phần năm số ca đi tầng 1, không thêm bước
  nào. 20,9% còn lại vẫn có đường, chỉ là phải bấm một lần.
- Cổng phải **thêm cột giới tính** vào `DM_BenhNhan` (bảng hiện có ID, CCCD, TenBN, SDT, Email,
  DiaChi, NgayTao, NgaySinh, HoTenKhongDau, IDTaiKhoan) ⇒ một script `.sql` idempotent, `datetime` +
  `GETDATE()` theo lệ repo.
- Vế 3 tồn tại **chỉ vì** vế 2 gán mà không hỏi. Bỏ nút gỡ đi thì tầng 1 thành đặt cược rằng luật khớp
  không bao giờ sai — mà 348 nhóm nói rằng nó sẽ sai. Thao tác *xoá hồ sơ* vẫn bị chặn khi hồ sơ đã có
  dữ liệu khám; *gỡ nối* là đường khác hẳn và phải nói rõ khác chỗ nào cho người dùng.
- Mỗi lần lưu là **một cuộc gọi sang máy khách**. Vẫn đúng nguyên tắc cũ — chỉ hỏi HIS **khi người
  dùng bấm**, không hỏi mỗi lần mở màn.

## Hồ sơ thiếu trường: nhắc mềm, không chặn

Câu hỏi đi kèm: tài khoản đăng ký lần đầu chỉ có **một** hồ sơ và hồ sơ đó thiếu trường bắt buộc thì
có **ép** hoàn thiện trước khi vào `/benh-nhan` không. Đề xuất ban đầu là **chặn cứng**.

**Chốt: nhắc mềm.** Vào thẳng `/benh-nhan`, đầu trang có dải nhắc dẫn sang *Sửa hồ sơ*, không chặn
điều hướng. Lý do đảo: chặn theo *thiếu trường* sẽ khoá cả những hồ sơ **đã nối mã mà còn khuyết dữ
liệu** — đúng trạng thái của tài khoản nghiệm thu `0363982926` (`MaBN = 100992`, thiếu ngày sinh và
giới tính). Một màn đang chạy và đang phục vụ tài liệu thật bỗng thành màn chặn: thiệt hại có thật,
đổi lấy một lợi ích giả định.

Cái giá đã biết và chấp nhận: **74%** tài khoản đang mang tên là số điện thoại, và dải nhắc thường bị
bỏ qua — nhiều khả năng con số đó giảm chậm. Nếu sau một thời gian đo lại mà nó không giảm thì chỗ đổi
sang chặn cứng là **`Services/Partner/LuongCongBenhNhan.cs:207-208`** — cổng hạ cánh duy nhất sau xác
thực, nơi luật *trên một hồ sơ thì qua màn chọn trước* đang sống. Chặn ở đó, và **chỉ chặn hồ sơ chưa
có mã nào**.

## Các lựa chọn đã cân nhắc

**Gộp việc nối vào màn *Thêm hồ sơ* (một luồng, hai kết cục).** Ít việc nhất, không phải dựng màn mới,
và view hiện tại đã viết sẵn gợi ý *"Dùng để nối hồ sơ với bệnh án tại cơ sở"* dưới ô ngày sinh — tức
người viết nó đã nghĩ tới việc này. Bỏ vì nó **bỏ rơi 14/19 tài khoản** đang mang tên là số điện thoại:
hồ sơ của họ tự đẻ lúc đăng ký, không đi qua màn *Thêm*. Thêm nữa, gõ sai một ô lần đầu là vĩnh viễn
không sửa lại được khi chưa có màn *Sửa*.

**Hai lối riêng ở *Hồ sơ của tôi*: *Thêm hồ sơ người thân* và *Nối hồ sơ đã khám ở đây*.** Mỗi màn làm
đúng một việc nên dễ đặt lời. Bỏ vì nó bắt bệnh nhân **tự trả lời câu chỉ HIS trả lời được** — người
khai hộ cho cha mẹ thường không biết cụ đã từng khám ở đây chưa; bấm nhầm là vẫn đẻ ra hồ sơ tự khai
trùng người thật.

**Nối bằng *Mã bệnh nhân* in trên toa/sổ.** Gõ ít nhất, và là đường duy nhất cho người có CCCD rác.
Bỏ làm luồng chính vì **ai cầm được tờ toa của người khác là nối được bệnh án của họ**; cửa
`SPWA_TraCuuTheoMaBN` lại có tầng chặn dò 20 lần/15 phút nên dễ khoá nhầm người thật. Vẫn giữ được
như lối phụ ở tầng 2 nếu sau này có ca đòi.

**Bỏ tầng 1, mọi ca đều bắt xác nhận tay.** Không bao giờ gán mà người dùng không biết, và khỏi cần
đường gỡ. Bỏ vì bắt thêm một lần bấm cho **79,1%** số ca đáng lẽ đi trơn tru — đúng kiểu đánh đổi sai
chỗ mà ADR 0018 đã từ chối một lần khi loại phương án "không tự gộp gì".

**Không làm nút gỡ, nối nhầm thì báo lễ tân chạy `.sql`.** Ít code, và tránh được chuyện bệnh nhân tự
gỡ nhầm mất tài liệu của chính mình. Bỏ vì trong lúc chờ người can thiệp, **người đó vẫn đang xem bệnh
án của người khác** — thời gian phơi nhiễm tính bằng ngày, không phải phút.

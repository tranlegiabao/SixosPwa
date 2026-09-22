# 0019 — Một tài khoản quản nhiều hồ sơ, và ai khai trước giữ CCCD

- **Tác giả:** Nam · **Ngày:** 2026-09-08 · **Trạng thái:** Đã bị thay thế bởi ADR 0036

> 🔴 **ĐÃ BỊ THAY THẾ bởi ADR 0036** (2026-09-19, đợt 1B).
> Vế 1 chuyển sang cặp (SĐT × cơ sở) + toggle `HT_Config.MOT_HO_SO`; vế 2 *"ai khai trước giữ CCCD"* **bị bỏ** cùng `UK_DM_BenhNhan_CCCD`.
> Xem [0036](0036-ho-so-la-cap-nguoi-x-co-so.md). Giữ file này để đọc lại bối cảnh cũ.

- **Bối cảnh liên quan:** [0014](0014-co-so-ub-dung-man-cua-khach.md) — nhánh bàn giao.
  [0016](0016-phien-con-song-di-thang-va-dau-an-doi-tac.md) — bàn giao đòi dấu ấn đối tác.
  [0018](0018-luat-gop-ho-so-cccd-ten-ngaysinh.md) — luật gộp hồ sơ.

## Bối cảnh

Cổng bệnh nhân trước 2026-09-08 buộc **một tài khoản = một con người** (`HT_TaiKhoan.IDBenhNhan`,
quan hệ 1–1), và phiên nhận diện bệnh nhân bằng claim `Cccd` lấy thẳng từ màn đăng nhập.

Hai phép đo trên DB khách thật nói mô hình đó không khớp thực tế:

| Sự thật đo được | Số |
|---|---|
| SĐT ở `PKDK_ThienNam` gắn với **≥2 CCCD khác nhau** | **4.140 số** (9,9% của 41.893 số) |
| Số bị nhập nhầm nhiều nhất | **1 số gắn 876 người** |
| Người có **≥2 `MaBN`** tại cùng một cơ sở | 8.454 (**17,3%**) |
| Bệnh nhân **không có cả CCCD lẫn SĐT** | 10.574 (**14,1%**) |

Cả nhà dùng chung một số điện thoại là chuyện thường; con đặt khám cho mẹ, mẹ theo dõi kết quả cho
con cũng vậy. Với mô hình 1–1, mỗi người trong nhà phải có một số điện thoại riêng mới dùng được cổng.

Hệ `DangKyOnlineUB` đã chạy thật giải bài này từ lâu: `DmTtbenhNhan.IdTk` là quan hệ **1–N**, có màn
liệt kê hồ sơ, cho **tự gõ tạo hồ sơ**, và ghi *hồ sơ đang chọn* vào claim của phiên.

## Quyết định

Ba vế, đi cùng nhau:

1. **Bê mô hình của `DangKyOnlineUB`**: `DM_BenhNhan.IDTaiKhoan` thay cho `HT_TaiKhoan.IDBenhNhan` —
   một tài khoản quản nhiều hồ sơ. Thêm màn *Hồ sơ của tôi*, thêm claim *hồ sơ đang chọn*, và
   **cho người dùng tự gõ tạo hồ sơ** kể cả khi cơ sở chưa có người đó.
2. **Ai khai trước giữ CCCD.** `UK_DM_BenhNhan_CCCD` là duy nhất toàn hệ; CCCD đã nằm trong một tài
   khoản thì tài khoản khác **bị chặn**, đúng như UB đang làm. Nhưng thông báo lỗi **phải chỉ đường
   ra** (nhờ người đang giữ xoá hồ sơ, hoặc liên hệ cơ sở) chứ không cụt như bản UB, và tài khoản
   đang giữ phải có nút **xoá hồ sơ** để nhả CCCD.
3. **KHÔNG áp mô hình này cho nhánh bàn giao.** Cơ sở `KieuApi='UB'` giữ nguyên 1 tài khoản = 1 người.

Hệ quả kèm theo, không tách rời: `DM_BenhNhanCoSo` bỏ `UK_DM_BenhNhanCoSo_HoSo`
(`IDBenhNhan, IDCoSo`) — chính nó là thứ chặn một người có nhiều `MaBN` tại một cơ sở, trái ADR 0018.
`UK_DM_BenhNhanCoSo_MaBN` đã là `(IDCoSo, MaBN)` nên giữ nguyên.

## Hệ quả

- **Hai mô hình danh tính song song trong một codebase.** Nhánh bàn giao 1–1, nhánh màn chung 1–N.
  Đây là điều người đọc code sau sẽ tưởng là bỏ sót, nên phải ghi rõ trong `CONTEXT.md`.
- Sinh ra **hai loại hồ sơ**: *tự khai* (chưa nối HIS, chưa có dữ liệu khám nào) và *đã nối HIS*.
  Hồ sơ tự khai sửa được mọi ô; hồ sơ đã nối thì họ tên/ngày sinh/CCCD **khoá cứng** — vì đó đúng là
  ba ô của luật gộp ở ADR 0018, sửa sau khi đã gộp là làm hồ sơ tự rụng khỏi nhóm.
- Màn đăng nhập **giữ nguyên ô CCCD** cho mọi cơ sở, và CCCD đó sinh ra hồ sơ *chính chủ* của tài
  khoản. Nhờ vậy màn đăng nhập không phải rẽ nhánh theo `KieuApi` — đổi lại, khái niệm *tài khoản*
  vẫn dính vào một con người, không sạch bằng UB.
- Người **chính chủ có thể bị chặn bởi người khai hộ**. Đây là cái giá đã biết và chấp nhận.

## Các lựa chọn đã cân nhắc

**Giữ 1 tài khoản = 1 người.** Không phải grill lại chốt nào, không đụng luồng đăng nhập đang chạy
thật. Bỏ vì 4.140 gia đình chung số điện thoại sẽ không dùng được cổng, và người già — đúng nhóm cần
tra kết quả nhất — thường là người không có điện thoại riêng.

**Nhiều tài khoản cùng thấy một con người (bảng nối N–N).** Không ai bị chặn ở cửa, không ai bị cướp
hồ sơ, và đúng đời thực là cả nhà cùng theo dõi. Bỏ vì phải đẻ thêm bảng nối cùng luật *ai là chủ sở
hữu, ai chỉ được xem*, trong khi khuôn UB đã chạy thật thì chỉ cần một cột.

**Chuyển quyền cho người tự đăng ký (chính chủ luôn thắng).** Giữ được mô hình 1–N gọn và giải đúng
ca mẹ đăng ký sau con. Bỏ vì cổng **tin CCCD** ở lối vào (xem ADR 0020): ai gõ CCCD người khác kèm
OTP máy mình sẽ *cướp* được hồ sơ khỏi tài khoản người ta, và người mất hồ sơ không hiểu vì sao.

**Áp mô hình mới cho cả nhánh bàn giao.** Một mô hình danh tính duy nhất cho cả hệ. Bỏ vì mật khẩu
đối tác lưu theo `HT_TaiKhoanDoiTac(IDTaiKhoan, IDCoSo)` — một dòng cho mỗi cặp — nên phải mở rộng
thành `(IDTaiKhoan, IDCoSo, IDBenhNhan)`, tức đụng thẳng luồng đang phục vụ bệnh nhân Ung Bướu thật
và viết lại ADR 0014/0016, trong khi Đợt 1 đã đủ việc.

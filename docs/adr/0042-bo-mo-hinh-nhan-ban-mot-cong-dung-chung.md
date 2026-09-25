# 0042 — Bỏ mô hình khuôn mẫu/nhân bản: một cổng dùng chung cho mọi cơ sở

- **Tác giả:** Nam · **Ngày:** 2026-09-22 · **Trạng thái:** Đã chốt
- **Bối cảnh liên quan:** [0013](0013-active-la-cong-hien-thi-duy-nhat.md) ·
  [0036](0036-ho-so-la-cap-nguoi-x-co-so.md) · [0038](0038-co-so-co-cua-rieng-thi-chuyen-huong-thang.md)

## Bối cảnh

Repo này sinh ra làm **khuôn mẫu**: một bản gốc không có nghiệp vụ, để mỗi khách hàng sao ra một
"bản nhân" riêng có tên, tên miền và vòng đời riêng. [ADR 0001](0001-chon-net7-du-het-ho-tro.md) và
[ADR 0002](0002-service-worker-khong-cache.md) đều viết theo giả định đó.

Thực tế đi hướng khác và **không ai tuyên bố**. Chưa có bản nhân nào ra đời — thứ duy nhất từng được
gọi là bản nhân là nhánh `19_Bao-Hieu`, nay đã chết. Thay vào đó cổng lớn dần thành **một hệ triển
khai duy nhất phục vụ nhiều cơ sở** trên cùng CSDL `HIS_CSKH`:

- [ADR 0036](0036-ho-so-la-cap-nguoi-x-co-so.md) — hồ sơ là **cặp người × cơ sở**, nghĩa là nhiều cơ sở
  cùng sống trong một CSDL chứ không mỗi khách một bản.
- [ADR 0038](0038-co-so-co-cua-rieng-thi-chuyen-huong-thang.md) — cơ sở có cổng riêng thì **chuyển hướng
  thẳng** sang trang của họ; trước kia phải dựng lại màn của khách ngay trong cổng.
- [ADR 0013](0013-active-la-cong-hien-thi-duy-nhat.md) — bật/tắt một cơ sở là **đổi dữ liệu**, không
  phải triển khai lại.

Hệ quả của việc im lặng: `CONTEXT.md` vừa định nghĩa "Khuôn mẫu" vừa tự dán một lời vá phía dưới nói
rằng định nghĩa đó không còn đúng, và người đọc ADR 0001/0002 đi tìm "bản nhân" không bao giờ thấy.

## Quyết định

**SixosPwa là một sản phẩm đang chạy thật, không phải khuôn mẫu.** Một lần triển khai, nhiều cơ sở.
Thêm khách là **thêm dữ liệu** (`DMCSKCB`), không phải sao ra một bản phần mềm mới.

## Đánh đổi đã biết

Mọi cơ sở đi chung một nhịp phát hành. Không thể để khách A đứng ở bản cũ trong khi khách B lên bản
mới, và một lỗi đẩy lên là lỗi của **tất cả** cơ sở cùng lúc — trước kia mỗi bản nhân hỏng riêng.
Đổi lại, một chỗ sửa là mọi cơ sở được sửa, và không còn cảnh mỗi khách một nhánh trôi xa dần.

## Hệ quả

- Hai thuật ngữ **Khuôn mẫu** và **Bản nhân** đã bị **bỏ khỏi `CONTEXT.md`** cùng lời vá trỏ nhánh
  `19_Bao-Hieu`.
- `README.md` bỏ mục *"Nhân bản thành phần mềm thật"*.
- [ADR 0001](0001-chon-net7-du-het-ho-tro.md) và [ADR 0002](0002-service-worker-khong-cache.md) **giữ
  nguyên** chữ "khuôn mẫu"/"bản nhân" — ADR đã chốt thì chỉ thêm, không sửa. Đọc hai ADR đó là đọc bối
  cảnh lúc chúng được viết; **quyết định bên trong chúng vẫn còn hiệu lực** (ghim .NET 7, service worker
  không cache), chỉ có từ vựng là cũ.

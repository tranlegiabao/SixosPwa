# Khuôn mẫu dựng trên .NET 7 dù .NET 7 đã hết hỗ trợ

Khuôn mẫu này sẽ được nhân bản nhiều lần, nên chọn phiên bản nền là quyết định lan rộng và tốn công
đảo ngược. Chúng tôi chọn **net7.0** — trùng với HisSoft đang chạy — để mọi bản nhân triển khai được
ngay lên hạ tầng khách hàng hiện tại mà không phải cài Hosting Bundle mới, và để code bê qua lại giữa
HisSoft với các bản nhân không vướng khác biệt phiên bản.

## Đánh đổi đã biết

.NET 7 **hết hỗ trợ bảo mật từ tháng 5/2024**. Nghĩa là mọi bản nhân sinh ra từ khuôn mẫu này đều thừa
hưởng một nền không còn được vá lỗi. Đây là lựa chọn có ý thức, không phải sơ suất: ưu tiên đồng bộ hạ
tầng hơn là tính thời sự của nền tảng.

## Phương án đã cân nhắc và loại

- **.NET 10 (LTS, hỗ trợ tới ~11/2028)** — đúng đắn nhất về lâu dài, nhưng đòi cài SDK trên máy dev và
  Hosting Bundle trên mọi server khách trước khi bản nhân đầu tiên chạy được.
- **.NET 9** — máy dev đã có sẵn SDK, nhưng là bản STS và cũng vừa hết hạn (~5/2026), tức là chịu đúng
  nhược điểm của net7 mà không được lợi gì về đồng bộ hạ tầng.

## Hệ quả

`global.json` ở gốc `Projects/SixosPwaTemplate/` **ghim SDK 7.0.410**. Máy này có sẵn cả SDK 9, nên nếu
bỏ `global.json` thì `dotnet` sẽ tự nhảy sang SDK 9 và tạo project mới ở phiên bản khác. Đừng xoá file đó.

Khi nào nâng: nếu một bản nhân cần thư viện chỉ có trên .NET mới, hoặc khi có yêu cầu tuân thủ bảo mật
bắt buộc, hãy nâng **khuôn mẫu trước** rồi mới nâng các bản nhân — đừng để mỗi bản nhân một phiên bản.

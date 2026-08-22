# Bàn giao sang trang Ung Bướu thay vì dựng lại màn trong PWA

`SixOSDatKhamAPI` đã có đủ endpoint để SixosPwa tự dựng hai màn *Lịch sử hẹn khám* và *Tra cứu hồ sơ
khám bệnh* (`api/LichSuDatHen/*`, `api/LichSuKhamBenh/*`, kể cả đường lấy JWT 24h bằng OTP mà không
cần mật khẩu). Chúng ta vẫn chọn **bàn giao phiên sang `kcg.bvungbuou.vn`** thay vì dựng lại, để
nghiệp vụ đặt lịch và hồ sơ chỉ tồn tại ở một nơi.

## Considered Options

- **Dựng lại trong PWA.** Được: bệnh nhân ở nguyên trong app đã cài, giao diện mobile thống nhất, một
  tên miền. Mất: mỗi màn UB làm thêm hoặc đổi nghiệp vụ là một lần ta phải bám theo — chi phí này
  không có điểm dừng, và nó rơi vào nhóm khác với nhóm đang phát triển UB.
- **Proxy ngược toàn trang UB dưới tên miền SixosPwa.** Được: không rời tên miền, giữ khung PWA. Mất:
  phải chuyển tiếp HTML/JS/ảnh, xử lý chuyển hướng và URL tuyệt đối; UB đổi giao diện là gãy.

## Consequences

Bệnh nhân **rời tên miền** khi bấm vào chức năng của cơ sở có API — mất khung PWA đã cài ra màn hình
chính từ điểm đó trở đi. Đây là cái giá đã biết và chấp nhận. Đổi lại, SixosPwa không phải bám theo
bất kỳ thay đổi nghiệp vụ nào của UB.

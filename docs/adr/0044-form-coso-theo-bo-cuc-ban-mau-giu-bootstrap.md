# 0044 — Form Cơ sở y tế theo bố cục bản mẫu, giữ nguyên Bootstrap/admin.css

- **Tác giả:** Nam · **Ngày:** 2026-09-22 · **Trạng thái:** Đề xuất

## Bối cảnh

Đồng nghiệp dựng một bản mẫu giao diện độc lập (`ui/admin-co-so-y-te-prototype/`, HTML/CSS/JS
thuần, không phụ thuộc HisSoft) cho hai màn Admin ▸ Cơ sở y tế ▸ Thêm mới/Sửa. Bản mẫu không chỉ đổi
bố cục — nó tự định nghĩa hẳn một bộ thiết kế riêng: biến màu `--navy/--blue` riêng, layout CSS grid
theo "band", công tắc bật/tắt dạng pill tự vẽ (`.switch`), và thanh Hủy/Lưu **dính đáy màn hình**
(`sticky-actions`). Toàn bộ khác với Bootstrap 5 + `admin.css` mà mọi màn Admin khác (Dashboard, Bệnh
nhân, Cấu hình) đang dùng (`_AdminLayout.cshtml:15-21`).

## Quyết định

Chỉ lấy **bố cục** của bản mẫu — nhóm field lại theo 4 khối (định danh / lịch hoạt động / địa chỉ /
hiển thị), thêm ô "Tên cơ sở viết tắt", đảo thứ tự khối địa chỉ — áp dụng cho cả `Create.cshtml` và
`Edit.cshtml` vì hai field địa chỉ/giờ hoạt động dùng chung partial
(`_CoSoYTeAddressFields.cshtml`, `_CoSoYTeOperatingHoursFields.cshtml`).

**Không** port bộ giao diện riêng của bản mẫu: giữ nguyên Bootstrap `form-check form-switch` (chỉ đổi
vị trí công tắc "Hiển thị công khai" lên đầu form), giữ nguyên màu/card/nút hiện có của `admin.css`,
và **không** làm thanh Lưu dính đáy màn hình.

## Vì sao

Đồng bộ hình ảnh với phần còn lại của khu Admin quan trọng hơn khớp đúng pixel một bản mẫu dựng rời —
nếu port nguyên bộ CSS mới, riêng hai màn Create/Edit của Cơ sở y tế sẽ "lệch tông" hẳn so với
Dashboard/Bệnh nhân/Cấu hình, và người sau nhìn vào sẽ không hiểu vì sao chỉ một cụm màn có giao diện
khác biệt. Bố cục (nhóm field, thứ tự, field mới) mới là phần đồng nghiệp thật sự muốn truyền đạt;
phần màu sắc/thành phần UI chỉ là do bản mẫu được dựng độc lập, tự chọn design tokens riêng để demo.

## Hệ quả

Lần sau có bản mẫu rời dạng này, mặc định tách hai lớp **bố cục** (nhóm field, thứ tự, field mới —
luôn đáng lấy) và **hệ thống thiết kế** (màu, component, hiệu ứng — chỉ lấy khi có quyết định rõ ràng
là đổi luôn cho cả khu Admin, không lấy lẻ cho một màn).

## Cập nhật 2026-09-22 — TenVietTat không còn "chỉ dùng trong Admin"

Lúc chốt field "Tên cơ sở viết tắt", phạm vi đặt ra là chỉ lưu trong Admin, không đụng màn nào khác.
Nhìn icon PWA cài trên máy thật (`PwaController.GetManifest()`) mới thấy `short_name` — thứ hệ điều
hành in ra dưới icon màn hình chính — đang lấy `TenCoSo` đầy đủ, dễ bị cắt bớt. Đó đúng là chỗ
`TenVietTat` sinh ra để giải quyết, nên đã nới phạm vi: `short_name` ưu tiên `TenVietTat`, rỗng thì
rơi về `TenCoSo` như cũ (`name` — tên đầy đủ — vẫn giữ `TenCoSo`, không đổi).

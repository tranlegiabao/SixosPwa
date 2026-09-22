# 0033 — Trình xem đi bằng danh sách cùng loại riêng, không bám danh sách trên màn

- **Tác giả:** Nam · **Ngày:** 2026-09-17 · **Trạng thái:** Đề xuất
- **Bối cảnh liên quan:** [0020](0020-tin-cccd-o-loi-vao-chan-o-tang-tai-lieu.md) ·
  [0030](0030-tai-lieu-nam-o-hai-kho.md)

## Bối cảnh

Màn *Tài liệu y tế* (`/benh-nhan/tai-lieu`) đang trả **toàn bộ** tài liệu của hồ sơ trong một lượt —
`HomeController.cs:449-451` là `OrderByDescending(...).ToListAsync()`, không có `Take`. Razor dựng đủ N
thẻ, rồi `tai-lieu-benh-nhan.js:124-176` lập tức bắn `pdfjsLib.getDocument()` cho **từng thẻ một**, mỗi
lượt là một lần tải trọn PDF qua `/api/v1/tai-lieu/xem/{id}`. Đó là lý do màn nạp chậm rãi kéo dài.

Việc cần làm là cắt danh sách thành từng mẻ và chỉ vẽ thumbnail cho thẻ đã lọt vào tầm nhìn. Nhưng làm
vậy là **đụng vào một tính năng khác đang chạy im lặng**: trình xem phóng to có hai mũi tên chuyển qua
lại giữa các tài liệu *cùng loại*, và bộ đếm "cái thứ mấy trên mấy" của nó.

Bộ đếm đó dựng từ DOM:

```js
// tai-lieu-benh-nhan.js:225
currentSameTypeDocs = allDocuments.filter(d => d.loai === docObj.loai);
```

`allDocuments` là kết quả của `collectDocumentsFromDOM()` (`:103`) — **những thẻ đang có trên màn**.
Hôm nay màn nạp đủ nên nó tình cờ bằng đúng số tài liệu của hồ sơ, nên không ai thấy vấn đề. Cắt còn 50
thẻ mỗi mẻ thì:

- bộ đếm tụt theo số đã nạp: hồ sơ có 34 đơn thuốc nhưng mẻ đầu chỉ chứa 9 ⇒ hiện `3 / 9`;
- `updateDocSwitcherUI()` (`:298`) đặt `btnNextDocTop.disabled = isLast` ⇒ mũi tên **tắt ngay ở mép
  mẻ**, 25 đơn thuốc còn lại thành vùng chết.

Đây là hồi quy im lặng: không lỗi, không cảnh báo, chỉ là bệnh nhân không tới được tài liệu của mình.

## Quyết định

**Danh sách cùng loại là tài sản của hồ sơ, không phải của màn hình.** Trình xem tự nạp nó bằng một
lượt gọi riêng, nhẹ — chỉ `id`, tên và ngày của đúng loại đang xem, không tải PDF nào — rồi đi trên đó.
Bộ đếm và hai mũi tên hoàn toàn **không** phụ thuộc danh sách ngoài đã cuộn tới đâu.

Lượt gọi này phải tự dựng lại danh tính người đang xem từ phiên (`LayHoSoDangDungAsync`) đúng như
`TaiLieuApiController.cs:188-201` đang làm, **không** nhận mã hồ sơ từ phía trình duyệt.

## Phương án đã cân và loại

**Đếm theo thẻ đã nạp, tới mép thì nạp thêm mẻ danh sách.** Không mất tài liệu nào và không cần endpoint
mới. Loại vì con số tổng **nhảy trước mắt người bệnh** — 9 → 21 → 33 → 34 — và mỗi lần vượt mép là một
lần chờ nạp giữa lúc đang đọc. Tệ hơn nữa là khi mẻ kế tiếp không chứa tài liệu nào cùng loại thì phải
nạp tiếp nhiều mẻ mới đi được một bước.

**Khoá trong mẻ đã nạp** (giữ nguyên hành vi hiện tại của `:298`). Ít việc nhất, không thêm gì cả. Loại
vì đó chính là hồi quy nói trên, chỉ là chấp nhận nó: bệnh nhân phải đóng trình xem, cuộn danh sách
thêm, rồi mở lại — một thao tác không ai đoán ra.

## Hệ quả

- Thêm một đường đọc cho *danh sách cùng loại*, và một lượt gọi mỗi lần bệnh nhân mở loại tài liệu mới
  trong một phiên. Đổi lại lượt đó không chạm tới kho nào, không tải byte PDF nào.
- `updateDocSwitcherUI()` đọc tổng từ danh sách vừa nạp thay vì từ `allDocuments`. `allDocuments` vẫn
  còn dùng cho menu ba chấm (`:689`, `:700`) nên không bỏ được.
- Trình xem chạy đúng kể cả khi danh sách ngoài mới nạp mẻ đầu — đây là điều kiện để việc cắt mẻ không
  làm mất tính năng.
- Thuật ngữ *Danh sách cùng loại* vào `CONTEXT.md` để lần sau không ai đọc "cùng loại" thành "cùng loại
  đang hiện trên màn".

## Thứ ADR này **không** đổi

- `Cache-Control: no-store` trên đường đọc tài liệu (`TaiLieuApiController.cs:216-219`) giữ nguyên. Nó
  là quyết định riêng tư đã chốt (chốt 37): phiếu bệnh nhân không phải ảnh logo công cộng. Cách làm màn
  hết chậm ở đây là **giảm số lượt tải**, không phải đệm lại thứ đã tải.
- Ba tầng kiểm quyền đọc tài liệu (`:188-201`, ADR 0020) giữ nguyên.
- Đường rẽ hai kho theo `NguonKho` (`:209-211`, ADR 0030) giữ nguyên.

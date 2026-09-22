# 0025 — Ô *Lịch hẹn* thành *Lịch khám của tôi*, trộn hai nguồn — đóng nợ ADR 0023

- **Tác giả:** Nam · **Ngày:** 2026-09-09 · **Trạng thái:** Đã chấp nhận
- **Bối cảnh liên quan:** [0023](0023-o-lich-hen-len-trunk-voi-du-lieu-mau.md) — món nợ mà ADR này
  đóng. [0017](0017-hai-chieu-theo-loai-du-lieu.md) — lịch thì *gọi thẳng* HIS, không giữ bản sao.

## Bối cảnh

ADR 0023 đưa ô *Lịch hẹn* lên trunk ở dạng vỏ giao diện chạy bằng dữ liệu mẫu hardcode trong view, và
tự đặt điều kiện đóng nợ: **nối cửa đọc lịch của HIS trước khi bất kỳ cơ sở thật nào bật màn này**. Nó
cũng cố ý **hoãn việc chốt tên** cho tới khi biết cửa HIS thật sự trả về cái gì.

Giờ đã đo. Trên `PKDK_ThienNam` (khách thật):

| Nguồn hẹn tái khám | Tổng | Ở tương lai |
|---|---|---|
| 🔴 `QL_ToaThuoc.NgayTaiKham` (bác sĩ ghi trên toa, kiểu `date`) | 85.673 | **7.566** |
| `QL_VaoVien.NgayTaiKham` (`nvarchar` `yyyyMMdd`) | 5.722 | 294 |
| `QL_GiayHenTaiKham` | **4** | **1** |
| `QL_GiayHen` · `QL_GiayHenCLS` | 0 · 0 | 0 · 0 |

Bảng mang đúng cái tên *giấy hẹn tái khám* thì gần như **chết**; hẹn thật nằm trên **toa thuốc**. Báo
cáo production *Bệnh nhân hẹn tái khám* (`AL_BaoCaoBenhNhanHenTaiKham`) cũng đọc `QL_ToaThuoc`.

Ba con số nữa định hình màn:

- **5.192 / 71.710 người từng khám có hẹn tương lai = 7,2%** ⇒ 🔴 **92,8% mở ô này ra sẽ thấy rỗng**
  nếu nó chỉ nói về hẹn. (6.229/7.566 hẹn nằm trong 30 ngày tới; **0** hẹn xa hơn 1 năm.)
- `NgayTaiKham` kiểu **`date`** — **không có giờ**. Modal đang hiện *"08:30"*, *"14:00"*.
- `QL_ToaThuoc` **không có cột `LoiDan`** (chỉ `GhiChu` của toa) ⇒ khối *📌 Lời dặn* trong thẻ **không
  có nguồn thật**. Đây đúng thứ ADR 0023 chỉ mặt là nguy hiểm: lời dặn y tế bịa.

## Quyết định

**Ô đổi tên thành *Lịch khám của tôi*, và trộn hai nguồn theo thời gian.**

- Phần **Sắp tới** — *gọi thẳng* HIS qua một cửa đọc mới, gom `QL_ToaThuoc.NgayTaiKham` ∪
  `QL_GiayHenTaiKham` (giữ bảng giấy hẹn trong phép hợp vì cơ sở khác có thể dùng nó thật). Hỏi theo
  **tất cả `MaBN`** của hồ sơ rồi gộp — hệ quả của ADR 0018 và 0076 bên `master_3`.
- Phần **Đã khám** — lấy từ `QL_DotKham` **đã nằm sẵn trong cơ sở dữ liệu cổng** (Đợt 3 đẩy lên).
  Không gọi HIS: mở modal là có ngay, và HIS chết thì phần này vẫn hiện.
- **Thẻ bỏ hai thứ không có nguồn thật**: giờ hẹn và khối *📌 Lời dặn*. **Giữ khoa và bác sĩ**
  vì có thật.
- **Huy hiệu *MỚI* và số *chưa xem* thì GIỮ**, nuôi bằng **một cột** `DM_BenhNhanCoSo.NgayXemLichCuoi`:
  mục có `QL_ToaThuoc.NgayKe` (hẹn) hoặc `QL_DotKham.NgayCapNhat` (đợt khám) **muộn hơn mốc** thì là
  mới; mở modal xong dời mốc. Nhãn góc phải là ***Chưa xem*** (không phải *Chạm để xem* — với cơ chế
  mốc thì chạm vào một thẻ không đổi trạng thái riêng của nó).
- **Cái van, ba trạng thái tách bạch:** cơ sở **chưa nối** ⇒ ô **biến mất** khỏi trang · đã nối và
  **gọi được** ⇒ đủ hai phần · đã nối nhưng **HIS không trả lời** ⇒ vẫn hiện phần *Đã khám*, chỗ lẽ ra
  là *Sắp tới* mang nhãn ***chưa hỏi được cơ sở***.

## Hệ quả

- **Món nợ 0023 đóng lại**: không còn dòng dữ liệu mẫu nào trong view, và ô có van tắt thật thay vì
  lối vào vô điều kiện.
- Tên *Lịch hẹn* bị bỏ vì ô này **chưa bao giờ chỉ nói về hẹn**. Vẫn **không** gọi là *Lịch đặt* —
  thứ bệnh nhân đặt từ cổng chưa tồn tại, đó là Đợt 5. `CONTEXT.md` đổi mục tương ứng.
- Lịch tháng mà đồng nghiệp đã dựng (điều hướng tháng, vuốt, ba trạng thái thanh đáy) **giữ nguyên và
  có cái để tô** cho cả ngày quá khứ lẫn tương lai — phần nhìn đã render kiểm chứng 09/09, không phải
  dựng lại.
- Trạng thái *chưa hỏi được cơ sở* là **bắt buộc**, không phải trang trí: Đợt 3 đã cắn đúng lỗi này
  một lần với `HoiMaDaCoNguoiNhan` — coi `null` là rỗng thì 100% dòng rơi nhầm trạng thái.
- Bên `master_3` phải mở một cửa đọc mới; nó **không cần đổi lược đồ nào** — chỉ đọc.

## Chưa xem / đã xem — vì sao một cột chứ không phải một bảng

Bản đầu của ADR này bỏ hẳn huy hiệu *MỚI* vì **tưởng không có chỗ nào lưu trạng thái đã đọc**. Đo lại
thì có sẵn: `QL_ToaThuoc.NgayKe` **không bao giờ NULL** (0/105.332 dòng ở Thiên Nam) và `QL_DotKham`
mang `NgayTao` + `NgayCapNhat`. Vậy *"mới"* suy được, không cần lưu từng mục.

Điều đó dẫn tới một phân biệt phải nói rõ: thông báo ngân hàng là **sự kiện được đẩy tới**, còn lịch
hẹn là **một trạng thái** người ta xem đi xem lại. Thứ đáng báo không phải *"bạn chưa đọc cái này"* mà
là ***"có hẹn mới xuất hiện kể từ lần bạn xem"*** — và cái đó chỉ tốn một cột.

Huy hiệu sẽ không ồn: **2.552** bệnh nhân có toa kê mới trong 7 ngày, đúng những người vừa đi khám.

**Bảng *đã đọc* từng mục** (`HT_DaDocMucLich`) đã được cân nhắc và bỏ: nó đòi cửa HIS trả thêm **mã
nguồn ổn định** cho từng hẹn — tức đổi hợp đồng — và tệ hơn, một hẹn **bị đổi ngày** vẫn nằm im ở
trạng thái *đã đọc* trừ khi băm thêm nội dung. Cơ chế mốc thì hẹn đổi ngày **tự bật lại mới** vì
`NgayCapNhat` nhích lên. Cái mất của nó — không đánh dấu lẻ từng mục, mở một lần là sạch cả danh sách
— chấp nhận được với danh sách chỉ vài mục.

## Các lựa chọn đã cân nhắc

**Giữ tên *Lịch hẹn*, chỉ hiện hẹn sắp tới.** Nghĩa của ô sạch nhất, không trộn hai loại dữ liệu, ít
việc nhất. Bỏ vì **92,8%** bệnh nhân vừa bấm vào một ô hứa hẹn có nội dung rồi thấy màn rỗng, và lịch
tháng không có gì tô cho ngày quá khứ — tức phân nửa giao diện đã dựng thành vô dụng.

**Giữ tên *Lịch hẹn*, khi rỗng thì đỡ bằng một dòng về lần khám gần nhất.** Giữ được nghĩa hẹp của chữ
*hẹn* mà màn không trống. Bỏ vì nội dung *đã khám* vẫn lọt vào một ô tên là *Lịch hẹn* — **cùng mâu
thuẫn với phương án đã chọn nhưng không thừa nhận trong tên gọi**, và người đọc code sau sẽ phải tự
đoán vì sao.

**Một cửa HIS trả cả quá khứ lẫn tương lai.** Một nguồn duy nhất, không phải ghép hai loại ở tầng JS.
Bỏ vì cùng một sự thật (lượt khám) sẽ có **hai bản** — `QL_DotKham` đã đẩy và bản HIS trả về — lệch
nhau thì không ai biết bên nào đúng; và HIS chết là modal trống trơn.

**Luôn hiện ô, cơ sở chưa nối thì báo trong modal.** Bố cục trang giữ nguyên năm ô ở mọi cơ sở nên
không nhảy, và người dùng biết tính năng tồn tại. Bỏ vì ở cơ sở chưa nối thì **100% số lần bấm là bấm
vào thứ không dùng được**; đã có tiền lệ ngay trong glossary — ô *Đăng ký khám theo gói* **ẩn** tới
giai đoạn 3 chứ không hiện ra rồi báo lỗi.

**Không làm van, HIS chết thì coi như không có hẹn.** Ít code nhất, không thêm nhánh nào. Bỏ vì đó
đúng là bẫy Đợt 3: im lặng bị dịch thành *"bạn không có hẹn"*, bệnh nhân có hẹn tái khám thật sẽ tin
là mình không có, và **không một dấu hiệu nào báo hỏng**.

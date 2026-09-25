# 0018 — Gộp hồ sơ đòi cả CCCD + tên + ngày sinh, không gộp bằng CCCD trần

- **Tác giả:** Nam · **Ngày:** 2026-09-08 · **Trạng thái:** Đã chấp nhận · **sửa đổi 2026-09-09** (ba ô → bốn ô, xem cuối tài liệu)
- **Bối cảnh liên quan:** [0011](0011-co-so-thuoc-doi-tac-mot-nhieu.md) — bài học "lọc chết âm thầm là
  loại hỏng tệ nhất". [0017](0017-hai-chieu-theo-loai-du-lieu.md) — dữ liệu HIS đẩy lên phải khớp vào
  đúng người.

## Bối cảnh

`CONTEXT.md` từ 2026-08-24 định nghĩa *Con người* là một dòng `DM_BenhNhan` khoá bằng **CCCD**, và
*Hồ sơ tại cơ sở* là việc một con người **có mặt tại** một cơ sở. Cách nói đó ngầm hiểu: một người ở
một cơ sở thì có **một** hồ sơ. Khi giai đoạn 2 bắt đầu nhận dữ liệu thật từ HIS, giả định đó phải
được kiểm — vì nó quyết định tài liệu treo vào đâu.

Đo read-only trên **7 cơ sở dữ liệu khách thật**:

| DB khách | Tổng BN | CCCD hợp lệ | Người | Người có ≥2 `MaBN` | % |
|---|---|---|---|---|---|
| `PKDK_ThienNam` (pilot) | 74.720 | 59.751 | 48.993 | **8.454** | **17,3%** |
| `PKDK_TamDuc_BL` | 35.175 | 31.406 | 24.169 | 5.961 | 24,7% |
| `Chinh_PKDKNhanDuc` | 92.094 | 55.009 | 48.287 | 6.132 | 12,7% |
| `Chinh_PKDKTinNghia` | 9.814 | 9.137 | 8.842 | 277 | 3,1% |
| `PKDK_ThanhAn1` | 8.935 | 7.967 | 7.651 | 260 | 3,4% |
| `PKDK_ThienMy` | 2.907 | 2.281 | 2.247 | 27 | 1,2% |
| `Chinh_PKDKMediLabo` | 4.934 | 3.665 | 3.645 | 19 | 0,5% |

Giả định cũ **sai**, và sai to ở đúng những khách lớn. Nhưng phép đo thứ hai lật ngược chiều còn lại:
trong các nhóm cùng CCCD ấy, **không phải nhóm nào cũng là một người**.

| DB khách | Nhóm ≥2 `MaBN` | Cùng tên | **Khác tên** | Cùng tên **và** cùng ngày sinh |
|---|---|---|---|---|
| `PKDK_ThienNam` | 8.454 | 8.109 | **345** | 7.877 (**93,2%**) |
| `PKDK_TamDuc_BL` | 5.961 | 5.944 | 17 | 5.712 (95,8%) |
| `Chinh_PKDKNhanDuc` | 6.132 | 6.035 | 97 | 5.723 (93,3%) |

Thêm một sự thật phải biết trước khi viết bất kỳ câu đếm nào: **CCCD trong HIS là rác có hệ thống** —
`000000000000` xuất hiện **2.847 lần** trên `Dev_Master3`, kèm `111111111111`, `012345678910`. Cột
`SoCMND` thì rỗng hoàn toàn (0 dòng có dữ liệu), đừng trông vào nó.

## Quyết định

Hai vế, phải đi cùng nhau:

1. **Thừa nhận một người có nhiều hồ sơ tại cùng một cơ sở.** `DM_BenhNhanCoSo` là quan hệ **1–N thật**;
   ràng buộc duy nhất đúng là **`UNIQUE(IDCoSo, MaBN)`**, không phải `(IDBenhNhan, IDCoSo)`. Mọi màn
   hiển thị gom theo *Con người*, không theo *Hồ sơ tại cơ sở*.
2. **Chỉ tự gộp khi khớp cả ba: CCCD hợp lệ + Họ tên không dấu + Ngày sinh.** Lệch bất kỳ ô nào thì
   không gộp, đẩy sang màn *Liên kết một lần* để bệnh nhân tự nhận. CCCD nằm trong danh sách rác thì
   coi như **không có CCCD**, không bao giờ dùng để gộp.

## Hệ quả

- Ở Thiên Nam, luật này tự động xử lý **93,2%** số nhóm; **577 nhóm (6,8%)** rơi xuống màn tự nhận.
- 345 nhóm cùng CCCD khác tên **không bao giờ bị gộp tự động** — đó chính là mục đích.
- Định nghĩa *Hồ sơ tại cơ sở* trong `CONTEXT.md` đã phải sửa. Đây là lần đầu một mục glossary bị lật
  bởi dữ liệu khách thật chứ không phải bởi đọc code.
- Phải có hàm chuẩn hoá tên không dấu **dùng chung một bản** cho cả hai đầu; hai bản khác nhau một dấu
  cách là hai người khác nhau, và sai kiểu đó im lặng.

## Các lựa chọn đã cân nhắc

**Chỉ gộp bằng CCCD.** Đúng nguyên văn định nghĩa *Con người* đang có, không phải sửa tài liệu, một
cột một chỉ mục. Bỏ vì 345 nhóm ở riêng Thiên Nam có cùng CCCD mà khác tên ⇒ bệnh nhân đọc được bệnh
án người lạ. Đó không phải lỗi hiệu năng hay lỗi hiển thị, nó là lộ hồ sơ y tế — không được phép xuất
xưởng.

**Chọn một `MaBN` chính, bỏ các mã còn lại.** Đơn giản nhất, giữ nguyên định nghĩa cũ, không bao giờ
gộp nhầm người. Bỏ vì 17,3% bệnh nhân ở chính pilot sẽ mất kết quả cũ **mà không có thông báo nào** —
họ sẽ tưởng phòng khám làm mất hồ sơ. Đúng loại "hỏng âm thầm" mà ADR 0011 đã gọi tên.

**Gộp bằng CCCD + tên, bỏ ngày sinh.** Phủ 95,9% thay vì 93,2%, và né được bẫy ngày sinh giả
`01/01/1900` vốn rất phổ biến trong HIS. Bỏ vì phần chênh 2,7% không đáng để tháo một lớp chắn: cha và
con trùng tên mà hồ sơ gõ nhầm cùng CCCD là ca có thật, và ca đó chỉ có ngày sinh mới tách được.

**Bắt HIS dọn trùng trước khi bật đồng bộ.** Dữ liệu sạch từ gốc thì mọi bài toán sau đều nhẹ đi. Bỏ
vì riêng Thiên Nam đã là 8.454 ca cần gộp tay ⇒ pilot sẽ không bao giờ khởi động; và gộp hồ sơ *trong*
HIS đụng công nợ, BHYT, bảng kê — rủi ro cao hơn hẳn việc chỉ gộp ở tầng hiển thị.

**Không tự gộp gì, bệnh nhân tự nhận hết.** Không bao giờ gộp nhầm, quy trình đồng nhất. Bỏ vì bắt
100% người dùng làm thêm một bước ngay lần đầu — kể cả ~83% chỉ có đúng một hồ sơ — là đánh đổi sai
chỗ, và rớt người dùng ngay tại cửa.

---

## Sửa đổi 2026-09-09 — luật gộp nâng từ BA ô lên BỐN ô

Đợt 4 đọc luồng *bệnh nhân cũ / bệnh nhân mới* của chính màn Tiếp nhận HisSoft
(`NhanBenhServices.kiemTraTrungKhopBenhNhan`) và phát hiện HIS khớp bằng một bộ ô **khác hẳn**:
họ tên (có dấu) + ngày sinh + **năm sinh** + **giới tính**, và **không dùng CCCD**.

Đo thử luật đó trên `PKDK_ThienNam` (74.950 người):

| Phép đo | Số |
|---|---|
| Nhóm trùng tên + ngày sinh + giới tính | **12.258** nhóm, ôm **27.422 người (36,6%)** |
| 🔴 Trong đó, nhóm có **CCCD hợp lệ KHÁC NHAU** | **348** |

348 nhóm ấy **chắc chắn là hai con người khác nhau**. Nó soi gương với con số 345 nhóm cùng CCCD khác
tên ở phần trên: bỏ ô nào ra khỏi phép khớp cũng có người trả giá.

**Quyết định sửa:** luật gộp từ nay đòi khớp **cả bốn** — CCCD hợp lệ · Họ tên không dấu · Ngày sinh ·
**Giới tính**. Kèm hai điều chỉnh:

- **Hạ cấp có điều kiện cho ngày sinh.** `5.611/74.950` hồ sơ (7,5%) mang `NgaySinh = 01/01` — ngày
  giả HIS lưu khi chỉ biết năm sinh. Bản HIS mang `01/01` thì ô ngày sinh **chỉ so năm**. Xử ngầm ở
  máy chủ; **không** bày lên màn, vì dạy bệnh nhân gõ một ngày giả là làm hỏng chính ô dùng để nhận
  ra họ.
- **Bò theo chuỗi `DM_BenhNhan.IdBncu`** sau khi khớp. Đây là cột HIS tự đặt khi lễ tân lưu một người
  *"đã từng khám nhưng khác thông tin hành chánh"*: Thiên Nam có **8.919/74.946** dòng mang nó, trong
  đó **596 cặp** lệch ít nhất một ô danh tính. Không bò theo chuỗi thì tìm ra bản mới mà **giấu mất
  tài liệu cũ** của đúng người đó.

**Vì sao sửa luật gốc thay vì thêm một lớp lọc riêng bên cổng:** một luật khớp duy nhất, áp ở mọi nơi,
là thứ người sau còn đọc hiểu được; hai tầng luật song song thì chỗ nào dùng luật nào sẽ thành câu hỏi
mỗi lần sửa. Phạm vi đổi hoá ra hẹp hơn lo ngại — `SpwaTraCuuService.TraCuuTheoDanhTinh` chỉ có **đúng
một** nơi gọi (`SPWA_TraCuuHoSo`), và **nhánh đẩy tài liệu đã nghiệm thu đầu–cuối KHÔNG dùng luật gộp**
(nó đi bằng `MaBN`). Cửa đọc phải nhận thêm tham số `gioiTinh` và trả thêm trường cùng tên; đây là
thời điểm duy nhất đổi được rẻ, vì chưa có bên thứ ba nào gọi vào.

**Không sửa:** hai bảng số đo phía trên vẫn đúng nguyên văn, và vế thứ nhất của quyết định (một người
có nhiều hồ sơ tại cùng một cơ sở) không đổi.

# ADR 0034 — `MAX(PhienBan)` lấy trong phạm vi `LaBanMoiNhat = 1`, không quét mọi phiên bản

- **Trạng thái:** Chấp nhận — 17/09/2026
- **Bối cảnh đo:** `HIS_CSKH` @ 118.69.34.247,8392 sau khi dựng tải 593.297 dòng `QL_TaiLieuBenhNhan`
- **Liên quan:** ADR 0031 (dựng tải bằng bản sao thật) · `Database/29_FIX_MAX_PHIENBAN_QUET_BANG.sql`

## Bối cảnh

`QL_TaiLieuBenhNhan_Save` sinh số phiên bản bằng câu đầu tiên trong transaction:

```sql
SELECT @PhienBan = ISNULL(MAX(PhienBan), 0) + 1
FROM dbo.QL_TaiLieuBenhNhan WITH (UPDLOCK, HOLDLOCK)
WHERE IDCoSo = @IDCoSo AND LoaiTaiLieu = @LoaiTaiLieu AND MaNguonHIS = @MaNguonHIS;
```

Câu này **cố ý không** lọc `LaBanMoiNhat`, vì về mặt ý định nó cần `MAX` qua *mọi* phiên bản, kể cả bản
đã hạ cờ. Nhưng **cả hai** index khớp đúng ba cột lọc đều là index **CÓ LỌC** trên `LaBanMoiNhat = 1`
(`UK_QL_TaiLieuBenhNhan_Nguon`, `IX_QL_TaiLieuBenhNhan_Bam`) ⇒ không index nào dùng được ⇒ **quét toàn
bảng**, *bên trong một transaction đang giữ khoá `UPDLOCK` phạm vi*.

Ở 245 dòng thì vô hình. Ở 593k dòng thì đo được:

| | Trước | Sau khi thêm `AND LaBanMoiNhat = 1` |
|---|---|---|
| logical reads | **28.625** (25 scan song song) | **6** |
| CPU | 515 ms | ~0 ms |

Hệ quả ở tải thật (8 luồng, 800 lượt): **85 deadlock trong 40 phút**, tranh chấp `pagelock` chế độ **U**
kèm `exchangeEvent`, và **746/800 lượt ghi hỏng** (`ResultCode = 99`). Đây là bệnh của **thiết kế index**,
nặng dần tuyến tính theo số dòng, không biến mất khi mua máy mạnh hơn.

## Quyết định

Thêm `AND LaBanMoiNhat = 1` vào chính câu đó. **Một dòng.** Không index mới, không di trú, không đụng
một dòng C# nào.

## 🔴 Vì sao việc này KHÔNG phải là một lỗi (đọc kỹ trước khi định gỡ ra)

Câu trên **trông như** đã thu hẹp phạm vi tìm kiếm và bỏ sót các phiên bản cũ. Nó không, vì có một
**bất biến** mà thủ tục tự bảo toàn:

> Trong mỗi nhóm `(IDCoSo, LoaiTaiLieu, MaNguonHIS)`, dòng có `LaBanMoiNhat = 1` **luôn** là dòng giữ
> `MAX(PhienBan)`.

Bất biến này đứng được nhờ hai thứ, cả hai đều kiểm được:

1. **Thủ tục luôn hạ cờ mọi bản cũ rồi mới chèn bản mới với `PhienBan = MAX + 1` và cờ bằng 1.** Nhánh
   `@ID <> 0` (sửa) không đụng tới `PhienBan` lẫn `LaBanMoiNhat`.
2. **Thủ tục là đường ghi DUY NHẤT vào bảng.** Toàn bộ C# chỉ gọi qua
   `AdminStoredProcedureService.SaveTaiLieuBenhNhanAsync`; không có `Add/Update/Remove` của EF trên
   `TaiLieuBenhNhans`; `PhienBan` và `LaBanMoiNhat` **không được C# ghi ở bất kỳ đâu** (`LaBanMoiNhat`
   xuất hiện đúng 5 chỗ, cả 5 đều là ĐỌC).

Kiểm trên dữ liệu thật ngày 17/09/2026: **0 / 571.282 nhóm vi phạm**.

```sql
-- Câu canh gác. Phải luôn trả 0. Trả khác 0 ⇒ bất biến đã vỡ ⇒ bản vá này KHÔNG còn đúng.
SELECT COUNT(*) FROM (
  SELECT IDCoSo, LoaiTaiLieu, MaNguonHIS,
         MaxAll     = MAX(PhienBan),
         MaxMoiNhat = MAX(CASE WHEN LaBanMoiNhat = 1 THEN PhienBan END),
         SoMoiNhat  = SUM(CASE WHEN LaBanMoiNhat = 1 THEN 1 ELSE 0 END)
  FROM dbo.QL_TaiLieuBenhNhan WHERE MaNguonHIS IS NOT NULL
  GROUP BY IDCoSo, LoaiTaiLieu, MaNguonHIS) g
WHERE MaxMoiNhat IS NULL OR MaxAll <> MaxMoiNhat OR SoMoiNhat <> 1;
```

🔴 **Điều kiện để bản vá này còn đúng.** Nếu sau này có thêm **bất kỳ** đường ghi nào khác vào
`QL_TaiLieuBenhNhan` — EF trực tiếp, một thủ tục thứ hai, một script `UPDATE`/`DELETE` tay — thì phải
chạy lại câu canh gác ở trên **trước** khi tin vào bản vá. Đặc biệt: **xoá dòng phiên bản mới nhất** sẽ
làm bất biến vỡ và số phiên bản bị dùng lại.

## Các phương án đã cân và lý do loại

| Phương án | Lý do loại |
|---|---|
| Thêm index **không lọc** `(IDCoSo, LoaiTaiLieu, MaNguonHIS)` INCLUDE `(PhienBan)` | Nút thắt nằm ở **đường GHI**; mỗi index mới là thêm việc cho `INSERT/UPDATE/DELETE`. Chữa `SELECT` bằng cách bóp đường ghi là đi ngược hẳn bệnh đang có |
| **Tách bảng** `QL_TaiLieuNguon` + `QL_TaiLieuPhienBan`, bỏ cờ `LaBanMoiNhat` | Giá: di trú 593k dòng, viết lại thủ tục, sửa 5 điểm đọc C# + model EF + DbContext, làm zero-downtime trên DB đang có người ghi. Mà **cả hai** lý do biện minh đều sụp: (a) bệnh đường ghi có thuốc một dòng; (b) tách bảng **không** cứu đường đọc — Key Lookup vẫn còn, chỉ đổi thành join sang bảng kia |
| `MAXDOP` mức thủ tục để dập `exchangeEvent` | Che triệu chứng, giữ nguyên quét bảng. Và sau khi vá thì **vô nghĩa**: kế hoạch không còn toán tử song song nào (cost 0,0066 / 0,0479, ngưỡng song song là 5) |
| Bỏ `UPDLOCK/HOLDLOCK`, chấp nhận retry | Vứt bỏ thứ đang **giữ tính đúng đắn** để chữa một vấn đề đã có thuốc rẻ hơn |

## Hệ quả

- Kế hoạch sau vá (`SHOWPLAN_XML`): **0 toán tử Scan, 0 toán tử Parallelism**. `SELECT` → Index Seek
  `UK_QL_TaiLieuBenhNhan_Nguon`; `UPDATE` hạ cờ → Index Update một dòng (wide update Split/Sort/Collapse,
  bình thường vì nó đổi cột lọc của index).
- Khoá `UPDLOCK/HOLDLOCK` **vẫn giữ nguyên** và vẫn cần thiết, nhưng giờ đọng trên **đúng một key** thay
  vì gần như mọi trang của bảng ⇒ hai lượt ghi vào **cùng một** `MaNguonHIS` vẫn **chặn nhau** (đúng —
  đó là tuần tự hoá), còn hai `MaNguonHIS` khác nhau thì không đụng nhau nữa.
- 🔴 **Chưa đóng:** thủ tục vẫn **nuốt lỗi** (`CATCH` → `ResultCode = 99`), và `TaiLieuService` đã
  **upload tệp lên FTP trước** khi lưu metadata ⇒ mỗi lượt hỏng để lại một **tệp PDF mồ côi** trên FTP,
  và tầng chống trùng (tra theo `BamNoiDung` trong DB) **không** chặn được lần gửi lại. Toàn bộ codebase
  **không có một dòng retry nào**, không chỗ nào bắt `1205`. Bản vá này **không** chạm tới lỗ đó —
  nó là việc riêng.

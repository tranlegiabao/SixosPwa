# ĐỢT A — Tái cấu trúc HIS_CSKH: 23 → **16 bảng**

> Thứ tự chạy, đường lùi, và những chỗ đã đạp phải. Đã chạy thật trên
> `HIS_CSKH@118.69.34.247,8392` ngày **18-09-2026**.

## Thứ tự chạy — KHÔNG đổi

| # | File | Làm gì | Bắt buộc trước |
|---|---|---|---|
| 1 | `30_DOTA_01_snapshot.sql` | Chụp `bak2.*`: 9 bảng nguyên văn + cột sắp xoá của 7 bảng + **định nghĩa 37 stored** | — |
| 2 | `30_DOTA_02_DM_CSKCB_gop_4_bang.sql` | `DM_CSKCB` mới **32 cột**, gộp `DM_DoiTacApi` + `DM_CSKCB_QuangCao` + `HT_KhoFtpCoSo` + `HT_KhoaApiCoSo` | 1 |
| 2b | `30_DOTA_02b_tiep_tuc_sau_loi_FK.sql` | Chỉ dùng nếu bước 2 dừng giữa chừng (xem "Đã đạp phải" #1) | 2 |
| 3 | `30_DOTA_04_bo_3_bang_con_lai.sql` | Bỏ `HT_TaiKhoanDoiTac`, `HT_ThietBi`, `DM_GioiTinh` ⇒ **16 bảng** | 2 |
| 4 | `30_DOTA_05_bo_16_cot_chet.sql` | Bỏ 16 cột chết ở các bảng còn lại | 3 |
| 5 | `30_DOTA_06_HT_Config_va_GioiTinh.sql` | `UNIQUE(MaChucNang)`, gộp `SoLuong`→`GiaTri`, `DM_GioiTinh`→`CHECK` | 3 |
| 6 | `30_DOTA_07_stored_34_thanh_30.sql` | Stored 34 → 30 | 2,3,4,5 |
| 7 | `30_DOTA_09_don_bang_bak.sql` | *(tuỳ chọn)* dọn 29 bảng `bak.*` cũ | sau khi nghiệm thu |
| 8 | `30_DOTA_10_va_HT_TaiKhoan_Save.sql` | 🔴 **BẮT BUỘC** — trả lại tham số `@IDBenhNhan` kiểu *nhận rồi bỏ* | 6 |

🔴 **Bước 8 không phải tuỳ chọn.** Thiếu nó thì màn *Gửi cho bệnh nhân* bên HIS **chết**:
xem "Đã đạp phải" #6.

🔴 **TẮT APP TRƯỚC KHI CHẠY.** Đợt 2 (08/2026) có một dòng rác sinh ra giữa `V001` và `V002`
vì app đang chạy. Kiểm bằng:
```sql
SELECT session_id, program_name, host_name FROM sys.dm_exec_sessions
WHERE database_id = DB_ID('HIS_CSKH') AND is_user_process = 1 AND session_id <> @@SPID;
```

## Đường lùi

`bak2.*` (bước 1) giữ **nguyên văn** mọi thứ bị sửa/xoá, kể cả định nghĩa 37 stored ở
`bak2.StoredDinhNghia_A0`. Khôi phục = đọc ngược từ đó. **Không** cần file `.bak`.
Riêng bước 7 (`bak.*`) là **không khôi phục được** — chỉ còn manifest số dòng ở
`bak2.ManifestBakDaXoa_A09`.

## Đã đạp phải — đọc trước khi sửa mấy file này

1. 🔴 **`FK_HT_TaiKhoanDoiTac_CoSo` chặn `DROP TABLE dbo.DM_CSKCB`.** Bảng
   `HT_TaiKhoanDoiTac` mãi bước 3 mới chết, nhưng **khoá ngoại của nó chặn ngay ở bước 2**.
   File bước 2 đã được vá để gỡ khoá này; `02b` là để chạy tiếp nếu ai đó gặp bản chưa vá.
2. 🔴 **KHÔNG `sp_rename` khi bảng còn mang ràng buộc.** Đợt 2 gãy thật ở `V003`: `sp_rename`
   **không đổi tên ràng buộc**, nên bảng cũ đã thành `ZZ_*` mà khoá chính vẫn tên
   `PK_DM_DoiTacApi` → tạo bảng mới cùng tên ràng buộc → `Msg 2714` → **bảng không được tạo**,
   mất luôn 1 CHECK + 1 khoá ngoại.
   Khuôn đúng đang dùng: bảng mới **trần** (không ràng buộc) → copy → đối chiếu → bỏ bảng cũ
   (ràng buộc cũ chết theo bảng) → `sp_rename` **bảng** → mới đặt ràng buộc với tên chính thức.
3. 🔴 **Câu kiểm phải lọc `WHERE SCHEMA_NAME(schema_id) = 'dbo'`.** Đợt 2 có 5 lần báo động giả
   vì đếm cả `bak.*`.
4. **`HT_KhoaApiCoSo` là 1:N thật** — cơ sở `IDCoSo=8` có 2 khoá. Gộp về 1 cột giữ khoá
   `Active=1` + `NgayCap` mới nhất. **Mất khả năng xoay khoá không gián đoạn** — đây là cái giá
   đã chốt ở V12, và nó **đảo ADR 0022**.
5. **`DM_CSKCB_Delete` đang hỏng sẵn từ trước đợt A**: nó `DELETE FROM dbo.QL_LichSuKham` —
   bảng đó không còn tồn tại, nên xoá cơ sở là ném lỗi. Bước 6 sửa luôn, và bổ sung
   `QL_TaiLieuBenhNhan` + `QL_DotKham` mà bản cũ quên xoá.
6. 🔴🔴 **HIS KHÔNG chỉ gọi cổng qua HTTP.** Stored `Dev_Master3.dbo.S00_UploadOnline` gọi
   **thẳng sang CSDL cổng qua LINKED SERVER `SPWA_CONG`**, đi qua **6 cửa**:
   `DM_BenhNhan_Save` → `HT_TaiKhoan_Save` → `DM_BenhNhan_NhanChuSoHuu` →
   `DM_BenhNhanCoSo_Save` → `QL_DotKham_Save` → `QL_TaiLieuBenhNhan_Save`.
   **Đổi chữ ký bất kỳ stored nào trong 6 cái này là làm chết màn *Gửi cho bệnh nhân* bên HIS**,
   mà build vẫn xanh và không grep nào trong repo SixosPwa thấy được.
   Đã đạp thật: bỏ `@IDBenhNhan` ⇒ `Procedure or function HT_TaiKhoan_Save has too many
   arguments specified`. Bước 8 vá bằng **tiền lệ V14** (nhận rồi bỏ), cột vẫn xoá.
   ⇒ **Luật chung:** 6 stored này chỉ được **thêm tham số có giá trị mặc định**, không bao giờ bớt.

## Hai luật đừng gỡ

**🔒 Bốn cột bí mật KHÔNG map vào thực thể EF** (`Models/DMCSKCB.cs`):
`KhoaBam` · `Ftp_TaiKhoan` · `Ftp_MatKhau` · `KetNoi_KhoaGoiHIS`.
Lý do: 59 chỗ đọc `DM_CSKCB` qua EF và trang công khai nạp **trọn thực thể**
(`HomeController.cs:224` còn `ToListAsync()` mọi cơ sở), mà `Ftp_MatKhau` **lưu thô** (cố ý —
FTP cần đăng nhập lại được). Chỉ **3 chỗ** đọc chúng bằng SQL riêng:
`Services/KhoCoSoService.cs` · `Security/KhoaCoSoAttribute.cs` · `Services/His/HisDocService.cs`.
Gỡ luật này là **làm hệ kém an toàn hơn trước khi dọn**.

**NULL = GIỮ NGUYÊN trong `DM_CSKCB_Save`** với `@KhoaBam`, `@Ftp_MatKhau`, `@Ftp_TaiKhoan`,
`@KetNoi_KhoaGoiHIS`, `@Khoa_NgayHetHan`. Màn Admin không hiện lại mấy ô đó nên không gửi lại
giá trị; ghi thẳng là **mỗi lần sửa địa chỉ cơ sở lại xoá trắng khoá API / mật khẩu FTP**.

**Tham số stored GIỮ dù cột đã bỏ** (tiền lệ V14): `QL_TaiLieuBenhNhan_Save` giữ `@MaBN`,
`@DungLuongByte`, `@GhiChu`; `QL_DotKham_Save` giữ `@MaBN`, `@NgayGioRa`, `@ChanDoan`.
HIS đang gửi lên — **nhận rồi bỏ**, bỏ tham số là vỡ bên HIS.

## Nghiệm thu đã chạy (18-09)

| Phép | Kết quả |
|---|---|
| `COUNT(*) sys.tables WHERE schema='dbo'` | **16** ✅ |
| `dotnet build SixosPwa.sln` | **0 Error(s)** ✅ |
| Đối chiếu 12 cơ sở / 8 kết nối / 11 quảng cáo / 1 FTP / 4 khoá API | khớp **hoàn toàn** ✅ |
| Chuỗi khoá HIS↔Cổng (cả 2 chiều) | **KHỚP** ✅ |
| `POST /api/v1/tai-lieu/tiep-nhan` khoá thật | **200** + đúng **1** dòng `HT_LogApiCoSo` ✅ |
| Stored còn tham chiếu bảng đã chết | **0** ✅ |

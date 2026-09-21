/* =============================================================================
   DOT A - buoc A7: bo 16 cot chet o cac bang con lai (V11b -> V16).
   Chay SAU A02/A04. Truoc moi cot phai go rang buoc/chi muc con vuong.
   Duong lui: bak2.CotXoa_* da chup o A01 -> xem R05.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
/* --- 1. DM_BenhNhanCoSo: 2 cot chup tu HIS, 0 tham chieu C# va stored ---- */
IF COL_LENGTH('dbo.DM_BenhNhanCoSo','TenBN_ChupTuHIS')    IS NOT NULL
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN TenBN_ChupTuHIS;
IF COL_LENGTH('dbo.DM_BenhNhanCoSo','NgaySinh_ChupTuHIS') IS NOT NULL
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP COLUMN NgaySinh_ChupTuHIS;
GO
/* --- 2. DM_DoiTac.IDPM: chi co property + map cot, luon truyen hang NULL -- */
IF COL_LENGTH('dbo.DM_DoiTac','IDPM') IS NOT NULL
    ALTER TABLE dbo.DM_DoiTac DROP COLUMN IDPM;
GO
/* --- 3. HT_PushDangKy.IDThietBi: vuong FK sang HT_ThietBi ---------------- */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HT_PushDangKy_ThietBi')
    ALTER TABLE dbo.HT_PushDangKy DROP CONSTRAINT FK_HT_PushDangKy_ThietBi;
IF COL_LENGTH('dbo.HT_PushDangKy','IDThietBi') IS NOT NULL
    ALTER TABLE dbo.HT_PushDangKy DROP COLUMN IDThietBi;
GO
/* --- 4. HT_TaiKhoan.IDBenhNhan: thi hanh not ADR 0019 ------------------- */
/* Chieu dung la DM_BenhNhan.IDTaiKhoan (1-N). Cot cu la 1-1 nen khong dien
   dat duoc mo hinh that => Dashboard chi thay MOT ho so tuy y. */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HT_TaiKhoan_BenhNhan')
    ALTER TABLE dbo.HT_TaiKhoan DROP CONSTRAINT FK_HT_TaiKhoan_BenhNhan;
IF COL_LENGTH('dbo.HT_TaiKhoan','IDBenhNhan') IS NOT NULL
    ALTER TABLE dbo.HT_TaiKhoan DROP COLUMN IDBenhNhan;
GO
/* --- 5. HT_LogApiCoSo.IDKhoa: he qua cua viec bo HT_KhoaApiCoSo --------- */
/* Khong co FK (co y: nhat ky phai ghi duoc ca khi khoa sai). */
IF COL_LENGTH('dbo.HT_LogApiCoSo','IDKhoa') IS NOT NULL
    ALTER TABLE dbo.HT_LogApiCoSo DROP COLUMN IDKhoa;
GO
/* --- 6. QL_DotKham: 3 cot ghi ma khong ai doc -------------------------- */
/* MaBN suy ra duoc qua IDBenhNhanCoSo; NgayGioRa/ChanDoan: 0 cho hien thi,
   0 truy van loc theo. THAM SO stored VAN GIU (HIS dang gui len - V14). */
IF COL_LENGTH('dbo.QL_DotKham','MaBN')      IS NOT NULL ALTER TABLE dbo.QL_DotKham DROP COLUMN MaBN;
IF COL_LENGTH('dbo.QL_DotKham','NgayGioRa') IS NOT NULL ALTER TABLE dbo.QL_DotKham DROP COLUMN NgayGioRa;
IF COL_LENGTH('dbo.QL_DotKham','ChanDoan')  IS NOT NULL ALTER TABLE dbo.QL_DotKham DROP COLUMN ChanDoan;
GO
/* --- 7. QL_TaiLieuBenhNhan: 3 cot, MaBN con vuong CHI MUC -------------- */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN'
                                       AND object_id = OBJECT_ID('dbo.QL_TaiLieuBenhNhan'))
    DROP INDEX IX_QL_TaiLieuBenhNhan_IdCoSo_MaBN ON dbo.QL_TaiLieuBenhNhan;
GO
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan','MaBN')          IS NOT NULL ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN MaBN;
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan','DungLuongByte') IS NOT NULL ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN DungLuongByte;
IF COL_LENGTH('dbo.QL_TaiLieuBenhNhan','GhiChu')        IS NOT NULL ALTER TABLE dbo.QL_TaiLieuBenhNhan DROP COLUMN GhiChu;
GO
/* --- tu kiem: 16 cot phai bien mat, khong con cot nao sot -------------- */
SELECT 'A05 con sot' AS Buoc, t.name AS Bang, c.name AS Cot
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
WHERE (t.name = 'DM_BenhNhanCoSo'    AND c.name IN ('TenBN_ChupTuHIS','NgaySinh_ChupTuHIS'))
   OR (t.name = 'DM_DoiTac'          AND c.name = 'IDPM')
   OR (t.name = 'HT_PushDangKy'      AND c.name = 'IDThietBi')
   OR (t.name = 'HT_TaiKhoan'        AND c.name = 'IDBenhNhan')
   OR (t.name = 'HT_LogApiCoSo'      AND c.name = 'IDKhoa')
   OR (t.name = 'QL_DotKham'         AND c.name IN ('MaBN','NgayGioRa','ChanDoan'))
   OR (t.name = 'QL_TaiLieuBenhNhan' AND c.name IN ('MaBN','DungLuongByte','GhiChu'));
GO
SELECT 'A05 xong' AS Buoc,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo') AS SoBangDbo,
       (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
        WHERE SCHEMA_NAME(t.schema_id) = 'dbo')                                AS TongSoCotDbo;
GO

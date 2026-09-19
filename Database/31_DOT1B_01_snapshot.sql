/* =============================================================================
   DOT 1B - buoc B01: CHUP ANH TRUOC KHI SUA (duong lui duy nhat)
   Chup nguyen van 6 bang se bi sua/xoa sang schema bak3, + dinh nghia stored.
   KHONG chup QL_TaiLieuBenhNhan (614k) va QL_DotKham (84k): B03 chi sp_rename
   COT, khong UPDATE dong nao, nen khong can ban sao du lieu.
   Chay lai duoc nhieu lan.
   ========================================================================== */
SET NOCOUNT ON;
GO
IF SCHEMA_ID('bak3') IS NULL EXEC('CREATE SCHEMA bak3');
GO
IF OBJECT_ID('bak3.DM_BenhNhan_B01')     IS NOT NULL DROP TABLE bak3.DM_BenhNhan_B01;
IF OBJECT_ID('bak3.DM_BenhNhanCoSo_B01') IS NOT NULL DROP TABLE bak3.DM_BenhNhanCoSo_B01;
IF OBJECT_ID('bak3.DM_DoiTac_B01')       IS NOT NULL DROP TABLE bak3.DM_DoiTac_B01;
IF OBJECT_ID('bak3.HT_TaiKhoan_B01')     IS NOT NULL DROP TABLE bak3.HT_TaiKhoan_B01;
IF OBJECT_ID('bak3.HT_ThongBao_B01')     IS NOT NULL DROP TABLE bak3.HT_ThongBao_B01;
IF OBJECT_ID('bak3.HT_PushDangKy_B01')   IS NOT NULL DROP TABLE bak3.HT_PushDangKy_B01;
IF OBJECT_ID('bak3.DM_CSKCB_CongTy_B01') IS NOT NULL DROP TABLE bak3.DM_CSKCB_CongTy_B01;
GO
SELECT * INTO bak3.DM_BenhNhan_B01     FROM dbo.DM_BenhNhan;
SELECT * INTO bak3.DM_BenhNhanCoSo_B01 FROM dbo.DM_BenhNhanCoSo;
SELECT * INTO bak3.DM_DoiTac_B01       FROM dbo.DM_DoiTac;
SELECT * INTO bak3.HT_TaiKhoan_B01     FROM dbo.HT_TaiKhoan;
SELECT * INTO bak3.HT_ThongBao_B01     FROM dbo.HT_ThongBao;
SELECT * INTO bak3.HT_PushDangKy_B01   FROM dbo.HT_PushDangKy;
/* chi cot sap bi xoa cua DM_CSKCB (B08 bo IDCongTy) */
SELECT ID, IDCongTy INTO bak3.DM_CSKCB_CongTy_B01 FROM dbo.DM_CSKCB;
GO
/* --- ban sao dinh nghia 30 stored hien tai ------------------------------ */
IF OBJECT_ID('bak3.StoredDinhNghia_B01') IS NOT NULL DROP TABLE bak3.StoredDinhNghia_B01;
SELECT SCHEMA_NAME(o.schema_id) AS Sch, o.name AS Ten, o.type_desc AS Loai,
       OBJECT_DEFINITION(o.object_id) AS DinhNghia, GETDATE() AS NgayChup
INTO bak3.StoredDinhNghia_B01
FROM sys.objects o WHERE o.type IN ('P','FN','IF','TF') AND o.is_ms_shipped = 0;
GO
/* --- tu kiem: phai khop so do nen do ngay 19-09-2026 -------------------- */
SELECT 'B01 snapshot' AS Buoc,
       (SELECT COUNT(*) FROM bak3.DM_BenhNhan_B01)     AS BenhNhan_ky_vong_20178,
       (SELECT COUNT(*) FROM bak3.DM_BenhNhanCoSo_B01) AS BenhNhanCoSo_ky_vong_24169,
       (SELECT COUNT(*) FROM bak3.DM_DoiTac_B01)       AS DoiTac_ky_vong_2,
       (SELECT COUNT(*) FROM bak3.HT_TaiKhoan_B01)     AS TaiKhoan_ky_vong_13154,
       (SELECT COUNT(*) FROM bak3.HT_ThongBao_B01)     AS ThongBao_ky_vong_112,
       (SELECT COUNT(*) FROM bak3.HT_PushDangKy_B01)   AS Push_ky_vong_29,
       (SELECT COUNT(*) FROM bak3.StoredDinhNghia_B01) AS Stored_ky_vong_30,
       (SELECT MAX(ID) FROM bak3.DM_BenhNhanCoSo_B01)  AS MaxID_BNCS_ky_vong_24481;
GO

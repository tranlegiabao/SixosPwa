/* =============================================================================
   DOT A - buoc A0: CHUP ANH TRUOC KHI SUA (duong lui)
   Chup nguyen van 12 bang se bi sua/xoa sang schema bak2. Chay lai duoc nhieu lan.
   Rieng 3 bang lon (QL_TaiLieuBenhNhan / QL_DotKham / DM_BenhNhanCoSo) chi chup
   CAC COT SAP BI XOA + khoa chinh, de khong nhan doi 614k dong.
   ========================================================================== */
SET NOCOUNT ON;
GO
IF SCHEMA_ID('bak2') IS NULL EXEC('CREATE SCHEMA bak2');
GO
/* --- ban day du cua cac bang se bi XOA hoac TAI TAO --------------------- */
IF OBJECT_ID('bak2.DM_CSKCB_A0')          IS NOT NULL DROP TABLE bak2.DM_CSKCB_A0;
IF OBJECT_ID('bak2.DM_DoiTacApi_A0')      IS NOT NULL DROP TABLE bak2.DM_DoiTacApi_A0;
IF OBJECT_ID('bak2.DM_CSKCB_QuangCao_A0') IS NOT NULL DROP TABLE bak2.DM_CSKCB_QuangCao_A0;
IF OBJECT_ID('bak2.HT_KhoFtpCoSo_A0')     IS NOT NULL DROP TABLE bak2.HT_KhoFtpCoSo_A0;
IF OBJECT_ID('bak2.HT_KhoaApiCoSo_A0')    IS NOT NULL DROP TABLE bak2.HT_KhoaApiCoSo_A0;
IF OBJECT_ID('bak2.HT_TaiKhoanDoiTac_A0') IS NOT NULL DROP TABLE bak2.HT_TaiKhoanDoiTac_A0;
IF OBJECT_ID('bak2.HT_ThietBi_A0')        IS NOT NULL DROP TABLE bak2.HT_ThietBi_A0;
IF OBJECT_ID('bak2.DM_GioiTinh_A0')       IS NOT NULL DROP TABLE bak2.DM_GioiTinh_A0;
IF OBJECT_ID('bak2.HT_Config_A0')         IS NOT NULL DROP TABLE bak2.HT_Config_A0;
GO
SELECT * INTO bak2.DM_CSKCB_A0          FROM dbo.DM_CSKCB;
SELECT * INTO bak2.DM_DoiTacApi_A0      FROM dbo.DM_DoiTacApi;
SELECT * INTO bak2.DM_CSKCB_QuangCao_A0 FROM dbo.DM_CSKCB_QuangCao;
SELECT * INTO bak2.HT_KhoFtpCoSo_A0     FROM dbo.HT_KhoFtpCoSo;
SELECT * INTO bak2.HT_KhoaApiCoSo_A0    FROM dbo.HT_KhoaApiCoSo;
SELECT * INTO bak2.HT_TaiKhoanDoiTac_A0 FROM dbo.HT_TaiKhoanDoiTac;
SELECT * INTO bak2.HT_ThietBi_A0        FROM dbo.HT_ThietBi;
SELECT * INTO bak2.DM_GioiTinh_A0       FROM dbo.DM_GioiTinh;
SELECT * INTO bak2.HT_Config_A0         FROM dbo.HT_Config;
GO
/* --- chi cac cot sap bi XOA cua 5 bang con lai -------------------------- */
IF OBJECT_ID('bak2.CotXoa_HT_TaiKhoan_A0')       IS NOT NULL DROP TABLE bak2.CotXoa_HT_TaiKhoan_A0;
IF OBJECT_ID('bak2.CotXoa_HT_PushDangKy_A0')     IS NOT NULL DROP TABLE bak2.CotXoa_HT_PushDangKy_A0;
IF OBJECT_ID('bak2.CotXoa_DM_DoiTac_A0')         IS NOT NULL DROP TABLE bak2.CotXoa_DM_DoiTac_A0;
IF OBJECT_ID('bak2.CotXoa_DM_BenhNhanCoSo_A0')   IS NOT NULL DROP TABLE bak2.CotXoa_DM_BenhNhanCoSo_A0;
IF OBJECT_ID('bak2.CotXoa_QL_DotKham_A0')        IS NOT NULL DROP TABLE bak2.CotXoa_QL_DotKham_A0;
IF OBJECT_ID('bak2.CotXoa_QL_TaiLieuBenhNhan_A0')IS NOT NULL DROP TABLE bak2.CotXoa_QL_TaiLieuBenhNhan_A0;
IF OBJECT_ID('bak2.CotXoa_HT_LogApiCoSo_A0')     IS NOT NULL DROP TABLE bak2.CotXoa_HT_LogApiCoSo_A0;
GO
SELECT ID, IDBenhNhan                       INTO bak2.CotXoa_HT_TaiKhoan_A0        FROM dbo.HT_TaiKhoan       WHERE IDBenhNhan IS NOT NULL;
SELECT ID, IDThietBi                        INTO bak2.CotXoa_HT_PushDangKy_A0      FROM dbo.HT_PushDangKy     WHERE IDThietBi  IS NOT NULL;
SELECT ID, IDPM                             INTO bak2.CotXoa_DM_DoiTac_A0          FROM dbo.DM_DoiTac;
SELECT ID, TenBN_ChupTuHIS, NgaySinh_ChupTuHIS INTO bak2.CotXoa_DM_BenhNhanCoSo_A0 FROM dbo.DM_BenhNhanCoSo   WHERE TenBN_ChupTuHIS IS NOT NULL OR NgaySinh_ChupTuHIS IS NOT NULL;
SELECT ID, MaBN, NgayGioRa, ChanDoan        INTO bak2.CotXoa_QL_DotKham_A0         FROM dbo.QL_DotKham;
SELECT ID, MaBN, DungLuongByte, GhiChu      INTO bak2.CotXoa_QL_TaiLieuBenhNhan_A0 FROM dbo.QL_TaiLieuBenhNhan;
SELECT ID, IDKhoa                           INTO bak2.CotXoa_HT_LogApiCoSo_A0      FROM dbo.HT_LogApiCoSo     WHERE IDKhoa IS NOT NULL;
GO
/* --- ban sao dinh nghia stored hien tai (de doi chieu / khoi phuc) ------ */
IF OBJECT_ID('bak2.StoredDinhNghia_A0') IS NOT NULL DROP TABLE bak2.StoredDinhNghia_A0;
SELECT SCHEMA_NAME(o.schema_id) AS Sch, o.name AS Ten, o.type_desc AS Loai,
       OBJECT_DEFINITION(o.object_id) AS DinhNghia, GETDATE() AS NgayChup
INTO bak2.StoredDinhNghia_A0
FROM sys.objects o WHERE o.type IN ('P','FN','IF','TF') AND o.is_ms_shipped = 0;
GO
/* --- tu kiem ------------------------------------------------------------ */
SELECT 'A01 snapshot' AS Buoc,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_A0)          AS CSKCB,
       (SELECT COUNT(*) FROM bak2.DM_DoiTacApi_A0)      AS DoiTacApi,
       (SELECT COUNT(*) FROM bak2.DM_CSKCB_QuangCao_A0) AS QuangCao,
       (SELECT COUNT(*) FROM bak2.HT_KhoFtpCoSo_A0)     AS KhoFtp,
       (SELECT COUNT(*) FROM bak2.HT_KhoaApiCoSo_A0)    AS KhoaApi,
       (SELECT COUNT(*) FROM bak2.StoredDinhNghia_A0)   AS Stored;
GO

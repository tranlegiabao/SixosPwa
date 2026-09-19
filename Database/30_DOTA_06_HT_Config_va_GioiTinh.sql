/* =============================================================================
   DOT A - buoc A8: va HT_Config + bien DM_GioiTinh thanh rang buoc CHECK.
   Chay SAU A04 (A04 la file xoa 3 bang HT_TaiKhoanDoiTac/HT_ThietBi/DM_GioiTinh).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
/* --- 1. HT_Config: gop SoLuong vao GiaTri ------------------------------- */
/* Du lieu that luc do (18-09): 1 dong, ca SoLuong lan GiaTri deu NULL, chi
   HieuLuc duoc dung => gop an toan. Van copy cho chac neu chay lai sau nay. */
IF COL_LENGTH('dbo.HT_Config','SoLuong') IS NOT NULL
BEGIN
    UPDATE dbo.HT_Config SET GiaTri = SoLuong WHERE GiaTri IS NULL AND SoLuong IS NOT NULL;

    /* Con dong nao mang HAI gia tri khac nhau thi DUNG LAI, khong am tham mat. */
    IF EXISTS (SELECT 1 FROM dbo.HT_Config WHERE SoLuong IS NOT NULL AND GiaTri IS NOT NULL AND SoLuong <> GiaTri)
    BEGIN
        RAISERROR(N'A06 DUNG LAI: co dong HT_Config co SoLuong <> GiaTri, phai xu ly tay truoc.', 16, 1);
        RETURN;
    END;

    ALTER TABLE dbo.HT_Config DROP COLUMN SoLuong;
END;
GO
/* --- 2. HT_Config.MaChucNang: khoa tra cuu phai DUY NHAT ---------------- */
/* HTConfigService.cs:46 tra bang FirstOrDefaultAsync tren khoa khong UNIQUE
   => trung khoa la am tham lay mot dong. Dat UNIQUE roi doi C# sang
   SingleOrDefaultAsync de trung khoa NO RA chu khong im lang. */
IF EXISTS (SELECT 1 FROM dbo.HT_Config WHERE MaChucNang IS NULL)
BEGIN
    RAISERROR(N'A06 DUNG LAI: HT_Config con dong MaChucNang NULL.', 16, 1);
    RETURN;
END;
IF EXISTS (SELECT 1 FROM dbo.HT_Config GROUP BY MaChucNang HAVING COUNT(*) > 1)
BEGIN
    RAISERROR(N'A06 DUNG LAI: HT_Config co MaChucNang trung.', 16, 1);
    RETURN;
END;
GO
IF COL_LENGTH('dbo.HT_Config','MaChucNang') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HT_Config')
                                           AND name = 'MaChucNang' AND is_nullable = 1)
    ALTER TABLE dbo.HT_Config ALTER COLUMN MaChucNang nvarchar(100) NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_HT_Config_MaChucNang'
                                           AND object_id = OBJECT_ID('dbo.HT_Config'))
    ALTER TABLE dbo.HT_Config ADD CONSTRAINT UK_HT_Config_MaChucNang UNIQUE (MaChucNang);
GO
/* --- 3. DM_GioiTinh -> CHECK tren DM_BenhNhan.GioiTinh ------------------ */
/* Bang danh muc 3 dong khong dang mot bang. Gia tri that dang co trong
   DM_BenhNhan.GioiTinh phai duoc doi chieu TRUOC khi dat chot. */
SELECT 'A06 gia tri GioiTinh dang co trong DM_BenhNhan' AS Buoc,
       ISNULL(GioiTinh, N'(NULL)') AS GiaTri, COUNT(*) AS SoDong
FROM dbo.DM_BenhNhan GROUP BY GioiTinh ORDER BY COUNT(*) DESC;
GO
/* Chot chi dat khi MOI gia tri hien co deu nam trong tap cho phep.
   Tap nay lay tu dung bang DM_GioiTinh cu: 1=Nam, 2=Nu, 3=Khong xac dinh,
   cong them cac chuoi chu ma code dang ghi (neu co) - xem ket qua tren. */
IF NOT EXISTS (
    SELECT 1 FROM dbo.DM_BenhNhan
    WHERE GioiTinh IS NOT NULL
      AND GioiTinh NOT IN ('1','2','3','Nam',N'Nữ','Nu',N'Không xác định','KXD'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DM_BenhNhan_GioiTinh')
        ALTER TABLE dbo.DM_BenhNhan WITH CHECK ADD CONSTRAINT CK_DM_BenhNhan_GioiTinh
            CHECK (GioiTinh IS NULL OR GioiTinh IN ('1','2','3','Nam',N'Nữ','Nu',N'Không xác định','KXD'));
    SELECT N'A06: da dat CK_DM_BenhNhan_GioiTinh' AS KetQua;
END
ELSE
    SELECT N'A06 BO QUA chot GioiTinh: con gia tri ngoai tap cho phep (xem bang tren)' AS KetQua;
GO
/* --- tu kiem ----------------------------------------------------------- */
SELECT 'A06 xong' AS Buoc,
       (SELECT COUNT(*) FROM sys.indexes WHERE name = 'UK_HT_Config_MaChucNang')     AS CoUkConfig,
       (SELECT COUNT(*) FROM sys.check_constraints WHERE name = 'CK_DM_BenhNhan_GioiTinh') AS CoCkGioiTinh,
       (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HT_Config') AND name = 'SoLuong') AS ConSoLuong;
GO

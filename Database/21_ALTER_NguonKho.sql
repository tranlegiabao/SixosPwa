-- ============================================================================
-- 21 — QL_TaiLieuBenhNhan.NguonKho: tai lieu nay nam o KHO NAO
--
--   'CONG' = kho FTP cua chinh cong (sixospwa/...), cong tu ghi tu ngay dau.
--   'COSO' = "Kho phieu co so" — FTP cua phong kham, cong CHI DOC (che do
--            TRO DUONG, ADR 0030).
--
-- Vi sao phai co cot, khong suy tu chuoi (chot 35):
--   Hai kho cung ton tai cho CUNG MOT co so. Suy kho bang cach do tien to
--   "sixospwa/" la luat ngam nam trong chuoi — doc nham kho thi khong bao gi
--   ca, chi ra 404 ma khong ai hieu tai sao.
--
-- Do 12/09 tren HIS_CSKH: 113 tai lieu, 113/113 deu o kho cong => migration
-- chi la mot cau UPDATE, khong co ca bien.
--
-- Chay mot lan, idempotent. Rollback o 99_ROLLBACK.sql.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.QL_TaiLieuBenhNhan') AND name = 'NguonKho')
BEGIN
    -- NOT NULL + DEFAULT trong cung cau ALTER => 113 dong cu tu nhan N'CONG'.
    ALTER TABLE dbo.QL_TaiLieuBenhNhan
        ADD NguonKho nvarchar(20) NOT NULL
            CONSTRAINT DF_QL_TaiLieuBenhNhan_NguonKho DEFAULT (N'CONG');
END;
GO

-- Luoi an toan: neu ai do da them cot bang tay ma khong co DEFAULT.
UPDATE dbo.QL_TaiLieuBenhNhan
SET NguonKho = N'CONG'
WHERE NguonKho IS NULL OR LTRIM(RTRIM(NguonKho)) = '';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_QL_TaiLieuBenhNhan_NguonKho')
BEGIN
    ALTER TABLE dbo.QL_TaiLieuBenhNhan
        ADD CONSTRAINT CK_QL_TaiLieuBenhNhan_NguonKho
            CHECK (NguonKho IN (N'CONG', N'COSO'));
END;
GO

-- Doi chieu sau khi chay: phai ra 113 / 0 tren HIS_CSKH ngay 12/09.
-- SELECT NguonKho, COUNT(*) FROM dbo.QL_TaiLieuBenhNhan GROUP BY NguonKho;

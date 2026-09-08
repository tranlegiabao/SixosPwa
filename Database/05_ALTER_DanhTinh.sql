-- ============================================================================
-- 05 — Tra no schema cua Dot 1 (§C) tren DM_BenhNhan / DM_BenhNhanCoSo
--
-- CHI THEM COT + NOI RANG BUOC. KHONG doi luong dang nhap, KHONG doi khoa tra
-- cuu cua DM_BenhNhanCoSo_Save trong dot nay — xem ghi chu o cuoi file.
--
-- 🔴 CHAY LAI DUOC. Lan chay dau (08/09) gay o buoc bo rang buoc: hai cai
--    UK_DM_BenhNhanCoSo_* la UNIQUE CONSTRAINT chu khong phai index thuong, nen
--    DROP INDEX bao Msg 3723. Moi GO la mot batch rieng nen phan truoc do van
--    chay: 6 cot moi da them, va MaBN da nullable (doi nullability tren cot chi
--    co unique constraint thuong thi khong bi chan — chi index CO BO LOC moi
--    chan, dung cai da lam 04 gay). Con lai dung hai rang buoc. Chay lai ca file.
--
-- Vi sao:
--   * Luat gop chay o SixosPwa, HIS tra THO (chot 1 dot 1) => cong phai co cho
--     giu NgaySinh + ten khong dau de chay luat gop 3 o.
--   * DM_BenhNhan.HoTenKhongDau ben HIS RONG 100% (74.725/74.725) => cong tu
--     chuan hoa luc chay, cot nay la cho luu ket qua chuan hoa cua CONG.
--   * Moi ho so giu BAN CHUP ten/ngay sinh rieng: hai ho so cung mot con nguoi
--     o hai co so co the mang hai cach viet ten khac nhau.
--   * Cua tai lieu (chot 9 dot 1, ADR 0020) can mot co tren tung HO SO.
--   * Bo UK_DM_BenhNhanCoSo_HoSo: mot nguoi tai MOT co so van co the co NHIEU
--     ho so — do that 17,3% benh nhan Thien Nam co >=2 MaBN (8.454 nguoi).
--     Rang buoc dung la (IDCoSo, MaBN), va no da co san.
-- ============================================================================

-- --- DM_BenhNhan -------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'NgaySinh')
    ALTER TABLE dbo.DM_BenhNhan ADD NgaySinh datetime NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'HoTenKhongDau')
    ALTER TABLE dbo.DM_BenhNhan ADD HoTenKhongDau nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhan') AND name = 'IDTaiKhoan')
    ALTER TABLE dbo.DM_BenhNhan ADD IDTaiKhoan bigint NULL;
GO

-- --- DM_BenhNhanCoSo ---------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'TenBN_ChupTuHIS')
    ALTER TABLE dbo.DM_BenhNhanCoSo ADD TenBN_ChupTuHIS nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'NgaySinh_ChupTuHIS')
    ALTER TABLE dbo.DM_BenhNhanCoSo ADD NgaySinh_ChupTuHIS datetime NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND name = 'DaMoTaiLieu')
    ALTER TABLE dbo.DM_BenhNhanCoSo ADD DaMoTaiLieu bit NOT NULL CONSTRAINT DF_DM_BenhNhanCoSo_DaMoTaiLieu DEFAULT (0);
GO

-- --- Rang buoc ---------------------------------------------------------------
-- 🔴 Hai rang buoc nay la UNIQUE CONSTRAINT chu khong phai index thuong
-- (sys.indexes.is_unique_constraint = 1). DROP INDEX se bao Msg 3723
-- "An explicit DROP INDEX is not allowed... used for UNIQUE KEY constraint
-- enforcement" — phai ALTER TABLE DROP CONSTRAINT. Van giu nhanh DROP INDEX
-- cho truong hop CSDL khac dung index thuong cung ten.

-- Bo rang buoc "mot nguoi mot ho so tai mot co so" (trai voi du lieu that:
-- 17,3% benh nhan Thien Nam co >=2 MaBN).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_HoSo'
           AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND is_unique_constraint = 1)
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP CONSTRAINT UK_DM_BenhNhanCoSo_HoSo;
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_HoSo'
                AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
    DROP INDEX UK_DM_BenhNhanCoSo_HoSo ON dbo.DM_BenhNhanCoSo;
GO

-- MaBN cho phep rong: ho so TU KHAI chua noi HIS thi khong co ma nao ca
-- (chot 12 dot 1). Rang buoc duy nhat doi sang unique CO LOC.
--
-- Go index TRUOC, va o BATCH RIENG. Khong ALTER COLUMN duoc chung nao con index
-- bam vao cot — dung cai bay da lam script 04 gay o lan chay dau (Msg 5074).
-- Cot MaBN nay chi co UK_DM_BenhNhanCoSo_MaBN (IDCoSo, MaBN) bam vao; da kiem
-- toan bo sys.indexes cua bang, khong con index nao khac cham toi no.
-- Cai cu la unique constraint tren (IDCoSo, MaBN) — constraint KHONG co bo loc
-- duoc, ma ta can bo loc "WHERE MaBN IS NOT NULL" de nhieu ho so tu khai cung
-- de trong MaBN. Nen phai bo constraint roi dung lai bang INDEX co loc.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
           AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND is_unique_constraint = 1)
    ALTER TABLE dbo.DM_BenhNhanCoSo DROP CONSTRAINT UK_DM_BenhNhanCoSo_MaBN;
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
                AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo') AND has_filter = 0)
    DROP INDEX UK_DM_BenhNhanCoSo_MaBN ON dbo.DM_BenhNhanCoSo;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo')
           AND name = 'MaBN' AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.DM_BenhNhanCoSo ALTER COLUMN MaBN varchar(20) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_DM_BenhNhanCoSo_MaBN'
               AND object_id = OBJECT_ID(N'dbo.DM_BenhNhanCoSo'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UK_DM_BenhNhanCoSo_MaBN
        ON dbo.DM_BenhNhanCoSo (IDCoSo, MaBN)
        WHERE MaBN IS NOT NULL;
END;
GO

-- ============================================================================
-- CO Y KHONG LAM TRONG DOT NAY — de lai cho dot refactor "mot tai khoan nhieu
-- ho so" (chot 2 dot 1), vi lam som se hong luong dang nhap dang chay:
--
--   1. KHONG doi khoa tra cuu cua DM_BenhNhanCoSo_Save tu (IDBenhNhan, IDCoSo)
--      sang (IDCoSo, MaBN). Doi bay gio, trong khi dang ky van goi
--      SinhMaBenhNhan() sinh ma moi moi lan, thi MOI lan luu se de mot ho so
--      MOI thay vi cap nhat ho so cu.
--   2. KHONG don 18/21 dong MaBN tu bia (BN-yyyyMMdd-####) ve NULL. Man danh
--      sach tai lieu dang loc "t.MaBN == maBn"; NULL hoa ngay se lam phep so
--      sanh do luon sai. Don sau khi tang C# da va (V5/V6).
--
-- Hai viec tren di cung nhau va di cung viec xoa SinhMaBenhNhan().
-- ============================================================================

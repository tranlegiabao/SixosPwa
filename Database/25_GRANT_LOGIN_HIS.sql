-- ============================================================================
-- 25 — Login rieng cho HIS tren HIS_CSKH (chot 52)
--
-- Che do Tro duong: HIS EXEC stored cua cong qua linked server SPWA_CONG.
-- Login duoi day la danh tinh ma linked server dung de dang nhap sang.
--
-- 🔴 LUAT CUNG — quyen it nhat co the:
--      GRANT EXECUTE tren DUNG 7 stored duoi day
--      0 GRANT SELECT tren bat ky bang nao
--      0 quyen ghi bang (INSERT/UPDATE/DELETE)
--      KHONG db_owner, KHONG db_datareader, KHONG db_datawriter
--
--    Vi sao chat den vay: HIS_CSKH dung chung cho 12 co so. DM_BenhNhan va
--    HT_TaiKhoan la bang TOAN HE THONG. GRANT SELECT cho login cua phong kham A
--    la A doc duoc benh nhan + danh ba so dien thoai cua B, C, D.
--    Cac stored ghi van chay duoc vi OWNERSHIP CHAINING: stored va bang cung
--    chu (dbo), nen quyen EXECUTE stored la du — khong can quyen tren bang.
--
-- 🔴 Vi the: MOI DUONG GHI PHAI DI QUA STORED. Ngay nao co ai viet mot cau
--    INSERT tho tu HIS sang, no se bi tu choi — DO LA DUNG, dung "va" bang
--    cach GRANT them quyen bang.
--
-- Khuon: 90_CAP_KHOA_THU_NGHIEM.sql (tien le cap bi mat/quyen bang .sql).
-- Chay MOT LAN, tay nguoi quan tri CSDL (can quyen securityadmin tren server).
-- ============================================================================

SET NOCOUNT ON;
GO

-- --- CHAY THE NAO ----------------------------------------------------------
-- File nay can SQLCMD MODE (SSMS: Query > SQLCMD Mode).
--
-- 🔴 MAT KHAU CO Y KHONG NAM TRONG FILE. Truyen luc chay, de no khong bao gio
--    cham dia va khong bao gio len git:
--
--      sqlcmd -f 65001 -S <host,port> -d HIS_CSKH -U <dba> -P <pw> -N -C
--             -v MatKhau="<mat khau cho login spwa_his>" -i 25_GRANT_LOGIN_HIS.sql
--
--    Trong SSMS SQLCMD Mode thi go tay mot dong o dau query:
--      :setvar MatKhau "<mat khau>"
--
--    Thieu bien nay sqlcmd dung han voi "variable is not defined" -- do la chot
--    chan, khong phai loi. Mat khau phai TRUNG voi @MatKhau o file ben HIS:
--    <ho so task>/sql/10_TAO_LINKED_SERVER_SPWA_CONG.sql
:setvar TenLogin "spwa_his"
-- ----------------------------------------------------------------------------

DECLARE @TenLogin sysname      = N'$(TenLogin)';
DECLARE @MatKhau  nvarchar(128) = N'$(MatKhau)';
DECLARE @sql nvarchar(max);

IF @MatKhau IS NULL OR LEN(@MatKhau) < 8
BEGIN
    RAISERROR (N'Mật khẩu cho login HIS quá ngắn (tối thiểu 8 ký tự) — xem tab Messages.', 16, 1);
    RETURN;
END

-- 1. Login o muc server (khong doi mat khau neu login da ton tai).
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @TenLogin)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@TenLogin)
             + N' WITH PASSWORD = ' + QUOTENAME(@MatKhau, '''')
             + N', CHECK_POLICY = OFF, DEFAULT_DATABASE = [HIS_CSKH];';
    EXEC sp_executesql @sql;
    PRINT N'  [+] Da tao login.';
END
ELSE PRINT N'  [=] Login da co, KHONG doi mat khau (tranh giat ket noi dang chay).';

-- 2. User trong HIS_CSKH — KHONG them vao role nao.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @TenLogin)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@TenLogin)
             + N' FOR LOGIN ' + QUOTENAME(@TenLogin) + N';';
    EXEC sp_executesql @sql;
    PRINT N'  [+] Da tao user trong HIS_CSKH.';
END
ELSE PRINT N'  [=] User da co.';
GO

-- 3. GRANT EXECUTE tren DUNG 7 stored. Them ten vao day la mo rong be mat —
--    can nhac that ky, va ghi ly do vao ADR 0031.
DECLARE @TenLogin sysname = N'$(TenLogin)';
DECLARE @sql nvarchar(max) = N'';
DECLARE @thieu nvarchar(max) = N'';

DECLARE @cua TABLE (Ten sysname);
INSERT INTO @cua (Ten) VALUES
    (N'S00_SPWA_DoHienTrang'),     -- cua DOC duy nhat (file 23)
    (N'DM_BenhNhan_Save'),         -- cua 1: ho so toan he thong
    (N'HT_TaiKhoan_Save'),         -- cua 2: tai khoan dang nhap (file 24)
    (N'DM_BenhNhan_NhanChuSoHuu'), -- cua 3: gan quyen so huu (DE QUEN NHAT)
    (N'DM_BenhNhanCoSo_Save'),     -- cua 4: noi ma BN cua co so
    (N'QL_DotKham_Save'),          -- cua 5: dot kham
    (N'QL_TaiLieuBenhNhan_Save');  -- cua 6: dong tai lieu (con tro)

SELECT @thieu = @thieu + Ten + N'  '
  FROM @cua WHERE OBJECT_ID(N'dbo.' + Ten, 'P') IS NULL;

IF LEN(@thieu) > 0
BEGIN
    PRINT N'  [X] Cac stored sau CHUA TON TAI, chay cac file Database/ truoc:';
    PRINT N'      ' + @thieu;
    RAISERROR (N'Thiếu stored — chưa GRANT gì cả.', 16, 1);
    RETURN;
END

SELECT @sql = @sql + N'GRANT EXECUTE ON OBJECT::dbo.' + QUOTENAME(Ten)
                   + N' TO ' + QUOTENAME(@TenLogin) + N';' + CHAR(13) + CHAR(10)
  FROM @cua;

EXEC sp_executesql @sql;
PRINT N'  [+] Da GRANT EXECUTE 7 stored.';
GO

-- 4. Doi chieu: phai ra DUNG 7 dong, toan permission_name = EXECUTE,
--    class_desc = OBJECT_OR_COLUMN. Bat ky dong SELECT/INSERT/UPDATE/DELETE
--    nao o day deu la SAI — go ngay.
SELECT p.permission_name, p.state_desc, o.name AS Doi_Tuong, p.class_desc
  FROM sys.database_permissions p
  LEFT JOIN sys.objects o ON o.object_id = p.major_id
 WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'$(TenLogin)')
 ORDER BY o.name;
GO

-- 5. Doi chieu vai tro: phai RONG (khong db_datareader/db_datawriter/db_owner).
SELECT r.name AS VaiTro
  FROM sys.database_role_members m
  JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
 WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'$(TenLogin)');
GO

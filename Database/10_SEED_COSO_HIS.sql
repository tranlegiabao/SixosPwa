/* =============================================================================
   10_SEED_COSO_HIS.sql -- Bat mot co so sang nhanh MAN CHUNG (KieuApi = 'HIS')
   DB: HIS_CSKH. Chay lai duoc. datetime + GETDATE().

   Dung khi noi mot ban HisSoft vao cong benh nhan (Giai doan 2, mach SPWA).

   🔴 SUA BON BIEN O DAU FILE roi hay chay. KHONG chay nguyen ban -- file tu
   chan bang RAISERROR "Chua doi @KhoaTho" neu ban chay thang.

   ⚠️ KHI NAO CAN CHAY: chi khi noi MOT CO SO MOI vao cong. Co so da co
   DM_DoiTacApi.KieuApi = 'HIS' thi KHONG can chay lai -- chay lai chi ghi de
   BaseUrl, con khoa thi giu nguyen (tru khi dat @GhiDeKhoa = 1).
   Kiem truoc khi chay:
       SELECT c.MaCoSo, c.TenCoSo, d.KieuApi, d.BaseUrl, d.Active
         FROM dbo.DM_CSKCB c JOIN dbo.DM_DoiTacApi d ON d.IdCoSo = c.Id
        WHERE d.KieuApi = 'HIS';

   HAI KHOA CHO HAI CHIEU, dung lan:
     (A) HIS -> cong : khoa CONG cap cho HIS. File nay sinh no, luu BAM vao
                       HT_KhoaApiCoSo, va IN KHOA THO ra man hinh dung MOT LAN
                       de dan sang ThongTinDoanhNghiep.SpwaKhoaXacThuc ben HIS.
     (B) cong -> HIS : khoa HIS cap cho cong. File nay KHONG dung toi. No nam o
                       ThongTinDoanhNghiep.SpwaKhoaNhanBam ben HIS (dang bam),
                       con ban tho thi nguoi quan tri giu de goi cua kiem tra
                       suc khoe.

   ⚠️ HT_KhoaApiCoSo.KhoaBam la varbinary(32) -- luu BYTE cua bam, khong phai
   chuoi hex. Ben HIS cot SpwaKhoaNhanBam lai la char(64) hex. Hai cach luu khac
   nhau cho CUNG mot bam; gia tri tren day van khop. Dung "sua cho giong nhau".

   SAU KHI CHAY: goi cua kiem tra suc khoe de biet duong noi song chua --
       GET /Admin/KiemTraHis/CoSo/{IdCoSo}?khoa={khoa B}
   ============================================================================= */

SET NOCOUNT ON;
GO

-- ─── SUA O DAY ───────────────────────────────────────────────────────────────
DECLARE @MaCoSo   varchar(50)   = 'PWTEST1';                  -- DM_CSKCB.MaCoSo
DECLARE @BaseUrl  nvarchar(255) = 'https://his.example.local'; -- goc dia chi HIS
DECLARE @KhoaTho  varchar(200)  = 'DOI-KHOA-NAY-TRUOC-KHI-CHAY';
DECLARE @GhiDeKhoa bit          = 0;  -- 1 = cap lai khoa moi cho co so da co khoa
-- ─────────────────────────────────────────────────────────────────────────────

DECLARE @IdCoSo bigint;
SELECT @IdCoSo = Id FROM dbo.DM_CSKCB WHERE MaCoSo = @MaCoSo;

IF @IdCoSo IS NULL
BEGIN
    RAISERROR (N'Khong co co so nao mang MaCoSo = %s trong DM_CSKCB. Tao co so truoc da.', 16, 1, @MaCoSo);
    RETURN;
END

/* Chot chan co y: file nay KHONG chay duoc nguyen ban. Bao ro phai lam gi chu
   khong chi bao "sai o dau" -- nguoi doc thong bao loi thuong khong doc header. */
IF @KhoaTho = 'DOI-KHOA-NAY-TRUOC-KHI-CHAY'
BEGIN
    PRINT N'';
    PRINT N'====================================================================';
    PRINT N'  FILE NAY CHUA CHAY. Day la CHOT CHAN co y, khong phai loi.';
    PRINT N'  Mo file, sua 3 dong o khoi "SUA O DAY" (khoang dong 30-33):';
    PRINT N'';
    PRINT N'    @MaCoSo   = ma co so trong DM_CSKCB (vd 77121)';
    PRINT N'    @BaseUrl  = goc dia chi HIS cua co so do';
    PRINT N'    @KhoaTho  = mot khoa THAT do ban tu dat (chuoi ngau nhien dai)';
    PRINT N'';
    PRINT N'  Roi chay lai file. Kiem xem co so da seed chua:';
    PRINT N'    SELECT c.MaCoSo, d.KieuApi, d.BaseUrl, d.Active';
    PRINT N'      FROM DM_CSKCB c JOIN DM_DoiTacApi d ON d.IdCoSo = c.Id';
    PRINT N'     WHERE d.KieuApi = ''HIS'';';
    PRINT N'  Co so da co KieuApi = HIS thi KHONG can chay file nay nua.';
    PRINT N'====================================================================';

    RAISERROR (N'Chưa đổi @KhoaTho — xem hướng dẫn vừa in ở tab Messages.', 16, 1);
    RETURN;
END

/* --- 0. Noi CHECK constraint de nhan them 'HIS' ---------------------------
   🔴 Bat duoc luc CHAY THAT (08/09): CK_DM_DoiTacApi_KieuApi chot cung
   ([KieuApi]='NONE' OR [KieuApi]='UB') -- dat tu truoc khi co kieu HIS. Khong
   noi no thi buoc 1 chet voi Msg 547, MA CAC BATCH SAU VAN CHAY TIEP (moi GO
   la mot batch doc lap) => khoa duoc cap trong khi cua van dong: DB o trang
   thai NUA VOI, khong phai "chua chay gi".
   Dung lai rang buoc chu khong bo han -- no chan duoc loi go sai kieu. */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DM_DoiTacApi_KieuApi')
    ALTER TABLE dbo.DM_DoiTacApi DROP CONSTRAINT CK_DM_DoiTacApi_KieuApi;

ALTER TABLE dbo.DM_DoiTacApi WITH CHECK
    ADD CONSTRAINT CK_DM_DoiTacApi_KieuApi
    CHECK (KieuApi IN ('NONE', 'UB', 'HIS'));

/* --- 1. DM_DoiTacApi: bat nhanh man chung --------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.DM_DoiTacApi WHERE IdCoSo = @IdCoSo)
    UPDATE dbo.DM_DoiTacApi
       SET KieuApi = 'HIS',
           BaseUrl = @BaseUrl,
           Active  = 1
     WHERE IdCoSo = @IdCoSo;
ELSE
    INSERT INTO dbo.DM_DoiTacApi (IdCoSo, KieuApi, BaseUrl, TrangChu, Active, NgayTao)
    VALUES (@IdCoSo, 'HIS', @BaseUrl, NULL, 1, GETDATE());

/* --- 2. HT_KhoaApiCoSo: khoa cho HIS goi len cong ------------------------- */
/* Bam SHA-256 tren BYTE UTF-8 -- phai giong het cach ben HIS bam, lech mot chi
   tiet la hai ben khong bao gio khop. */
DECLARE @Bam varbinary(32) = HASHBYTES('SHA2_256', CAST(@KhoaTho AS varchar(200)));

IF EXISTS (SELECT 1 FROM dbo.HT_KhoaApiCoSo WHERE IDCoSo = @IdCoSo AND Active = 1)
BEGIN
    IF @GhiDeKhoa = 1
    BEGIN
        UPDATE dbo.HT_KhoaApiCoSo SET Active = 0 WHERE IDCoSo = @IdCoSo AND Active = 1;

        INSERT INTO dbo.HT_KhoaApiCoSo (IDCoSo, TenKhoa, KhoaBam, Active, NgayCap)
        VALUES (@IdCoSo, N'Mạch SPWA — cấp lại', @Bam, 1, GETDATE());

        PRINT N'Da cap khoa MOI. Khoa cu bi tat (Active = 0).';
    END
    ELSE
        PRINT N'Co so nay DA co khoa Active. Giu nguyen. Muon cap lai thi dat @GhiDeKhoa = 1.';
END
ELSE
BEGIN
    INSERT INTO dbo.HT_KhoaApiCoSo (IDCoSo, TenKhoa, KhoaBam, Active, NgayCap)
    VALUES (@IdCoSo, N'Mạch SPWA', @Bam, 1, GETDATE());

    PRINT N'Da cap khoa moi cho co so.';
END

/* --- 2b. Co so co HIEN ra cho benh nhan khong? ----------------------------
   🔴 Bat duoc 08/09: seed duong API dung het ma co so van TANG HINH tren cong.
   Man /Home/DanhSachCoSo-{nhom} loc `WHERE IdNhomCS = <nhom> AND Active`
   (HomeController.cs), nen co so thieu IdNhomCS thi khong thuoc danh sach nao
   — benh nhan khong co duong nao bam vao, du API hai chieu chay hoan hao.
   Canh bao chu khong chan: van co the co so co y an di. */
IF EXISTS (SELECT 1 FROM dbo.DM_CSKCB WHERE Id = @IdCoSo AND (IdNhomCS IS NULL OR Active = 0))
BEGIN
    PRINT N'';
    PRINT N'!!! CANH BAO: co so nay se KHONG HIEN tren cong cho benh nhan.';
    PRINT N'    Thieu IdNhomCS (nhom co so) hoac Active = 0.';
    PRINT N'    Sua bang:';
    PRINT N'      UPDATE DM_CSKCB';
    PRINT N'         SET IdNhomCS = (SELECT ID FROM DM_NhomCS WHERE MaNhom = ''pkdk''),';
    PRINT N'             Active = 1';
    PRINT N'       WHERE MaCoSo = ''<ma co so>'';';
    PRINT N'    (MaNhom: benhvien | pkdk | nhakhoa | phongmach | nhathuoc)';
    PRINT N'';
END

/* --- 3. Doi chieu ----------------------------------------------------------
   KHONG dat GO o tren: ca file nay la MOT batch, @IdCoSo khai bao o dau va con
   duoc dung o duoi — GO cat batch la mat bien. */
SELECT c.Id AS IdCoSo, c.MaCoSo, c.TenCoSo,
       d.KieuApi, d.BaseUrl, d.Active AS CuaMo,
       (SELECT COUNT(*) FROM dbo.HT_KhoaApiCoSo k WHERE k.IDCoSo = c.Id AND k.Active = 1) AS SoKhoaActive
  FROM dbo.DM_CSKCB c
  LEFT JOIN dbo.DM_DoiTacApi d ON d.IdCoSo = c.Id
 WHERE c.Id = @IdCoSo;

PRINT N'--------------------------------------------------------------';
PRINT N'BUOC TIEP THEO -- lam du ca ba, thieu mot la duong noi khong chay:';
PRINT N'  1. Ben HIS: dan khoa tho vua dat vao ThongTinDoanhNghiep.SpwaKhoaXacThuc';
PRINT N'     va dat SpwaUrlCong = goc dia chi cua cong nay.';
PRINT N'  2. Ben HIS: sinh mot khoa KHAC cho chieu cong -> HIS, luu BAM (hex) vao';
PRINT N'     SpwaKhoaNhanBam. Hai chieu HAI KHOA RIENG.';
PRINT N'  3. Kiem ThongTinDoanhNghiep.MaCSKCB ben HIS PHAI BANG DM_CSKCB.MaCoSo o';
PRINT N'     tren. Lech la moi cuoc goi day deu 401 KHOA_KHAC_CO_SO.';
PRINT N'Roi goi: GET /Admin/KiemTraHis/CoSo/{IdCoSo}?khoa={khoa chieu cong->HIS}';
PRINT N'--------------------------------------------------------------';
GO

/* =============================================================================
   NGUOC LAI (chay tay khi can go mot co so khoi nhanh man chung).
   KHONG de trong 99_ROLLBACK.sql vi file do la cua dot CSDL, con file nay la
   seed cho TUNG co so -- go nham ca cum thi mat khoa cua co so khac.

   DECLARE @MaCoSo varchar(50) = '<ma co so>';
   DECLARE @IdCoSo bigint = (SELECT Id FROM dbo.DM_CSKCB WHERE MaCoSo = @MaCoSo);

   UPDATE dbo.HT_KhoaApiCoSo SET Active = 0 WHERE IDCoSo = @IdCoSo;
   UPDATE dbo.DM_DoiTacApi   SET KieuApi = 'NONE', BaseUrl = NULL WHERE IdCoSo = @IdCoSo;

   -- Chi siet lai CHECK khi KHONG con co so nao dung kieu HIS:
   IF NOT EXISTS (SELECT 1 FROM dbo.DM_DoiTacApi WHERE KieuApi = 'HIS')
   BEGIN
       ALTER TABLE dbo.DM_DoiTacApi DROP CONSTRAINT CK_DM_DoiTacApi_KieuApi;
       ALTER TABLE dbo.DM_DoiTacApi WITH CHECK
           ADD CONSTRAINT CK_DM_DoiTacApi_KieuApi CHECK (KieuApi IN ('NONE', 'UB'));
   END
   ============================================================================= */

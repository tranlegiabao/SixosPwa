/* =============================================================================
   10_SEED_COSO_HIS.sql -- Bat mot co so sang nhanh MAN CHUNG (KieuApi = 'HIS')
   DB: HIS_CSKH. Chay lai duoc. datetime + GETDATE().

   Dung khi noi mot ban HisSoft vao cong benh nhan (Giai doan 2, mach SPWA).

   🔴 SUA BON BIEN O DAU FILE roi hay chay. KHONG chay nguyen ban.

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

IF @KhoaTho = 'DOI-KHOA-NAY-TRUOC-KHI-CHAY'
BEGIN
    RAISERROR (N'Chua doi @KhoaTho. Dat mot khoa that roi chay lai.', 16, 1);
    RETURN;
END

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

/* --- 3. Doi chieu ---------------------------------------------------------- */
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

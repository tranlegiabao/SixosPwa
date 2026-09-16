-- ============================================================================
-- 23 — dbo.S00_SPWA_DoHienTrang: cua DOC cua che do Tro duong (chot 52)
--
-- HIS goi stored nay QUA LINKED SERVER truoc khi ghi bat cu thu gi, de biet
-- ben cong DANG CO NHUNG GI. Mot luot di-ve lay DU 7 su that, thay vi 6 lan
-- hoi le -- moi luot RPC qua linked server la mot vong mang.
--
-- 🔴 VI SAO PHAI CO STORED NAY, khong GRANT SELECT cho login HIS:
--    HIS_CSKH dung chung cho 12 co so. DM_BenhNhan / HT_TaiKhoan la bang TOAN
--    HE THONG. GRANT SELECT cho login cua phong kham A la A doc duoc benh nhan
--    va danh ba so dien thoai cua B, C, D. Stored nay chi tra DUNG nhung dong
--    lien quan toi co so dang hoi, va chi tra ID -- khong tra ten, khong tra SDT.
--
-- 🔴 MOI THAM SO PHAI VO HUONG. Tuyet doi KHONG TVP: table-valued parameter
--    KHONG di qua linked server duoc.
--
-- Stored nay CHI DOC. Khong INSERT/UPDATE/DELETE mot dong nao.
-- Khuon: 15_STO_HT_TAI_KHOAN_LOC.sql (stored doc, tra result set).
-- ============================================================================
IF OBJECT_ID('dbo.S00_SPWA_DoHienTrang', 'P') IS NOT NULL
    DROP PROCEDURE dbo.S00_SPWA_DoHienTrang;
GO

CREATE PROCEDURE dbo.S00_SPWA_DoHienTrang
    @MaCoSo      nvarchar(50),              -- DM_CSKCB.MaCoSo, = ThongTinDoanhNghiep.MaCSKCB ben HIS
    @Cccd        varchar(20)   = NULL,
    @Sdt         varchar(20)   = NULL,
    @MaBN        varchar(20)   = NULL,
    @LoaiTaiLieu nvarchar(50)  = NULL,
    @MaNguonHIS  varchar(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @MaCoSo      = NULLIF(LTRIM(RTRIM(@MaCoSo)), N'');
    SET @Cccd        = NULLIF(LTRIM(RTRIM(@Cccd)), '');
    SET @Sdt         = NULLIF(LTRIM(RTRIM(@Sdt)), '');
    SET @MaBN        = NULLIF(LTRIM(RTRIM(@MaBN)), '');
    SET @LoaiTaiLieu = NULLIF(LTRIM(RTRIM(@LoaiTaiLieu)), N'');
    SET @MaNguonHIS  = NULLIF(LTRIM(RTRIM(@MaNguonHIS)), '');

    DECLARE @IDCoSo            bigint = NULL;
    DECLARE @IDBenhNhan        bigint = NULL;
    DECLARE @IDBenhNhanCoSo    bigint = NULL;
    DECLARE @MaBNDangNoi       varchar(20) = NULL;
    DECLARE @IDTaiKhoanTheoSdt bigint = NULL;
    DECLARE @IDTaiLieuDaCo     bigint = NULL;
    DECLARE @DuongDanDaCo      nvarchar(500) = NULL;

    -- (1) Co so. Het buoc nay ma NULL thi moi thu con lai vo nghia -> tra som.
    SELECT TOP 1 @IDCoSo = ID FROM dbo.DM_CSKCB WITH (NOLOCK) WHERE MaCoSo = @MaCoSo;

    IF @IDCoSo IS NOT NULL
    BEGIN
        -- (2) Ho so benh nhan toan he thong, tra theo CCCD.
        IF @Cccd IS NOT NULL
            SELECT TOP 1 @IDBenhNhan = ID
              FROM dbo.DM_BenhNhan WITH (NOLOCK)
             WHERE CCCD = @Cccd;

        -- (3)+(4) Dong noi tai CHINH co so nay, va no dang giu MA NAO.
        --   🔴 Day la su that quan trong nhat cua chot 47: HIS phai biet dong
        --   nay DANG NOI MA KHAC hay khong TRUOC khi goi DM_BenhNhanCoSo_Save
        --   -- vi nhanh co-dong-roi cua stored do la UPDATE SET MaBN = @MaBN,
        --   no KHONG mang hang rao nao (08_MO_CUA_TAI_LIEU.sql:72).
        IF @IDBenhNhan IS NOT NULL
            SELECT TOP 1 @IDBenhNhanCoSo = ID, @MaBNDangNoi = MaBN
              FROM dbo.DM_BenhNhanCoSo WITH (NOLOCK)
             WHERE IDBenhNhan = @IDBenhNhan AND IDCoSo = @IDCoSo;

        -- (5) Tai khoan dang nhap theo SDT. NULL = cong chua co tai khoan nao
        --     cho so nay -> HIS phai dung moi (cua 4).
        IF @Sdt IS NOT NULL
            SELECT TOP 1 @IDTaiKhoanTheoSdt = ID
              FROM dbo.HT_TaiKhoan WITH (NOLOCK)
             WHERE SDT = @Sdt;

        -- (6)+(7) Ban MOI NHAT cua dung tai lieu nay, kem DUONG DAN dang tro.
        --   Chot 46: khoa tu nhien la (IDCoSo, LoaiTaiLieu, MaNguonHIS).
        --   HIS so @DuongDanDaCo voi duong moi:
        --     giong  -> DA_GUI_RUI, khong goi cua nao
        --     khac   -> goi @ID = 0 de sinh PHIEN BAN MOI (khong bao gio UPDATE)
        --   BamNoiDung KHONG dung o che do nay -- dong tai lieu la CON TRO,
        --   cong doc tep song moi lan mo (chot 37), nen bam khong con viec.
        IF @LoaiTaiLieu IS NOT NULL AND @MaNguonHIS IS NOT NULL
            SELECT TOP 1 @IDTaiLieuDaCo = ID, @DuongDanDaCo = DuongDanFtp
              FROM dbo.QL_TaiLieuBenhNhan WITH (NOLOCK)
             WHERE IDCoSo = @IDCoSo
               AND LoaiTaiLieu = @LoaiTaiLieu
               AND MaNguonHIS = @MaNguonHIS
               AND LaBanMoiNhat = 1;
    END

    -- LUON tra DUNG MOT DONG, ke ca khi khong tim thay gi (moi cot NULL).
    -- Ben HIS `INSERT INTO @bang EXEC ... AT SPWA_CONG` nen so cot va THU TU
    -- cot la HOP DONG -- doi o day la vo ben kia. Them cot thi them O CUOI.
    SELECT @IDCoSo            AS IDCoSo,
           @IDBenhNhan        AS IDBenhNhan,
           @IDBenhNhanCoSo    AS IDBenhNhanCoSo,
           @MaBNDangNoi       AS MaBNDangNoi,
           @IDTaiKhoanTheoSdt AS IDTaiKhoanTheoSdt,
           @IDTaiLieuDaCo     AS IDTaiLieuDaCo,
           @DuongDanDaCo      AS DuongDanDaCo;
END;
GO

PRINT '--- Thu: co so khong ton tai phai ra 1 dong toan NULL, khong loi ---';
EXEC dbo.S00_SPWA_DoHienTrang @MaCoSo = N'__KHONG_CO__';
GO

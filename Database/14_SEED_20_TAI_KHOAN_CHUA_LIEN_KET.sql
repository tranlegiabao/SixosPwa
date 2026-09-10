/* =============================================================================
   14_SEED_20_TAI_KHOAN_CHUA_LIEN_KET.sql
   TRICH XUAT 20 BENH NHAN THAT TU Dev_Master3 SANG HIS_CSKH (CHUA LIEN KET).

   DB NGUON:  Dev_Master3 (CSDL HIS cua phong kham / co so)
   DB DICH:   HIS_CSKH    (CSDL Cong cham soc khach hang SixosPwa)
   CO SO:     PKĐK Thiên Nam (MaCoSo = '77121')

   MUC DICH:
     - Lay dung 20 benh nhan THAT (Ten that, CCCD 12 so that, Ngay sinh that,
       Gioi tinh that, SDT that, Dia chi that) tu [Dev_Master3].[dbo].[DM_BenhNhan].
     - Tao tai khoan tren cong (HT_TaiKhoan) theo SDT that cua tung nguoi.
     - Tao ho so con nguoi (DM_BenhNhan) mang dung danh tinh that ben HIS.
     - Tao ho so tai co so (DM_BenhNhanCoSo) voi MaBN = NULL (Ho so tu khai,
       DaNoiHIS = 0, DaMoTaiLieu = 1) -> DUNG CHUAN TRANG THAI "CHUA LIEN KET".

   KHI TEST TREN SIXOSPWA:
     - Dang nhap bang SDT that cua benh nhan, nhap OTP test: 123456.
     - Man "Ho so cua toi" (/benh-nhan/ho-so) bao: "Chua lien ket" (DaNoiHIS = false).
     - Bam "Sua ho so" -> Bam "Luu": Cong goi SPWA_TraCuuHoSo sang Dev_Master3,
       khop dung 4 o danh tinh voi MaBN that dang nam san ben Dev_Master3,
       va tu dong gan MaBN that vao ho so!

   Script thiet ke IDEMPOTENT (chay lai an toan, tu don sach du lieu dummy cu).
   Tuan thu le repo: datetime + GETDATE(), SET QUOTED_IDENTIFIER ON.
   ============================================================================= */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
GO

PRINT N'=============================================================================';
PRINT N'  BAT DAU SEED 20 BENH NHAN THAT TU Dev_Master3 SANG HIS_CSKH';
PRINT N'=============================================================================';

-- ─── 0. KIEM TRA KET NOI CSDL & CO SO CSKCB ─────────────────────────────────
IF DB_ID('Dev_Master3') IS NULL
BEGIN
    RAISERROR (N'Không tìm thấy CSDL Dev_Master3 trên SQL Server instance này. Cần kết nối tới cùng instance chứa Dev_Master3.', 16, 1);
    RETURN;
END;

DECLARE @MaCoSo varchar(50) = '77121'; -- PKĐK Thiên Nam
DECLARE @IdCoSo bigint = (SELECT Id FROM dbo.DM_CSKCB WHERE MaCoSo = @MaCoSo);

IF @IdCoSo IS NULL
BEGIN
    RAISERROR (N'Chưa có cơ sở mã %s trong DM_CSKCB. Vui lòng chạy 10_SEED_COSO_HIS.sql trước.', 16, 1, @MaCoSo);
    RETURN;
END;

PRINT N'-> Da tim thay co so: ' + @MaCoSo + N' (ID: ' + CAST(@IdCoSo AS varchar(10)) + N')';

-- ─── 1. DON SACH CAC TAI KHOAN DUMMY 09000000% TEST CU (NEU CO) ─────────────
PRINT N'-> Don sach cac tai khoan dummy test cu (0900000002 -> 0900000020)...';

DECLARE @SdtCu TABLE (SDT varchar(20));
INSERT INTO @SdtCu (SDT)
SELECT SDT FROM dbo.HT_TaiKhoan 
 WHERE (SDT LIKE '09000000%' AND SDT <> '0900000001') OR SDT = '079075005678';

-- 1.1 Xoa thiet bi dang nhap
DELETE tb
  FROM dbo.HT_ThietBi tb
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = tb.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtCu);

-- 1.2 Go khoa ngoai vong: HT_TaiKhoan.IdBenhNhan tro toi DM_BenhNhan
UPDATE dbo.HT_TaiKhoan
   SET IdBenhNhan = NULL
 WHERE SDT IN (SELECT SDT FROM @SdtCu);

-- 1.3 Xoa ho so co so (DM_BenhNhanCoSo)
DELETE cs
  FROM dbo.DM_BenhNhanCoSo cs
  JOIN dbo.DM_BenhNhan bn ON bn.ID = cs.IDBenhNhan
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = bn.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtCu);

-- 1.4 Xoa con nguoi (DM_BenhNhan)
DELETE bn
  FROM dbo.DM_BenhNhan bn
  JOIN dbo.HT_TaiKhoan tk ON tk.Id = bn.IDTaiKhoan
 WHERE tk.SDT IN (SELECT SDT FROM @SdtCu);

-- 1.5 Xoa tai khoan (HT_TaiKhoan)
DELETE FROM dbo.HT_TaiKhoan
 WHERE SDT IN (SELECT SDT FROM @SdtCu);

-- ─── 2. TRICH XUAT 20 BENH NHAN THAT TU Dev_Master3 ─────────────────────────
IF OBJECT_ID('tempdb..#NguoiThatDevMaster3') IS NOT NULL DROP TABLE #NguoiThatDevMaster3;

WITH NguoiHopLe AS (
    SELECT 
        b.ID AS IdHis,
        b.MaBN,
        b.TenBN,
        b.NgaySinh,
        CASE WHEN b.IDGT = 1 THEN '1' ELSE '2' END AS GioiTinh,
        LTRIM(RTRIM(b.SoCCCD)) AS SoCCCD,
        LTRIM(RTRIM(b.DienThoai)) AS DienThoai,
        b.DiaChi,
        ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(b.SoCCCD)) ORDER BY b.ID DESC) AS RnCccd,
        ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(b.DienThoai)) ORDER BY b.ID DESC) AS RnSdt
    FROM [Dev_Master3].[dbo].[DM_BenhNhan] b
    WHERE LEN(LTRIM(RTRIM(b.SoCCCD))) = 12
      AND b.SoCCCD NOT LIKE '%0000%'
      AND b.SoCCCD NOT LIKE '%1111%'
      AND b.SoCCCD NOT LIKE '%2222%'
      AND b.SoCCCD NOT LIKE '%3333%'
      AND b.SoCCCD NOT LIKE '%4444%'
      AND b.SoCCCD NOT LIKE '%5555%'
      AND b.SoCCCD NOT LIKE '%7777%'
      AND b.SoCCCD NOT LIKE '%9999%'
      AND b.DienThoai LIKE '0[35789][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
      AND b.TenBN NOT LIKE '%TEST%'
      AND b.TenBN NOT LIKE '%CKS%'
      AND b.TenBN NOT LIKE '%???%'
      AND b.TenBN NOT LIKE '%HD%'
      AND b.TenBN NOT LIKE '%THAI%'
      AND b.NgaySinh > '1940-01-01'
      AND b.NgaySinh NOT LIKE '%01-01%'
)
SELECT TOP 20
    ROW_NUMBER() OVER (ORDER BY IdHis DESC) AS Stt,
    IdHis,
    MaBN,
    TenBN,
    NgaySinh,
    GioiTinh,
    SoCCCD,
    DienThoai,
    DiaChi
INTO #NguoiThatDevMaster3
FROM NguoiHopLe
WHERE RnCccd = 1 AND RnSdt = 1
ORDER BY IdHis DESC;

DECLARE @SoLuongTrichXuat int = (SELECT COUNT(*) FROM #NguoiThatDevMaster3);
PRINT N'-> Da trich xuat ' + CAST(@SoLuongTrichXuat AS varchar(5)) + N' benh nhan THAT tu Dev_Master3.';

-- ─── 3. TIEN HANH SEED VAO HIS_CSKH (TRANG THAI CHUA LIEN KET) ──────────────
DECLARE @DemTkMoi int = 0;
DECLARE @DemBnMoi int = 0;
DECLARE @DemCsMoi int = 0;

DECLARE @SttCur int = 1;

WHILE @SttCur <= @SoLuongTrichXuat
BEGIN
    DECLARE @MaBN_His   nvarchar(50);
    DECLARE @TenBN_His  nvarchar(500);
    DECLARE @NgaySinh   datetime;
    DECLARE @GioiTinh   varchar(10);
    DECLARE @SoCCCD     varchar(20);
    DECLARE @DienThoai  varchar(20);
    DECLARE @DiaChi     nvarchar(500);

    SELECT 
        @MaBN_His  = MaBN,
        @TenBN_His = TenBN,
        @NgaySinh  = NgaySinh,
        @GioiTinh  = GioiTinh,
        @SoCCCD    = SoCCCD,
        @DienThoai = DienThoai,
        @DiaChi    = DiaChi
    FROM #NguoiThatDevMaster3
    WHERE Stt = @SttCur;

    -- 3.1 Tao tai khoan HT_TaiKhoan (theo SDT that cua benh nhan)
    DECLARE @IdTaiKhoan bigint = (SELECT Id FROM dbo.HT_TaiKhoan WHERE SDT = @DienThoai);

    IF @IdTaiKhoan IS NULL
    BEGIN
        INSERT INTO dbo.HT_TaiKhoan (SDT, Email, Role, MatKhauNoiBo, IdBenhNhan, NgayTao)
        VALUES (@DienThoai, @DienThoai + '@sixospwa.local', 'BenhNhan', NULL, NULL, GETDATE());

        SET @IdTaiKhoan = SCOPE_IDENTITY();
        SET @DemTkMoi = @DemTkMoi + 1;
    END;

    -- 3.2 Tao con nguoi DM_BenhNhan (mang thong tin that tu Dev_Master3)
    DECLARE @IdBenhNhan bigint = (SELECT ID FROM dbo.DM_BenhNhan WHERE CCCD = @SoCCCD);

    IF @IdBenhNhan IS NULL
    BEGIN
        INSERT INTO dbo.DM_BenhNhan (CCCD, TenBN, SDT, Email, DiaChi, IDTaiKhoan, NgaySinh, GioiTinh, HoTenKhongDau, NgayTao)
        VALUES (@SoCCCD, @TenBN_His, @DienThoai, @DienThoai + '@sixospwa.local', @DiaChi, @IdTaiKhoan, @NgaySinh, @GioiTinh, NULL, GETDATE());

        SET @IdBenhNhan = SCOPE_IDENTITY();
        SET @DemBnMoi = @DemBnMoi + 1;
    END
    ELSE
    BEGIN
        UPDATE dbo.DM_BenhNhan
           SET IDTaiKhoan = @IdTaiKhoan,
               TenBN = @TenBN_His,
               SDT = @DienThoai,
               NgaySinh = @NgaySinh,
               GioiTinh = @GioiTinh,
               DiaChi = @DiaChi
         WHERE ID = @IdBenhNhan;
    END;

    -- Cap nhat IdBenhNhan vao HT_TaiKhoan
    UPDATE dbo.HT_TaiKhoan
       SET IdBenhNhan = @IdBenhNhan
     WHERE Id = @IdTaiKhoan AND (IdBenhNhan IS NULL OR IdBenhNhan <> @IdBenhNhan);

    -- 3.3 Tao ho so tai co so: MaBN IS NULL (Ho so tu khai, DaNoiHIS = 0, DaMoTaiLieu = 1)
    -- 🔴 DAY LA DIEM QUYET DINH TRANG THAI "CHUA LIEN KET"
    IF NOT EXISTS (SELECT 1 FROM dbo.DM_BenhNhanCoSo
                    WHERE IDBenhNhan = @IdBenhNhan AND IDCoSo = @IdCoSo AND MaBN IS NULL)
    BEGIN
        INSERT INTO dbo.DM_BenhNhanCoSo (IDBenhNhan, IDCoSo, MaBN, DaMoTaiLieu, NgayTao)
        VALUES (@IdBenhNhan, @IdCoSo, NULL, 1, GETDATE());

        SET @DemCsMoi = @DemCsMoi + 1;
    END;

    SET @SttCur = @SttCur + 1;
END;

PRINT N'-> Hoan tat seed vao HIS_CSKH:';
PRINT N'   + Tai khoan tao moi (HT_TaiKhoan):       ' + CAST(@DemTkMoi AS varchar(10));
PRINT N'   + Con nguoi tao moi (DM_BenhNhan):        ' + CAST(@DemBnMoi AS varchar(10));
PRINT N'   + Ho so tu khai moi (DM_BenhNhanCoSo):   ' + CAST(@DemCsMoi AS varchar(10));

-- ─── 4. BANG DOI CHIEU 20 BENH NHAN THAT & HUONG DAN KIEM THU ──────────────
PRINT N'';
PRINT N'====================================================================================================';
PRINT N'  DANH SACH 20 BENH NHAN THAT TU Dev_Master3 DA SAN SANG TEST TREN SIXOSPWA (CHUA LIEN KET)';
PRINT N'====================================================================================================';

SELECT 
    t.Stt,
    t.DienThoai AS [SĐT Đăng nhập],
    N'123456' AS [OTP Test],
    t.TenBN AS [Họ và Tên Thật],
    t.SoCCCD AS [CCCD Thật],
    CONVERT(varchar(10), t.NgaySinh, 103) AS [Ngày sinh],
    CASE t.GioiTinh WHEN '1' THEN N'Nam' WHEN '2' THEN N'Nữ' ELSE N'Chưa rõ' END AS [Giới tính],
    t.MaBN AS [Mã BN thật bên HIS (Dev_Master3)],
    CASE WHEN cs.MaBN IS NULL THEN N'Chưa nối (Tự khai)' ELSE cs.MaBN END AS [Trạng thái trên PWA]
FROM #NguoiThatDevMaster3 t
JOIN dbo.HT_TaiKhoan tk ON tk.SDT = t.DienThoai
JOIN dbo.DM_BenhNhan bn ON bn.IDTaiKhoan = tk.Id
LEFT JOIN dbo.DM_BenhNhanCoSo cs ON cs.IDBenhNhan = bn.ID AND cs.IDCoSo = @IdCoSo AND cs.MaBN IS NULL
ORDER BY t.Stt;

DROP TABLE #NguoiThatDevMaster3;

PRINT N'';
PRINT N'HƯỚNG DẪN KIỂM THỬ THỰC TẾ:';
PRINT N'1. Mở cổng: https://localhost:7024/DangNhap/Login?coSo=pkdk-thien-nam';
PRINT N'2. Đăng nhập bằng bất kỳ SĐT nào trong bảng trên (ví dụ 0348932730), OTP: 123456.';
PRINT N'3. Vào màn "Hồ sơ của tôi": Thấy đúng tên thật của bệnh nhân và trạng thái "Chưa liên kết".';
PRINT N'4. Bấm "Sửa" -> Bấm "Lưu hồ sơ": Cổng gọi SPWA_TraCuuHoSo sang Dev_Master3,';
PRINT N'   tự động gán đúng Mã BN thật tương ứng (ví dụ 145824) vào hồ sơ!';
GO

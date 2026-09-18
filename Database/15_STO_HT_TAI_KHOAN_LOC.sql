-- ============================================================================
-- Thủ tục: dbo.HT_TaiKhoan_Loc
-- Mô tả: Phân trang cuộn danh sách tài khoản theo chuẩn 0307
--        (OFFSET / FETCH NEXT, trả về TongSoDong và kèm hồ sơ bệnh nhân).
-- ============================================================================
IF OBJECT_ID('dbo.HT_TaiKhoan_Loc', 'P') IS NOT NULL
    DROP PROCEDURE dbo.HT_TaiKhoan_Loc;
GO

CREATE PROCEDURE dbo.HT_TaiKhoan_Loc
    @Trang    int           = 1,
    @SoDong   int           = 50,
    @SDT      varchar(20)   = NULL,
    @CCCD     varchar(20)   = NULL,
    @MaBN     varchar(50)   = NULL,
    @Role     varchar(20)   = NULL,
    @LoaiCS   varchar(50)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @Trang = ISNULL(@Trang, 1);
    IF @Trang < 1 SET @Trang = 1;
    SET @SoDong = ISNULL(@SoDong, 50);
    IF @SoDong NOT IN (20, 50, 100, 500) SET @SoDong = 50;

    SET @SDT = NULLIF(LTRIM(RTRIM(@SDT)), '');
    SET @CCCD = NULLIF(LTRIM(RTRIM(@CCCD)), '');
    SET @MaBN = NULLIF(LTRIM(RTRIM(@MaBN)), '');
    SET @Role = NULLIF(LTRIM(RTRIM(@Role)), '');
    SET @LoaiCS = NULLIF(LTRIM(RTRIM(@LoaiCS)), '');

    DECLARE @IdCoSoFilter bigint = NULL;
    DECLARE @MaNhomFilter varchar(50) = NULL;

    IF @LoaiCS IS NOT NULL
    BEGIN
        IF LOWER(@LoaiCS) LIKE 'cs:%'
        BEGIN
            SET @IdCoSoFilter = TRY_CAST(SUBSTRING(@LoaiCS, 4, 20) AS bigint);
        END
        ELSE
        BEGIN
            SET @MaNhomFilter = LOWER(@LoaiCS);
        END
    END

    -- 1. Lọc các IdTaiKhoan thỏa mãn điều kiện
    CREATE TABLE #TaiKhoanLoc (ID bigint PRIMARY KEY);

    INSERT INTO #TaiKhoanLoc (ID)
    SELECT tk.ID
    FROM dbo.HT_TaiKhoan tk WITH (NOLOCK)
    WHERE
        -- Lọc Vai trò
        (@Role IS NULL 
         OR (@Role = 'Admin' AND tk.Role = 'Admin') 
         OR (@Role = 'BenhNhan' AND ISNULL(tk.Role, '') <> 'Admin'))
        
        -- Lọc SĐT
        AND (@SDT IS NULL 
             OR tk.SDT LIKE '%' + @SDT + '%'
             OR EXISTS (
                 SELECT 1 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 WHERE b.IDTaiKhoan = tk.ID AND b.SDT LIKE '%' + @SDT + '%'
             ))
        
        -- Lọc CCCD
        AND (@CCCD IS NULL 
             OR EXISTS (
                 SELECT 1 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 WHERE (b.IDTaiKhoan = tk.ID OR b.ID = tk.IDBenhNhan OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                   AND b.CCCD LIKE '%' + @CCCD + '%'
             ))
        
        -- Lọc Mã bệnh nhân tại cơ sở
        AND (@MaBN IS NULL 
             OR EXISTS (
                 SELECT 1 
                 FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                 JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                 WHERE (b.IDTaiKhoan = tk.ID OR b.ID = tk.IDBenhNhan OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                   AND cs.MaBN LIKE '%' + @MaBN + '%'
             ))
        
        -- Lọc Loại cơ sở hoặc cơ sở cụ thể
        AND (
            (@IdCoSoFilter IS NULL AND @MaNhomFilter IS NULL)
            OR (@IdCoSoFilter IS NOT NULL AND (
                EXISTS (SELECT 1 FROM dbo.HT_TaiKhoanDoiTac dt WITH (NOLOCK) WHERE dt.IDTaiKhoan = tk.ID AND dt.IDCoSo = @IdCoSoFilter)
                OR EXISTS (
                    SELECT 1 
                    FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                    JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                    WHERE (b.IDTaiKhoan = tk.ID OR b.ID = tk.IDBenhNhan OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                      AND cs.IDCoSo = @IdCoSoFilter
                )
            ))
            OR (@MaNhomFilter IS NOT NULL AND (
                EXISTS (
                    SELECT 1 
                    FROM dbo.HT_TaiKhoanDoiTac dt WITH (NOLOCK)
                    JOIN dbo.DM_CSKCB c WITH (NOLOCK) ON c.ID = dt.IDCoSo
                    JOIN dbo.DM_NhomCS n WITH (NOLOCK) ON n.ID = c.IdNhomCS
                    WHERE dt.IDTaiKhoan = tk.ID AND LOWER(n.MaNhom) = @MaNhomFilter
                )
                OR EXISTS (
                    SELECT 1 
                    FROM dbo.DM_BenhNhan b WITH (NOLOCK)
                    JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
                    JOIN dbo.DM_CSKCB c WITH (NOLOCK) ON c.ID = cs.IDCoSo
                    JOIN dbo.DM_NhomCS n WITH (NOLOCK) ON n.ID = c.IdNhomCS
                    WHERE (b.IDTaiKhoan = tk.ID OR b.ID = tk.IDBenhNhan OR (tk.SDT IS NOT NULL AND b.SDT = tk.SDT))
                      AND LOWER(n.MaNhom) = @MaNhomFilter
                )
            ))
        );

    DECLARE @TongSoDong int = (SELECT COUNT(*) FROM #TaiKhoanLoc);

    -- 2. Phân trang đúng @SoDong dòng của trang đang xem
    CREATE TABLE #Trang (ID bigint PRIMARY KEY, ThuTu int);

    INSERT INTO #Trang (ID, ThuTu)
    SELECT ID, ROW_NUMBER() OVER (ORDER BY ID DESC)
    FROM #TaiKhoanLoc
    ORDER BY ID DESC
    OFFSET (@Trang - 1) * @SoDong ROWS FETCH NEXT @SoDong ROWS ONLY;

    -- Result Set 1: Danh sách tài khoản đã phân trang
    SELECT tk.ID, tk.SDT, tk.Email, tk.Role, tk.MatKhauNoiBo, tk.IDBenhNhan, tk.NgayTao,
           @TongSoDong AS TongSoDong
    FROM #Trang tr
    JOIN dbo.HT_TaiKhoan tk WITH (NOLOCK) ON tr.ID = tk.ID
    ORDER BY tr.ThuTu;

    -- Result Set 2: Hồ sơ bệnh nhân kèm theo của các tài khoản trong trang
    SELECT tr.ThuTu,
           b.ID AS IdBenhNhan,
           tr.ID AS IdTaiKhoan,
           b.TenBN,
           b.CCCD,
           b.SDT,
           b.Email,
           b.DiaChi,
           b.NgaySinh,
           CAST(b.GioiTinh AS varchar(10)) AS GioiTinhStr,
           cs.MaBN,
           kcb.TenCoSo,
           cs.ID AS IdHoSoCoSo,
           cs.IDCoSo,
           (SELECT COUNT(*) FROM dbo.DM_BenhNhanCoSo c2 WITH (NOLOCK) WHERE c2.IDBenhNhan = b.ID AND c2.MaBN IS NOT NULL AND c2.MaBN <> '') AS SoCoSo
    FROM #Trang tr
    JOIN dbo.HT_TaiKhoan tk WITH (NOLOCK) ON tr.ID = tk.ID
    JOIN dbo.DM_BenhNhan b WITH (NOLOCK) ON b.IDTaiKhoan = tr.ID OR (b.IDTaiKhoan IS NULL AND tk.SDT IS NOT NULL AND b.SDT = tk.SDT) OR (b.ID = tk.IDBenhNhan)
    LEFT JOIN dbo.DM_BenhNhanCoSo cs WITH (NOLOCK) ON cs.IDBenhNhan = b.ID
    LEFT JOIN dbo.DM_CSKCB kcb WITH (NOLOCK) ON cs.IDCoSo = kcb.ID
    ORDER BY tr.ThuTu, b.ID, (CASE WHEN cs.MaBN IS NOT NULL AND cs.MaBN <> '' THEN 0 ELSE 1 END), cs.ID;

    DROP TABLE #Trang;
    DROP TABLE #TaiKhoanLoc;
END;
GO

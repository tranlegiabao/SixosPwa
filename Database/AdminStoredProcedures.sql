CREATE OR ALTER PROCEDURE dbo.Admin_TaiKhoan_Save
    @Id BIGINT,
    @SDT VARCHAR(20),
    @Role NVARCHAR(50),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @Id = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM TaiKhoan WHERE SDT = @SDT)
            BEGIN
                SET @ResultCode = 2;
                SET @ResultMessage = N'Số điện thoại đã tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            INSERT INTO TaiKhoan (SDT, Role)
            VALUES (@SDT, @Role);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM TaiKhoan WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Tài khoản không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            IF EXISTS (SELECT 1 FROM TaiKhoan WHERE SDT = @SDT AND ID <> @Id)
            BEGIN
                SET @ResultCode = 2;
                SET @ResultMessage = N'Số điện thoại đã tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE TaiKhoan
            SET SDT = @SDT,
                Role = @Role
            WHERE ID = @Id;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_QCKCB_Save
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @NoiDung NVARCHAR(MAX),
    @Img NVARCHAR(MAX),
    @Enabled BIT,
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    DECLARE @NormalizedMaCoSo NVARCHAR(10) = NULLIF(LTRIM(RTRIM(@MaCoSo)), N'');
    DECLARE @NormalizedTenCoSo NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@TenCoSo)), N'');
    DECLARE @NormalizedNoiDung NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@NoiDung)), N'');
    DECLARE @NormalizedImg NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@Img)), N'');
    DECLARE @ExistingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT TOP (1) @ExistingId = ID
        FROM QC_KCB WITH (UPDLOCK, HOLDLOCK)
        WHERE (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
           OR (@NormalizedMaCoSo IS NULL
               AND (MaCoSo IS NULL OR MaCoSo = N'')
               AND TenCoSo = @NormalizedTenCoSo)
        ORDER BY ID;

        IF ISNULL(@Enabled, 0) = 0
           OR (@NormalizedNoiDung IS NULL AND @NormalizedImg IS NULL)
        BEGIN
            DELETE FROM QC_KCB
            WHERE (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
               OR (@NormalizedMaCoSo IS NULL
                   AND (MaCoSo IS NULL OR MaCoSo = N'')
                   AND TenCoSo = @NormalizedTenCoSo);
        END
        ELSE IF @ExistingId IS NULL
        BEGIN
            INSERT INTO QC_KCB (MaCoSo, TenCoSo, NoiDung, Img)
            VALUES (@NormalizedMaCoSo, @NormalizedTenCoSo, @NormalizedNoiDung, @NormalizedImg);
        END
        ELSE
        BEGIN
            UPDATE QC_KCB
            SET MaCoSo = @NormalizedMaCoSo,
                TenCoSo = @NormalizedTenCoSo,
                NoiDung = @NormalizedNoiDung,
                Img = @NormalizedImg
            WHERE ID = @ExistingId;

            DELETE FROM QC_KCB
            WHERE ID <> @ExistingId
              AND ((@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                   OR (@NormalizedMaCoSo IS NULL
                       AND (MaCoSo IS NULL OR MaCoSo = N'')
                       AND TenCoSo = @NormalizedTenCoSo));
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_NDCSKCB_Get
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @LoaiND BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StoredLoaiND NVARCHAR(20) = NULLIF(CONVERT(NVARCHAR(20), @LoaiND), N'');
    DECLARE @LegacyLoaiND NVARCHAR(20) = (
        SELECT NULLIF(LTRIM(RTRIM(LoaiND)), N'')
        FROM DMChuDe
        WHERE ID = @LoaiND
    );

    SELECT TOP (1) NoiDung
    FROM ND_CSKCB
    WHERE (LoaiND = @StoredLoaiND
        OR (@LegacyLoaiND IS NOT NULL AND LoaiND = @LegacyLoaiND))
      AND (
            (NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NOT NULL
             AND MaCoSo = NULLIF(LTRIM(RTRIM(@MaCoSo)), N''))
         OR (NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NULL
             AND (MaCoSo IS NULL OR MaCoSo = N'')
             AND TenCoSo = NULLIF(LTRIM(RTRIM(@TenCoSo)), N''))
      )
    ORDER BY ID DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_NDCSKCB_Save
    @MaCoSo NVARCHAR(10),
    @TenCoSo NVARCHAR(100),
    @LoaiND BIGINT,
    @NoiDung NVARCHAR(MAX),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    DECLARE @StoredLoaiND NVARCHAR(20) = NULLIF(CONVERT(NVARCHAR(20), @LoaiND), N'');
    DECLARE @LegacyLoaiND NVARCHAR(20) = (
        SELECT NULLIF(LTRIM(RTRIM(LoaiND)), N'')
        FROM DMChuDe
        WHERE ID = @LoaiND
    );

    IF @LoaiND IS NULL OR @LoaiND <= 0
    BEGIN
        SET @ResultCode = 4;
        SET @ResultMessage = N'Chủ đề nội dung không hợp lệ.';
        RETURN;
    END;

    DECLARE @NormalizedMaCoSo NVARCHAR(10) = NULLIF(LTRIM(RTRIM(@MaCoSo)), N'');
    DECLARE @NormalizedTenCoSo NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@TenCoSo)), N'');
    DECLARE @NormalizedNoiDung NVARCHAR(MAX) = NULLIF(LTRIM(RTRIM(@NoiDung)), N'');
    DECLARE @ExistingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @NormalizedNoiDung IS NULL
        BEGIN
            DELETE FROM ND_CSKCB
            WHERE (LoaiND = @StoredLoaiND
                OR (@LegacyLoaiND IS NOT NULL AND LoaiND = @LegacyLoaiND))
              AND (
                    (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                 OR (@NormalizedMaCoSo IS NULL
                     AND (MaCoSo IS NULL OR MaCoSo = N'')
                     AND TenCoSo = @NormalizedTenCoSo)
              );
        END
        ELSE
        BEGIN
            SELECT TOP (1) @ExistingId = ID
            FROM ND_CSKCB WITH (UPDLOCK, HOLDLOCK)
            WHERE (LoaiND = @StoredLoaiND
                OR (@LegacyLoaiND IS NOT NULL AND LoaiND = @LegacyLoaiND))
              AND (
                    (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                 OR (@NormalizedMaCoSo IS NULL
                     AND (MaCoSo IS NULL OR MaCoSo = N'')
                     AND TenCoSo = @NormalizedTenCoSo)
              )
            ORDER BY ID;

            IF @ExistingId IS NULL
            BEGIN
                INSERT INTO ND_CSKCB (MaCoSo, TenCoSo, NoiDung, LoaiND)
                VALUES (@NormalizedMaCoSo, @NormalizedTenCoSo, @NormalizedNoiDung, @StoredLoaiND);
            END
            ELSE
            BEGIN
                UPDATE ND_CSKCB
                SET MaCoSo = @NormalizedMaCoSo,
                    TenCoSo = @NormalizedTenCoSo,
                    NoiDung = @NormalizedNoiDung,
                    LoaiND = @StoredLoaiND
                WHERE ID = @ExistingId;

                DELETE FROM ND_CSKCB
                WHERE ID <> @ExistingId
                  AND (LoaiND = @StoredLoaiND
                      OR (@LegacyLoaiND IS NOT NULL AND LoaiND = @LegacyLoaiND))
                  AND (
                        (@NormalizedMaCoSo IS NOT NULL AND MaCoSo = @NormalizedMaCoSo)
                     OR (@NormalizedMaCoSo IS NULL
                         AND (MaCoSo IS NULL OR MaCoSo = N'')
                         AND TenCoSo = @NormalizedTenCoSo)
                  );
            END;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_DoiTac_Save
    @Id BIGINT,
    @MaDT NVARCHAR(20),
    @TenDT NVARCHAR(100),
    @DiaChi NVARCHAR(255),
    @SDT VARCHAR(20),
    @Email VARCHAR(100),
    @BrandName NVARCHAR(100),
    @Password NVARCHAR(255),
    @UpdatePassword BIT,
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM DMDoiTac WHERE MaDT = @MaDT AND ID <> @Id)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã đối tác đã tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @Id = 0
        BEGIN
            IF NULLIF(LTRIM(RTRIM(@Password)), N'') IS NULL
            BEGIN
                SET @ResultCode = 4;
                SET @ResultMessage = N'Vui lòng nhập mật khẩu đối tác.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            INSERT INTO DMDoiTac (MaDT, TenDT, DiaChi, SDT, Email, BrandName, Password)
            VALUES (@MaDT, @TenDT, @DiaChi, @SDT, @Email, @BrandName, @Password);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM DMDoiTac WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Đối tác không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE DMDoiTac
            SET MaDT = @MaDT,
                TenDT = @TenDT,
                DiaChi = @DiaChi,
                SDT = @SDT,
                Email = @Email,
                BrandName = @BrandName,
                Password = CASE WHEN @UpdatePassword = 1 THEN @Password ELSE Password END
            WHERE ID = @Id;
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.Admin_CoSoYTe_Save
    @Id BIGINT,
    @MaCoSo NVARCHAR(10),
    @Slug NVARCHAR(100),
    @TenCoSo NVARCHAR(100),
    @DiaChi NVARCHAR(255),
    @SoToaNha NVARCHAR(100),
    @Tinh INT,
    @PhuongXa INT,
    @LoaiCS NVARCHAR(20),
    @TGLamViec NVARCHAR(50),
    @NgayLamViec NVARCHAR(50),
    @GioMoCua TIME(0),
    @GioDongCua TIME(0),
    @XacMinh INT,
    @Img NVARCHAR(500),
    @Logo NVARCHAR(MAX),
    @QuangCao DECIMAL(15, 0),
    @OldMaCoSo NVARCHAR(10),
    @OldTenCoSo NVARCHAR(100),
    @ResultCode INT OUTPUT,
    @ResultMessage NVARCHAR(4000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @ResultCode = 0;
    SET @ResultMessage = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NULLIF(LTRIM(RTRIM(@MaCoSo)), N'') IS NOT NULL
           AND EXISTS (SELECT 1 FROM DMCSKCB WHERE MaCoSo = @MaCoSo AND ID <> @Id)
        BEGIN
            SET @ResultCode = 2;
            SET @ResultMessage = N'Mã cơ sở đã tồn tại.';
            ROLLBACK TRANSACTION;
            RETURN;
        END;

        IF @Id = 0
        BEGIN
            INSERT INTO DMCSKCB
                (MaCoSo, Slug, TenCoSo, DiaChi, SoToaNha, Tinh, PhuongXa, LoaiCS,
                 TGLamViec, NgayLamViec, GioMoCua, GioDongCua, XacMinh, Img, logo, QuangCao)
            VALUES
                (@MaCoSo, @Slug, @TenCoSo, @DiaChi, @SoToaNha, @Tinh, @PhuongXa, @LoaiCS,
                 @TGLamViec, @NgayLamViec, @GioMoCua, @GioDongCua, @XacMinh, @Img, @Logo, @QuangCao);
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM DMCSKCB WHERE ID = @Id)
            BEGIN
                SET @ResultCode = 3;
                SET @ResultMessage = N'Cơ sở y tế không tồn tại.';
                ROLLBACK TRANSACTION;
                RETURN;
            END;

            UPDATE DMCSKCB
            SET MaCoSo = @MaCoSo,
                Slug = @Slug,
                TenCoSo = @TenCoSo,
                DiaChi = @DiaChi,
                SoToaNha = @SoToaNha,
                Tinh = @Tinh,
                PhuongXa = @PhuongXa,
                LoaiCS = @LoaiCS,
                TGLamViec = @TGLamViec,
                NgayLamViec = @NgayLamViec,
                GioMoCua = @GioMoCua,
                GioDongCua = @GioDongCua,
                XacMinh = @XacMinh,
                Img = @Img,
                logo = @Logo,
                QuangCao = @QuangCao
            WHERE ID = @Id;
        END;

        IF (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') IS NOT NULL
            OR NULLIF(LTRIM(RTRIM(@OldTenCoSo)), N'') IS NOT NULL)
           AND (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') <> NULLIF(LTRIM(RTRIM(@MaCoSo)), N'')
                OR ISNULL(@OldTenCoSo, N'') <> ISNULL(@TenCoSo, N''))
        BEGIN
            UPDATE ND_CSKCB
            SET MaCoSo = @MaCoSo,
                TenCoSo = @TenCoSo
            WHERE (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') IS NOT NULL
                   AND MaCoSo = NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N''))
               OR (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') IS NULL
                   AND (MaCoSo IS NULL OR MaCoSo = N'')
                   AND TenCoSo = NULLIF(LTRIM(RTRIM(@OldTenCoSo)), N''));

            UPDATE QC_KCB
            SET MaCoSo = @MaCoSo,
                TenCoSo = @TenCoSo
            WHERE (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') IS NOT NULL
                   AND MaCoSo = NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N''))
               OR (NULLIF(LTRIM(RTRIM(@OldMaCoSo)), N'') IS NULL
                   AND (MaCoSo IS NULL OR MaCoSo = N'')
                   AND TenCoSo = NULLIF(LTRIM(RTRIM(@OldTenCoSo)), N''));
        END;

        COMMIT TRANSACTION;
        SET @ResultCode = 1;
        SET @ResultMessage = N'OK';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

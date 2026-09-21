/* =============================================================================
   DOT 1B - buoc B11: don 807 nhom (SDT, co so) giu nhieu hon mot nguoi  - C8
   ---------------------------------------------------------------------------
   Luat R2: mot so dien thoai giu dung MOT ho so tai MOT co so.
   🔴 KHONG dat duoc UNIQUE(SDT, IDCoSo) o DB vi 807 nhom dang vi pham san
      => luat nay song o tang ung dung/stored, KHONG co luoi an toan o DB.

   Cach don: GIU mot dong moi nhom (uu tien dong CO MaBN, roi toi ID nho nhat),
   GO SDT cua cac dong con lai ve NULL. Khong xoa dong nao - tai lieu va dot
   kham cua ho VAN CON NGUYEN, chi la tam thoi khong co loi dang nhap.

   ═══ CAI GIA DA DO VA USER DA CHAP NHAN SAU KHI XEM SO ═══════════════════
     1.336 dong bi go SDT, trong do:
         607 dong RONG TUOT (khong MaBN, khong tai lieu, khong dot kham)
                 -> khong mat gi
         729 dong la BENH NHAN THAT (co MaBN)
                 -> giu 30.175 tai lieu + 3.837 dot kham
     Cong 235 dong SDT rong san => 1.571 ho so khong co loi vao ngay sau khi chay.
   ═════════════════════════════════════════════════════════════════════════

   🔴 DAY KHONG PHAI VIEC CHAY MOT LAN. Cua 1 DM_BenhNhan_Save co cau
      SDT = ISNULL(@SDT, SDT) => HIS day sang la SDT QUAY LAI, nhom vi pham
      song lai. Mat tot: 729 nguoi chi mat duong vao TOI LAN KHAM KE TIEP,
      khong mat han. Mat xau: phai chay lai file nay dinh ky.
      File viet de CHAY LAI DUOC nhieu lan.

   🔴 Tat app truoc khi chay (claim HoSoDangChon trong cookie giu ID cu).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF COL_LENGTH('dbo.DM_BenhNhan', 'IDCoSo') IS NULL
    THROW 50120, 'Chua chay B02. DUNG LAI.', 1;
IF SCHEMA_ID('bak3') IS NULL EXEC('CREATE SCHEMA bak3');
GO

/* --- 1. kho chup (ID, SDT) - DUONG LUI DUY NHAT ------------------------- */
IF OBJECT_ID('bak3.SdtDaGo_B11') IS NULL
    CREATE TABLE bak3.SdtDaGo_B11
    (
        ID       bigint      NOT NULL,
        SDT      varchar(20) NULL,
        IDCoSo   bigint      NULL,
        MaBN     varchar(20) NULL,
        NgayGo   datetime    NOT NULL CONSTRAINT DF_SdtDaGo_B11_NgayGo DEFAULT (GETDATE()),
        CONSTRAINT PK_SdtDaGo_B11 PRIMARY KEY (ID, NgayGo)
    );
GO

/* --- 2. bang tam: nhung dong sap bi go ---------------------------------- */
IF OBJECT_ID('tempdb..#go') IS NOT NULL DROP TABLE #go;

SELECT b.ID, b.SDT, b.IDCoSo, b.MaBN
  INTO #go
  FROM (
        SELECT ID, SDT, IDCoSo, MaBN,
               ROW_NUMBER() OVER (
                   PARTITION BY SDT, IDCoSo
                   /* uu tien GIU: dong co MaBN truoc, roi toi ID nho nhat */
                   ORDER BY CASE WHEN MaBN IS NOT NULL THEN 0 ELSE 1 END, ID
               ) AS hang
          FROM dbo.DM_BenhNhan
         WHERE SDT IS NOT NULL AND SDT <> ''
           AND IDCoSo IS NOT NULL      -- dong neo khong tinh: no khong dang nhap duoc
       ) b
 WHERE b.hang > 1;
GO

/* --- 3. bao cao TRUOC khi dong vao - de doi chieu voi bien ban ---------- */
SELECT 'B11 truoc khi go' AS Buoc,
       (SELECT COUNT(*) FROM #go)                                  AS SoDongSeGo_ky_vong_1336,
       (SELECT COUNT(*) FROM #go WHERE MaBN IS NOT NULL)           AS BenhNhanThat_ky_vong_729,
       (SELECT COUNT(*) FROM #go WHERE MaBN IS NULL)               AS RongTuot_ky_vong_607,
       (SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan t
         WHERE t.IDBenhNhan IN (SELECT ID FROM #go))               AS TaiLieu_anh_huong_ky_vong_30175,
       (SELECT COUNT(*) FROM dbo.QL_DotKham d
         WHERE d.IDBenhNhan IN (SELECT ID FROM #go))               AS DotKham_anh_huong_ky_vong_3837,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan
         WHERE IDCoSo IS NOT NULL AND (SDT IS NULL OR SDT = ''))   AS SdtRongSan_ky_vong_235;
GO

/* --- 4. CHUP roi GO, trong CUNG MOT BATCH ------------------------------- */
/* Chot cung batch voi cau ghi: THROW/RETURN chi thoat BATCH. */
DECLARE @soGo int = (SELECT COUNT(*) FROM #go);

IF @soGo = 0
BEGIN
    SELECT 'B11: khong con nhom vi pham nao - bo qua.' AS KetQua;
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    INSERT INTO bak3.SdtDaGo_B11 (ID, SDT, IDCoSo, MaBN)
    SELECT ID, SDT, IDCoSo, MaBN FROM #go;

    DECLARE @daChup int = @@ROWCOUNT;

    IF @daChup <> @soGo
    BEGIN
        ROLLBACK TRANSACTION;
        RAISERROR('B11 DUNG LAI: can chup %d dong, chup duoc %d. KHONG go SDT.',
                  16, 1, @soGo, @daChup);
    END
    ELSE
    BEGIN
        UPDATE b SET b.SDT = NULL
          FROM dbo.DM_BenhNhan b
          JOIN #go g ON g.ID = b.ID;

        COMMIT TRANSACTION;
        SELECT 'B11: da go SDT.' AS KetQua, @soGo AS SoDongDaGo;
    END
END
GO

/* --- 5. tu kiem --------------------------------------------------------- */
SELECT 'B11 sau khi go' AS Buoc,
       (SELECT COUNT(*) FROM (
            SELECT SDT, IDCoSo FROM dbo.DM_BenhNhan
             WHERE SDT IS NOT NULL AND SDT <> '' AND IDCoSo IS NOT NULL
             GROUP BY SDT, IDCoSo HAVING COUNT(*) > 1) t)             AS NhomConViPham_phai_0,
       (SELECT COUNT(*) FROM bak3.SdtDaGo_B11)                        AS DaChupDuongLui,
       (SELECT COUNT(*) FROM dbo.DM_BenhNhan
         WHERE IDCoSo IS NOT NULL AND (SDT IS NULL OR SDT = ''))       AS KhongCoLoiVao_ky_vong_1571,
       (SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan)                  AS TaiLieu_van_nguyen_614636,
       (SELECT COUNT(*) FROM dbo.QL_DotKham)                          AS DotKham_van_nguyen_84153;
GO
DROP TABLE #go;
GO

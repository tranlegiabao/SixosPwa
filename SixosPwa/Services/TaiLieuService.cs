using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

public class TaiLieuService : ITaiLieuService
{
    private const int MaxPdfSizeBytes = 20 * 1024 * 1024; // 20MB
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _spService;
    private readonly IFtpService _ftpService;
    private readonly ILogger<TaiLieuService> _logger;

    public TaiLieuService(
        ApplicationDbContext db,
        AdminStoredProcedureService spService,
        IFtpService ftpService,
        ILogger<TaiLieuService> logger)
    {
        _db = db;
        _spService = spService;
        _ftpService = ftpService;
        _logger = logger;
    }

    public async Task<TiepNhanTaiLieuResponseData> TiepNhanTaiLieuAsync(
        DMCSKCB cskcb,
        string loaiTaiLieu,
        TiepNhanTaiLieuRequest request)
    {
        if (cskcb == null)
            throw new ArgumentNullException(nameof(cskcb));

        if (string.IsNullOrWhiteSpace(loaiTaiLieu))
            throw new ArgumentException("Loại tài liệu (X-Loai-Tai-Lieu) không được để trống.");

        if (string.IsNullOrWhiteSpace(request.MaBenhNhan))
            throw new ArgumentException("Mã bệnh nhân (maBenhNhan) không được để trống.");

        if (string.IsNullOrWhiteSpace(request.FilePdf))
            throw new ArgumentException("Dữ liệu tệp PDF mã hóa Base64 (filePdf) không được để trống.");

        // 1. Tách phần tiền tố Data URL nếu có (ví dụ: data:application/pdf;base64,...)
        var base64 = request.FilePdf.Trim();
        var commaIndex = base64.IndexOf(',');
        if (commaIndex >= 0 && base64[..commaIndex].Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            base64 = base64[(commaIndex + 1)..].Trim();
        }

        // 2. Giải mã Base64 sang mảng byte
        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Dữ liệu tệp PDF không đúng định dạng Base64 hợp lệ.");
        }

        // 3. Kiểm tra kích thước file
        if (pdfBytes.Length == 0)
        {
            throw new ArgumentException("Tệp PDF có kích thước rỗng (0 byte).");
        }

        if (pdfBytes.Length > MaxPdfSizeBytes)
        {
            throw new ArgumentException($"Dung lượng tệp PDF ({pdfBytes.Length / (1024 * 1024.0):F2}MB) vượt quá giới hạn cho phép (tối đa {MaxPdfSizeBytes / (1024 * 1024)}MB).");
        }

        // 4. Kiểm tra magic bytes của định dạng PDF (%PDF-)
        if (pdfBytes.Length < 4 ||
            pdfBytes[0] != 0x25 || // %
            pdfBytes[1] != 0x50 || // P
            pdfBytes[2] != 0x44 || // D
            pdfBytes[3] != 0x46)   // F
        {
            throw new ArgumentException("Tệp dữ liệu không phải định dạng PDF hợp lệ (không tìm thấy tiêu đề %PDF-).");
        }

        // 5. Upload lên máy chủ FTP dùng chung theo ADR 0012
        var now = DateTime.Now;
        var remoteDir = $"{KhoAnh.GocFtp}/tailieu/{cskcb.MaCoSo}/{now:yyyy}/{now:MM}";

        var safeMaBN = Regex.Replace(request.MaBenhNhan.Trim(), @"[^a-zA-Z0-9_\-]", "_");
        var safeLoaiTL = Regex.Replace(loaiTaiLieu.Trim(), @"[^a-zA-Z0-9_\-]", "_");
        var fileName = $"{safeMaBN}_{safeLoaiTL}_{now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}.pdf";

        var remoteFilePath = await _ftpService.UploadBytesAsync(pdfBytes, fileName, remoteDir);

        // 6. Xác định hồ sơ bệnh nhân tại cơ sở (DM_BenhNhanCoSo)
        long? idBenhNhanCoSo = null;

        // 6.1. Ưu tiên tìm định danh con người (DM_BenhNhan) theo CCCD hoặc SĐT nếu có gửi
        BenhNhan? benhNhan = null;
        if (!string.IsNullOrWhiteSpace(request.Cccd))
        {
            var cccdClean = request.Cccd.Trim();
            benhNhan = await _db.BenhNhans
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.CCCD == cccdClean);
        }

        if (benhNhan == null && !string.IsNullOrWhiteSpace(request.Sdt))
        {
            var sdtClean = request.Sdt.Trim();
            benhNhan = await _db.BenhNhans
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.SDT == sdtClean);
        }

        // 6.2. Nếu chưa có con người trong hệ thống nhưng có CCCD -> Tự động đăng ký con người mới
        if (benhNhan == null && !string.IsNullOrWhiteSpace(request.Cccd))
        {
            var hoTen = !string.IsNullOrWhiteSpace(request.HoTen)
                ? request.HoTen.Trim()
                : $"Bệnh nhân {request.MaBenhNhan.Trim()}";

            var (kqBn, newIdBn) = await _spService.SaveBenhNhanAsync(
                cccd: request.Cccd.Trim(),
                tenBN: hoTen,
                sdt: request.Sdt?.Trim(),
                email: null,
                diaChi: null);

            if (kqBn.Succeeded && newIdBn > 0)
            {
                benhNhan = new BenhNhan
                {
                    Id = newIdBn,
                    CCCD = request.Cccd.Trim(),
                    TenBN = hoTen,
                    SDT = request.Sdt?.Trim()
                };
                _logger.LogInformation("Tự động tạo bản ghi DM_BenhNhan mới (ID={Id}, CCCD={Cccd})", newIdBn, request.Cccd.Trim());
            }
        }

        // 6.3. Nếu đã xác định được con người (DM_BenhNhan)
        if (benhNhan != null)
        {
            // Kiểm tra xem con người này đã có hồ sơ tại cơ sở này chưa
            var bnCoSo = await _db.BenhNhanCoSos
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IdCoSo == cskcb.Id && b.IdBenhNhan == benhNhan.Id);

            if (bnCoSo != null)
            {
                idBenhNhanCoSo = bnCoSo.Id;
            }
            else
            {
                // Kiểm tra xem MaBN tại cơ sở này đã bị gán cho người khác chưa
                var trungMaBn = await _db.BenhNhanCoSos
                    .AsNoTracking()
                    .AnyAsync(b => b.IdCoSo == cskcb.Id && b.MaBN == request.MaBenhNhan.Trim());

                if (!trungMaBn)
                {
                    // Tự động tạo hồ sơ tại cơ sở này
                    var (kqCoSo, newIdCoSo) = await _spService.SaveBenhNhanCoSoAsync(
                        idBenhNhan: benhNhan.Id,
                        idCoSo: cskcb.Id,
                        maBN: request.MaBenhNhan.Trim());

                    if (kqCoSo.Succeeded && newIdCoSo > 0)
                    {
                        idBenhNhanCoSo = newIdCoSo;
                        _logger.LogInformation("Tự động liên kết DM_BenhNhanCoSo (ID={Id}, MaBN={MaBN}, IDCoSo={IDCoSo})",
                            newIdCoSo, request.MaBenhNhan.Trim(), cskcb.Id);
                    }
                }
            }
        }

        // 6.4. Fallback: Nếu không có CCCD/SĐT hoặc không map được con người, tìm theo (IDCoSo, MaBN) như cũ
        if (idBenhNhanCoSo == null)
        {
            var bnCoSo = await _db.BenhNhanCoSos
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.IdCoSo == cskcb.Id && b.MaBN == request.MaBenhNhan.Trim());
            idBenhNhanCoSo = bnCoSo?.Id;
        }

        // 7. Xác định tên tài liệu
        var tenTaiLieu = string.IsNullOrWhiteSpace(request.TenTaiLieu)
            ? $"{loaiTaiLieu.Trim()} - BN {request.MaBenhNhan.Trim()} - {now:dd/MM/yyyy HH:mm}"
            : request.TenTaiLieu.Trim();

        // 8. Lưu metadata vào Database qua Stored Procedure (ADR 0008)
        var (ketQua, idTaiLieu) = await _spService.SaveTaiLieuBenhNhanAsync(
            id: 0,
            idCoSo: cskcb.Id,
            idBenhNhanCoSo: idBenhNhanCoSo,
            maBN: request.MaBenhNhan.Trim(),
            loaiTaiLieu: loaiTaiLieu.Trim(),
            tenTaiLieu: tenTaiLieu,
            duongDanFtp: remoteFilePath,
            dungLuongByte: pdfBytes.Length,
            ngayKham: request.NgayKham,
            ghiChu: request.GhiChu?.Trim());

        if (!ketQua.Succeeded)
        {
            _logger.LogError("Lỗi khi lưu metadata tài liệu vào DB: {Message}", ketQua.Message);
            throw new InvalidOperationException(ketQua.Message ?? "Không thể lưu thông tin tài liệu vào hệ thống.");
        }

        return new TiepNhanTaiLieuResponseData
        {
            Id = idTaiLieu,
            IdBenhNhanCoSo = idBenhNhanCoSo,
            MaBN = request.MaBenhNhan.Trim(),
            LoaiTaiLieu = loaiTaiLieu.Trim(),
            TenTaiLieu = tenTaiLieu,
            DuongDan = $"/api/v1/tai-lieu/xem/{idTaiLieu}",
            DungLuongByte = pdfBytes.Length,
            NgayTao = now
        };
    }

    public Task<TaiLieuBenhNhan?> LayTaiLieuAsync(long id)
    {
        return _db.TaiLieuBenhNhans
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public Task<Stream> TaiStreamPdfAsync(string duongDanFtp)
    {
        return _ftpService.DownloadAsync(duongDanFtp);
    }
}

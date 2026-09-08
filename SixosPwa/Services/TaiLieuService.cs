using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

/// <summary>
/// Ma benh nhan chua co ho so nao nhan ben cong => tu choi ca goi tin
/// (chot 3, ADR 0021). Tach thanh ngoai le RIENG chu khong dung
/// InvalidOperationException chung, de controller tra dung 409 kem ma may doc
/// duoc thay vi 500 — hang doi ben HIS phai phan biet duoc "chua co nguoi nhan"
/// voi "loi ky thuat", neu khong thi no thu lai vo ich mai.
/// </summary>
public class ChuaCoNguoiNhanException : Exception
{
    public string MaBN { get; }

    public ChuaCoNguoiNhanException(string maBN)
        : base($"Mã bệnh nhân '{maBN}' chưa có hồ sơ nào nhận tại cổng.")
    {
        MaBN = maBN;
    }
}

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

        // 5. Xác định hồ sơ TRƯỚC KHI đụng tới kho tệp.
        //
        // 🔴 Thứ tự này là bắt buộc (V4). Bản trước đẩy tệp lên FTP rồi mới tra
        // hồ sơ, nên mỗi lần bị từ chối lại bỏ lại một tệp rác trên kho mà không
        // ai dọn — cơ sở dữ liệu sạch nhưng kho thì không.
        //
        // 🔴 Khoá tra cứu CHỈ là (cơ sở, mã bệnh nhân) — V2/V3. Bản trước dò
        // theo CCCD rồi rơi xuống dò theo số điện thoại, và còn tự tạo bản ghi
        // con người mới từ chính gói tin. Cả ba đều là đường đưa bệnh án người
        // này cho người kia: đo trên dữ liệu thật có 345 nhóm cùng CCCD khác
        // tên, và một số điện thoại gắn tới 876 người (ADR 0018). Việc nối hồ sơ
        // là của NGƯỜI DÙNG ở màn Nối hồ sơ, không phải của đường API.
        //
        // Không tìm thấy thì TỪ CHỐI (chốt 3, ADR 0021) — cổng không giữ một
        // byte bệnh án nào của người chưa phải người dùng của nó.
        var maBNSach = request.MaBenhNhan.Trim();

        var hoSo = await _db.BenhNhanCoSos
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.IdCoSo == cskcb.Id && b.MaBN == maBNSach);

        if (hoSo == null)
        {
            _logger.LogInformation(
                "Tu choi tai lieu: ma benh nhan {MaBN} chua co ho so nao tai co so {MaCoSo}",
                maBNSach, cskcb.MaCoSo);
            throw new ChuaCoNguoiNhanException(maBNSach);
        }

        // 6. Upload lên máy chủ FTP dùng chung theo ADR 0012
        var now = DateTime.Now;
        var remoteDir = $"{KhoAnh.GocFtp}/tailieu/{cskcb.MaCoSo}/{now:yyyy}/{now:MM}";

        var safeMaBN = Regex.Replace(maBNSach, @"[^a-zA-Z0-9_\-]", "_");
        var safeLoaiTL = Regex.Replace(loaiTaiLieu.Trim(), @"[^a-zA-Z0-9_\-]", "_");
        var fileName = $"{safeMaBN}_{safeLoaiTL}_{now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}.pdf";

        var remoteFilePath = await _ftpService.UploadBytesAsync(pdfBytes, fileName, remoteDir);

        // 7. Xác định tên tài liệu
        var tenTaiLieu = string.IsNullOrWhiteSpace(request.TenTaiLieu)
            ? $"{loaiTaiLieu.Trim()} - BN {maBNSach} - {now:dd/MM/yyyy HH:mm}"
            : request.TenTaiLieu.Trim();

        // 8. Lưu metadata vào Database qua Stored Procedure (ADR 0008)
        var (ketQua, idTaiLieu) = await _spService.SaveTaiLieuBenhNhanAsync(
            id: 0,
            idCoSo: cskcb.Id,
            idBenhNhanCoSo: hoSo.Id,
            maBN: maBNSach,
            loaiTaiLieu: loaiTaiLieu.Trim(),
            tenTaiLieu: tenTaiLieu,
            duongDanFtp: remoteFilePath,
            dungLuongByte: pdfBytes.Length,
            ngayKham: request.NgayKham,
            ghiChu: request.GhiChu?.Trim(),
            maNguonHIS: request.MaNguonHIS?.Trim());

        if (!ketQua.Succeeded)
        {
            _logger.LogError("Lỗi khi lưu metadata tài liệu vào DB: {Message}", ketQua.Message);
            throw new InvalidOperationException(ketQua.Message ?? "Không thể lưu thông tin tài liệu vào hệ thống.");
        }

        return new TiepNhanTaiLieuResponseData
        {
            Id = idTaiLieu,
            IdBenhNhanCoSo = hoSo.Id,
            MaBN = maBNSach,
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

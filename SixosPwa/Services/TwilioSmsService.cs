using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SixosPwa.Services;

/// <summary>
/// Dịch vụ gửi SMS qua Twilio
/// </summary>
public class TwilioSmsService : ISmsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromPhoneNumber;

    public TwilioSmsService(IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _accountSid = _configuration["Twilio:AccountSid"] ?? "";
        _authToken = _configuration["Twilio:AuthToken"] ?? "";
        _fromPhoneNumber = _configuration["Twilio:FromPhoneNumber"] ?? "";

        if (!string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken))
        {
            TwilioClient.Init(_accountSid, _authToken);
        }
    }

    public async Task<bool> GuiNhacLichKhamAsync(string soDienThoai, string tenBenhNhan, DateTime thoiGianKham, string tenBacSi, string phongKham)
    {
        var ngayKham = thoiGianKham.ToString("dd/MM/yyyy");
        var gioKham = thoiGianKham.ToString("HH:mm");
        
        var noiDung = $"Chào {tenBenhNhan},\n" +
                     $"Nhắc lịch khám: {ngayKham} lúc {gioKham}\n" +
                     $"Bác sĩ: {tenBacSi}\n" +
                     $"Địa điểm: {phongKham}\n" +
                     $"Vui lòng đến trước 15 phút.\n" +
                     $"Liên hệ để đổi lịch nếu cần.";

        return await GuiSmsAsync(soDienThoai, noiDung);
    }

    public async Task<bool> GuiXacNhanDatLichAsync(string soDienThoai, string tenBenhNhan, DateTime thoiGianKham, string maDatLich)
    {
        var ngayKham = thoiGianKham.ToString("dd/MM/yyyy");
        var gioKham = thoiGianKham.ToString("HH:mm");
        
        var noiDung = $"Xin chào {tenBenhNhan},\n" +
                     $"Đã đặt lịch thành công!\n" +
                     $"Mã đặt lịch: {maDatLich}\n" +
                     $"Thời gian: {ngayKham} - {gioKham}\n" +
                     $"Cảm ơn bạn đã tin tưởng!";

        return await GuiSmsAsync(soDienThoai, noiDung);
    }

    public async Task<bool> GuiKetQuaXetNghiemAsync(string soDienThoai, string tenBenhNhan, string loaiXetNghiem, string linkXemKetQua)
    {
        var noiDung = $"Chào {tenBenhNhan},\n" +
                     $"Kết quả {loaiXetNghiem} đã sẵn sàng!\n" +
                     $"Xem tại: {linkXemKetQua}\n" +
                     $"Liên hệ nếu cần tư vấn thêm.";

        return await GuiSmsAsync(soDienThoai, noiDung);
    }

    public async Task<bool> GuiNhacUongThuocAsync(string soDienThoai, string tenBenhNhan, string tenThuoc, string lieuDung)
    {
        var noiDung = $"Nhắc nhở: {tenBenhNhan}\n" +
                     $"Đã đến giờ uống thuốc {tenThuoc}\n" +
                     $"Liều dùng: {lieuDung}\n" +
                     $"Chúc bạn mau khỏe!";

        return await GuiSmsAsync(soDienThoai, noiDung);
    }

    public async Task<bool> GuiSmsAsync(string soDienThoai, string noiDung)
    {
        try
        {
            if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken))
            {
                _logger.LogWarning("Chưa cấu hình Twilio. SMS không được gửi: {NoiDung}", noiDung);
                return false;
            }

            // Chuẩn hóa số điện thoại (thêm +84 cho VN nếu cần)
            var soDienThoaiChuan = ChuanHoaSoDienThoai(soDienThoai);

            var message = await MessageResource.CreateAsync(
                body: noiDung,
                from: new PhoneNumber(_fromPhoneNumber),
                to: new PhoneNumber(soDienThoaiChuan)
            );

            _logger.LogInformation("Đã gửi SMS thành công. SID: {MessageSid}", message.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi SMS đến {SoDienThoai}", soDienThoai);
            return false;
        }
    }

    private string ChuanHoaSoDienThoai(string soDienThoai)
    {
        // Bỏ khoảng trắng và ký tự đặc biệt
        var so = new string(soDienThoai.Where(char.IsDigit).ToArray());

        // Nếu bắt đầu bằng 0 (VN) thì đổi thành +84
        if (so.StartsWith("0") && so.Length == 10)
        {
            return "+84" + so.Substring(1);
        }

        // Nếu bắt đầu bằng 84 thì thêm +
        if (so.StartsWith("84") && so.Length == 11)
        {
            return "+" + so;
        }

        // Nếu đã có + thì giữ nguyên
        if (soDienThoai.StartsWith("+"))
        {
            return soDienThoai;
        }

        return soDienThoai;
    }
}

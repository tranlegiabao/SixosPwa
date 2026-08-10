using Microsoft.AspNetCore.SignalR;

namespace SixosPwa.Hubs;

/// <summary>
/// Hub SignalR cho thông báo realtime
/// </summary>
public class ThongBaoHub : Hub
{
    private readonly ILogger<ThongBaoHub> _logger;

    public ThongBaoHub(ILogger<ThongBaoHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Lấy thông tin user từ Context
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var benhNhanId = Context.User?.FindFirst("BenhNhanId")?.Value;

        if (!string.IsNullOrEmpty(benhNhanId))
        {
            // Thêm user vào group theo BenhNhanId để gửi thông báo riêng
            await Groups.AddToGroupAsync(Context.ConnectionId, $"BenhNhan_{benhNhanId}");
            _logger.LogInformation("Bệnh nhân {BenhNhanId} đã kết nối SignalR", benhNhanId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var benhNhanId = Context.User?.FindFirst("BenhNhanId")?.Value;
        
        if (!string.IsNullOrEmpty(benhNhanId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"BenhNhan_{benhNhanId}");
            _logger.LogInformation("Bệnh nhân {BenhNhanId} đã ngắt kết nối SignalR", benhNhanId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

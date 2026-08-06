# Script chạy app và mở Cloudflare tunnel
# Sử dụng: .\start-tunnel.ps1

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "  Khởi động SixosPwa với Tunnel  " -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Kiểm tra cloudflared đã cài chưa
$cloudflaredExists = Get-Command cloudflared -ErrorAction SilentlyContinue
if (-not $cloudflaredExists) {
    Write-Host "❌ Chưa cài cloudflared!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Cài đặt bằng 1 trong 2 cách:" -ForegroundColor Yellow
    Write-Host "  1. winget install --id Cloudflare.cloudflared" -ForegroundColor White
    Write-Host "  2. Tải từ: https://github.com/cloudflare/cloudflared/releases" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "✓ Tìm thấy cloudflared" -ForegroundColor Green
Write-Host ""

# Chạy app trong background
Write-Host "🚀 Đang khởi động ứng dụng..." -ForegroundColor Yellow
$appJob = Start-Job -ScriptBlock {
    Set-Location "d:\web\SixosPwaTemplate\SixosPwaTemplate"
    dotnet run --project SixosPwa --launch-profile https
}

Write-Host "⏳ Chờ app khởi động (10 giây)..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Kiểm tra app đã chạy chưa
$appRunning = Test-NetConnection -ComputerName localhost -Port 7024 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
if (-not $appRunning.TcpTestSucceeded) {
    Write-Host "❌ App chưa khởi động được!" -ForegroundColor Red
    Write-Host "Kiểm tra lỗi:" -ForegroundColor Yellow
    Receive-Job $appJob
    Stop-Job $appJob
    Remove-Job $appJob
    exit 1
}

Write-Host "✓ App đã chạy trên https://localhost:7024" -ForegroundColor Green
Write-Host ""

# Mở tunnel
Write-Host "🌐 Đang mở Cloudflare Tunnel..." -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""

# Cleanup function
$cleanup = {
    Write-Host ""
    Write-Host "🛑 Đang tắt server..." -ForegroundColor Yellow
    Stop-Job $appJob -ErrorAction SilentlyContinue
    Remove-Job $appJob -ErrorAction SilentlyContinue
    Write-Host "✓ Đã tắt. Tạm biệt!" -ForegroundColor Green
}

# Đăng ký cleanup khi Ctrl+C
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action $cleanup | Out-Null

try {
    # Chạy cloudflared - sẽ in ra link
    cloudflared tunnel --url https://localhost:7024
}
finally {
    & $cleanup
}

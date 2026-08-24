
$content = Get-Content -Path "SixosPwa/Areas/Admin/Views/Dashboard/Index.cshtml" -Raw
$content = $content -replace "@keyframes", "@@keyframes"
Set-Content -Path "SixosPwa/Areas/Admin/Views/Dashboard/Index.cshtml" -Value $content -Encoding UTF8


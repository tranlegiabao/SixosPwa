# Script tu dong dong bo code sang ca 2 repository
# Su dung: 
#   .\sync-repos.ps1
#   hoac: .\sync-repos.ps1 "Noi dung commit"

param(
    [string]$Message = ""
)

$ErrorActionPreference = "Continue"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   DONG BO CODE SANG CA 2 REPOSITORY GITHUB       " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

$repo1Url = "https://github.com/tranlegiabao/SixosPwa.git"
$repo1Branch = "master1"

$repo2Url = "https://github.com/tranlegiabao/SixosPwa25.git"
$repo2Branch = "main"

# Cau hinh hoac cap nhat remote neu chua co
$existingRemotes = git remote
if ($existingRemotes -notcontains "repo-sixospwa") {
    Write-Host "[1/4] Them remote repo-sixospwa..." -ForegroundColor Yellow
    git remote add repo-sixospwa $repo1Url
} else {
    git remote set-url repo-sixospwa $repo1Url
}

if ($existingRemotes -notcontains "repo-sixospwa25") {
    Write-Host "[1/4] Them remote repo-sixospwa25..." -ForegroundColor Yellow
    git remote add repo-sixospwa25 $repo2Url
} else {
    git remote set-url repo-sixospwa25 $repo2Url
}

# Hoi noi dung commit neu chua truyen vao
if ([string]::IsNullOrWhiteSpace($Message)) {
    $inputMsg = Read-Host "Nhap noi dung commit (De trong de dung mac dinh)"
    if ([string]::IsNullOrWhiteSpace($inputMsg)) {
        $currentTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $Message = "Cap nhat dong bo [$currentTime]"
    } else {
        $Message = $inputMsg
    }
}

Write-Host ""
Write-Host "[2/4] Dang gom file va commit..." -ForegroundColor Yellow
git add .
$status = git status --porcelain
if ($status) {
    git commit -m "$Message"
    Write-Host "[OK] Da tao commit: $Message" -ForegroundColor Green
} else {
    Write-Host "[INFO] Khong co thay doi moi de commit, tiep tuc dong bo..." -ForegroundColor Gray
}

Write-Host ""
Write-Host "[3/4] Dang day len SixosPwa (nhanh $repo1Branch)..." -ForegroundColor Yellow
git push repo-sixospwa "HEAD:$repo1Branch"
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Day len SixosPwa ($repo1Branch) thanh cong!" -ForegroundColor Green
} else {
    Write-Host "[CANH BAO] Day len SixosPwa gap loi, vui long kiem tra quyen truy cap." -ForegroundColor Red
}

Write-Host ""
Write-Host "[4/4] Dang day len SixosPwa25 (nhanh $repo2Branch)..." -ForegroundColor Yellow
git push repo-sixospwa25 "HEAD:$repo2Branch"
if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Day len SixosPwa25 ($repo2Branch) thanh cong!" -ForegroundColor Green
} else {
    Write-Host "[CANH BAO] Day len SixosPwa25 gap loi, vui long kiem tra quyen truy cap." -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "           HOAN TAT DONG BO CA 2 REPO             " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

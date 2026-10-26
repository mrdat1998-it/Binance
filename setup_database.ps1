# ========================================================================================
# SCRIPT TỰ ĐỘNG KHỞI TẠO CƠ SỞ DỮ LIỆU MICROSOFT SQL SERVER CHO DỰ ÁN BINANCE
# ========================================================================================
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "Binance Tracker - Database Generator"

Write-Host "========================================================================================" -ForegroundColor Cyan
Write-Host ">>> TIẾN TRÌNH TỰ ĐỘNG GEN DATABASE SQL SERVER [BinanceTracker]" -ForegroundColor Yellow
Write-Host "========================================================================================" -ForegroundColor Cyan

$server = "."
$user = "admin"
$pass = "shc@1234"
$sqlFile = Join-Path $PSScriptRoot "database_setup.sql"

if (-not (Test-Path $sqlFile)) {
    Write-Host "[X] Không tìm thấy file database_setup.sql tại: $sqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "[1/3] Kiểm tra công cụ sqlcmd trên máy..." -ForegroundColor White
$sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmdPath) {
    Write-Host "[!] Không tìm thấy lệnh sqlcmd trong PATH." -ForegroundColor Yellow
    Write-Host "    Bạn có thể mở SQL Server Management Studio (SSMS) và chạy file 'database_setup.sql'." -ForegroundColor Gray
    exit 1
}
Write-Host "    -> Đã tìm thấy sqlcmd: $($sqlcmdPath.Source)" -ForegroundColor Green

Write-Host "[2/3] Đang kết nối tới SQL Server ($server) và thực thi database_setup.sql..." -ForegroundColor White
try {
    & sqlcmd -S $server -U $user -P $pass -C -i $sqlFile
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================================================================" -ForegroundColor Green
        Write-Host ">>> CHÚC MỪNG: CƠ SỞ DỮ LIỆU [BinanceTracker] ĐÃ ĐƯỢC GEN THÀNH CÔNG 100%!" -ForegroundColor Green
        Write-Host "    - Database: BinanceTracker" -ForegroundColor Cyan
        Write-Host "    - Bảng dữ liệu: [dbo].[TradeSignals]" -ForegroundColor Cyan
        Write-Host "    - Đầy đủ 5 Indexes tối ưu và 15 lệnh mẫu." -ForegroundColor Cyan
        Write-Host "========================================================================================" -ForegroundColor Green
    } else {
        Write-Host "[X] sqlcmd hoàn tất với mã lỗi: $LASTEXITCODE" -ForegroundColor Red
    }
} catch {
    Write-Host "[X] Lỗi khi thực thi: $($_.Exception.Message)" -ForegroundColor Red
}

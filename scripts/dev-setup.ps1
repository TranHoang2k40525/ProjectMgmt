<#
.SYNOPSIS
  Dựng nhanh môi trường dev local: tạo DB, chạy migration 3 module, vá các cột còn thiếu migration,
  kích hoạt tài khoản (khi không có SMTP) và (tùy chọn) chạy backend.

.EXAMPLE
  # Lần đầu: tạo DB + migration + vá cột, rồi chạy backend
  .\scripts\dev-setup.ps1 -Run

.EXAMPLE
  # Xóa DB làm lại từ đầu
  .\scripts\dev-setup.ps1 -Reset -Run

.EXAMPLE
  # Kích hoạt tài khoản vừa đăng ký trên giao diện (không cần OTP)
  .\scripts\dev-setup.ps1 -ActivateEmail ten@example.com -SkipMigrations

.NOTES
  - Mật khẩu MySQL lấy theo thứ tự: tham số -MySqlPassword, biến môi trường PROJECTMGMT_DEV_MYSQL_PASSWORD,
    hoặc hỏi khi chạy (không hiện ra màn hình). Script không ghi mật khẩu vào file nào.
  - Chuỗi kết nối được đặt bằng biến môi trường ConnectionStrings__ProjectMgmt cho tiến trình này,
    nên không cần sửa appsettings.Development.json.
  - Phần "vá cột" chỉ là tạm thời cho tới khi có migration chính thức; cột đã có thì bỏ qua.
#>
[CmdletBinding()]
param(
  [string]$Database = 'projectmgmt',
  [string]$MySqlHost = '127.0.0.1',
  [int]$MySqlPort = 3306,
  [string]$MySqlUser = 'root',
  [string]$MySqlPassword,
  [switch]$Reset,
  [switch]$SkipMigrations,
  [string[]]$ActivateEmail = @(),
  [switch]$Run
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

. "$PSScriptRoot\dev-common.ps1"

$MySqlPassword = Resolve-MySqlPassword $MySqlPassword $MySqlUser
$mysql = @{ Exe = Find-MySqlExe; Host = $MySqlHost; Port = $MySqlPort; User = $MySqlUser; Password = $MySqlPassword }
function Invoke-Sql([string]$sql, [string]$db = '') { Invoke-MySql $mysql $sql $db }
Assert-DatabaseName $Database

Write-Step "Kiểm tra kết nối MySQL ($MySqlUser@${MySqlHost}:$MySqlPort)"
Invoke-Sql 'SELECT 1;' | Out-Null
Write-Ok 'Kết nối được.'

if ($Reset) {
  Write-Step "Xóa DB '$Database'"
  Invoke-Sql "DROP DATABASE IF EXISTS ``$Database``;" | Out-Null
  Write-Ok 'Đã xóa.'
}

$env:ConnectionStrings__ProjectMgmt = "Server=$MySqlHost;Port=$MySqlPort;Database=$Database;User=$MySqlUser;Password=$MySqlPassword;SslMode=None;AllowPublicKeyRetrieval=True;"

# --- Migration -------------------------------------------------------------------------
if (-not $SkipMigrations) {
  $efVersion = & dotnet ef --version 2>$null
  if ($LASTEXITCODE -ne 0) {
    Write-Step 'Cài dotnet-ef 9.0.19'
    & dotnet tool install --global dotnet-ef --version 9.0.19 | Out-Null
  }

  Invoke-Sql "CREATE DATABASE IF NOT EXISTS ``$Database`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;" | Out-Null

  $modules = @(
    @{ Project = 'ProjectMgmt.Modules.IdentityExperience/Infrastructure'; Context = 'IdentityExperienceDbContext' },
    @{ Project = 'ProjectMgmt.Modules.Planning/Infrastructure'; Context = 'PlanningDbContext' },
    @{ Project = 'ProjectMgmt.Modules.DeliveryIntelligence/Infrastructure'; Context = 'DeliveryIntelligenceDbContext' }
  )
  # Backend đang chạy thì khóa file dll, không build lại được: dùng bản build hiện có.
  $efArgs = @()
  if (Get-Process -Name 'ProjectMgmt.Solution' -ErrorAction SilentlyContinue) {
    Write-Warn 'Backend đang chạy nên không build lại; migration dùng bản build hiện có.'
    $efArgs = @('--no-build')
  }
  Push-Location $repoRoot
  try {
    foreach ($module in $modules) {
      Write-Step "Migration $($module.Context)"
      & dotnet ef database update --project $module.Project --startup-project ProjectMgmt.Solution --context $module.Context @efArgs |
        Select-String -Pattern 'Applying migration|Done|No migrations|error' |
        ForEach-Object { Write-Host "    $_" }
      if ($LASTEXITCODE -ne 0) { throw "Migration $($module.Context) thất bại." }
    }
  } finally {
    Pop-Location
  }

  # --- Vá cột code đã dùng nhưng chưa có migration (tạm thời) ----------------------------
  Write-Step 'Vá các cột còn thiếu migration'
  $patches = @(
    @{ Table = 'Issue'; Column = 'IsBlocked'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
    @{ Table = 'Issue'; Column = 'BlockReason'; Definition = 'VARCHAR(500) NULL' },
    @{ Table = 'Issue'; Column = 'BlockedAt'; Definition = 'DATETIME(6) NULL' },
    @{ Table = 'Issue'; Column = 'EstimatedHours'; Definition = 'DECIMAL(5,2) NULL' },
    @{ Table = 'Issue'; Column = 'RemainingHours'; Definition = 'DECIMAL(5,2) NULL' },
    @{ Table = 'WorkflowStatus'; Column = 'IsDone'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
    @{ Table = 'WorkflowStatus'; Column = 'IsActiveWork'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
    @{ Table = 'WorkflowStatus'; Column = 'IsCanceled'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' }
  )
  foreach ($patch in $patches) {
    $exists = Invoke-Sql "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='$Database' AND TABLE_NAME='$($patch.Table)' AND COLUMN_NAME='$($patch.Column)';"
    if ("$exists".Trim() -eq '0') {
      Invoke-Sql "ALTER TABLE ``$($patch.Table)`` ADD COLUMN ``$($patch.Column)`` $($patch.Definition);" $Database | Out-Null
      Write-Ok "Đã thêm $($patch.Table).$($patch.Column)"
    }
  }
  Write-Ok 'Xong.'
}

# --- Kích hoạt tài khoản (thay cho OTP khi không có SMTP) --------------------------------
foreach ($email in $ActivateEmail) {
  if ($email -notmatch '^[^\s@''"]+@[^\s@''"]+\.[^\s@''"]+$') { throw "Email không hợp lệ: $email" }
  Write-Step "Kích hoạt tài khoản $email"
  $count = Invoke-Sql "UPDATE ``User`` SET IsEmailVerified = 1 WHERE LOWER(Email) = LOWER('$email'); SELECT ROW_COUNT();" $Database
  if ("$count".Trim() -eq '0') {
    Write-Host '    Không tìm thấy tài khoản (hoặc đã kích hoạt). Hãy đăng ký trên giao diện trước.' -ForegroundColor Yellow
  } else {
    Write-Ok 'Đã kích hoạt, đăng nhập được ngay.'
  }
}

Write-Host ''
Write-Host 'Môi trường DB đã sẵn sàng.' -ForegroundColor Green
Write-Host 'Dữ liệu mẫu: chạy BE rồi chạy .\scripts\dev-seed.ps1 ở cửa sổ khác.'
Write-Host 'FE: trong src/environments/environment.ts đặt apiBaseUrl/hubBaseUrl về http://localhost:5083 (không commit), rồi npm start.'

# --- Chạy backend ----------------------------------------------------------------------
if ($Run) {
  Write-Step 'Chạy backend tại http://localhost:5083 (Ctrl+C để dừng)'
  Push-Location (Join-Path $repoRoot 'ProjectMgmt.Solution')
  try {
    & dotnet run --launch-profile http
  } finally {
    Pop-Location
  }
}

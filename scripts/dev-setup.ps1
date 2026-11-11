<#
.SYNOPSIS
  Dựng nhanh môi trường dev local: tạo DB, chạy migration 3 module, bổ sung bảng/cột còn thiếu migration,
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
  - Phần "bổ sung bảng/cột" chỉ là tạm thời cho tới khi có migration chính thức; cái đã có thì bỏ qua.
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

    # --- Bổ sung bảng/cột có trong model EF nhưng chưa có migration (tạm thời) ---------------
    # Nhiều entity đã thêm bảng/cột mà chưa có migration (ProjectDocument, Role.IsActive, WorkflowStatus.IsDone...).
    # DDL sinh từ chính model EF nên khớp code: chỉ tạo bảng còn thiếu và thêm cột còn thiếu, không sửa/xóa gì đã có.
    Write-Step 'Bổ sung bảng/cột còn thiếu migration'
    $existingTables = @(Invoke-Sql "SELECT LOWER(TABLE_NAME) FROM information_schema.TABLES WHERE TABLE_SCHEMA='$Database';" | ForEach-Object { "$_".Trim() })
    $existingColumns = @{}
    Invoke-Sql "SELECT LOWER(TABLE_NAME), LOWER(COLUMN_NAME) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='$Database';" | ForEach-Object {
      $parts = "$_".Trim() -split "`t"
      if ($parts.Count -eq 2) { $existingColumns["$($parts[0]).$($parts[1])"] = $true }
    }
    $changes = @()
    foreach ($module in $modules) {
      $modelScript = Join-Path ([IO.Path]::GetTempPath()) "projectmgmt-$($module.Context).sql"
      & dotnet ef dbcontext script --project $module.Project --startup-project ProjectMgmt.Solution --context $module.Context @efArgs -o $modelScript | Out-Null
      if ($LASTEXITCODE -ne 0) { throw "Không sinh được script model $($module.Context)." }
      $createdTables = @()
      foreach ($statement in ((Get-Content $modelScript -Raw -Encoding UTF8) -split ";\s*\r?\n")) {
        if ($statement -match 'CREATE TABLE `(\w+)`') {
          $table = $Matches[1]
          if ($existingTables -notcontains $table.ToLower()) {
            try {
              Invoke-Sql "$statement;" $Database | Out-Null
            } catch {
              # Model có khóa ngoại lệch kiểu với bảng đích: tạo bảng không kèm khóa ngoại để dev vẫn chạy được.
              $withoutFk = (($statement -split "\r?\n") | Where-Object { $_ -notmatch 'FOREIGN KEY' }) -join "`n"
              $withoutFk = $withoutFk -replace ',(\s*\n\s*\))', '$1'
              Invoke-Sql ($withoutFk + ';') $Database | Out-Null
              Write-Warn "Tạo $table không kèm khóa ngoại (model lệch kiểu cột với bảng đích)."
            }
            $createdTables += $table
            $changes += "bảng $table"
            continue
          }
          foreach ($line in ($statement -split "\r?\n")) {
            if ($line -match '^\s*`(\w+)`\s+(.+?),?\s*$' -and -not $existingColumns.ContainsKey("$($table.ToLower()).$($Matches[1].ToLower())")) {
              $column = $Matches[1]
              Invoke-Sql "ALTER TABLE ``$table`` ADD COLUMN ``$column`` $($Matches[2]);" $Database | Out-Null
              $changes += "$table.$column"
            }
          }
        } elseif ($statement -match 'CREATE (UNIQUE )?INDEX `\w+` ON `(\w+)`' -and $createdTables -contains $Matches[2]) {
          Invoke-Sql "$statement;" $Database | Out-Null
        }
      }
      Remove-Item $modelScript -ErrorAction SilentlyContinue
    }
    if ($changes.Count -gt 0) { Write-Ok "Đã bổ sung: $($changes -join ', ')" } else { Write-Ok 'Không thiếu gì.' }
  } finally {
    Pop-Location
  }

  # --- Bảng Workspace dùng SQL thô, không nằm trong model EF (thành viên, lời mời, mẫu workflow tổ chức...) ---
  $workspaceSql = Join-Path $repoRoot 'scratch/create_workspace_tables.sql'
  if (Test-Path $workspaceSql) {
    Write-Step 'Tạo bảng Workspace (scratch/create_workspace_tables.sql)'
    # File tự chọn DB bằng USE: bỏ dòng đó để chạy đúng DB của script này; toàn bộ là CREATE TABLE IF NOT EXISTS.
    $workspaceDdl = (Get-Content $workspaceSql -Encoding UTF8 | Where-Object { $_ -notmatch '^\s*USE\s' }) -join "`n"
    Invoke-Sql $workspaceDdl $Database | Out-Null

    # Code (WorkspaceController, ProjectCustomizationController.ApplyTemplate) đọc thêm Code/IsActive/IsPreset và
    # các mẫu có sẵn OrgId = NULL, nhưng file SQL trên chưa có: bổ sung để áp mẫu workflow không lỗi 500.
    $templateColumns = @(
      @{ Column = 'Code'; Definition = 'VARCHAR(64) NULL' },
      @{ Column = 'IsActive'; Definition = 'TINYINT(1) NOT NULL DEFAULT 1' },
      @{ Column = 'IsPreset'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' }
    )
    foreach ($item in $templateColumns) {
      $exists = Invoke-Sql "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='$Database' AND TABLE_NAME='OrganizationWorkflowTemplate' AND COLUMN_NAME='$($item.Column)';"
      if ("$exists".Trim() -eq '0') { Invoke-Sql "ALTER TABLE OrganizationWorkflowTemplate ADD COLUMN ``$($item.Column)`` $($item.Definition);" $Database | Out-Null }
    }
    Invoke-Sql "ALTER TABLE OrganizationWorkflowTemplate MODIFY OrgId CHAR(36) NULL;" $Database | Out-Null
    # Cùng tên, trạng thái với WorkspaceController.GetSystemPresets; Id trùng thì bỏ qua (chạy lại không nhân bản).
    $presets = @(
      @{ Code = 'scrum-std'; Name = 'Scrum chuẩn'; Type = 'Scrum'; Default = 1; Statuses = @(@('To Do', 'ToDo'), @('In Progress', 'InProgress'), @('Code Review', 'InProgress'), @('Done', 'Done')) },
      @{ Code = 'agile-qa'; Name = 'Agile + QA'; Type = 'Scrum'; Default = 0; Statuses = @(@('To Do', 'ToDo'), @('In Progress', 'InProgress'), @('Code Review', 'InProgress'), @('QA Testing', 'InProgress'), @('Done', 'Done')) },
      @{ Code = 'kanban-simple'; Name = 'Kanban đơn giản'; Type = 'Kanban'; Default = 0; Statuses = @(@('To Do', 'ToDo'), @('In Progress', 'InProgress'), @('Done', 'Done')) },
      @{ Code = 'enterprise'; Name = 'Enterprise'; Type = 'Scrum'; Default = 0; Statuses = @(@('Backlog', 'ToDo'), @('Spec Review', 'InProgress'), @('In Progress', 'InProgress'), @('Code Review', 'InProgress'), @('QA Verification', 'InProgress'), @('Done', 'Done')) }
    )
    foreach ($preset in $presets) {
      $json = '[' + (($preset.Statuses | ForEach-Object { '{"Name":"' + $_[0] + '","Category":"' + $_[1] + '"}' }) -join ',') + ']'
      Invoke-Sql "INSERT IGNORE INTO OrganizationWorkflowTemplate (Id, OrgId, Name, ManagementType, StatusesJson, IsDefault, Code, IsActive, IsPreset) VALUES ('preset-$($preset.Code)', NULL, '$($preset.Name)', '$($preset.Type)', '$json', $($preset.Default), '$($preset.Code)', 1, 1);" $Database | Out-Null
    }
    Write-Ok 'Xong (kèm 4 mẫu workflow có sẵn).'
  }
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

# Hàm dùng chung cho các script dev (dot-source: . "$PSScriptRoot\dev-common.ps1").

function Write-Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Ok([string]$message) { Write-Host "    $message" -ForegroundColor Green }
function Write-Warn([string]$message) { Write-Host "    $message" -ForegroundColor Yellow }

# Mật khẩu MySQL: tham số, biến môi trường PROJECTMGMT_DEV_MYSQL_PASSWORD, hoặc hỏi (không hiện ra màn hình).
function Resolve-MySqlPassword([string]$password, [string]$user) {
  if ($password) { return $password }
  if ($env:PROJECTMGMT_DEV_MYSQL_PASSWORD) { return $env:PROJECTMGMT_DEV_MYSQL_PASSWORD }
  $secure = Read-Host -AsSecureString "Mật khẩu MySQL của '$user'"
  return [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

function Find-MySqlExe {
  $exe = (Get-Command mysql.exe -ErrorAction SilentlyContinue).Source
  if (-not $exe) {
    $exe = Get-ChildItem 'C:\Program Files\MySQL\MySQL Server *\bin\mysql.exe' -ErrorAction SilentlyContinue |
      Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
  }
  if (-not $exe) { throw 'Không tìm thấy mysql.exe. Cài MySQL Server 8 hoặc thêm thư mục bin của MySQL vào PATH.' }
  return $exe
}

# $MySql: hashtable { Exe, Host, Port, User, Password }
# Câu SQL ghi thẳng vào stdin dạng UTF-8 (không BOM): pipe của PowerShell 5.1 đổi tiếng Việt thành '?' tùy $OutputEncoding.
# Mật khẩu chỉ nằm trong biến môi trường của tiến trình mysql, không hiện trên dòng lệnh.
function Invoke-MySql([hashtable]$MySql, [string]$sql, [string]$db = '') {
  $mysqlArgs = @('-h', $MySql.Host, '-P', "$($MySql.Port)", '-u', $MySql.User, '-N', '--default-character-set=utf8mb4')
  if ($db) { $mysqlArgs += @('-D', $db) }
  $utf8 = New-Object System.Text.UTF8Encoding($false)
  $info = New-Object System.Diagnostics.ProcessStartInfo $MySql.Exe
  $info.Arguments = ($mysqlArgs | ForEach-Object { '"' + $_ + '"' }) -join ' '
  $info.UseShellExecute = $false
  $info.RedirectStandardInput = $true
  $info.RedirectStandardOutput = $true
  $info.RedirectStandardError = $true
  $info.StandardOutputEncoding = $utf8
  $info.StandardErrorEncoding = $utf8
  $info.EnvironmentVariables['MYSQL_PWD'] = $MySql.Password
  $process = [System.Diagnostics.Process]::Start($info)
  $stdout = $process.StandardOutput.ReadToEndAsync()
  $stderr = $process.StandardError.ReadToEndAsync()
  $stdin = New-Object System.IO.StreamWriter($process.StandardInput.BaseStream, $utf8)
  $stdin.Write($sql)
  $stdin.Close()
  $process.WaitForExit()
  if ($process.ExitCode -ne 0) { throw "MySQL lỗi: $($stderr.Result.Trim())" }
  return @($stdout.Result -split "\r?\n" | Where-Object { $_ -ne '' })
}

function Assert-DatabaseName([string]$name) {
  if ($name -notmatch '^[A-Za-z0-9_]+$') { throw "Tên DB không hợp lệ: $name" }
}

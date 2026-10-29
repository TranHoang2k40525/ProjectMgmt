<#
.SYNOPSIS
  Tạo dữ liệu mẫu để test giao diện: 3 tài khoản demo, 2 tổ chức, 3 dự án (Scrum + Kanban), sprint và công việc
  có người nhận, trạng thái, độ ưu tiên, hạn khác nhau.

.DESCRIPTION
  Cần backend đang chạy (vd .\scripts\dev-setup.ps1 -Run ở cửa sổ khác). Dữ liệu được tạo qua API của backend
  (đúng nghiệp vụ, đúng quyền); riêng kích hoạt tài khoản và hạn công việc ghi thẳng vào DB vì API chưa có.
  Chạy lại khi dữ liệu mẫu đã có thì bỏ qua (không tạo trùng).

.EXAMPLE
  .\scripts\dev-seed.ps1
  .\scripts\dev-seed.ps1 -DemoPassword 'MatKhauDemo@1'

.EXAMPLE
  # Xóa 3 tài khoản demo và 2 tổ chức mẫu (kèm dự án, sprint, công việc) rồi tạo lại với mật khẩu mới
  .\scripts\dev-seed.ps1 -ResetDemoAccounts
#>
[CmdletBinding()]
param(
  [string]$ApiBase = 'http://localhost:5083/api',
  [string]$Database = 'projectmgmt',
  [string]$MySqlHost = '127.0.0.1',
  [int]$MySqlPort = 3306,
  [string]$MySqlUser = 'root',
  [string]$MySqlPassword,
  [string]$DemoPassword,
  [switch]$ResetDemoAccounts
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\dev-common.ps1"
Assert-DatabaseName $Database

$MySqlPassword = Resolve-MySqlPassword $MySqlPassword $MySqlUser
$mysql = @{ Exe = Find-MySqlExe; Host = $MySqlHost; Port = $MySqlPort; User = $MySqlUser; Password = $MySqlPassword }
function Invoke-Sql([string]$sql) { Invoke-MySql $mysql $sql $Database }

if (-not $DemoPassword) { $DemoPassword = $env:PROJECTMGMT_DEMO_PASSWORD }
if (-not $DemoPassword) {
  Write-Host 'Mật khẩu cho 3 tài khoản demo (≥ 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt):'
  $secure = Read-Host -AsSecureString 'Mật khẩu demo'
  $DemoPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

# --- Gọi API ---------------------------------------------------------------------------
function Invoke-Api([string]$method, [string]$path, $body = $null, [string]$token = '') {
  $headers = @{ Accept = 'application/json' }
  if ($token) { $headers.Authorization = "Bearer $token" }
  $params = @{ Method = $method; Uri = "$ApiBase$path"; Headers = $headers; UseBasicParsing = $true }
  if ($null -ne $body) {
    $params.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8 -Compress))
    $params.ContentType = 'application/json; charset=utf-8'
  }
  try {
    $response = Invoke-WebRequest @params
    $text = [Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray())
    if (-not $text) { return $null }
    return $text | ConvertFrom-Json
  } catch {
    $detail = $_.ErrorDetails.Message
    if (-not $detail -and $_.Exception.Response) {
      $reader = New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())
      $detail = $reader.ReadToEnd()
    }
    throw "API $method $path lỗi: $detail"
  }
}

Write-Step "Kiểm tra backend ($ApiBase)"
try {
  Invoke-WebRequest -Uri ($ApiBase -replace '/api$', '/health/live') -UseBasicParsing | Out-Null
  Write-Ok 'Backend đang chạy.'
} catch {
  throw 'Backend chưa chạy. Mở cửa sổ khác chạy .\scripts\dev-setup.ps1 -Run rồi thử lại.'
}

# --- Tài khoản demo ---------------------------------------------------------------------
$users = @(
  @{ Key = 'owner'; Email = 'demo.owner@example.com'; Name = 'Nguyễn Minh Anh'; Phone = '0912000101' },
  @{ Key = 'dev'; Email = 'demo.dev@example.com'; Name = 'Trần Quốc Bảo'; Phone = '0912000102' },
  @{ Key = 'qa'; Email = 'demo.qa@example.com'; Name = 'Lê Thu Hà'; Phone = '0912000103' }
)

$demoEmailList = ($users | ForEach-Object { "'$($_.Email)'" }) -join ','

$seedSlugList = "'huce-software-lab','nhom-do-an-tot-nghiep'"

# Xóa 2 tổ chức mẫu cùng mọi dữ liệu thuộc các dự án của chúng (theo thứ tự khóa ngoại).
function Remove-SeedData {
  $orgIds = "SELECT Id FROM Organization WHERE Slug IN ($seedSlugList)"
  $projectIds = "SELECT Id FROM Project WHERE OrgId IN ($orgIds)"
  $handled = @('project', 'issue', 'sprint', 'workflowtransition')
  $otherTables = Invoke-Sql "SELECT c.TABLE_NAME FROM information_schema.COLUMNS c JOIN information_schema.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME WHERE c.TABLE_SCHEMA = '$Database' AND c.COLUMN_NAME = 'ProjectId' AND t.TABLE_TYPE = 'BASE TABLE';" |
    ForEach-Object { "$_".Trim() } | Where-Object { $_ -and ($handled -notcontains $_.ToLower()) }
  $statements = @(
    "CREATE TEMPORARY TABLE seed_project AS $projectIds;",
    "DELETE FROM SprintMessage WHERE SprintId IN (SELECT Id FROM Sprint WHERE ProjectId IN (SELECT Id FROM seed_project));",
    "DELETE FROM WorkflowTransition WHERE ProjectId IN (SELECT Id FROM seed_project);",
    "DELETE FROM Issue WHERE ProjectId IN (SELECT Id FROM seed_project);",
    "DELETE FROM Sprint WHERE ProjectId IN (SELECT Id FROM seed_project);"
  )
  $statements += $otherTables | ForEach-Object { "DELETE FROM ``$_`` WHERE ProjectId IN (SELECT Id FROM seed_project);" }
  $statements += @(
    "DELETE FROM Project WHERE Id IN (SELECT Id FROM seed_project);",
    "DELETE FROM Organization WHERE Slug IN ($seedSlugList);"
  )
  Invoke-Sql ($statements -join "`n") | Out-Null
}

Write-Step 'Tài khoản demo'
if ($ResetDemoAccounts) {
  Remove-SeedData
  # Các bảng tham chiếu User đều ON DELETE CASCADE (hồ sơ, vai trò, OTP, refresh token, kỹ năng, đăng nhập ngoài).
  Invoke-Sql "DELETE FROM ``User`` WHERE LOWER(Email) IN ($demoEmailList);" | Out-Null
  Write-Ok 'Đã xóa tài khoản demo và dữ liệu mẫu cũ.'
}
foreach ($user in $users) {
  $exists = Invoke-Sql "SELECT COUNT(*) FROM ``User`` WHERE LOWER(Email) = '$($user.Email)';"
  if ("$exists".Trim() -eq '0') {
    Invoke-Api POST '/v1/auth/register' @{ email = $user.Email; password = $DemoPassword; fullName = $user.Name; phoneNumber = $user.Phone } | Out-Null
    $saved = Invoke-Sql "SELECT COUNT(*) FROM ``User`` WHERE LOWER(Email) = '$($user.Email)';"
    if ("$saved".Trim() -eq '0') {
      throw "Backend ở $ApiBase không ghi vào DB '$Database' (đăng ký xong nhưng không thấy tài khoản). Kiểm tra backend đang chạy dùng đúng DB, hoặc truyền -Database / -ApiBase cho khớp."
    }
    Write-Ok "Đã đăng ký $($user.Email)"
  }
}
# Không có SMTP thì không nhận được OTP: kích hoạt thẳng trong DB (chỉ cho tài khoản demo).
Invoke-Sql "UPDATE ``User`` SET IsEmailVerified = 1 WHERE LOWER(Email) IN ($demoEmailList);" | Out-Null

$tokens = @{}
foreach ($user in $users) {
  try {
    $login = Invoke-Api POST '/v1/auth/login' @{ email = $user.Email; password = $DemoPassword }
  } catch {
    throw "Không đăng nhập được $($user.Email). Nếu tài khoản demo đã tạo trước đó với mật khẩu khác, dùng lại mật khẩu cũ (-DemoPassword) hoặc chạy lại với -ResetDemoAccounts. Chi tiết: $_"
  }
  $tokens[$user.Key] = $login.accessToken
  $user.Id = (Invoke-Api GET '/v1/users/me' $null $login.accessToken).userId
}
Write-Ok 'Đăng nhập được cả 3 tài khoản.'
$owner = $tokens['owner']
$devId = ($users | Where-Object Key -eq 'dev').Id
$qaId = ($users | Where-Object Key -eq 'qa').Id
$ownerId = ($users | Where-Object Key -eq 'owner').Id

$seedOwners = @(Invoke-Sql "SELECT DISTINCT OwnerId FROM Organization WHERE Slug IN ($seedSlugList);" | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
if ($seedOwners | Where-Object { $_ -ne $ownerId }) {
  throw 'Tổ chức mẫu đã có nhưng thuộc tài khoản khác (tài khoản demo đã bị tạo lại). Chạy lại với -ResetDemoAccounts để xóa và tạo lại toàn bộ.'
}
if ($seedOwners.Count -gt 0) {
  Write-Warn 'Dữ liệu mẫu đã có (tổ chức huce-software-lab). Bỏ qua phần tạo dữ liệu.'
} else {
  # --- Tổ chức & dự án --------------------------------------------------------------------
  Write-Step 'Tổ chức'
  $lab = Invoke-Api POST '/v1/organizations' @{ name = 'HUCE Software Lab'; slug = 'huce-software-lab' } $owner
  $thesis = Invoke-Api POST '/v1/organizations' @{ name = 'Nhóm Đồ án Tốt nghiệp'; slug = 'nhom-do-an-tot-nghiep' } $owner
  Write-Ok "$($lab.name), $($thesis.name)"

  function New-Project($org, [string]$key, [string]$name, [string]$description, [string[]]$members, [string]$template) {
    $project = Invoke-Api POST '/v1/projects' @{
      orgId = $org.id; projectKey = $key; name = $name; description = $description; leadUserId = $ownerId; memberUserIds = $members
    } $owner
    if ($template) { Invoke-Api POST "/v1/projects/$($project.id)/workflow/template" @{ templateId = $template } $owner | Out-Null }
    Write-Ok "Dự án $key · $name"
    return $project
  }

  Write-Step 'Dự án'
  $ctsv = New-Project $lab 'CTSV' 'Cổng thông tin sinh viên' 'Cổng tra cứu điểm, lịch học và học phí.' @($devId, $qaId) 'scrum-std'
  $dpht = New-Project $lab 'DPHT' 'Đặt phòng hội thảo' 'Đặt phòng theo khung giờ, duyệt và nhắc lịch.' @($qaId) 'kanban-simple'
  $qlda = New-Project $thesis 'QLDA' 'Quản lý dự án Scrum tích hợp AI' 'Đồ án tốt nghiệp nhóm 7.' @($devId) 'agile-qa'

  # --- Sprint ------------------------------------------------------------------------------
  Write-Step 'Sprint'
  $today = Get-Date
  $fmt = { param($days) $today.AddDays($days).ToString('yyyy-MM-dd') }
  $ctsvSprint = Invoke-Api POST "/v1/projects/$($ctsv.id)/sprints" @{ name = 'CTSV Sprint 1'; goal = 'Tra cứu điểm và lịch học'; startDate = (& $fmt -7); endDate = (& $fmt 7); status = 'active' } $owner
  Invoke-Api POST "/v1/projects/$($ctsv.id)/sprints" @{ name = 'CTSV Sprint 2'; goal = 'Học phí và thông báo'; startDate = (& $fmt 8); endDate = (& $fmt 21); status = 'future' } $owner | Out-Null
  $qldaSprint = Invoke-Api POST "/v1/projects/$($qlda.id)/sprints" @{ name = 'QLDA Sprint 3'; goal = 'Hoàn thiện báo cáo'; startDate = (& $fmt -3); endDate = (& $fmt 11); status = 'active' } $owner
  Write-Ok 'CTSV Sprint 1 (đang chạy), CTSV Sprint 2, QLDA Sprint 3 (đang chạy)'

  # --- Công việc ----------------------------------------------------------------------------
  Write-Step 'Công việc'
  $dueDates = @()
  function New-Issue($project, [hashtable]$fields, $dueInDays = $null) {
    $body = @{ title = $fields.Title; issueType = $fields.Type; statusName = $fields.Status; priority = $fields.Priority; storyPoints = $fields.Points; labels = @() }
    if ($fields.Assignee) { $body.assigneeId = $fields.Assignee }
    if ($fields.Sprint) { $body.sprintId = $fields.Sprint }
    if ($fields.Parent) { $body.parentId = $fields.Parent }
    if ($fields.Epic) { $body.epicId = $fields.Epic }
    if ($fields.Description) { $body.description = $fields.Description }
    $issue = Invoke-Api POST "/v1/projects/$($project.id)/issues" $body $owner
    if ($null -ne $dueInDays) { $script:dueDates += @{ Id = $issue.id; Days = $dueInDays } }
    return $issue
  }

  $epic = New-Issue $ctsv @{ Title = 'Tra cứu học tập'; Type = 'Epic'; Status = 'In Progress'; Priority = 'High'; Points = 0 }
  $story = New-Issue $ctsv @{ Title = 'Sinh viên xem bảng điểm theo học kỳ'; Type = 'Story'; Status = 'In Progress'; Priority = 'High'; Points = 5; Assignee = $ownerId; Sprint = $ctsvSprint.id; Epic = $epic.id; Description = 'Lọc theo học kỳ, hiển thị GPA.' } -2
  New-Issue $ctsv @{ Title = 'API lấy điểm theo học kỳ'; Type = 'Subtask'; Status = 'In Review'; Priority = 'High'; Points = 2; Assignee = $devId; Sprint = $ctsvSprint.id; Parent = $story.id } 0 | Out-Null
  New-Issue $ctsv @{ Title = 'Màn hình bảng điểm trên di động'; Type = 'Subtask'; Status = 'To Do'; Priority = 'Medium'; Points = 2; Assignee = $ownerId; Sprint = $ctsvSprint.id; Parent = $story.id } 1 | Out-Null
  New-Issue $ctsv @{ Title = 'Lịch học theo tuần'; Type = 'Story'; Status = 'To Do'; Priority = 'Medium'; Points = 3; Assignee = $devId; Sprint = $ctsvSprint.id; Epic = $epic.id } 3 | Out-Null
  New-Issue $ctsv @{ Title = 'Sửa lỗi hiển thị lịch thi trùng phòng'; Type = 'Bug'; Status = 'In Progress'; Priority = 'Highest'; Points = 1; Assignee = $qaId; Sprint = $ctsvSprint.id } -1 | Out-Null
  New-Issue $ctsv @{ Title = 'Kiểm thử đăng nhập SSO'; Type = 'Task'; Status = 'Done'; Priority = 'Medium'; Points = 2; Assignee = $qaId; Sprint = $ctsvSprint.id } -4 | Out-Null
  New-Issue $ctsv @{ Title = 'Xuất hóa đơn học phí PDF'; Type = 'Story'; Status = 'Backlog'; Priority = 'Medium'; Points = 5; Assignee = $ownerId } 10 | Out-Null
  New-Issue $ctsv @{ Title = 'Tối ưu truy vấn bảng điểm'; Type = 'Task'; Status = 'Backlog'; Priority = 'Low'; Points = 3 } | Out-Null

  New-Issue $dpht @{ Title = 'Form đặt phòng theo khung giờ'; Type = 'Task'; Status = 'In Progress'; Priority = 'High'; Points = 3; Assignee = $ownerId } 1 | Out-Null
  New-Issue $dpht @{ Title = 'Thông báo xác nhận qua email'; Type = 'Task'; Status = 'To Do'; Priority = 'Medium'; Points = 2; Assignee = $qaId } 6 | Out-Null
  New-Issue $dpht @{ Title = 'Chặn đặt trùng phòng'; Type = 'Bug'; Status = 'Done'; Priority = 'High'; Points = 1; Assignee = $ownerId } -3 | Out-Null

  New-Issue $qlda @{ Title = 'Viết chương 3 — thiết kế hệ thống'; Type = 'Task'; Status = 'In Progress'; Priority = 'High'; Points = 5; Assignee = $ownerId; Sprint = $qldaSprint.id } -1 | Out-Null
  New-Issue $qlda @{ Title = 'Chuẩn bị slide bảo vệ'; Type = 'Task'; Status = 'To Do'; Priority = 'Medium'; Points = 3; Assignee = $ownerId; Sprint = $qldaSprint.id } 9 | Out-Null
  New-Issue $qlda @{ Title = 'Đánh giá mô hình AI phân rã'; Type = 'Story'; Status = 'QA Testing'; Priority = 'High'; Points = 8; Assignee = $devId; Sprint = $qldaSprint.id } 4 | Out-Null
  Write-Ok '15 công việc (Epic, Story, Task, Bug, Subtask).'

  # API tạo công việc chưa nhận hạn: ghi hạn thẳng vào DB.
  foreach ($due in $dueDates) {
    if ($due.Id -notmatch '^[0-9a-fA-F-]{36}$') { continue }
    Invoke-Sql "UPDATE Issue SET DueDate = DATE_ADD(CURDATE(), INTERVAL $([int]$due.Days) DAY) WHERE Id = '$($due.Id)';" | Out-Null
  }
  Write-Ok "Đặt hạn cho $($dueDates.Count) công việc (có quá hạn, hôm nay, sắp tới)."
}

Write-Host ''
Write-Host 'Xong. Đăng nhập bằng một trong các tài khoản (mật khẩu demo vừa nhập):' -ForegroundColor Green
foreach ($user in $users) { Write-Host ("  {0,-24} {1}" -f $user.Email, $user.Name) }
Write-Host ''
Write-Host 'Gợi ý: demo.owner là chủ cả 2 tổ chức (thấy tab Cài đặt); demo.dev / demo.qa là thành viên dự án.'
Write-Host 'Thử lời mời: đăng nhập demo.owner > Cài đặt > Thành viên, mời một email khác rồi đăng nhập email đó trên cùng trình duyệt.'

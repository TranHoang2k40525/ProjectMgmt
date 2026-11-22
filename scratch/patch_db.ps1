$patches = @(
  @{ Table = 'Issue'; Column = 'IsBlocked'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
  @{ Table = 'Issue'; Column = 'BlockReason'; Definition = 'VARCHAR(500) NULL' },
  @{ Table = 'Issue'; Column = 'BlockedAt'; Definition = 'DATETIME(6) NULL' },
  @{ Table = 'Issue'; Column = 'EstimatedHours'; Definition = 'DECIMAL(5,2) NULL' },
  @{ Table = 'Issue'; Column = 'RemainingHours'; Definition = 'DECIMAL(5,2) NULL' },
  @{ Table = 'WorkflowStatus'; Column = 'IsDone'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
  @{ Table = 'WorkflowStatus'; Column = 'IsActiveWork'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
  @{ Table = 'WorkflowStatus'; Column = 'IsCanceled'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
  @{ Table = 'ProjectDocument'; Column = 'ParentId'; Definition = 'CHAR(36) NULL' },
  @{ Table = 'ProjectDocument'; Column = 'Name'; Definition = 'VARCHAR(255) NOT NULL DEFAULT ""' },
  @{ Table = 'ProjectDocument'; Column = 'RelativePath'; Definition = 'VARCHAR(1000) NOT NULL DEFAULT ""' },
  @{ Table = 'ProjectDocument'; Column = 'IsDirectory'; Definition = 'TINYINT(1) NOT NULL DEFAULT 0' },
  @{ Table = 'ProjectDocument'; Column = 'Extension'; Definition = 'VARCHAR(20) NULL' },
  @{ Table = 'ProjectDocument'; Column = 'FileSize'; Definition = 'BIGINT NOT NULL DEFAULT 0' }
)

# Check if ProjectDocument table exists first
$docTableExists = & 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' -u root -p123456 -s -N -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='projectmgmt' AND TABLE_NAME='ProjectDocument';"
if ($docTableExists.Trim() -eq '0') {
    Write-Host "Creating ProjectDocument table..."
    & 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' -u root -p123456 projectmgmt -e "
    CREATE TABLE ProjectDocument (
      Id CHAR(36) NOT NULL PRIMARY KEY,
      ProjectId CHAR(36) NOT NULL,
      ParentId CHAR(36) NULL,
      Name VARCHAR(255) NOT NULL DEFAULT '',
      RelativePath VARCHAR(1000) NOT NULL DEFAULT '',
      IsDirectory TINYINT(1) NOT NULL DEFAULT 0,
      Extension VARCHAR(20) NULL,
      FileSize BIGINT NOT NULL DEFAULT 0,
      StoragePath VARCHAR(1000) NULL,
      UploadedByUserId CHAR(36) NOT NULL,
      CreatedAt DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
      UpdatedAt DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;"
}

foreach ($patch in $patches) {
  $tbl = $patch.Table
  $col = $patch.Column
  $def = $patch.Definition
  $chk = & 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' -u root -p123456 -s -N -e "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='projectmgmt' AND TABLE_NAME='$tbl' AND COLUMN_NAME='$col';"
  if ($chk.Trim() -eq '0') {
    Write-Host "Adding $tbl.$col..."
    & 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe' -u root -p123456 projectmgmt -e "ALTER TABLE $tbl ADD COLUMN $col $def;"
  } else {
    Write-Host "Already exists: $tbl.$col"
  }
}

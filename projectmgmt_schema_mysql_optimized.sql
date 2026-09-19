-- =====================================================================================
--  HỆ THỐNG QUẢN LÝ DỰ ÁN SCRUM (Jira-clone) + AI AUTO TASK BREAKDOWN + AI ASSIGNMENT
--  DDL cho MySQL 8.0.16+  (bắt buộc >= 8.0.16 để CHECK constraint có hiệu lực thật)
--  Engine: InnoDB | Charset: utf8mb4 | Collation: utf8mb4_0900_ai_ci (hỗ trợ tiếng Việt)
--
--  Phiên bản: 3.1  (fix MySQL 3823 + bổ sung index tìm kiếm/hiệu năng)
--
--  KIẾN TRÚC: Modular Monolith — 1 database vật lý, tách logic theo module.
--  Quy ước bắt buộc khi 3 người làm 3 module song song:
--    1. FOREIGN KEY chỉ được tạo TRONG cùng một module.
--    2. Tham chiếu XUYÊN MODULE chỉ lưu GUID thô (có INDEX, KHÔNG có FK) — xem chú thích
--       "-- XMOD" trên từng cột. Việc validate GUID đó thực hiện qua Interface .Contracts.
--    3. Mỗi module dùng bảng lịch sử migration riêng, tránh xung đột khi merge:
--         __EFMigrationsHistory_IdentityAccess
--         __EFMigrationsHistory_ProjectManagement
--         __EFMigrationsHistory_IssueTracking
--         __EFMigrationsHistory_AiAssist
--         __EFMigrationsHistory_Notification
--       (EF Core: options.MigrationsHistoryTable("__EFMigrationsHistory_XXX"))
--
--  PHÂN CÔNG ĐỀ XUẤT CHO 3 THÀNH VIÊN:
--    Người A — M1 (Identity & Access) + M8 (Notification) + M6 AI Breakdown
--    Người B — M2 (Project & Workflow) + M3 (Sprint & Backlog) + M7 AI Assignment
--    Người C — M4 (Issue Tracking, module lớn nhất)          + M5 AI Dataset & Training
--  (Cân bằng: mỗi người 1 module Scrum nặng + 1 module AI)
--
--  LƯU Ý MYSQL:
--    * Tên bảng để PascalCase cho khớp entity .NET. Trên Linux, MySQL phân biệt HOA/thường
--      với tên bảng. BẮT BUỘC set trong my.cnf:  lower_case_table_names = 1
--      (phải set TRƯỚC khi initialize data directory, không đổi được sau).
--    * Khóa chính CHAR(36) cho dễ debug. Nếu cần tối ưu (>5 triệu dòng/bảng), đổi sang
--      BINARY(16) + Pomelo `.HasConversion<GuidToBytesConverter>()` — tiết kiệm ~55% dung
--      lượng index. Ở quy mô dự án này CHAR(36) hoàn toàn ổn.
--    * Provider EF Core: Pomelo.EntityFrameworkCore.MySql (KHÔNG dùng Oracle MySql.Data
--      cho EF Core 10 — Pomelo bám sát bản EF Core mới hơn).
-- =====================================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;
SET SESSION sql_mode = 'STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION,ERROR_FOR_DIVISION_BY_ZERO';

DROP DATABASE IF EXISTS `projectmgmt`;
CREATE DATABASE `projectmgmt`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;
USE `projectmgmt`;


-- =====================================================================================
-- MODULE 1: IDENTITY & ACCESS
-- Owner: Người A
-- =====================================================================================

CREATE TABLE `User` (
    `Id`                CHAR(36)     NOT NULL COMMENT 'GUID - PK toàn hệ thống',
    `Email`             VARCHAR(256) NOT NULL COMMENT 'Định danh đăng nhập chính',
    `NormalizedEmail`   VARCHAR(256) NOT NULL COMMENT 'Email viết HOA để so sánh không phân biệt hoa/thường',
    `PasswordHash`      VARCHAR(255) NULL     COMMENT 'BCrypt/Argon2. NULL nếu tài khoản thuần OAuth',
    `IsEmailVerified`   TINYINT(1)   NOT NULL DEFAULT 0,
    `IsActive`          TINYINT(1)   NOT NULL DEFAULT 1 COMMENT 'Soft-disable, không xóa dữ liệu',
    `SecurityStamp`     CHAR(36)     NOT NULL COMMENT 'Đổi giá trị này để vô hiệu toàn bộ token cũ',
    `LastLoginAt`       DATETIME(6)  NULL,
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`         DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_User_NormalizedEmail` (`NormalizedEmail`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Tài khoản đăng nhập cốt lõi';

CREATE TABLE `UserProfile` (
    `Id`            CHAR(36)     NOT NULL,
    `UserId`        CHAR(36)     NOT NULL,
    `DisplayName`   VARCHAR(150) NOT NULL,
    `AvatarUrl`     VARCHAR(500) NULL,
    `PhoneNumber`   VARCHAR(30)  NULL,
    `Timezone`      VARCHAR(64)  NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    `JobTitle`      VARCHAR(150) NULL COMMENT 'AI ASSIGNMENT: dùng cho cold-start người mới',
    `SeniorityLevel` VARCHAR(20) NULL COMMENT 'Intern/Junior/Middle/Senior/Lead - cold-start',
    `YearsOfExperience` DECIMAL(4,1) NULL COMMENT 'Cold-start khi chưa có log task',
    `Bio`           TEXT         NULL,
    `CreatedAt`     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`     DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_UserProfile_UserId` (`UserId`),
    CONSTRAINT `FK_UserProfile_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_UserProfile_Seniority` CHECK (`SeniorityLevel` IS NULL OR `SeniorityLevel` IN ('Intern','Junior','Middle','Senior','Lead','Principal'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Thông tin cá nhân, tách khỏi bảng bảo mật User (1-1)';

CREATE TABLE `ExternalLogin` (
    `Id`          CHAR(36)     NOT NULL,
    `UserId`      CHAR(36)     NOT NULL,
    `Provider`    VARCHAR(50)  NOT NULL COMMENT 'Google / Microsoft / Facebook',
    `ProviderKey` VARCHAR(255) NOT NULL COMMENT 'claim "sub" - KHÔNG dùng email vì email đổi được',
    `LinkedAt`    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ExternalLogin_Provider` (`Provider`, `ProviderKey`),
    KEY `IX_ExternalLogin_UserId` (`UserId`),
    CONSTRAINT `FK_ExternalLogin_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Liên kết đăng nhập OAuth2';

CREATE TABLE `OtpCode` (
    `Id`           CHAR(36)     NOT NULL,
    `UserId`       CHAR(36)     NOT NULL,
    `CodeHash`     VARCHAR(255) NOT NULL COMMENT 'HASH của OTP, không lưu plain-text',
    `Purpose`      VARCHAR(30)  NOT NULL COMMENT 'VerifyEmail / ResetPassword / Login2FA',
    `ExpiresAt`    DATETIME(6)  NOT NULL,
    `IsUsed`       TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Chống replay',
    `AttemptCount` INT          NOT NULL DEFAULT 0 COMMENT 'Chống brute-force, khóa sau N lần',
    `CreatedAt`    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_OtpCode_User_Purpose` (`UserId`, `Purpose`, `IsUsed`),
    KEY `IX_OtpCode_ExpiresAt` (`ExpiresAt`) COMMENT 'Cho job dọn OTP hết hạn',
    CONSTRAINT `FK_OtpCode_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_OtpCode_Purpose` CHECK (`Purpose` IN ('VerifyEmail','ResetPassword','Login2FA'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Mã OTP đa mục đích';

CREATE TABLE `RefreshToken` (
    `Id`                 CHAR(36)     NOT NULL,
    `UserId`             CHAR(36)     NOT NULL,
    `TokenHash`          CHAR(64)     NOT NULL COMMENT 'SHA-256 hex của refresh token',
    `ExpiresAt`          DATETIME(6)  NOT NULL,
    `IsRevoked`          TINYINT(1)   NOT NULL DEFAULT 0,
    `ReplacedByTokenId`  CHAR(36)     NULL COMMENT 'Chuỗi rotation - phát hiện token reuse attack',
    `CreatedByIp`        VARCHAR(45)  NULL COMMENT 'IPv6-safe',
    `UserAgent`          VARCHAR(400) NULL,
    `CreatedAt`          DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_RefreshToken_Hash` (`TokenHash`),
    KEY `IX_RefreshToken_UserId` (`UserId`, `IsRevoked`),
    CONSTRAINT `FK_RefreshToken_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RefreshToken_Replaced` FOREIGN KEY (`ReplacedByTokenId`) REFERENCES `RefreshToken`(`Id`) ON DELETE SET NULL
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Chỉ RefreshToken lưu DB; Access Token JWT tự-chứa nên không lưu';

CREATE TABLE `Role` (
    `Id`          CHAR(36)     NOT NULL,
    `Name`        VARCHAR(80)  NOT NULL,
    `Scope`       VARCHAR(20)  NOT NULL COMMENT 'Phạm vi mặc định: System / Organization / Project',
    `IsSystem`    TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Vai trò hệ thống, không cho xóa',
    `Description` VARCHAR(255) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Role_Name` (`Name`),
    CONSTRAINT `CK_Role_Scope` CHECK (`Scope` IN ('System','Organization','Project'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Danh mục vai trò RBAC';

CREATE TABLE `Permission` (
    `Id`          CHAR(36)     NOT NULL,
    `Code`        VARCHAR(80)  NOT NULL COMMENT 'issue.create, sprint.close, ai.breakdown.request...',
    `Description` VARCHAR(255) NULL,
    `Grouping`    VARCHAR(50)  NULL COMMENT 'Nhóm hiển thị trên UI phân quyền',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Permission_Code` (`Code`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Danh mục hành động nhỏ nhất có thể cấp phép';

CREATE TABLE `RolePermission` (
    `RoleId`       CHAR(36) NOT NULL,
    `PermissionId` CHAR(36) NOT NULL,
    PRIMARY KEY (`RoleId`, `PermissionId`),
    KEY `IX_RolePermission_PermissionId` (`PermissionId`),
    CONSTRAINT `FK_RolePermission_Role` FOREIGN KEY (`RoleId`) REFERENCES `Role`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RolePermission_Permission` FOREIGN KEY (`PermissionId`) REFERENCES `Permission`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='N-N Role <-> Permission';

CREATE TABLE `UserRole` (
    `Id`        CHAR(36)    NOT NULL,
    `UserId`    CHAR(36)    NOT NULL,
    `RoleId`    CHAR(36)    NOT NULL,
    `ScopeType` VARCHAR(20) NOT NULL COMMENT 'System / Organization / Project',
    `ScopeId`   CHAR(36)    NULL COMMENT 'XMOD: Id của Organization hoặc Project. NULL nếu ScopeType=System',
    `GrantedBy` CHAR(36)    NULL COMMENT 'Ai là người gán vai trò này',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    -- Cột generated để unique hoạt động đúng khi ScopeId NULL (NULL không so sánh được trong UNIQUE)
    `ScopeKey` CHAR(36) GENERATED ALWAYS AS (IFNULL(`ScopeId`, '00000000-0000-0000-0000-000000000000')) STORED,
    UNIQUE KEY `UQ_UserRole_Scoped` (`UserId`, `RoleId`, `ScopeType`, `ScopeKey`),
    KEY `IX_UserRole_Lookup` (`UserId`, `ScopeType`, `ScopeId`) COMMENT 'Truy vấn chính của AuthorizationHandler',
    KEY `IX_UserRole_Scope` (`ScopeType`, `ScopeId`) COMMENT 'Liệt kê thành viên của 1 project/org',
    CONSTRAINT `FK_UserRole_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserRole_Role` FOREIGN KEY (`RoleId`) REFERENCES `Role`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_UserRole_ScopeType` CHECK (`ScopeType` IN ('System','Organization','Project'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Gán vai trò có phạm vi, thay cho OrgMember/ProjectMember riêng';

-- ---- Bổ sung phục vụ AI PHÂN PHỐI TASK (cold-start & skill matching) -----------------

CREATE TABLE `SkillCatalog` (
    `Id`        CHAR(36)     NOT NULL,
    `Code`      VARCHAR(60)  NOT NULL COMMENT 'dotnet, angular, sql, devops, testing...',
    `Name`      VARCHAR(120) NOT NULL,
    `Category`  VARCHAR(50)  NULL COMMENT 'Backend / Frontend / Database / QA / DevOps / Design',
    `IsActive`  TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_SkillCatalog_Code` (`Code`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Danh mục kỹ năng chuẩn hóa, dùng chung cho user và issue';

CREATE TABLE `UserSkill` (
    `Id`                CHAR(36)     NOT NULL,
    `UserId`            CHAR(36)     NOT NULL,
    `SkillId`           CHAR(36)     NOT NULL,
    `ProficiencyLevel`  TINYINT      NOT NULL DEFAULT 3 COMMENT '1..5 - do user tự khai hoặc lead đánh giá',
    `YearsOfExperience` DECIMAL(4,1) NULL,
    `IsSelfDeclared`    TINYINT(1)   NOT NULL DEFAULT 1 COMMENT '0 = đã được lead xác nhận, tin cậy hơn',
    `UpdatedAt`         DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_UserSkill` (`UserId`, `SkillId`),
    KEY `IX_UserSkill_SkillId` (`SkillId`, `ProficiencyLevel`),
    CONSTRAINT `FK_UserSkill_User` FOREIGN KEY (`UserId`) REFERENCES `User`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserSkill_Skill` FOREIGN KEY (`SkillId`) REFERENCES `SkillCatalog`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_UserSkill_Level` CHECK (`ProficiencyLevel` BETWEEN 1 AND 5)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='AI ASSIGNMENT: nguồn dữ liệu chính cho cold-start người mới vào project';


-- =====================================================================================
-- MODULE 2: ORGANIZATION, PROJECT & WORKFLOW
-- Owner: Người B
-- =====================================================================================

CREATE TABLE `Organization` (
    `Id`        CHAR(36)     NOT NULL,
    `Name`      VARCHAR(200) NOT NULL,
    `Slug`      VARCHAR(80)  NOT NULL COMMENT 'URL-friendly, dùng cho subdomain/route',
    `OwnerId`   CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `IsActive`  TINYINT(1)   NOT NULL DEFAULT 1,
    `CreatedAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt` DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Organization_Slug` (`Slug`),
    KEY `IX_Organization_OwnerId` (`OwnerId`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Workspace cấp cao nhất, tương đương Site trong Jira Cloud';

CREATE TABLE `Project` (
    `Id`             CHAR(36)     NOT NULL,
    `OrgId`          CHAR(36)     NOT NULL,
    `ProjectKey`     VARCHAR(10)  NOT NULL COMMENT 'Mã viết tắt sinh issue key, VD "PROJ". `Key` là từ khóa MySQL nên đổi tên cột',
    `Name`           VARCHAR(200) NOT NULL,
    `Description`    TEXT         NULL,
    `LeadUserId`     CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `IssueCounter`   INT          NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG: bộ đếm sinh IssueNumber (PROJ-1, PROJ-2...)',
    `IsArchived`     TINYINT(1)   NOT NULL DEFAULT 0,
    `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt`      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`      DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Project_Org_Key` (`OrgId`, `ProjectKey`),
    KEY `IX_Project_LeadUserId` (`LeadUserId`),
    CONSTRAINT `FK_Project_Organization` FOREIGN KEY (`OrgId`) REFERENCES `Organization`(`Id`),
    CONSTRAINT `CK_Project_Key_Format` CHECK (`ProjectKey` REGEXP '^[A-Z][A-Z0-9]{1,9}$')
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Dự án - đơn vị chứa Sprint/Issue/Board/cấu hình workflow riêng';

CREATE TABLE `ProjectComponent` (
    `Id`          CHAR(36)     NOT NULL,
    `ProjectId`   CHAR(36)     NOT NULL,
    `Name`        VARCHAR(150) NOT NULL COMMENT 'Backend API, Mobile App, Database...',
    `Description` VARCHAR(500) NULL,
    `LeadUserId`  CHAR(36)     NULL COMMENT 'XMOD -> User.Id. Dùng auto-suggest assignee',
    `DefaultSkillId` CHAR(36)  NULL COMMENT 'XMOD -> SkillCatalog.Id. AI ASSIGNMENT: skill mặc định của component',
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ProjectComponent_Name` (`ProjectId`, `Name`),
    CONSTRAINT `FK_ProjectComponent_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Tương đương Components của Jira';

CREATE TABLE `ProjectVersion` (
    `Id`          CHAR(36)     NOT NULL,
    `ProjectId`   CHAR(36)     NOT NULL,
    `Name`        VARCHAR(60)  NOT NULL COMMENT 'v1.2.0',
    `Description` VARCHAR(500) NULL,
    `StartDate`   DATE         NULL,
    `ReleaseDate` DATE         NULL,
    `IsReleased`  TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ProjectVersion_Name` (`ProjectId`, `Name`),
    CONSTRAINT `FK_ProjectVersion_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Tương đương Fix Version / Release của Jira';

CREATE TABLE `WorkflowStatus` (
    `Id`         CHAR(36)     NOT NULL,
    `ProjectId`  CHAR(36)     NOT NULL,
    `Name`       VARCHAR(80)  NOT NULL,
    `Category`   VARCHAR(20)  NOT NULL COMMENT 'ToDo / InProgress / Done - chuẩn hóa để tính báo cáo',
    `ColorHex`   CHAR(7)      NOT NULL DEFAULT '#8993A4',
    `OrderIndex` INT          NOT NULL DEFAULT 0,
    `IsInitial`  TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Trạng thái mặc định khi tạo issue mới',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_WorkflowStatus_Name` (`ProjectId`, `Name`),
    KEY `IX_WorkflowStatus_Project_Order` (`ProjectId`, `OrderIndex`),
    CONSTRAINT `FK_WorkflowStatus_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_WorkflowStatus_Category` CHECK (`Category` IN ('ToDo','InProgress','Done'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Mỗi Project tự định nghĩa bộ trạng thái riêng';

CREATE TABLE `WorkflowTransition` (
    `Id`           CHAR(36)    NOT NULL,
    `ProjectId`    CHAR(36)    NOT NULL COMMENT 'BỔ SUNG: thiếu trong bản v2.0. Không có cột này phải JOIN 2 lần mới validate được',
    `FromStatusId` CHAR(36)    NOT NULL,
    `ToStatusId`   CHAR(36)    NOT NULL,
    `Name`         VARCHAR(80) NULL COMMENT 'Tên nút bấm hiển thị, VD "Gửi review"',
    `RequiredPermissionCode` VARCHAR(80) NULL COMMENT 'Chỉ role có quyền này mới được chuyển',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_WorkflowTransition` (`ProjectId`, `FromStatusId`, `ToStatusId`),
    KEY `IX_WorkflowTransition_From` (`FromStatusId`),
    KEY `IX_WorkflowTransition_To` (`ToStatusId`),
    CONSTRAINT `FK_WorkflowTransition_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WorkflowTransition_From` FOREIGN KEY (`FromStatusId`) REFERENCES `WorkflowStatus`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_WorkflowTransition_To` FOREIGN KEY (`ToStatusId`) REFERENCES `WorkflowStatus`(`Id`),
    CONSTRAINT `CK_WorkflowTransition_NotSelf` CHECK (`FromStatusId` <> `ToStatusId`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Luật chuyển trạng thái hợp lệ - chặn kéo thẳng ToDo -> Done';

CREATE TABLE `IssueType` (
    `Id`         CHAR(36)     NOT NULL,
    `ProjectId`  CHAR(36)     NOT NULL,
    `Name`       VARCHAR(60)  NOT NULL COMMENT 'Epic / Story / Task / Bug / Sub-task',
    `IconKey`    VARCHAR(50)  NULL,
    `ColorHex`   CHAR(7)      NOT NULL DEFAULT '#0052CC',
    `IsSubtask`  TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Validate khi gán ParentId',
    `HierarchyLevel` TINYINT  NOT NULL DEFAULT 1 COMMENT '2=Epic, 1=Story/Task/Bug, 0=Sub-task',
    `OrderIndex` INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_IssueType_Name` (`ProjectId`, `Name`),
    CONSTRAINT `FK_IssueType_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Loại issue cấu hình theo từng project';

CREATE TABLE `Priority` (
    `Id`        CHAR(36)    NOT NULL,
    `ProjectId` CHAR(36)    NULL COMMENT 'SỬA: NULL = mức ưu tiên dùng chung toàn hệ thống; có giá trị = riêng project (khớp Mục 5.2)',
    `Name`      VARCHAR(40) NOT NULL,
    `Level`     INT         NOT NULL COMMENT '1 = Highest ... 5 = Lowest',
    `ColorHex`  CHAR(7)     NOT NULL DEFAULT '#6B778C',
    `IconKey`   VARCHAR(50) NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_Priority_Project` (`ProjectId`, `Level`),
    CONSTRAINT `FK_Priority_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Mức độ ưu tiên';

CREATE TABLE `Board` (
    `Id`        CHAR(36)     NOT NULL,
    `ProjectId` CHAR(36)     NOT NULL,
    `Name`      VARCHAR(150) NOT NULL,
    `Type`      VARCHAR(20)  NOT NULL COMMENT 'Scrum (gắn Sprint) hoặc Kanban (liên tục)',
    `IsDefault` TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Board_Name` (`ProjectId`, `Name`),
    CONSTRAINT `FK_Board_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_Board_Type` CHECK (`Type` IN ('Scrum','Kanban'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Một project có thể có nhiều board';

CREATE TABLE `BoardColumn` (
    `Id`         CHAR(36)     NOT NULL,
    `BoardId`    CHAR(36)     NOT NULL,
    `StatusId`   CHAR(36)     NOT NULL COMMENT 'Cột ánh xạ tới WorkflowStatus nào',
    `Name`       VARCHAR(80)  NULL COMMENT 'Ghi đè tên hiển thị, NULL = lấy theo status',
    `OrderIndex` INT          NOT NULL DEFAULT 0,
    `WipLimit`   INT          NULL COMMENT 'BỔ SUNG: Mục 5.5 yêu cầu WIP limit nhưng v2.0 không có chỗ lưu',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_BoardColumn_Status` (`BoardId`, `StatusId`),
    CONSTRAINT `FK_BoardColumn_Board` FOREIGN KEY (`BoardId`) REFERENCES `Board`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_BoardColumn_Status` FOREIGN KEY (`StatusId`) REFERENCES `WorkflowStatus`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_BoardColumn_Wip` CHECK (`WipLimit` IS NULL OR `WipLimit` > 0)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='BỔ SUNG: cột board + giới hạn WIP';


-- =====================================================================================
-- MODULE 3: SPRINT & BACKLOG
-- Owner: Người B
-- =====================================================================================

CREATE TABLE `Sprint` (
    `Id`          CHAR(36)     NOT NULL,
    `ProjectId`   CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id',
    `Name`        VARCHAR(150) NOT NULL,
    `Goal`        VARCHAR(1000) NULL COMMENT 'Sprint Goal',
    `StartDate`   DATE         NULL,
    `EndDate`     DATE         NULL,
    `ActualStartAt`    DATETIME(6) NULL COMMENT 'Thời điểm bấm Start thật',
    `ActualCompleteAt` DATETIME(6) NULL,
    `Status`      VARCHAR(20)  NOT NULL DEFAULT 'Planned' COMMENT 'Planned / Active / Completed',
    `OrderIndex`  INT          NOT NULL DEFAULT 0,
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`   DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    -- Ràng buộc "mỗi Project chỉ có 1 Sprint Active" được ép Ở TẦNG DATABASE bằng
    -- generated column + unique index. Đây là cách duy nhất làm được trong MySQL
    -- (không có filtered index như SQL Server). An toàn hơn nhiều so với check ở code:
    -- khi 2 người bấm Start cùng lúc, code check sẽ lọt, DB thì không.
    `ActiveGuard` CHAR(36) GENERATED ALWAYS AS (IF(`Status` = 'Active', `ProjectId`, NULL)) STORED,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Sprint_OneActivePerProject` (`ActiveGuard`),
    KEY `IX_Sprint_Project_Status` (`ProjectId`, `Status`),
    CONSTRAINT `CK_Sprint_Status` CHECK (`Status` IN ('Planned','Active','Completed')),
    CONSTRAINT `CK_Sprint_Dates` CHECK (`EndDate` IS NULL OR `StartDate` IS NULL OR `EndDate` >= `StartDate`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Chu kỳ Scrum. UNIQUE trên ActiveGuard ép ràng buộc 1 Sprint Active/Project';

CREATE TABLE `SprintSnapshot` (
    `Id`               CHAR(36)    NOT NULL,
    `SprintId`         CHAR(36)    NOT NULL,
    `SnapshotDate`     DATE        NOT NULL,
    `TotalPoints`      DECIMAL(9,2) NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG: để vẽ được ideal line',
    `RemainingPoints`  DECIMAL(9,2) NOT NULL DEFAULT 0,
    `CompletedPoints`  DECIMAL(9,2) NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG',
    `AddedPoints`      DECIMAL(9,2) NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG: scope change trong sprint',
    `RemainingIssueCount` INT      NOT NULL DEFAULT 0,
    `TotalIssueCount`  INT         NOT NULL DEFAULT 0,
    `CreatedAt`        DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_SprintSnapshot_Day` (`SprintId`, `SnapshotDate`) COMMENT 'Job chạy lại trong ngày không tạo dòng trùng',
    CONSTRAINT `FK_SprintSnapshot_Sprint` FOREIGN KEY (`SprintId`) REFERENCES `Sprint`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Ảnh chụp hằng ngày cho Burndown - không thể tính on-the-fly';

CREATE TABLE `SprintMemberCapacity` (
    `Id`               CHAR(36)     NOT NULL,
    `SprintId`         CHAR(36)     NOT NULL,
    `UserId`           CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `CapacityPoints`   DECIMAL(9,2) NULL COMMENT 'Số point tối đa nhận được trong sprint này',
    `AvailableHours`   DECIMAL(7,2) NULL COMMENT 'Trừ nghỉ phép, họp, on-call',
    `Note`             VARCHAR(255) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_SprintCapacity` (`SprintId`, `UserId`),
    CONSTRAINT `FK_SprintCapacity_Sprint` FOREIGN KEY (`SprintId`) REFERENCES `Sprint`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='BỔ SUNG - AI ASSIGNMENT: trần công suất từng người, tránh gán quá tải';


-- =====================================================================================
-- MODULE 4: ISSUE TRACKING  (module lớn nhất)
-- Owner: Người C
-- =====================================================================================

CREATE TABLE `Issue` (
    `Id`            CHAR(36)     NOT NULL,
    `ProjectId`     CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id',
    `IssueNumber`   INT          NOT NULL COMMENT 'BỔ SUNG: số thứ tự trong project, ghép Project.ProjectKey thành "PROJ-123"',
    `SprintId`      CHAR(36)     NULL COMMENT 'XMOD -> Sprint.Id. NULL = còn ở Backlog',
    `ParentId`      CHAR(36)     NULL COMMENT 'Self-ref: Epic -> Story -> Sub-task',
    `EpicId`        CHAR(36)     NULL COMMENT 'BỔ SUNG: denormalize Epic gốc, tránh đệ quy khi gom nhóm Backlog',
    `AssigneeId`    CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `ReporterId`    CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `StatusId`      CHAR(36)     NOT NULL COMMENT 'XMOD -> WorkflowStatus.Id',
    `IssueTypeId`   CHAR(36)     NOT NULL COMMENT 'XMOD -> IssueType.Id',
    `PriorityId`    CHAR(36)     NULL     COMMENT 'XMOD -> Priority.Id',
    `Title`         VARCHAR(500) NOT NULL,
    `Description`   MEDIUMTEXT   NULL,
    `StoryPoints`   DECIMAL(6,2) NULL COMMENT 'DECIMAL thay INT để hỗ trợ 0.5 point',
    `OriginalEstimateMinutes`  INT NULL,
    `TimeSpentMinutes`         INT NOT NULL DEFAULT 0 COMMENT 'AI ASSIGNMENT: feature ước lượng độ chính xác',
    `RankOrder`     DECIMAL(30,15) NOT NULL COMMENT 'Chèn giữa = trung bình cộng 2 rank kề. Job rebalance khi khoảng cách < 1e-9',
    `DueDate`       DATE         NULL,
    `StartedAt`     DATETIME(6)  NULL COMMENT 'AI ASSIGNMENT: lần đầu vào trạng thái InProgress',
    `ResolvedAt`    DATETIME(6)  NULL COMMENT 'AI ASSIGNMENT: thời điểm vào Done, dùng tính cycle time',
    `IsAiGenerated` TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Sub-task do AI Breakdown sinh ra',
    `AiGenerationLogId` CHAR(36) NULL COMMENT 'XMOD -> AiGenerationLog.Id. Truy vết sub-task đến từ request AI nào',
    `IsAiAssigned`  TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG: assignee hiện tại do AI gợi ý và được chấp nhận',
    `IsDeleted`     TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedAt`     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`     DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Issue_Key` (`ProjectId`, `IssueNumber`),
    KEY `IX_Issue_Backlog` (`ProjectId`, `SprintId`, `RankOrder`) COMMENT 'Truy vấn Backlog & Board',
    KEY `IX_Issue_Sprint_Status` (`SprintId`, `StatusId`),
    KEY `IX_Issue_Assignee` (`AssigneeId`, `StatusId`) COMMENT 'AI ASSIGNMENT: tính workload hiện tại',
    KEY `IX_Issue_Parent` (`ParentId`),
    KEY `IX_Issue_Epic` (`EpicId`),
    KEY `IX_Issue_AiLog` (`AiGenerationLogId`),
    KEY `IX_Issue_Resolved` (`ProjectId`, `ResolvedAt`) COMMENT 'Báo cáo velocity & cycle time',
    CONSTRAINT `FK_Issue_Parent` FOREIGN KEY (`ParentId`) REFERENCES `Issue`(`Id`) ON DELETE SET NULL,
    CONSTRAINT `CK_Issue_Points` CHECK (`StoryPoints` IS NULL OR `StoryPoints` >= 0),
    CONSTRAINT `CK_Issue_AiLogConsistency` CHECK (`IsAiGenerated` = 0 OR `AiGenerationLogId` IS NOT NULL)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Entity trung tâm: Epic/Story/Task/Bug/Sub-task';

-- MySQL 8 không cho ParentId vừa nằm trong CHECK constraint vừa tham gia FK ON DELETE SET NULL.
-- Dùng trigger để giữ rule: Issue không được làm cha trực tiếp của chính nó.
DELIMITER $$
CREATE TRIGGER `TRG_Issue_NoSelfParent_Insert`
BEFORE INSERT ON `Issue`
FOR EACH ROW
BEGIN
    IF NEW.`ParentId` IS NOT NULL AND NEW.`ParentId` = NEW.`Id` THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Issue cannot be its own parent';
    END IF;
END$$

CREATE TRIGGER `TRG_Issue_NoSelfParent_Update`
BEFORE UPDATE ON `Issue`
FOR EACH ROW
BEGIN
    IF NEW.`ParentId` IS NOT NULL AND NEW.`ParentId` = NEW.`Id` THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Issue cannot be its own parent';
    END IF;
END$$
DELIMITER ;

CREATE TABLE `IssueLink` (
    `Id`            CHAR(36)    NOT NULL,
    `SourceIssueId` CHAR(36)    NOT NULL,
    `TargetIssueId` CHAR(36)    NOT NULL,
    `LinkType`      VARCHAR(30) NOT NULL COMMENT 'Blocks / IsBlockedBy / Relates / Duplicates / IsDuplicatedBy',
    `CreatedBy`     CHAR(36)    NOT NULL COMMENT 'XMOD -> User.Id',
    `CreatedAt`     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_IssueLink` (`SourceIssueId`, `TargetIssueId`, `LinkType`),
    KEY `IX_IssueLink_Target` (`TargetIssueId`),
    CONSTRAINT `FK_IssueLink_Source` FOREIGN KEY (`SourceIssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_IssueLink_Target` FOREIGN KEY (`TargetIssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_IssueLink_NotSelf` CHECK (`SourceIssueId` <> `TargetIssueId`),
    CONSTRAINT `CK_IssueLink_Type` CHECK (`LinkType` IN ('Blocks','IsBlockedBy','Relates','Duplicates','IsDuplicatedBy'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Quan hệ ngang giữa issue. Tạo Blocks thì tự sinh bản ghi IsBlockedBy ngược chiều';

CREATE TABLE `IssueWatcher` (
    `IssueId`   CHAR(36)    NOT NULL,
    `UserId`    CHAR(36)    NOT NULL COMMENT 'XMOD -> User.Id',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`IssueId`, `UserId`),
    KEY `IX_IssueWatcher_UserId` (`UserId`),
    CONSTRAINT `FK_IssueWatcher_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='N-N theo dõi issue';

CREATE TABLE `Comment` (
    `Id`              CHAR(36)    NOT NULL,
    `IssueId`         CHAR(36)    NOT NULL,
    `UserId`          CHAR(36)    NOT NULL COMMENT 'XMOD -> User.Id',
    `ParentCommentId` CHAR(36)    NULL COMMENT 'BỔ SUNG: reply lồng nhau',
    `Content`         MEDIUMTEXT  NOT NULL COMMENT 'Markdown',
    `MentionedUserIds` JSON       NULL COMMENT 'BỔ SUNG: mảng GUID được @mention, để module Notification xử lý',
    `IsEdited`        TINYINT(1)  NOT NULL DEFAULT 0,
    `IsDeleted`       TINYINT(1)  NOT NULL DEFAULT 0,
    `CreatedAt`       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`       DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Comment_Issue` (`IssueId`, `CreatedAt`),
    KEY `IX_Comment_Parent` (`ParentCommentId`),
    CONSTRAINT `FK_Comment_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Comment_Parent` FOREIGN KEY (`ParentCommentId`) REFERENCES `Comment`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Bình luận do người dùng chủ động viết';

CREATE TABLE `Attachment` (
    `Id`           CHAR(36)     NOT NULL,
    `IssueId`      CHAR(36)     NOT NULL,
    `UploadedBy`   CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `FileName`     VARCHAR(255) NOT NULL COMMENT 'Tên file gốc',
    `StoredPath`   VARCHAR(500) NOT NULL COMMENT 'Đường dẫn local / S3 key',
    `ContentType`  VARCHAR(120) NOT NULL COMMENT 'BỔ SUNG: cần cho Content-Disposition khi tải về',
    `FileSizeBytes` BIGINT      NOT NULL COMMENT 'BỔ SUNG: kiểm tra quota',
    `CreatedAt`    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Attachment_Issue` (`IssueId`),
    CONSTRAINT `FK_Attachment_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='File đính kèm';

CREATE TABLE `ActivityLog` (
    `Id`        CHAR(36)    NOT NULL,
    `IssueId`   CHAR(36)    NOT NULL,
    `UserId`    CHAR(36)    NULL COMMENT 'XMOD -> User.Id. NULL = do hệ thống/AI thực hiện',
    `Action`    VARCHAR(60) NOT NULL COMMENT 'Created / StatusChanged / AssigneeChanged / AiBreakdownApplied...',
    `FieldName` VARCHAR(80) NULL,
    `OldValue`  TEXT        NULL,
    `NewValue`  TEXT        NULL,
    `Detail`    JSON        NULL COMMENT 'Payload đầy đủ dạng JSON, query được bằng JSON_EXTRACT',
    `Source`    VARCHAR(20) NOT NULL DEFAULT 'User' COMMENT 'User / System / AI - phân biệt để báo cáo AI',
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_ActivityLog_Issue` (`IssueId`, `CreatedAt`),
    KEY `IX_ActivityLog_Action` (`Action`, `CreatedAt`) COMMENT 'Báo cáo tỉ lệ sửa nội dung AI sinh',
    CONSTRAINT `FK_ActivityLog_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_ActivityLog_Source` CHECK (`Source` IN ('User','System','AI'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Nhật ký thay đổi tự động - sinh qua IActivityLogService';

CREATE TABLE `IssueStatusHistory` (
    `Id`             CHAR(36)    NOT NULL,
    `IssueId`        CHAR(36)    NOT NULL,
    `FromStatusId`   CHAR(36)    NULL COMMENT 'XMOD. NULL = trạng thái đầu tiên khi tạo',
    `ToStatusId`     CHAR(36)    NOT NULL COMMENT 'XMOD -> WorkflowStatus.Id',
    `FromCategory`   VARCHAR(20) NULL,
    `ToCategory`     VARCHAR(20) NOT NULL,
    `ChangedBy`      CHAR(36)    NULL COMMENT 'XMOD -> User.Id',
    `DurationSeconds` BIGINT     NULL COMMENT 'Thời gian ĐÃ Ở trạng thái trước đó - tính sẵn khi ghi',
    `ChangedAt`      DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_IssueStatusHistory_Issue` (`IssueId`, `ChangedAt`),
    CONSTRAINT `FK_IssueStatusHistory_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='BỔ SUNG - AI ASSIGNMENT: ActivityLog dạng text không tính được cycle time. Bảng này lưu có cấu trúc + duration tính sẵn';

CREATE TABLE `IssueAssignmentHistory` (
    `Id`             CHAR(36)     NOT NULL,
    `IssueId`        CHAR(36)     NOT NULL,
    `FromAssigneeId` CHAR(36)     NULL COMMENT 'XMOD -> User.Id. NULL = chưa gán ai',
    `ToAssigneeId`   CHAR(36)     NULL COMMENT 'XMOD -> User.Id. NULL = gỡ assignee',
    `AssignedBy`     CHAR(36)     NULL COMMENT 'XMOD -> User.Id. NULL nếu do AI tự gán',
    `AssignmentSource` VARCHAR(20) NOT NULL DEFAULT 'Manual' COMMENT 'Manual / AiSuggested / AiAuto / SelfPick',
    `AiCandidateId`  CHAR(36)     NULL COMMENT 'XMOD -> AiAssignmentCandidate.Id nếu đến từ gợi ý AI',
    `StoryPointsAtTime` DECIMAL(6,2) NULL COMMENT 'Snapshot point tại thời điểm gán (point có thể bị đổi sau)',
    `Reason`         VARCHAR(500) NULL COMMENT 'Lý do override nếu PM không theo gợi ý AI',
    `AssignedAt`     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_IssueAssignHistory_Issue` (`IssueId`, `AssignedAt`),
    KEY `IX_IssueAssignHistory_To` (`ToAssigneeId`, `AssignedAt`),
    KEY `IX_IssueAssignHistory_Source` (`AssignmentSource`, `AssignedAt`),
    CONSTRAINT `FK_IssueAssignHistory_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_IssueAssignHistory_Source` CHECK (`AssignmentSource` IN ('Manual','AiSuggested','AiAuto','SelfPick'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='BỔ SUNG - BẢNG QUAN TRỌNG NHẤT cho AI phân phối task: toàn bộ lịch sử giao việc + nguồn gán';

CREATE TABLE `Label` (
    `Id`        CHAR(36)    NOT NULL,
    `ProjectId` CHAR(36)    NOT NULL COMMENT 'XMOD -> Project.Id',
    `Name`      VARCHAR(60) NOT NULL,
    `ColorHex`  CHAR(7)     NOT NULL DEFAULT '#DFE1E6',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_Label_Name` (`ProjectId`, `Name`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Nhãn tự do theo project';

CREATE TABLE `IssueLabel` (
    `IssueId` CHAR(36) NOT NULL,
    `LabelId` CHAR(36) NOT NULL,
    PRIMARY KEY (`IssueId`, `LabelId`),
    KEY `IX_IssueLabel_Label` (`LabelId`),
    CONSTRAINT `FK_IssueLabel_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_IssueLabel_Label` FOREIGN KEY (`LabelId`) REFERENCES `Label`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='N-N Issue <-> Label';

CREATE TABLE `IssueComponentLink` (
    `IssueId`     CHAR(36) NOT NULL,
    `ComponentId` CHAR(36) NOT NULL COMMENT 'XMOD -> ProjectComponent.Id',
    PRIMARY KEY (`IssueId`, `ComponentId`),
    KEY `IX_IssueComponent_Component` (`ComponentId`),
    CONSTRAINT `FK_IssueComponent_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='BỔ SUNG: v2.0 yêu cầu báo cáo theo Component nhưng thiếu bảng nối này';

CREATE TABLE `IssueVersionLink` (
    `IssueId`   CHAR(36)    NOT NULL,
    `VersionId` CHAR(36)    NOT NULL COMMENT 'XMOD -> ProjectVersion.Id',
    `LinkType`  VARCHAR(20) NOT NULL DEFAULT 'FixVersion' COMMENT 'FixVersion / AffectsVersion',
    PRIMARY KEY (`IssueId`, `VersionId`, `LinkType`),
    KEY `IX_IssueVersion_Version` (`VersionId`),
    CONSTRAINT `FK_IssueVersion_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_IssueVersion_LinkType` CHECK (`LinkType` IN ('FixVersion','AffectsVersion'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='BỔ SUNG: bảng nối Issue <-> Version cho release note';

CREATE TABLE `IssueRequiredSkill` (
    `Id`            CHAR(36) NOT NULL,
    `IssueId`       CHAR(36) NOT NULL,
    `SkillId`       CHAR(36) NOT NULL COMMENT 'XMOD -> SkillCatalog.Id',
    `MinLevel`      TINYINT  NOT NULL DEFAULT 1 COMMENT '1..5',
    `Weight`        DECIMAL(4,3) NOT NULL DEFAULT 1.000 COMMENT 'Trọng số khi tính skill-match score',
    `Source`        VARCHAR(20) NOT NULL DEFAULT 'Manual' COMMENT 'Manual / AI / DerivedFromComponent',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_IssueRequiredSkill` (`IssueId`, `SkillId`),
    KEY `IX_IssueRequiredSkill_Skill` (`SkillId`),
    CONSTRAINT `FK_IssueRequiredSkill_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_IssueRequiredSkill_Level` CHECK (`MinLevel` BETWEEN 1 AND 5),
    CONSTRAINT `CK_IssueRequiredSkill_Source` CHECK (`Source` IN ('Manual','AI','DerivedFromComponent'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='BỔ SUNG - AI ASSIGNMENT: kỹ năng yêu cầu của task, ghép với UserSkill để tính skill-match';

CREATE TABLE `AcceptanceCriteria` (
    `Id`         CHAR(36)    NOT NULL,
    `IssueId`    CHAR(36)    NOT NULL COMMENT 'SỬA: cho phép gắn mọi loại issue, không chỉ Story (ví dụ JSON ở Mục 5.9.8 sinh AC cho Sub-task)',
    `Content`    TEXT        NOT NULL COMMENT 'Given-When-Then hoặc gạch đầu dòng',
    `IsMet`      TINYINT(1)  NOT NULL DEFAULT 0,
    `MetBy`      CHAR(36)    NULL COMMENT 'XMOD -> User.Id',
    `MetAt`      DATETIME(6) NULL,
    `OrderIndex` INT         NOT NULL DEFAULT 0,
    `Source`     VARCHAR(20) NOT NULL DEFAULT 'Manual' COMMENT 'AI / Manual - đánh giá độ tin cậy gợi ý AI',
    `AiGenerationLogId` CHAR(36) NULL COMMENT 'XMOD: truy vết AC này do request AI nào sinh',
    `WasEditedAfterAi`  TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'BỔ SUNG: nhãn huấn luyện - AC do AI sinh có bị sửa không',
    `CreatedAt`  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`  DATETIME(6) NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_AcceptanceCriteria_Issue` (`IssueId`, `OrderIndex`),
    KEY `IX_AcceptanceCriteria_Source` (`Source`, `CreatedAt`),
    CONSTRAINT `FK_AcceptanceCriteria_Issue` FOREIGN KEY (`IssueId`) REFERENCES `Issue`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_AcceptanceCriteria_Source` CHECK (`Source` IN ('AI','Manual'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Tiêu chí nghiệm thu, tick được từng dòng cho Definition of Done';


-- =====================================================================================
-- MODULE 5: AI ASSIST — PHẦN CHUNG (Model registry & Prompt versioning)
-- Owner: Người C (dataset) phối hợp cả nhóm
-- =====================================================================================

CREATE TABLE `AiModel` (
    `Id`              CHAR(36)     NOT NULL,
    `Code`            VARCHAR(100) NOT NULL COMMENT 'qwen2.5:3b-instruct, qwen2.5:3b-scrum-lora-v1',
    `DisplayName`     VARCHAR(150) NOT NULL,
    `Provider`        VARCHAR(50)  NOT NULL DEFAULT 'Ollama' COMMENT 'Ollama / OpenAI / Local',
    `TaskType`        VARCHAR(30)  NOT NULL COMMENT 'Breakdown / Assignment / Embedding',
    `BaseModelCode`   VARCHAR(100) NULL COMMENT 'Model gốc nếu đây là bản fine-tune',
    `AdapterPath`     VARCHAR(500) NULL COMMENT 'Đường dẫn LoRA adapter / Modelfile',
    `TrainingRunId`   CHAR(36)     NULL COMMENT 'XMOD -> AiTrainingRun.Id nếu sinh từ fine-tune',
    `IsActive`        TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Model đang được dùng ở production',
    `ContextWindow`   INT          NULL,
    `DefaultParams`   JSON         NULL COMMENT 'temperature, top_p, num_predict, seed...',
    `CreatedAt`       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiModel_Code` (`Code`),
    KEY `IX_AiModel_Task_Active` (`TaskType`, `IsActive`),
    CONSTRAINT `CK_AiModel_TaskType` CHECK (`TaskType` IN ('Breakdown','Assignment','Embedding'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Registry model - BẮT BUỘC có nếu định fine-tune: phải biết kết quả nào sinh bởi model nào để so sánh A/B';

CREATE TABLE `AiPromptTemplate` (
    `Id`           CHAR(36)     NOT NULL,
    `Code`         VARCHAR(80)  NOT NULL COMMENT 'breakdown.system.v1',
    `Version`      INT          NOT NULL DEFAULT 1,
    `TaskType`     VARCHAR(30)  NOT NULL,
    `Language`     VARCHAR(10)  NOT NULL DEFAULT 'vi' COMMENT 'vi / en / auto',
    `SystemPrompt` MEDIUMTEXT   NOT NULL,
    `UserTemplate` MEDIUMTEXT   NULL COMMENT 'Template có placeholder {{Title}}, {{Description}}, {{IssueTypes}}',
    `JsonSchema`   JSON         NULL COMMENT 'QUAN TRỌNG: schema gửi vào tham số `format` của Ollama để ÉP định dạng ở mức decoder. Giải quyết bài toán "AI sinh task thừa/sai format" mà KHÔNG cần fine-tune',
    `IsActive`     TINYINT(1)   NOT NULL DEFAULT 0,
    `CreatedBy`    CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `CreatedAt`    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiPromptTemplate` (`Code`, `Version`),
    KEY `IX_AiPromptTemplate_Active` (`TaskType`, `IsActive`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Prompt có version - không hard-code prompt trong C#, nếu không sẽ không A/B test được';


-- =====================================================================================
-- MODULE 6: AI AUTO TASK BREAKDOWN
-- Owner: Người A
-- =====================================================================================

CREATE TABLE `AiGenerationLog` (
    `Id`               CHAR(36)     NOT NULL,
    `IssueId`          CHAR(36)     NOT NULL COMMENT 'XMOD -> Issue.Id (Story gốc được yêu cầu chia nhỏ)',
    `ProjectId`        CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id. Denormalize để rate-limit theo project không phải JOIN',
    `UserId`           CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id (PO/Tech Lead kích hoạt)',
    `ModelId`          CHAR(36)     NULL,
    `PromptTemplateId` CHAR(36)     NULL,
    `InputText`        TEXT         NULL COMMENT 'Mô tả bổ sung người dùng nhập',
    `RenderedPrompt`   MEDIUMTEXT   NULL COMMENT 'Prompt cuối cùng đã ghép - BẮT BUỘC lưu để tái lập kết quả',
    `RawResponseJson`  MEDIUMTEXT   NULL COMMENT 'Phản hồi thô trước khi parse, phục vụ audit & đổi logic parse',
    `ParsedJson`       JSON         NULL COMMENT 'Sau khi parse & validate schema',
    `Status`           VARCHAR(20)  NOT NULL DEFAULT 'Pending' COMMENT 'Pending / Processing / Completed / Failed / Cancelled',
    `ErrorMessage`     VARCHAR(1000) NULL,
    `ErrorCode`        VARCHAR(50)  NULL COMMENT 'Timeout / InvalidJson / SchemaMismatch / OllamaUnavailable',
    `PromptTokens`     INT          NULL,
    `CompletionTokens` INT          NULL,
    `LatencyMs`        INT          NULL COMMENT 'Theo dõi hiệu năng model cục bộ',
    `RetryCount`       INT          NOT NULL DEFAULT 0,
    `HangfireJobId`    VARCHAR(64)  NULL COMMENT 'Truy vết ngược sang Hangfire dashboard khi debug',
    `AppliedAt`        DATETIME(6)  NULL COMMENT 'Thời điểm user bấm "Áp dụng". NULL = chưa từng áp dụng',
    `AppliedCount`     INT          NOT NULL DEFAULT 0 COMMENT 'Số sub-task thực sự được tạo (có thể < số gợi ý)',
    `CreatedAt`        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `CompletedAt`      DATETIME(6)  NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_AiGenerationLog_Issue` (`IssueId`, `CreatedAt`),
    KEY `IX_AiGenerationLog_RateLimit` (`ProjectId`, `CreatedAt`) COMMENT 'Đếm số request gần đây để rate-limit',
    KEY `IX_AiGenerationLog_Status` (`Status`, `CreatedAt`),
    KEY `IX_AiGenerationLog_User` (`UserId`, `CreatedAt`),
    CONSTRAINT `FK_AiGenerationLog_Model` FOREIGN KEY (`ModelId`) REFERENCES `AiModel`(`Id`),
    CONSTRAINT `FK_AiGenerationLog_Prompt` FOREIGN KEY (`PromptTemplateId`) REFERENCES `AiPromptTemplate`(`Id`),
    CONSTRAINT `CK_AiGenerationLog_Status` CHECK (`Status` IN ('Pending','Processing','Completed','Failed','Cancelled'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Mỗi lần yêu cầu AI chia nhỏ Story';

CREATE TABLE `AiSuggestedTask` (
    `Id`                CHAR(36)     NOT NULL,
    `AiGenerationLogId` CHAR(36)     NOT NULL,
    `OrderIndex`        INT          NOT NULL DEFAULT 0,

    -- Bản GỐC do AI sinh (BẤT BIẾN, không bao giờ được UPDATE)
    `OriginalSummary`     VARCHAR(500) NOT NULL,
    `OriginalDescription` TEXT         NULL,
    `OriginalAcceptanceCriteria` JSON  NULL COMMENT 'Mảng chuỗi AC do AI sinh',
    `OriginalEstimatePoints`     DECIMAL(6,2) NULL,
    `OriginalSuggestedSkills`    JSON  NULL COMMENT 'AI gợi ý skill cần có -> nạp vào IssueRequiredSkill',

    -- Bản SAU KHI người dùng sửa (human-in-the-loop)
    `FinalSummary`      VARCHAR(500) NULL,
    `FinalDescription`  TEXT         NULL,
    `FinalAcceptanceCriteria` JSON   NULL,
    `FinalEstimatePoints`     DECIMAL(6,2) NULL,

    `UserAction`        VARCHAR(20)  NOT NULL DEFAULT 'Pending'
                        COMMENT 'Pending / Kept / Edited / Rejected — ĐÂY LÀ NHÃN HUẤN LUYỆN QUAN TRỌNG NHẤT',
    `EditDistanceRatio` DECIMAL(5,4) NULL COMMENT '0.0 = giữ nguyên, 1.0 = viết lại hoàn toàn. Tính bằng Levenshtein chuẩn hóa',
    `RejectReason`      VARCHAR(500) NULL COMMENT 'Vì sao bỏ gợi ý này - dữ liệu vàng để cải thiện prompt',
    `CreatedIssueId`    CHAR(36)     NULL COMMENT 'XMOD -> Issue.Id nếu đã được tạo thành Sub-task thật',
    `ReviewedBy`        CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `ReviewedAt`        DATETIME(6)  NULL,
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_AiSuggestedTask_Log` (`AiGenerationLogId`, `OrderIndex`),
    KEY `IX_AiSuggestedTask_Action` (`UserAction`, `CreatedAt`) COMMENT 'Truy vấn export dataset theo nhãn + thời gian',
    KEY `IX_AiSuggestedTask_CreatedIssue` (`CreatedIssueId`),
    CONSTRAINT `FK_AiSuggestedTask_Log` FOREIGN KEY (`AiGenerationLogId`) REFERENCES `AiGenerationLog`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_AiSuggestedTask_Action` CHECK (`UserAction` IN ('Pending','Kept','Edited','Rejected'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='BẢNG CỐT LÕI CHO FINE-TUNE. Chỉ lưu RawResponseJson như thiết kế v2.0 thì KHÔNG BAO GIỜ biết người dùng đã sửa gì -> mất sạch tín hiệu huấn luyện';


-- =====================================================================================
-- MODULE 7: AI ASSIGNMENT — PHÂN PHỐI TASK TỰ ĐỘNG
-- Owner: Người B
--
-- LƯU Ý THIẾT KẾ: bài toán này KHÔNG nên dùng LLM sinh văn bản. Đây là bài toán
-- ranking/tối ưu có ràng buộc. Giai đoạn 1 dùng scoring function tường minh (giải thích
-- được, chạy trong milliseconds); giai đoạn 2 train learning-to-rank trên chính dữ liệu
-- của bảng AiAssignmentCandidate + AiAssignmentDecision bên dưới.
--
-- NGUYÊN TẮC SỐNG CÒN: FeatureSnapshot phải lưu giá trị TẠI THỜI ĐIỂM gợi ý.
-- Tính lại từ DB hiện tại khi train = data leakage = model vô dụng.
-- =====================================================================================

CREATE TABLE `UserWorkloadSnapshot` (
    `Id`                  CHAR(36)     NOT NULL,
    `UserId`              CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `ProjectId`           CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id',
    `SprintId`            CHAR(36)     NULL     COMMENT 'XMOD -> Sprint.Id',
    `SnapshotDate`        DATE         NOT NULL,
    `OpenIssueCount`      INT          NOT NULL DEFAULT 0,
    `InProgressCount`     INT          NOT NULL DEFAULT 0,
    `OpenPoints`          DECIMAL(9,2) NOT NULL DEFAULT 0 COMMENT 'Tổng point chưa Done đang gánh',
    `InProgressPoints`    DECIMAL(9,2) NOT NULL DEFAULT 0,
    `OverdueCount`        INT          NOT NULL DEFAULT 0,
    `CapacityPoints`      DECIMAL(9,2) NULL COMMENT 'Lấy từ SprintMemberCapacity',
    `UtilizationRatio`    DECIMAL(6,3) NULL COMMENT 'OpenPoints / CapacityPoints. > 1.0 = quá tải',
    `CreatedAt`           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_UserWorkloadSnapshot` (`UserId`, `ProjectId`, `SnapshotDate`),
    KEY `IX_UserWorkload_Project_Date` (`ProjectId`, `SnapshotDate`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Hangfire job hằng ngày. Đây là feature "độ cân bằng khối lượng công việc" mà đề xuất nhắc tới';

CREATE TABLE `UserPerformanceMetric` (
    `Id`                     CHAR(36)     NOT NULL,
    `UserId`                 CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `ProjectId`              CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id',
    `SprintId`               CHAR(36)     NULL     COMMENT 'XMOD -> Sprint.Id. NULL = số liệu tổng toàn project',
    `PeriodStart`            DATE         NOT NULL,
    `PeriodEnd`              DATE         NOT NULL,
    `AssignedIssueCount`     INT          NOT NULL DEFAULT 0,
    `CompletedIssueCount`    INT          NOT NULL DEFAULT 0,
    `CommittedPoints`        DECIMAL(9,2) NOT NULL DEFAULT 0,
    `CompletedPoints`        DECIMAL(9,2) NOT NULL DEFAULT 0,
    `AvgCycleTimeHours`      DECIMAL(9,2) NULL COMMENT 'Trung bình từ InProgress -> Done',
    `MedianCycleTimeHours`   DECIMAL(9,2) NULL COMMENT 'Median chống ảnh hưởng của outlier tốt hơn Avg',
    `OnTimeRatio`            DECIMAL(5,4) NULL COMMENT 'Tỉ lệ hoàn thành trước DueDate',
    `ReopenedCount`          INT          NOT NULL DEFAULT 0 COMMENT 'Proxy cho chất lượng công việc',
    `EstimateAccuracyRatio`  DECIMAL(6,3) NULL COMMENT 'TimeSpent / OriginalEstimate',
    `CalculatedAt`           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_UserPerfMetric` (`UserId`, `ProjectId`, `PeriodStart`, `PeriodEnd`),
    KEY `IX_UserPerfMetric_Sprint` (`SprintId`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Feature "năng lực thực tế" cho AI phân phối task';

CREATE TABLE `AiAssignmentRun` (
    `Id`               CHAR(36)     NOT NULL,
    `ProjectId`        CHAR(36)     NOT NULL COMMENT 'XMOD -> Project.Id',
    `SprintId`         CHAR(36)     NULL     COMMENT 'XMOD -> Sprint.Id',
    `RequestedBy`      CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `TriggerSource`    VARCHAR(30)  NOT NULL DEFAULT 'Manual'
                       COMMENT 'Manual / AfterAiBreakdown / SprintPlanning - luồng "AI phân phối task VỪA ĐƯỢC AI bóc tách" dùng AfterAiBreakdown',
    `SourceGenerationLogId` CHAR(36) NULL COMMENT 'XMOD -> AiGenerationLog.Id. Nối 2 AI với nhau',
    `Strategy`         VARCHAR(30)  NOT NULL DEFAULT 'WeightedScore'
                       COMMENT 'WeightedScore (giai đoạn 1) / LearnedRanker (giai đoạn 2) / RoundRobin (baseline đối chứng)',
    `ModelId`          CHAR(36)     NULL COMMENT 'NULL nếu dùng scoring function thuần',
    `Weights`          JSON         NULL COMMENT 'Trọng số dùng cho lần chạy này, VD {"load":0.4,"skill":0.3,"history":0.2,"capacity":0.1} - lưu để tái lập',
    `CandidateUserIds` JSON         NULL COMMENT 'Danh sách user được xét (thành viên project tại thời điểm chạy)',
    `IssueCount`       INT          NOT NULL DEFAULT 0,
    `Status`           VARCHAR(20)  NOT NULL DEFAULT 'Pending' COMMENT 'Pending / Processing / Completed / Failed',
    `ErrorMessage`     VARCHAR(1000) NULL,
    `LatencyMs`        INT          NULL,
    `CreatedAt`        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `CompletedAt`      DATETIME(6)  NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_AiAssignmentRun_Project` (`ProjectId`, `CreatedAt`),
    KEY `IX_AiAssignmentRun_Source` (`SourceGenerationLogId`),
    CONSTRAINT `CK_AiAssignmentRun_Status` CHECK (`Status` IN ('Pending','Processing','Completed','Failed')),
    CONSTRAINT `CK_AiAssignmentRun_Strategy` CHECK (`Strategy` IN ('WeightedScore','LearnedRanker','RoundRobin','Hybrid'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Một lần chạy phân phối cho một tập issue';

CREATE TABLE `AiAssignmentCandidate` (
    `Id`                CHAR(36)     NOT NULL,
    `RunId`             CHAR(36)     NOT NULL,
    `IssueId`           CHAR(36)     NOT NULL COMMENT 'XMOD -> Issue.Id',
    `CandidateUserId`   CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id',
    `Rank`              INT          NOT NULL COMMENT '1 = gợi ý tốt nhất',
    `TotalScore`        DECIMAL(9,6) NOT NULL,

    -- Điểm thành phần: BẮT BUỘC lưu tách để giải thích cho PM "vì sao gán cho A"
    `LoadBalanceScore`  DECIMAL(9,6) NULL COMMENT 'Càng ít point đang gánh, điểm càng cao',
    `SkillMatchScore`   DECIMAL(9,6) NULL COMMENT 'Khớp IssueRequiredSkill với UserSkill',
    `HistoryScore`      DECIMAL(9,6) NULL COMMENT 'Từng làm task tương tự (component/label) và làm tốt',
    `CapacityScore`     DECIMAL(9,6) NULL COMMENT 'Còn dư capacity trong sprint',
    `IsColdStart`       TINYINT(1)   NOT NULL DEFAULT 0
                        COMMENT 'TRUE = user chưa có log task, điểm tính từ UserProfile/UserSkill thay vì lịch sử',

    `FeatureSnapshot`   JSON         NOT NULL
                        COMMENT 'TOÀN BỘ feature thô tại thời điểm chạy: open_points, in_progress_count, capacity, skill_levels, avg_cycle_time... KHÔNG ĐƯỢC tính lại khi train, nếu không sẽ data leakage',
    `Explanation`       VARCHAR(1000) NULL COMMENT 'Câu giải thích hiển thị cho PM',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiAssignmentCandidate` (`RunId`, `IssueId`, `CandidateUserId`),
    KEY `IX_AiAssignmentCandidate_Issue` (`IssueId`, `Rank`),
    CONSTRAINT `FK_AiAssignmentCandidate_Run` FOREIGN KEY (`RunId`) REFERENCES `AiAssignmentRun`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Mỗi (issue, user) một dòng có điểm - đây chính là format dữ liệu chuẩn cho learning-to-rank sau này';

CREATE TABLE `AiAssignmentDecision` (
    `Id`                  CHAR(36)     NOT NULL,
    `RunId`               CHAR(36)     NOT NULL,
    `IssueId`             CHAR(36)     NOT NULL COMMENT 'XMOD -> Issue.Id',
    `SuggestedUserId`     CHAR(36)     NULL COMMENT 'XMOD -> User.Id. Ứng viên Rank = 1',
    `SuggestedCandidateId` CHAR(36)    NULL,
    `FinalUserId`         CHAR(36)     NULL COMMENT 'XMOD -> User.Id. Người thực sự được gán',
    `Outcome`             VARCHAR(20)  NOT NULL DEFAULT 'Pending'
                          COMMENT 'Pending / Accepted / Overridden / Rejected — NHÃN HUẤN LUYỆN',
    `OverrideReason`      VARCHAR(500) NULL COMMENT 'PM ghi lý do đổi người - dữ liệu quý nhất để cải thiện model',
    `DecidedBy`           CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `DecidedAt`           DATETIME(6)  NULL,

    -- Đo hiệu quả THỰC TẾ của quyết định (job cập nhật sau khi issue Done)
    `ActualCycleTimeHours` DECIMAL(9,2) NULL,
    `WasCompletedOnTime`   TINYINT(1)   NULL,
    `WasReassignedLater`   TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Bị chuyển người sau đó = dấu hiệu gợi ý sai',
    `OutcomeEvaluatedAt`   DATETIME(6)  NULL,

    `CreatedAt`           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiAssignmentDecision` (`RunId`, `IssueId`),
    KEY `IX_AiAssignmentDecision_Outcome` (`Outcome`, `CreatedAt`),
    KEY `IX_AiAssignmentDecision_Issue` (`IssueId`),
    CONSTRAINT `FK_AiAssignmentDecision_Run` FOREIGN KEY (`RunId`) REFERENCES `AiAssignmentRun`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AiAssignmentDecision_Candidate` FOREIGN KEY (`SuggestedCandidateId`) REFERENCES `AiAssignmentCandidate`(`Id`) ON DELETE SET NULL,
    CONSTRAINT `CK_AiAssignmentDecision_Outcome` CHECK (`Outcome` IN ('Pending','Accepted','Overridden','Rejected'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Nhãn cho AI phân phối: PM có theo gợi ý không, và kết quả thực tế ra sao';


-- =====================================================================================
-- MODULE 8: AI DATASET & TRAINING
-- Owner: Người C
-- Ghi chú: người này nên làm TRƯỚC và ĐỘC LẬP phần schema + tập eval thủ công,
-- vì nếu chờ 2 module kia có dữ liệu thì sẽ bị block toàn bộ thời gian đầu.
-- =====================================================================================

CREATE TABLE `AiDataset` (
    `Id`          CHAR(36)     NOT NULL,
    `Code`        VARCHAR(80)  NOT NULL COMMENT 'breakdown-vi-v1, assignment-ranking-v1',
    `Name`        VARCHAR(200) NOT NULL,
    `TaskType`    VARCHAR(30)  NOT NULL COMMENT 'Breakdown / Assignment',
    `Description` TEXT         NULL,
    `Languages`   JSON         NULL COMMENT '["vi","en"]',
    `CreatedBy`   CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiDataset_Code` (`Code`),
    CONSTRAINT `CK_AiDataset_TaskType` CHECK (`TaskType` IN ('Breakdown','Assignment'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Bộ dữ liệu huấn luyện';

CREATE TABLE `AiDatasetVersion` (
    `Id`              CHAR(36)     NOT NULL,
    `DatasetId`       CHAR(36)     NOT NULL,
    `VersionTag`      VARCHAR(40)  NOT NULL COMMENT 'v1.0.0',
    `SampleCount`     INT          NOT NULL DEFAULT 0,
    `TrainCount`      INT          NOT NULL DEFAULT 0,
    `ValidationCount` INT          NOT NULL DEFAULT 0,
    `TestCount`       INT          NOT NULL DEFAULT 0,
    `SplitSeed`       INT          NULL COMMENT 'Seed chia split - BẮT BUỘC để tái lập được kết quả',
    `Checksum`        CHAR(64)     NULL COMMENT 'SHA-256 của file JSONL export',
    `ExportPath`      VARCHAR(500) NULL,
    `IsFrozen`        TINYINT(1)   NOT NULL DEFAULT 0 COMMENT 'Đã đóng băng thì KHÔNG được sửa sample nữa',
    `FrozenAt`        DATETIME(6)  NULL,
    `Notes`           TEXT         NULL,
    `CreatedAt`       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiDatasetVersion` (`DatasetId`, `VersionTag`),
    CONSTRAINT `FK_AiDatasetVersion_Dataset` FOREIGN KEY (`DatasetId`) REFERENCES `AiDataset`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Version hóa dataset - so sánh model chỉ có ý nghĩa khi cùng dataset version';

CREATE TABLE `AiDatasetSample` (
    `Id`                 CHAR(36)     NOT NULL,
    `DatasetVersionId`   CHAR(36)     NOT NULL,

    -- Nguồn gốc mẫu (truy vết ngược về dữ liệu sản xuất)
    `SourceType`         VARCHAR(30)  NOT NULL COMMENT 'AiSuggestedTask / AiAssignmentDecision / ManualCurated / Synthetic',
    `SourceRefId`        CHAR(36)     NULL COMMENT 'XMOD: Id của bản ghi nguồn',

    -- Nội dung mẫu, format Alpaca/ShareGPT cho LoRA
    `Instruction`        MEDIUMTEXT   NOT NULL,
    `InputJson`          JSON         NOT NULL COMMENT 'Với Breakdown: {title, description, projectContext}. Với Assignment: {issue_features, candidates[]}',
    `OutputJson`         JSON         NOT NULL COMMENT 'Đầu ra CHUẨN đã qua người duyệt',
    `Language`           VARCHAR(10)  NOT NULL DEFAULT 'vi',
    `TokenCount`         INT          NULL COMMENT 'Loại bỏ mẫu vượt context window',

    `SplitType`          VARCHAR(20)  NOT NULL DEFAULT 'Train' COMMENT 'Train / Validation / Test',
    `QualityStatus`      VARCHAR(20)  NOT NULL DEFAULT 'Raw'
                         COMMENT 'Raw -> Cleaned -> Approved / Rejected',
    `QualityScore`       DECIMAL(4,3) NULL COMMENT '0..1 do người duyệt chấm',
    `IsPiiRedacted`      TINYINT(1)   NOT NULL DEFAULT 0
                         COMMENT 'BẮT BUỘC = 1 trước khi Approved. Story thật hay chứa tên KH, email, tên hệ thống nội bộ',
    `ContentHash`        CHAR(64)     NOT NULL COMMENT 'SHA-256 của Instruction+Input+Output - khử trùng lặp',

    `ReviewStatus`       VARCHAR(20)  NOT NULL DEFAULT 'NotReviewed' COMMENT 'NotReviewed / Approved / Rejected / NeedsFix',
    `ReviewedBy`         CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `ReviewedAt`         DATETIME(6)  NULL,
    `ReviewNote`         VARCHAR(1000) NULL,

    `CreatedAt`          DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`          DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiDatasetSample_Dedup` (`DatasetVersionId`, `ContentHash`) COMMENT 'Chặn mẫu trùng ngay ở tầng DB',
    KEY `IX_AiDatasetSample_Split` (`DatasetVersionId`, `SplitType`, `QualityStatus`),
    KEY `IX_AiDatasetSample_Source` (`SourceType`, `SourceRefId`),
    CONSTRAINT `FK_AiDatasetSample_Version` FOREIGN KEY (`DatasetVersionId`) REFERENCES `AiDatasetVersion`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_AiDatasetSample_Split` CHECK (`SplitType` IN ('Train','Validation','Test')),
    CONSTRAINT `CK_AiDatasetSample_Quality` CHECK (`QualityStatus` IN ('Raw','Cleaned','Approved','Rejected'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Từng mẫu huấn luyện. Export ra JSONL từ bảng này';

CREATE TABLE `AiDataCleaningRule` (
    `Id`          CHAR(36)     NOT NULL,
    `Code`        VARCHAR(80)  NOT NULL,
    `Name`        VARCHAR(200) NOT NULL,
    `RuleType`    VARCHAR(30)  NOT NULL COMMENT 'SchemaValidation / PiiDetection / Deduplication / LengthCheck / LanguageCheck / Heuristic',
    `Severity`    VARCHAR(20)  NOT NULL DEFAULT 'Warning' COMMENT 'Info / Warning / Error (Error = tự động Reject)',
    `Config`      JSON         NULL COMMENT 'Regex, ngưỡng min/max, danh sách từ cấm...',
    `AppliesTo`   VARCHAR(30)  NOT NULL DEFAULT 'All' COMMENT 'Breakdown / Assignment / All',
    `IsActive`    TINYINT(1)   NOT NULL DEFAULT 1,
    `CreatedAt`   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_AiDataCleaningRule_Code` (`Code`),
    CONSTRAINT `CK_AiDataCleaningRule_Severity` CHECK (`Severity` IN ('Info','Warning','Error'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Rule làm sạch dạng cấu hình - không hard-code trong script Python';

CREATE TABLE `AiDataQualityFlag` (
    `Id`         CHAR(36)     NOT NULL,
    `SampleId`   CHAR(36)     NOT NULL,
    `RuleId`     CHAR(36)     NOT NULL,
    `Severity`   VARCHAR(20)  NOT NULL,
    `Message`    VARCHAR(1000) NULL,
    `FieldPath`  VARCHAR(200) NULL COMMENT 'VD: $.subTasks[0].acceptanceCriteria',
    `IsResolved` TINYINT(1)   NOT NULL DEFAULT 0,
    `ResolvedBy` CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `ResolvedAt` DATETIME(6)  NULL,
    `DetectedAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_AiDataQualityFlag_Sample` (`SampleId`, `IsResolved`),
    KEY `IX_AiDataQualityFlag_Rule` (`RuleId`),
    CONSTRAINT `FK_AiDataQualityFlag_Sample` FOREIGN KEY (`SampleId`) REFERENCES `AiDatasetSample`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AiDataQualityFlag_Rule` FOREIGN KEY (`RuleId`) REFERENCES `AiDataCleaningRule`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Kết quả chạy rule làm sạch trên từng mẫu';

CREATE TABLE `AiTrainingRun` (
    `Id`                CHAR(36)     NOT NULL,
    `DatasetVersionId`  CHAR(36)     NOT NULL,
    `Name`              VARCHAR(200) NOT NULL,
    `TaskType`          VARCHAR(30)  NOT NULL,
    `BaseModelCode`     VARCHAR(100) NOT NULL COMMENT 'qwen2.5:3b-instruct',
    `Method`            VARCHAR(30)  NOT NULL DEFAULT 'LoRA' COMMENT 'LoRA / QLoRA / FullFineTune / GBDT (cho assignment)',
    `Hyperparameters`   JSON         NULL COMMENT 'lora_r, lora_alpha, lr, epochs, batch_size, seed',
    `Status`            VARCHAR(20)  NOT NULL DEFAULT 'Queued' COMMENT 'Queued / Running / Completed / Failed / Cancelled',
    `ArtifactPath`      VARCHAR(500) NULL COMMENT 'Đường dẫn adapter / .gguf sau khi convert',
    `LogPath`           VARCHAR(500) NULL,
    `TrainLoss`         DECIMAL(10,6) NULL,
    `ValidationLoss`    DECIMAL(10,6) NULL,
    `DurationSeconds`   INT          NULL,
    `HardwareInfo`      VARCHAR(255) NULL COMMENT 'GPU/CPU dùng để train, để so sánh công bằng',
    `ErrorMessage`      TEXT         NULL,
    `StartedAt`         DATETIME(6)  NULL,
    `FinishedAt`        DATETIME(6)  NULL,
    `CreatedBy`         CHAR(36)     NULL COMMENT 'XMOD -> User.Id',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_AiTrainingRun_Dataset` (`DatasetVersionId`),
    CONSTRAINT `FK_AiTrainingRun_Dataset` FOREIGN KEY (`DatasetVersionId`) REFERENCES `AiDatasetVersion`(`Id`),
    CONSTRAINT `CK_AiTrainingRun_Status` CHECK (`Status` IN ('Queued','Running','Completed','Failed','Cancelled'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Mỗi lần fine-tune - lưu đủ để tái lập thí nghiệm';

CREATE TABLE `AiEvaluationResult` (
    `Id`               CHAR(36)     NOT NULL,
    `TrainingRunId`    CHAR(36)     NULL COMMENT 'NULL nếu đánh giá model gốc / prompt thuần (baseline)',
    `ModelId`          CHAR(36)     NULL,
    `DatasetVersionId` CHAR(36)     NOT NULL,
    `SplitType`        VARCHAR(20)  NOT NULL DEFAULT 'Test',
    `MetricName`       VARCHAR(80)  NOT NULL
                       COMMENT 'Breakdown: json_valid_rate, schema_match_rate, avg_edit_distance, keep_rate, rouge_l. Assignment: precision@1, ndcg@3, mae_workload_gap',
    `MetricValue`      DECIMAL(12,6) NOT NULL,
    `SampleSize`       INT          NULL,
    `Notes`            VARCHAR(500) NULL,
    `EvaluatedAt`      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_AiEvaluationResult_Run` (`TrainingRunId`, `MetricName`),
    KEY `IX_AiEvaluationResult_Model` (`ModelId`, `MetricName`),
    CONSTRAINT `FK_AiEvaluationResult_TrainingRun` FOREIGN KEY (`TrainingRunId`) REFERENCES `AiTrainingRun`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AiEvaluationResult_Model` FOREIGN KEY (`ModelId`) REFERENCES `AiModel`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AiEvaluationResult_Dataset` FOREIGN KEY (`DatasetVersionId`) REFERENCES `AiDatasetVersion`(`Id`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC
  COMMENT='Không có bảng này thì không biết fine-tune có tốt hơn prompt thuần hay không';


-- =====================================================================================
-- MODULE 9: NOTIFICATION
-- Owner: Người A
-- =====================================================================================

CREATE TABLE `Notification` (
    `Id`         CHAR(36)     NOT NULL,
    `UserId`     CHAR(36)     NOT NULL COMMENT 'XMOD -> User.Id (người nhận)',
    `Type`       VARCHAR(50)  NOT NULL
                 COMMENT 'IssueAssigned / NewComment / Mention / SprintEnding / AiBreakdownCompleted / AiBreakdownFailed / AiAssignmentReady',
    `Title`      VARCHAR(255) NOT NULL,
    `Content`    TEXT         NULL,
    `EntityType` VARCHAR(50)  NULL COMMENT 'BỔ SUNG: Issue / Sprint / Project / AiGenerationLog',
    `EntityId`   CHAR(36)     NULL COMMENT 'BỔ SUNG: v2.0 thiếu cột này -> bấm vào thông báo không biết đi đâu',
    `ProjectId`  CHAR(36)     NULL COMMENT 'XMOD: lọc thông báo theo project',
    `ActorId`    CHAR(36)     NULL COMMENT 'XMOD -> User.Id. NULL = do hệ thống/AI',
    `IsRead`     TINYINT(1)   NOT NULL DEFAULT 0,
    `ReadAt`     DATETIME(6)  NULL,
    `CreatedAt`  DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Notification_Inbox` (`UserId`, `IsRead`, `CreatedAt`) COMMENT 'Truy vấn Notification Center + badge đếm',
    KEY `IX_Notification_Entity` (`EntityType`, `EntityId`)
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Thông báo, kết hợp SignalR đẩy realtime';


-- =====================================================================================
-- PERFORMANCE INDEXES / SEARCH INDEXES
-- Nguyên tắc:
--   * KHÔNG tạo INDEX(Id) khi Id đã là PRIMARY KEY GUID.
--   * Vẫn giữ index GUID trong PK/FK/composite index khi cần JOIN/lọc theo Project/Sprint/User.
--   * VARCHAR Name/Title: BTREE phục vụ =, IN, ORDER BY và LIKE 'prefix%'.
--   * TEXT/MEDIUMTEXT: FULLTEXT phục vụ MATCH(...) AGAINST(...), không thay thế LIKE '%...%'.
--   * Không index riêng các cột flag/count có độ phân biệt thấp nếu không đi kèm cột lọc/sort khác.
-- =====================================================================================

-- Identity / Access
ALTER TABLE `User`
    ADD KEY `IX_User_Active_Created` (`IsActive`, `CreatedAt`),
    ADD KEY `IX_User_LastLoginAt` (`LastLoginAt`);

ALTER TABLE `UserProfile`
    ADD KEY `IX_UserProfile_DisplayName` (`DisplayName`),
    ADD KEY `IX_UserProfile_JobTitle` (`JobTitle`),
    ADD FULLTEXT KEY `FT_UserProfile_Search` (`DisplayName`, `JobTitle`, `Bio`);

ALTER TABLE `RefreshToken`
    ADD KEY `IX_RefreshToken_ExpiresAt` (`ExpiresAt`);

ALTER TABLE `SkillCatalog`
    ADD KEY `IX_SkillCatalog_Name` (`Name`),
    ADD KEY `IX_SkillCatalog_Category_Name` (`Category`, `Name`);

-- Organization / Project / Workflow
ALTER TABLE `Organization`
    ADD KEY `IX_Organization_Name` (`Name`);

ALTER TABLE `Project`
    ADD KEY `IX_Project_Name` (`Name`),
    ADD KEY `IX_Project_Org_Visible_Name` (`OrgId`, `IsDeleted`, `IsArchived`, `Name`),
    ADD FULLTEXT KEY `FT_Project_Search` (`Name`, `Description`);

ALTER TABLE `ProjectComponent`
    ADD KEY `IX_ProjectComponent_Name` (`Name`);

ALTER TABLE `ProjectVersion`
    ADD KEY `IX_ProjectVersion_Name` (`Name`),
    ADD KEY `IX_ProjectVersion_Release` (`IsReleased`, `ReleaseDate`);

ALTER TABLE `WorkflowStatus`
    ADD KEY `IX_WorkflowStatus_Name` (`Name`);

ALTER TABLE `WorkflowTransition`
    ADD KEY `IX_WorkflowTransition_Name` (`Name`);

ALTER TABLE `IssueType`
    ADD KEY `IX_IssueType_Name` (`Name`),
    ADD KEY `IX_IssueType_Project_Order` (`ProjectId`, `OrderIndex`);

ALTER TABLE `Priority`
    ADD KEY `IX_Priority_Name` (`Name`);

ALTER TABLE `Board`
    ADD KEY `IX_Board_Name` (`Name`);

ALTER TABLE `BoardColumn`
    ADD KEY `IX_BoardColumn_Name` (`Name`),
    ADD KEY `IX_BoardColumn_Order` (`BoardId`, `OrderIndex`);

-- Sprint / Backlog
ALTER TABLE `Sprint`
    ADD KEY `IX_Sprint_Name` (`Name`),
    ADD KEY `IX_Sprint_Project_Order` (`ProjectId`, `OrderIndex`),
    ADD KEY `IX_Sprint_Status_EndDate` (`Status`, `EndDate`),
    ADD KEY `IX_Sprint_Project_Dates` (`ProjectId`, `StartDate`, `EndDate`);

-- Issue Tracking: bảng trung tâm, có khả năng lớn nhất
ALTER TABLE `Issue`
    ADD KEY `IX_Issue_Project_Recent` (`ProjectId`, `IsDeleted`, `CreatedAt`),
    ADD KEY `IX_Issue_Board_Filter` (`SprintId`, `StatusId`, `IsDeleted`, `RankOrder`),
    ADD KEY `IX_Issue_Assignee_Workload` (`AssigneeId`, `IsDeleted`, `StatusId`, `DueDate`),
    ADD KEY `IX_Issue_Project_Due` (`ProjectId`, `IsDeleted`, `DueDate`),
    ADD KEY `IX_Issue_Reporter_Created` (`ReporterId`, `CreatedAt`),
    ADD FULLTEXT KEY `FT_Issue_Search` (`Title`, `Description`);

ALTER TABLE `Comment`
    ADD KEY `IX_Comment_User_Created` (`UserId`, `CreatedAt`),
    ADD FULLTEXT KEY `FT_Comment_Content` (`Content`);

ALTER TABLE `Attachment`
    ADD KEY `IX_Attachment_FileName` (`FileName`),
    ADD KEY `IX_Attachment_CreatedAt` (`CreatedAt`);

ALTER TABLE `ActivityLog`
    ADD KEY `IX_ActivityLog_User_Created` (`UserId`, `CreatedAt`),
    ADD KEY `IX_ActivityLog_Source_Created` (`Source`, `CreatedAt`),
    ADD KEY `IX_ActivityLog_CreatedAt` (`CreatedAt`);

ALTER TABLE `IssueStatusHistory`
    ADD KEY `IX_IssueStatusHistory_Category_Date` (`ToCategory`, `ChangedAt`),
    ADD KEY `IX_IssueStatusHistory_ChangedAt` (`ChangedAt`);

ALTER TABLE `IssueAssignmentHistory`
    ADD KEY `IX_IssueAssignHistory_AssignedAt` (`AssignedAt`);

ALTER TABLE `Label`
    ADD KEY `IX_Label_Name` (`Name`);

ALTER TABLE `AcceptanceCriteria`
    ADD FULLTEXT KEY `FT_AcceptanceCriteria_Content` (`Content`);

-- AI model / prompt / generation
ALTER TABLE `AiModel`
    ADD KEY `IX_AiModel_DisplayName` (`DisplayName`);

ALTER TABLE `AiGenerationLog`
    ADD KEY `IX_AiGenerationLog_HangfireJobId` (`HangfireJobId`),
    ADD KEY `IX_AiGenerationLog_Error` (`ErrorCode`, `CreatedAt`);

ALTER TABLE `AiSuggestedTask`
    ADD FULLTEXT KEY `FT_AiSuggestedTask_Search`
        (`OriginalSummary`, `OriginalDescription`, `FinalSummary`, `FinalDescription`);

-- AI Assignment
ALTER TABLE `UserWorkloadSnapshot`
    ADD KEY `IX_UserWorkload_Sprint_Date` (`SprintId`, `SnapshotDate`);

ALTER TABLE `UserPerformanceMetric`
    ADD KEY `IX_UserPerfMetric_Project_Period` (`ProjectId`, `PeriodEnd`);

ALTER TABLE `AiAssignmentRun`
    ADD KEY `IX_AiAssignmentRun_Status_Created` (`Status`, `CreatedAt`),
    ADD KEY `IX_AiAssignmentRun_Strategy_Created` (`Strategy`, `CreatedAt`);

ALTER TABLE `AiAssignmentCandidate`
    ADD KEY `IX_AiAssignmentCandidate_Run_Issue_Rank` (`RunId`, `IssueId`, `Rank`);

-- AI Dataset / Training: dữ liệu có thể tăng rất nhanh
ALTER TABLE `AiDataset`
    ADD KEY `IX_AiDataset_Name` (`Name`);

ALTER TABLE `AiDatasetSample`
    ADD KEY `IX_AiDatasetSample_ReviewQueue` (`ReviewStatus`, `QualityStatus`, `CreatedAt`),
    ADD KEY `IX_AiDatasetSample_Version_Review` (`DatasetVersionId`, `ReviewStatus`, `QualityStatus`, `CreatedAt`),
    ADD KEY `IX_AiDatasetSample_TokenCount` (`DatasetVersionId`, `TokenCount`),
    ADD FULLTEXT KEY `FT_AiDatasetSample_Instruction` (`Instruction`);

ALTER TABLE `AiDataCleaningRule`
    ADD KEY `IX_AiDataCleaningRule_Name` (`Name`);

ALTER TABLE `AiDataQualityFlag`
    ADD KEY `IX_AiDataQualityFlag_Queue` (`IsResolved`, `Severity`, `DetectedAt`);

ALTER TABLE `AiTrainingRun`
    ADD KEY `IX_AiTrainingRun_Name` (`Name`),
    ADD KEY `IX_AiTrainingRun_Status_Created` (`Status`, `CreatedAt`),
    ADD KEY `IX_AiTrainingRun_Task_Status` (`TaskType`, `Status`, `CreatedAt`);

ALTER TABLE `AiEvaluationResult`
    ADD KEY `IX_AiEvaluationResult_Dataset_Metric` (`DatasetVersionId`, `SplitType`, `MetricName`);

-- Notification
ALTER TABLE `Notification`
    ADD KEY `IX_Notification_Project_Created` (`ProjectId`, `CreatedAt`),
    ADD KEY `IX_Notification_Type_Created` (`Type`, `CreatedAt`),
    ADD FULLTEXT KEY `FT_Notification_Search` (`Title`, `Content`);

-- =====================================================================================
-- VIEW HỖ TRỢ (chỉ dùng cho REPORTING & trích xuất feature AI)
-- Các view này JOIN xuyên module — đây là ngoại lệ CÓ CHỦ ĐÍCH, chỉ đọc, không dùng
-- trong logic nghiệp vụ của module. Nếu sau này tách microservice, thay bằng
-- read-model riêng hoặc gọi qua Contracts.
-- =====================================================================================

CREATE OR REPLACE VIEW `vw_UserActiveWorkload` AS
SELECT
    i.`ProjectId`,
    i.`SprintId`,
    i.`AssigneeId`                                              AS `UserId`,
    COUNT(*)                                                    AS `OpenIssueCount`,
    SUM(CASE WHEN ws.`Category` = 'InProgress' THEN 1 ELSE 0 END) AS `InProgressCount`,
    COALESCE(SUM(i.`StoryPoints`), 0)                           AS `OpenPoints`,
    COALESCE(SUM(CASE WHEN ws.`Category` = 'InProgress' THEN i.`StoryPoints` ELSE 0 END), 0) AS `InProgressPoints`,
    SUM(CASE WHEN i.`DueDate` IS NOT NULL AND i.`DueDate` < CURDATE() THEN 1 ELSE 0 END) AS `OverdueCount`
FROM `Issue` i
JOIN `WorkflowStatus` ws ON ws.`Id` = i.`StatusId`
WHERE i.`IsDeleted` = 0
  AND i.`AssigneeId` IS NOT NULL
  AND ws.`Category` <> 'Done'
GROUP BY i.`ProjectId`, i.`SprintId`, i.`AssigneeId`;

CREATE OR REPLACE VIEW `vw_SprintVelocity` AS
SELECT
    s.`ProjectId`,
    s.`Id`                            AS `SprintId`,
    s.`Name`                          AS `SprintName`,
    s.`StartDate`,
    s.`EndDate`,
    COALESCE(SUM(CASE WHEN ws.`Category` = 'Done' THEN i.`StoryPoints` ELSE 0 END), 0) AS `CompletedPoints`,
    COALESCE(SUM(i.`StoryPoints`), 0) AS `CommittedPoints`,
    COUNT(i.`Id`)                     AS `IssueCount`
FROM `Sprint` s
LEFT JOIN `Issue` i          ON i.`SprintId` = s.`Id` AND i.`IsDeleted` = 0
LEFT JOIN `WorkflowStatus` ws ON ws.`Id` = i.`StatusId`
WHERE s.`Status` = 'Completed'
GROUP BY s.`ProjectId`, s.`Id`, s.`Name`, s.`StartDate`, s.`EndDate`;

CREATE OR REPLACE VIEW `vw_AiBreakdownQuality` AS
SELECT
    l.`ProjectId`,
    l.`ModelId`,
    l.`PromptTemplateId`,
    DATE(l.`CreatedAt`)                                          AS `Day`,
    COUNT(DISTINCT l.`Id`)                                       AS `RequestCount`,
    COUNT(t.`Id`)                                                AS `SuggestedTaskCount`,
    SUM(CASE WHEN t.`UserAction` = 'Kept'     THEN 1 ELSE 0 END) AS `KeptCount`,
    SUM(CASE WHEN t.`UserAction` = 'Edited'   THEN 1 ELSE 0 END) AS `EditedCount`,
    SUM(CASE WHEN t.`UserAction` = 'Rejected' THEN 1 ELSE 0 END) AS `RejectedCount`,
    AVG(t.`EditDistanceRatio`)                                   AS `AvgEditDistance`,
    AVG(l.`LatencyMs`)                                           AS `AvgLatencyMs`
FROM `AiGenerationLog` l
LEFT JOIN `AiSuggestedTask` t ON t.`AiGenerationLogId` = l.`Id`
WHERE l.`Status` = 'Completed'
GROUP BY l.`ProjectId`, l.`ModelId`, l.`PromptTemplateId`, DATE(l.`CreatedAt`);

CREATE OR REPLACE VIEW `vw_AiAssignmentAccuracy` AS
SELECT
    r.`ProjectId`,
    r.`Strategy`,
    DATE(r.`CreatedAt`)                                             AS `Day`,
    COUNT(d.`Id`)                                                   AS `DecisionCount`,
    SUM(CASE WHEN d.`Outcome` = 'Accepted'   THEN 1 ELSE 0 END)     AS `AcceptedCount`,
    SUM(CASE WHEN d.`Outcome` = 'Overridden' THEN 1 ELSE 0 END)     AS `OverriddenCount`,
    SUM(CASE WHEN d.`WasReassignedLater` = 1 THEN 1 ELSE 0 END)     AS `ReassignedLaterCount`,
    AVG(d.`ActualCycleTimeHours`)                                   AS `AvgCycleTimeHours`
FROM `AiAssignmentRun` r
JOIN `AiAssignmentDecision` d ON d.`RunId` = r.`Id`
GROUP BY r.`ProjectId`, r.`Strategy`, DATE(r.`CreatedAt`);


-- =====================================================================================
-- SEED DATA
-- =====================================================================================

INSERT INTO `Role` (`Id`, `Name`, `Scope`, `IsSystem`, `Description`) VALUES
('11111111-0000-0000-0000-000000000001', 'Admin',           'System',       1, 'Quản trị toàn hệ thống'),
('11111111-0000-0000-0000-000000000002', 'OrgOwner',        'Organization', 1, 'Chủ sở hữu tổ chức'),
('11111111-0000-0000-0000-000000000003', 'ProjectManager',  'Project',      1, 'Quản lý dự án'),
('11111111-0000-0000-0000-000000000004', 'ScrumMaster',     'Project',      1, 'Điều phối quy trình Scrum'),
('11111111-0000-0000-0000-000000000005', 'ProductOwner',    'Project',      1, 'Chủ sở hữu sản phẩm, quản lý Backlog'),
('11111111-0000-0000-0000-000000000006', 'Developer',       'Project',      1, 'Thành viên phát triển'),
('11111111-0000-0000-0000-000000000007', 'Viewer',          'Project',      1, 'Chỉ xem');

INSERT INTO `Permission` (`Id`, `Code`, `Grouping`, `Description`) VALUES
('22222222-0000-0000-0000-000000000001', 'project.create',          'Project',  'Tạo dự án'),
('22222222-0000-0000-0000-000000000002', 'project.update',          'Project',  'Sửa thông tin dự án'),
('22222222-0000-0000-0000-000000000003', 'project.delete',          'Project',  'Xóa dự án'),
('22222222-0000-0000-0000-000000000004', 'project.settings.manage', 'Project',  'Cấu hình workflow, issue type, board'),
('22222222-0000-0000-0000-000000000005', 'member.invite',           'Member',   'Mời thành viên'),
('22222222-0000-0000-0000-000000000006', 'member.role.assign',      'Member',   'Gán vai trò'),
('22222222-0000-0000-0000-000000000007', 'issue.create',            'Issue',    'Tạo issue'),
('22222222-0000-0000-0000-000000000008', 'issue.update',            'Issue',    'Sửa issue'),
('22222222-0000-0000-0000-000000000009', 'issue.delete',            'Issue',    'Xóa issue'),
('22222222-0000-0000-0000-000000000010', 'issue.transition',        'Issue',    'Chuyển trạng thái issue'),
('22222222-0000-0000-0000-000000000011', 'issue.assign',            'Issue',    'Gán người xử lý'),
('22222222-0000-0000-0000-000000000012', 'backlog.rank',            'Backlog',  'Sắp xếp thứ tự backlog'),
('22222222-0000-0000-0000-000000000013', 'sprint.create',           'Sprint',   'Tạo sprint'),
('22222222-0000-0000-0000-000000000014', 'sprint.start',            'Sprint',   'Bắt đầu sprint'),
('22222222-0000-0000-0000-000000000015', 'sprint.close',            'Sprint',   'Kết thúc sprint'),
('22222222-0000-0000-0000-000000000016', 'report.view',             'Report',   'Xem báo cáo'),
('22222222-0000-0000-0000-000000000017', 'ai.breakdown.request',    'AI',       'Yêu cầu AI chia nhỏ task'),
('22222222-0000-0000-0000-000000000018', 'ai.breakdown.apply',      'AI',       'Áp dụng gợi ý AI thành issue thật'),
('22222222-0000-0000-0000-000000000019', 'ai.assignment.run',       'AI',       'Chạy AI phân phối task'),
('22222222-0000-0000-0000-000000000020', 'ai.assignment.apply',     'AI',       'Áp dụng kết quả phân phối'),
('22222222-0000-0000-0000-000000000021', 'ai.dataset.manage',       'AI',       'Quản lý dataset & training'),
('22222222-0000-0000-0000-000000000022', 'ai.model.manage',         'AI',       'Đăng ký/kích hoạt model');

-- Admin: toàn quyền
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000001', `Id` FROM `Permission`;

-- ProjectManager: toàn quyền trừ quản lý model/dataset
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000003', `Id` FROM `Permission`
WHERE `Code` NOT IN ('project.delete','ai.dataset.manage','ai.model.manage');

-- ScrumMaster
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000004', `Id` FROM `Permission`
WHERE `Code` IN ('issue.create','issue.update','issue.transition','issue.assign','backlog.rank',
                 'sprint.create','sprint.start','sprint.close','report.view',
                 'ai.breakdown.request','ai.breakdown.apply','ai.assignment.run','ai.assignment.apply');

-- ProductOwner
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000005', `Id` FROM `Permission`
WHERE `Code` IN ('issue.create','issue.update','backlog.rank','report.view',
                 'ai.breakdown.request','ai.breakdown.apply');

-- Developer
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000006', `Id` FROM `Permission`
WHERE `Code` IN ('issue.create','issue.update','issue.transition','report.view','ai.breakdown.request');

-- Viewer
INSERT INTO `RolePermission` (`RoleId`, `PermissionId`)
SELECT '11111111-0000-0000-0000-000000000007', `Id` FROM `Permission`
WHERE `Code` IN ('report.view');

-- Priority dùng chung (ProjectId = NULL)
INSERT INTO `Priority` (`Id`, `ProjectId`, `Name`, `Level`, `ColorHex`) VALUES
('33333333-0000-0000-0000-000000000001', NULL, 'Highest', 1, '#CD1317'),
('33333333-0000-0000-0000-000000000002', NULL, 'High',    2, '#E9494B'),
('33333333-0000-0000-0000-000000000003', NULL, 'Medium',  3, '#E97F33'),
('33333333-0000-0000-0000-000000000004', NULL, 'Low',     4, '#2A8735'),
('33333333-0000-0000-0000-000000000005', NULL, 'Lowest',  5, '#57A55A');

INSERT INTO `SkillCatalog` (`Id`, `Code`, `Name`, `Category`) VALUES
('44444444-0000-0000-0000-000000000001', 'dotnet',     '.NET / C#',            'Backend'),
('44444444-0000-0000-0000-000000000002', 'angular',    'Angular / TypeScript', 'Frontend'),
('44444444-0000-0000-0000-000000000003', 'sql',        'SQL / Database',       'Database'),
('44444444-0000-0000-0000-000000000004', 'devops',     'CI/CD / Docker',       'DevOps'),
('44444444-0000-0000-0000-000000000005', 'testing',    'Kiểm thử / QA',        'QA'),
('44444444-0000-0000-0000-000000000006', 'uiux',       'UI/UX Design',         'Design'),
('44444444-0000-0000-0000-000000000007', 'ai-ml',      'AI / Machine Learning','AI'),
('44444444-0000-0000-0000-000000000008', 'signalr',    'Realtime / SignalR',   'Backend'),
('44444444-0000-0000-0000-000000000009', 'security',   'Bảo mật / Xác thực',   'Backend');

-- Model gốc dùng ở Giai đoạn 1
INSERT INTO `AiModel` (`Id`, `Code`, `DisplayName`, `Provider`, `TaskType`, `IsActive`, `ContextWindow`, `DefaultParams`) VALUES
('55555555-0000-0000-0000-000000000001', 'qwen2.5:3b-instruct', 'Qwen2.5 3B Instruct', 'Ollama', 'Breakdown', 1, 32768,
 JSON_OBJECT('temperature', 0.2, 'top_p', 0.9, 'num_predict', 2048, 'seed', 42)),
('55555555-0000-0000-0000-000000000002', 'rule:weighted-score-v1', 'Scoring function có trọng số (baseline)', 'Local', 'Assignment', 1, NULL,
 JSON_OBJECT('load', 0.40, 'skill', 0.30, 'history', 0.20, 'capacity', 0.10));

-- Prompt v1 KÈM JSON SCHEMA. Truyền JsonSchema vào tham số `format` của Ollama
-- => model bị ép sinh đúng cấu trúc ở mức decoder, không cần fine-tune để sửa format.
INSERT INTO `AiPromptTemplate`
    (`Id`, `Code`, `Version`, `TaskType`, `Language`, `IsActive`, `SystemPrompt`, `UserTemplate`, `JsonSchema`)
VALUES
('66666666-0000-0000-0000-000000000001', 'breakdown.system', 1, 'Breakdown', 'vi', 1,
 'Bạn là Scrum Master kiêm Technical Lead giàu kinh nghiệm. Phân tích User Story được cung cấp thành các Sub-task KỸ THUẬT có thể thực thi được.
QUY TẮC:
- Sinh tối đa 8 sub-task. Chỉ sinh những task THỰC SỰ cần thiết để hoàn thành story, không thêm task chung chung như "họp", "nghiên cứu", "kiểm tra lại".
- Mỗi sub-task phải hoàn thành được trong 1 ngày làm việc.
- Mỗi sub-task có 2 đến 5 tiêu chí nghiệm thu, viết cụ thể, kiểm chứng được (có số liệu, mã lỗi HTTP, điều kiện rõ ràng).
- Ước lượng story point theo thang Fibonacci: 1, 2, 3, 5, 8.
- Trả lời bằng cùng ngôn ngữ với User Story.
- CHỈ trả về JSON đúng schema, không thêm bất kỳ lời giải thích nào.',
 'User Story: {{Title}}

Mô tả: {{Description}}

Loại issue có sẵn trong dự án: {{IssueTypes}}
Component liên quan: {{Components}}
Ghi chú bổ sung từ người dùng: {{InputText}}',
 CAST('{
   "type": "object",
   "required": ["subTasks"],
   "additionalProperties": false,
   "properties": {
     "subTasks": {
       "type": "array", "minItems": 1, "maxItems": 8,
       "items": {
         "type": "object",
         "required": ["summary", "description", "acceptanceCriteria", "estimatePoints"],
         "additionalProperties": false,
         "properties": {
           "summary": { "type": "string", "minLength": 5, "maxLength": 200 },
           "description": { "type": "string", "minLength": 10 },
           "acceptanceCriteria": {
             "type": "array", "minItems": 2, "maxItems": 5,
             "items": { "type": "string", "minLength": 10 }
           },
           "estimatePoints": { "type": "number", "enum": [1, 2, 3, 5, 8] },
           "suggestedSkills": { "type": "array", "items": { "type": "string" } }
         }
       }
     }
   }
 }' AS JSON));

INSERT INTO `AiDataCleaningRule` (`Id`, `Code`, `Name`, `RuleType`, `Severity`, `AppliesTo`, `Config`) VALUES
('77777777-0000-0000-0000-000000000001', 'schema.valid',    'Output phải khớp JSON Schema',            'SchemaValidation', 'Error',   'Breakdown',
 JSON_OBJECT('schemaRef', 'breakdown.system.v1')),
('77777777-0000-0000-0000-000000000002', 'pii.email',       'Phát hiện địa chỉ email',                 'PiiDetection',     'Error',   'All',
 JSON_OBJECT('pattern', '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}', 'action', 'redact')),
('77777777-0000-0000-0000-000000000003', 'pii.phone.vn',    'Phát hiện số điện thoại Việt Nam',        'PiiDetection',     'Error',   'All',
 JSON_OBJECT('pattern', '(0|\\+84)[0-9]{9,10}', 'action', 'redact')),
('77777777-0000-0000-0000-000000000004', 'pii.secret',      'Phát hiện API key / token / mật khẩu',    'PiiDetection',     'Error',   'All',
 JSON_OBJECT('keywords', JSON_ARRAY('api_key','apikey','password','secret','token','connectionstring','bearer'))),
('77777777-0000-0000-0000-000000000005', 'dedup.hash',      'Loại bỏ mẫu trùng theo ContentHash',      'Deduplication',    'Error',   'All',
 JSON_OBJECT('hashField', 'ContentHash')),
('77777777-0000-0000-0000-000000000006', 'length.min',      'Story quá ngắn, không đủ ngữ cảnh',       'LengthCheck',      'Warning', 'Breakdown',
 JSON_OBJECT('minInputChars', 40, 'minOutputChars', 120)),
('77777777-0000-0000-0000-000000000007', 'length.token.max','Vượt quá context window của model',       'LengthCheck',      'Error',   'All',
 JSON_OBJECT('maxTokens', 8192)),
('77777777-0000-0000-0000-000000000008', 'lang.consistent', 'Ngôn ngữ input và output phải khớp',      'LanguageCheck',    'Warning', 'Breakdown',
 JSON_OBJECT('allowed', JSON_ARRAY('vi','en'))),
('77777777-0000-0000-0000-000000000009', 'quality.rejected','Mẫu bị người dùng Rejected - không dùng làm nhãn dương', 'Heuristic', 'Warning', 'Breakdown',
 JSON_OBJECT('excludeUserAction', JSON_ARRAY('Rejected'))),
('77777777-0000-0000-0000-000000000010','quality.generic', 'Sub-task chung chung, không có giá trị',   'Heuristic',        'Warning', 'Breakdown',
 JSON_OBJECT('banPhrases', JSON_ARRAY('nghiên cứu','tìm hiểu','họp bàn','kiểm tra lại','làm rõ yêu cầu','trao đổi với team')));

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================================
-- CHECKLIST TRIỂN KHAI (đọc trước khi code)
--
-- [ ] my.cnf: lower_case_table_names = 1  (Linux, set TRƯỚC khi init data dir)
-- [ ] my.cnf: innodb_buffer_pool_size >= 1G, max_allowed_packet >= 64M (do có cột JSON/MEDIUMTEXT)
-- [ ] NuGet: Pomelo.EntityFrameworkCore.MySql (KHÔNG dùng MySql.EntityFrameworkCore)
-- [ ] Mỗi module cấu hình MigrationsHistoryTable riêng để 3 người không conflict migration
-- [ ] Cột `ActiveGuard` và `ScopeKey` là GENERATED — đánh dấu
--     .ValueGeneratedOnAddOrUpdate().Metadata.SetAfterSaveBehavior(Save.Ignore) trong EF,
--     nếu không EF sẽ cố INSERT vào và MySQL sẽ báo lỗi.
-- [ ] Hangfire tự tạo bảng riêng (prefix HangFire_) — không cần khai báo ở đây.
-- [ ] Job hằng ngày cần chạy: SprintSnapshot, UserWorkloadSnapshot, UserPerformanceMetric,
--     rebalance RankOrder, dọn OtpCode/RefreshToken hết hạn.
-- [ ] Job sau khi issue Done: cập nhật AiAssignmentDecision.ActualCycleTimeHours /
--     WasCompletedOnTime / WasReassignedLater.
--
-- THỨ TỰ LÀM VIỆC ĐỀ XUẤT CHO 3 NGƯỜI (tránh block nhau):
--   Tuần 1-2: cả 3 cùng dựng Phase 0 + Người A làm xong Identity (2 người kia bị chặn nếu thiếu auth)
--   Song song: Người C dựng sẵn schema JSON + 50-100 mẫu eval THỦ CÔNG.
--              Đây là việc duy nhất của module AI làm được ngay từ ngày đầu,
--              và nó là thứ quyết định có đo được model tốt/xấu hay không.
--   Người B làm Project/Workflow/Sprint song song ngay sau khi Identity xong.
--   Module AI chỉ bật sau khi Issue Tracking chạy được (cần IIssueService.CreateIssue).
-- =====================================================================================

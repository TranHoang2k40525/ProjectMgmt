using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityExperience.Infrastructure.Migrations;

[DbContext(typeof(IdentityExperienceDbContext))]
[Migration("20260930090000_ExtendIdentityPermissions")]
public partial class ExtendIdentityPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO `Permission` (`Id`, `Code`, `Grouping`, `Description`) VALUES
            ('22222222-0000-0000-0000-000000000023', 'project.read',          'Project',  'Xem dự án và cấu hình cơ bản'),
            ('22222222-0000-0000-0000-000000000024', 'member.read',           'Member',   'Xem thành viên và vai trò dự án'),
            ('22222222-0000-0000-0000-000000000025', 'member.remove',         'Member',   'Gỡ thành viên khỏi dự án'),
            ('22222222-0000-0000-0000-000000000026', 'identity.role.manage',  'Identity', 'Quản lý vai trò và ma trận quyền'),
            ('22222222-0000-0000-0000-000000000027', 'skill.catalog.manage',  'Identity', 'Quản lý danh mục kỹ năng toàn hệ thống'),
            ('22222222-0000-0000-0000-000000000028', 'skill.verify',          'Identity', 'Xác nhận kỹ năng của thành viên'),
            ('22222222-0000-0000-0000-000000000029', 'ai.prompt.manage',      'AI',       'Quản lý và kích hoạt mẫu prompt')
            ON DUPLICATE KEY UPDATE
                `Grouping` = VALUES(`Grouping`),
                `Description` = VALUES(`Description`);
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000001', `Id`
            FROM `Permission`;
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000002', `Id`
            FROM `Permission`
            WHERE `Code` NOT IN ('ai.dataset.manage', 'ai.model.manage', 'skill.catalog.manage');
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000003', `Id`
            FROM `Permission`
            WHERE `Code` IN ('project.read', 'member.read', 'member.remove', 'skill.verify', 'ai.prompt.manage');
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000004', `Id`
            FROM `Permission`
            WHERE `Code` IN ('project.read', 'member.read', 'skill.verify');
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT '11111111-0000-0000-0000-000000000005', `Id`
            FROM `Permission`
            WHERE `Code` IN ('project.read', 'member.read');
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `RolePermission` (`RoleId`, `PermissionId`)
            SELECT `Role`.`Id`, `Permission`.`Id`
            FROM `Role`
            CROSS JOIN `Permission`
            WHERE `Role`.`Name` IN ('Developer', 'Viewer')
              AND `Permission`.`Code` IN ('project.read', 'member.read');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM `RolePermission`
            WHERE `RoleId` = '11111111-0000-0000-0000-000000000002';
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `RolePermission`
            WHERE `PermissionId` IN (
                '22222222-0000-0000-0000-000000000023',
                '22222222-0000-0000-0000-000000000024',
                '22222222-0000-0000-0000-000000000025',
                '22222222-0000-0000-0000-000000000026',
                '22222222-0000-0000-0000-000000000027',
                '22222222-0000-0000-0000-000000000028',
                '22222222-0000-0000-0000-000000000029');
            """);

        migrationBuilder.Sql(
            """
            DELETE FROM `Permission`
            WHERE `Id` IN (
                '22222222-0000-0000-0000-000000000023',
                '22222222-0000-0000-0000-000000000024',
                '22222222-0000-0000-0000-000000000025',
                '22222222-0000-0000-0000-000000000026',
                '22222222-0000-0000-0000-000000000027',
                '22222222-0000-0000-0000-000000000028',
                '22222222-0000-0000-0000-000000000029');
            """);
    }
}

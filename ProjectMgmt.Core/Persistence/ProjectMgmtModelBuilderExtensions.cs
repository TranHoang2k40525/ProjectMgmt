using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ProjectMgmt.BuildingBlocks.Persistence;

public static class ProjectMgmtModelBuilderExtensions
{
    public static ModelBuilder ApplyProjectMgmtColumnConventions(this ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

            if (type == typeof(Guid))
            {
                property.SetColumnType("char(36)");
            }
            else if (type == typeof(DateTime))
            {
                property.SetColumnType("datetime(6)");
            }
            else if (type == typeof(DateOnly))
            {
                property.SetColumnType("date");
            }
        }

        return modelBuilder;
    }

    public static IMutableProperty UseCurrentTimestamp(this IMutableProperty property)
    {
        property.SetDefaultValueSql("CURRENT_TIMESTAMP(6)");
        return property;
    }
}

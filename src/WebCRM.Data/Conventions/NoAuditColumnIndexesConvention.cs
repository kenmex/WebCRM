using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using WebCRM.Core.Entities;

namespace WebCRM.Data.Conventions;

/// <summary>
/// EF's foreign-key index convention, except that CreatedBy/UpdatedBy get no index.
/// They keep their FKs to AspNetUsers, but nothing queries by them and users are never
/// deleted, so an index would only be write cost.
/// </summary>
/// <remarks>
/// Replaces <see cref="ForeignKeyIndexConvention"/> instead of removing the indexes afterwards:
/// that convention re-creates an FK index whenever one is removed.
/// </remarks>
public sealed class NoAuditColumnIndexesConvention(ProviderConventionSetBuilderDependencies dependencies)
    : ForeignKeyIndexConvention(dependencies)
{
    private static readonly string[] AuditColumns =
        [nameof(ICreationAudited.CreatedBy), nameof(IModificationAudited.UpdatedBy)];

    protected override IConventionIndex? CreateIndex(
        IReadOnlyList<IConventionProperty> properties,
        bool unique,
        IConventionEntityTypeBuilder entityTypeBuilder)
    {
        if (properties is [var property] && AuditColumns.Contains(property.Name))
        {
            return null;
        }

        return base.CreateIndex(properties, unique, entityTypeBuilder);
    }
}

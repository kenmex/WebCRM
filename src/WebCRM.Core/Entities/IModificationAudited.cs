namespace WebCRM.Core.Entities;

/// <summary>Entities with UpdatedAt / UpdatedBy, filled by the SaveChanges interceptor.</summary>
public interface IModificationAudited
{
    /// <summary>UTC.</summary>
    DateTime? UpdatedAt { get; set; }

    /// <summary>User id (AspNetUsers.Id).</summary>
    string? UpdatedBy { get; set; }
}

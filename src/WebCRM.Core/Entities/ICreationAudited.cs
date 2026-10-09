namespace WebCRM.Core.Entities;

/// <summary>Entities with CreatedAt / CreatedBy, filled by the SaveChanges interceptor.</summary>
public interface ICreationAudited
{
    /// <summary>UTC.</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>User id (AspNetUsers.Id).</summary>
    string CreatedBy { get; set; }
}

namespace WebCRM.Core.Entities;

public class Team : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>User id of a user with role Manager.</summary>
    public string? ManagerId { get; set; }
}

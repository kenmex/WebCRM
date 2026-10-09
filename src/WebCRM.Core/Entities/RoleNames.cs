namespace WebCRM.Core.Entities;

/// <summary>Names of the Identity roles.</summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Sales = "Sales";

    public static readonly string[] All = [Admin, Manager, Sales];
}

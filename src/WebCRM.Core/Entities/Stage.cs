namespace WebCRM.Core.Entities;

/// <summary>Pipeline stage (lookup). Exactly one Won and one Lost stage.</summary>
public class Stage
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Board column order.</summary>
    public int SortOrder { get; set; }

    /// <summary>0 to 100.</summary>
    public decimal DefaultProbability { get; set; }

    public bool IsWon { get; set; }

    public bool IsLost { get; set; }

    public bool IsActive { get; set; } = true;
}

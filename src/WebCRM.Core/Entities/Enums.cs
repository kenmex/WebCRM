namespace WebCRM.Core.Entities;

// Fixed code sets stored as tinyint (not lookup tables); values match docs/data-dictionary.md.

public enum AddressType : byte
{
    Billing = 1,
    Shipping = 2,
}

public enum ApiTokenScope : byte
{
    Read = 1,
    ReadWrite = 2,
}

public enum ImportStatus : byte
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RolledBack = 4,
}

public enum DuplicateRule : byte
{
    Skip = 0,
    UpdateExisting = 1,
}

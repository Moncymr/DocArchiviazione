namespace DocN.Data.Constants;

/// <summary>
/// Constants for document connector types
/// </summary>
public static class ConnectorTypes
{
    public const string SharePoint = "SharePoint";
    public const string OneDrive = "OneDrive";
    public const string GoogleDrive = "GoogleDrive";
    public const string LocalFolder = "LocalFolder";
    public const string FTP = "FTP";
    public const string SFTP = "SFTP";
}

/// <summary>
/// Constants for ingestion schedule types
/// </summary>
public static class ScheduleTypes
{
    public const string Manual = "Manual";
    public const string Scheduled = "Scheduled";
    public const string Continuous = "Continuous";
}

/// <summary>
/// Constants for ingestion execution status
/// </summary>
public static class IngestionStatus
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

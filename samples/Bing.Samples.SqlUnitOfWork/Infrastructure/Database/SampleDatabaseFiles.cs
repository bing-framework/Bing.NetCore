namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

/// <summary>
/// 管理当前宿主唯一拥有的临时 SQLite 目录。
/// </summary>
public sealed class SampleDatabaseFiles : IDisposable
{
    public SampleDatabaseFiles(string directoryPath)
    {
        DirectoryPath = directoryPath;
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }
    public string OrdersPath => Path.Combine(DirectoryPath, "orders.db");
    public string ReportingPath => Path.Combine(DirectoryPath, "reporting.db");
    public string OrdersConnectionString => $"Data Source={OrdersPath};Pooling=False";
    public string ReportingConnectionString => $"Data Source={ReportingPath};Pooling=False";

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, recursive: true);
    }
}

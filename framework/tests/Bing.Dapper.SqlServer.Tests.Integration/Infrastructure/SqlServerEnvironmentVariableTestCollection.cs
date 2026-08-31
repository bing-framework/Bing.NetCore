namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 集成测试中修改进程环境变量的测试集合定义。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerEnvironmentVariableTestCollection
{
    /// <summary>
    /// 环境变量测试集合名称。
    /// </summary>
    public const string Name = "SqlServerEnvironmentVariableTests";
}
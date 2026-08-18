namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// 1～10 表 Lambda 集成测试的公共列模型。
/// </summary>
public abstract class SqliteAritySampleBase
{
    /// <summary>
    /// 标识。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>一表测试来源。</summary>
public sealed class SqliteArity01 : SqliteAritySampleBase { }

/// <summary>二表测试来源。</summary>
public sealed class SqliteArity02 : SqliteAritySampleBase { }

/// <summary>三表测试来源。</summary>
public sealed class SqliteArity03 : SqliteAritySampleBase { }

/// <summary>四表测试来源。</summary>
public sealed class SqliteArity04 : SqliteAritySampleBase { }

/// <summary>五表测试来源。</summary>
public sealed class SqliteArity05 : SqliteAritySampleBase { }

/// <summary>六表测试来源。</summary>
public sealed class SqliteArity06 : SqliteAritySampleBase { }

/// <summary>七表测试来源。</summary>
public sealed class SqliteArity07 : SqliteAritySampleBase { }

/// <summary>八表测试来源。</summary>
public sealed class SqliteArity08 : SqliteAritySampleBase { }

/// <summary>九表测试来源。</summary>
public sealed class SqliteArity09 : SqliteAritySampleBase { }

/// <summary>十表测试来源。</summary>
public sealed class SqliteArity10 : SqliteAritySampleBase { }

/// <summary>
/// 1～10 表 Lambda 查询的统一结果模型。
/// </summary>
public sealed class SqliteArityResult
{
    /// <summary>
    /// 标识。
    /// </summary>
    public int Id { get; set; }
}

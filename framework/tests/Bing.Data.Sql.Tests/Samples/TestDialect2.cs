using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试方言2
/// </summary>
public class TestDialect2 : DialectBase
{
    /// <summary>
    /// 封闭构造方法
    /// </summary>
    private TestDialect2() { }

    /// <summary>
    /// 测试方言实例
    /// </summary>
    public static IDialect Instance => new TestDialect2();

    /// <inheritdoc />
    public override char OpeningIdentifier => '$';

    /// <inheritdoc />
    public override char ClosingIdentifier => '&';

    /// <inheritdoc />
    public override string GetPrefix() => "*";
}

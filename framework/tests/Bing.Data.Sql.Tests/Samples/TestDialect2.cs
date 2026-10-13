using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试方言2
/// </summary>
public class TestDialect2 : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="TestDialect2"/> 类型的实例。
    /// </summary>
    private TestDialect2() { }

    /// <summary>
    /// 获取测试方言实例
    /// </summary>
    public static IDialect Instance => new TestDialect2();

    /// <inheritdoc />
    public override char OpeningIdentifier => '$';

    /// <inheritdoc />
    public override char ClosingIdentifier => '&';

    /// <inheritdoc />
    public override string GetPrefix() => "*";
}

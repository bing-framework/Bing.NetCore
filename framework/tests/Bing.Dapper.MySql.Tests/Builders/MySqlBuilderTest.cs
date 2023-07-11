using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySql Sql生成器测试
/// </summary>
public partial class MySqlBuilderTest
{
    /// <summary>
    /// MySql Sql生成器
    /// </summary>
    private MySqlBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public MySqlBuilderTest()
    {
        _builder = new MySqlBuilder();
    }
}

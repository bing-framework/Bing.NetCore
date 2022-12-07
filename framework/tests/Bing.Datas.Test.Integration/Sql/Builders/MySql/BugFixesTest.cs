using System;
using Bing.Datas.Dapper.MySql;
using Bing.Data.Sql;
using Bing.Data.Test.Integration.Samples.Bugs;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.MySql;

public class BugFixesTest
{
    /// <summary>
    /// 输出
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Sql生成器
    /// </summary>
    private readonly ISqlBuilder _builder;

    public BugFixesTest(ITestOutputHelper output)
    {
        _output = output;
        _builder = new MySqlBuilder();
    }

    [Fact]
    public void Issue1()
    {
        var id = Guid.Empty;
        _builder.Select<Issue1>(x => x.EShopCode)
            .From<Issue1>("a")
            .Where<Issue1>(x => x.WarehouseId == id)
            .Where<Issue1>(x => x.Enabled);
        _output.WriteLine(_builder.ToDebugSql());
    }
}
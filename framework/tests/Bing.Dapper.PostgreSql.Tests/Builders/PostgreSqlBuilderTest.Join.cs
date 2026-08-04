using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// PostgreSql生成器测试 - Join 与 On 子句
/// </summary>
public partial class PostgreSqlBuilderTest
{
    /// <summary>
    /// 测试 - 三类 Join 的值 On 条件应使用 PostgreSql 标识符引用并按调用顺序生成参数。
    /// </summary>
    [Fact]
    public void Join_WhenInnerLeftRightAndValueOnConfigured_ShouldRenderQuotedSqlAndParameters()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nJoin \"audit\".\"roles\" As \"r\" On \"r\".\"Rank\">@_p_0 \r\nLeft Join \"audit\".\"permissions\" As \"p\" On \"p\".\"Enabled\"=@_p_1 \r\nRight Join \"audit\".\"tenants\" As \"t\" On \"t\".\"Code\"=@_p_2";

        // Act
        var sql = _builder.Select("u.Id").From("public.users", "u")
            .Join("audit.roles", "r").On("r.Rank", 7, Operator.Greater)
            .LeftJoin("audit.permissions", "p").On("p.Enabled", true)
            .RightJoin("audit.tenants", "t").On("t.Code", "tenant-a").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(3, _builder.GetParams().Count);
        Assert.Equal(7, _builder.GetParam("@_p_0"));
        Assert.True((bool)_builder.GetParam("@_p_1"));
        Assert.Equal("tenant-a", _builder.GetParam("@_p_2"));
    }

    /// <summary>
    /// 测试 - AppendOn 应绑定到最后一个原始 Join，且原始文本不应被方言处理。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenMultipleRawJoinsExist_ShouldBindAppendOnToLatestJoin()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nJoin Lateral (Select 1 As Id) As x On x.Id=u.RoleId \r\nLeft Join audit.permissions p On p.RoleId=x.Id";

        // Act
        var sql = _builder.Select("u.Id").From("public.users", "u")
            .AppendJoin("Lateral (Select 1 As Id) As x").AppendOn("x.Id=u.RoleId")
            .AppendLeftJoin("audit.permissions p").AppendOn("p.RoleId=x.Id").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// 测试 - 类型化 Join 与 Lambda On 应使用实体别名并保留值参数。
    /// </summary>
    [Fact]
    public void Join_WhenTypedJoinAndLambdaOnConfigured_ShouldRenderAliasesAndValueParameter()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\",\"r\".\"Name\" \r\nFrom \"public\".\"PgUser\" As \"u\" \r\nJoin \"audit\".\"PgRole\" As \"r\" On \"u\".\"RoleId\"=\"r\".\"Id\" And \"u\".\"Enabled\"=@_p_0";

        // Act
        var sql = _builder.Select("Id", "u").Select("Name", "r").From<PgUser>("u", "public")
            .Join<PgRole>("r", "audit").On<PgUser, PgRole>(user => user.RoleId, role => role.Id)
            .On("u.Enabled", true).ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Single(_builder.GetParams());
        Assert.True((bool)_builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试 - 独立 Join 子查询与外层参数同名时，应重命名子查询参数并保留两侧值。
    /// </summary>
    [Fact]
    public void Join_WhenExternalSubqueryParameterConflicts_ShouldRenameSubqueryParameter()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nJoin (Select \"UserId\" \r\nFrom \"audit\".\"role_assignments\" \r\nWhere \"Enabled\"=@_p_1) As \"ra\" On ra.UserId=u.Id \r\nWhere \"u\".\"TenantId\"=@_p_0";
        var subquery = new PostgreSqlBuilder().Select("UserId").From("audit.role_assignments").Where("Enabled", true);

        // Act
        var sql = _builder.Select("u.Id").From("public.users", "u").Where("u.TenantId", 8)
            .Join(subquery, "ra").AppendOn("ra.UserId=u.Id").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal(8, _builder.GetParam("@_p_0"));
        Assert.True((bool)_builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：子查询参数冲突改名时，不得修改 PostgreSQL dollar-quoted 文本或 @@ 系统变量。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryContainsDollarQuotedTextOrSystemVariable_ShouldRenameOnlyParameterToken()
    {
        // Arrange
        const string expected = "Select * \r\nFrom \"Parent\" \r\nJoin (Select * \r\nFrom \"Child\" \r\nWhere payload=$tag$@_p_0$tag$ And server=@@version And value=@_p_1) As \"c\" \r\nWhere \"Id\"=@_p_0";
        var subquery = new PostgreSqlBuilder().From("Child")
            .AppendWhere("payload=$tag$@_p_0$tag$ And server=@@version And value=@_p_0")
            .AddParam("_p_0", "child");

        // Act
        var sql = _builder.From("Parent").Where("Id", 1).Join(subquery, "c").ToSql();

        // Assert
        Assert.Equal(expected, sql);
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试 - 未配置 Join 时 On 应抛出异常，且不添加参数或污染后续 Join。
    /// </summary>
    [Fact]
    public void On_WhenNoJoinExists_ShouldThrowWithoutAddingParameterOrAffectingFollowingJoin()
    {
        // Arrange
        const string expected = "Select \"u\".\"Id\" \r\nFrom \"public\".\"users\" As \"u\" \r\nJoin \"audit\".\"roles\" As \"r\"";

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.On("u.Enabled", true));
        var sql = _builder.Select("u.Id").From("public.users", "u").Join("audit.roles", "r").ToSql();

        // Assert
        Assert.Equal("当前不存在可追加 On 条件的 Join。", exception.Message);
        Assert.Empty(_builder.GetParams());
        Assert.Equal(expected, sql);
    }

    /// <summary>
    /// 测试 - Join 使用与 From 相同的结构化别名时应立即失败。
    /// </summary>
    [Fact]
    public void Join_WhenAliasDuplicatesFromAlias_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _builder.From("public.users", "u");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.Join("audit.roles", "u"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"u\"。", exception.Message);
        Assert.Empty(_builder.GetParams());
    }

    /// <summary>
    /// PostgreSQL Join 左侧实体。
    /// </summary>
    private sealed class PgUser
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 角色标识。
        /// </summary>
        public int RoleId { get; set; }

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// PostgreSQL Join 右侧实体。
    /// </summary>
    private sealed class PgRole
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
}
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试 - Join 子句
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 内连接
    /// </summary>
    [Fact]
    public void Test_Join_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 内连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_Join_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .Join<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 添加Join子查询
    /// </summary>
    [Fact]
    public void Test_Join_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").Join(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：独立子查询与外层查询生成相同参数名时，应重命名子查询参数并保留两侧参数值。
    /// </summary>
    [Fact]
    public void Join_WhenExternalSubqueryParameterConflicts_ShouldRenameSubqueryParameter()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Test2").Where("Name", "child");
        var expected = new StringBuilder();
        expected.AppendLine("Select * ");
        expected.AppendLine("From [Test] ");
        expected.AppendLine("Join (Select * ");
        expected.AppendLine("From [Test2] ");
        expected.AppendLine("Where [Name]=@_p_1) As [t] ");
        expected.Append("Where [Age]=@_p_0");

        // Act
        _builder.From("Test").Where("Age", 1).Join(subquery, "t");

        // Assert
        Assert.Equal(expected.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：作为 On 条件的独立 Builder 必须合并参数，并在冲突时重命名避免错误绑定到外层值。
    /// </summary>
    [Fact]
    public void Join_WhenOnUsesExternalBuilderCondition_ShouldMergeParameters()
    {
        // Arrange
        const string expected = "Select * \r\nFrom [Parent] \r\nJoin [Child] On [TenantId]=@_p_1 \r\nWhere [TenantId]=@_p_0";
        var condition = new TestSqlBuilder().Where("TenantId", "child");

        // Act
        _builder.From("Parent").Where("TenantId", "parent").Join("Child").On(condition);

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal("parent", _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：子查询连续参数均与外层冲突时，应基于原始 SQL 一次性重命名，避免先替换的参数被后续替换再次改写。
    /// </summary>
    [Fact]
    public void Join_WhenChildHasSequentialConflictingParameters_ShouldRenameEachTokenOnce()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Test2").Where("Name", "child-name").Where("Age", 18);
        const string expected = "Select * \r\nFrom [Test] \r\nJoin (Select * \r\nFrom [Test2] \r\nWhere [Name]=@_p_1 And [Age]=@_p_2) As [t] \r\nWhere [Id]=@_p_0";

        // Act
        _builder.From("Test").Where("Id", 1).Join(subquery, "t");

        // Assert
        Assert.Equal(expected, _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child-name", _builder.GetParam("@_p_1"));
        Assert.Equal(18, _builder.GetParam("@_p_2"));
    }

    /// <summary>
    /// 测试目的：子查询参数冲突改名时，只应替换 SQL 代码中的参数标记，不得修改字符串、注释或方括号标识符。
    /// </summary>
    [Fact]
    public void Join_WhenSubqueryContainsQuotedOrCommentedParameterText_ShouldRenameOnlyParameterToken()
    {
        // Arrange
        var subquery = new TestSqlBuilder().From("Child")
            .AppendWhere("[Text]='@_p_0' And [@_p_0]=1 /* @_p_0 */ -- @_p_0\r\n And [Value]=@_p_0")
            .AddParam("_p_0", "child");
        var expected = new StringBuilder();
        expected.AppendLine("Select * ");
        expected.AppendLine("From [Parent] ");
        expected.AppendLine("Join (Select * ");
        expected.AppendLine("From [Child] ");
        expected.AppendLine("Where [Text]='@_p_0' And [@_p_0]=1 /* @_p_0 */ -- @_p_0");
        expected.AppendLine(" And [Value]=@_p_1) As [c] ");
        expected.Append("Where [Id]=@_p_0");

        // Act
        _builder.From("Parent").Where("Id", 1).Join(subquery, "c");

        // Assert
        Assert.Equal(expected.ToString(), _builder.ToSql());
        Assert.Equal(1, _builder.GetParam("@_p_0"));
        Assert.Equal("child", _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：结构化 From 和 Join 在同一查询范围内使用重复 alias 时应立即失败。
    /// </summary>
    [Fact]
    public void Join_WhenAliasDuplicatesFromAlias_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _builder.From("Orders", "o");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.Join("OrderItems", "o"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"o\"。", exception.Message);
    }

    /// <summary>
    /// 测试目的：同一实体类型自连接时，类型化 On 表达式应分别使用来源表和最新 Join 表的别名。
    /// </summary>
    [Fact]
    public void Join_WhenSelfJoinTypedOnConfigured_ShouldRenderDistinctAliases()
    {
        // Arrange
        _builder.Select("s.Email")
            .From<Sample>("s")
            .Join<Sample>("p");

        // Act
        _builder.On<Sample, Sample>((left, right) => left.IntValue == right.IntValue);

        // Assert
        Assert.Equal("Select [s].[Email] \r\nFrom [Sample] As [s] \r\nJoin [Sample] As [p] On [s].[IntValue]=[p].[IntValue]", _builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Cross Join 不允许通过任意 On 入口附加连接条件。
    /// </summary>
    [Fact]
    public void CrossJoin_WhenOnConfigured_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _builder.Select("s.Id").From("Samples", "s").CrossJoin("Reviews", "r");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.AppendOn("s.Id=r.SampleId"));

        // Assert
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Append 原始 SQL 不应参与别名冲突校验。
    /// </summary>
    [Fact]
    public void AppendJoin_WhenSqlContainsAlias_ShouldNotRegisterAlias()
    {
        _builder.AppendFrom("(Select 1) As source");
        _builder.AppendJoin("(Select 2) As source");

        Assert.Contains("Join (Select 2) As source", _builder.ToSql());
    }

    /// <summary>
    /// 添加Join子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_Join_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").Join(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 内连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_Join_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接
    /// </summary>
    [Fact]
    public void Test_LeftJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_LeftJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .LeftJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    [Fact]
    public void Test_LeftJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").LeftJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_LeftJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Left Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").LeftJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 左外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_LeftJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Left Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendLeftJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 左连接 - lambda表达式
    /// </summary>
    [Fact]
    public void Test_LeftJoin_7()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a].[Email],[a].[BoolValue],[b].[Description],[b].[IntValue] ");
        result.AppendLine("From [Sample] As [a] ");
        result.Append("Left Join [Sample2] As [b] On [a].[Email]=[b].[StringValue] And [a].[IntValue]<>[b].[IntValue]");

        //执行
        _builder.Select<Sample>(t => new object[] { t.Email, t.BoolValue })
            .Select<Sample2>(t => new object[] { t.Description, t.IntValue })
            .From<Sample>("a")
            .LeftJoin<Sample2>("b").On<Sample, Sample2>((l, r) => l.Email == r.StringValue && l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接
    /// </summary>
    [Fact]
    public void Test_RightJoin_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [c] As [d]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接 - 泛型
    /// </summary>
    [Fact]
    public void Test_RightJoin_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join [d].[Sample] As [c]");

        //执行
        _builder.Select("a")
            .From("b")
            .RightJoin<Sample>("c", "d");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    [Fact]
    public void Test_RightJoin_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        var builder2 = _builder.New().From("Test2").Where("Name", "a");
        _builder.From("Test").RightJoin(builder2, "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接子查询 - 委托
    /// </summary>
    [Fact]
    public void Test_RightJoin_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select * ");
        result.AppendLine("From [Test] ");
        result.AppendLine("Right Join (Select * ");
        result.AppendLine("From [Test2] ");
        result.AppendLine("Where [Name]=@_p_0) As [t] ");
        result.Append("Where [Age]=@_p_1");

        //执行
        _builder.From("Test").RightJoin(builder => builder.From("Test2").Where("Name", "a"), "t").Where("Age", 1);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        Assert.Equal(2, _builder.GetParams().Count);
        Assert.Equal("a", _builder.GetParam("@_p_0"));
        Assert.Equal(1, _builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 右外连接 - 添加原始Sql
    /// </summary>
    [Fact]
    public void Test_RightJoin_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Right Join c");

        //执行
        _builder.Select("a")
            .From("b")
            .AppendRightJoin("c");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件
    /// </summary>
    [Fact]
    public void Test_On_1()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [b] ");
        result.Append("Join [c] As [d] On [b].[Id]<>@_p_0");

        //执行
        _builder.Select("a")
            .From("b")
            .Join("c", "d").On("b.Id", "c", Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 属性表达式
    /// </summary>
    [Fact]
    public void Test_On_2()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 布尔表达式
    /// </summary>
    [Fact]
    public void Test_On_3()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a] ");
        result.AppendLine("From [Sample] As [b] ");
        result.Append("Join [Sample2] As [c] On [b].[IntValue]<>[c].[IntValue]");

        //执行
        _builder.Select("a")
            .From<Sample>("b")
            .Join<Sample2>("c").On<Sample, Sample2>((l, r) => l.IntValue != r.IntValue);

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
    }

    /// <summary>
    /// 连接条件 - 值为字面量
    /// </summary>
    [Fact]
    public void Test_On_4()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == "a");

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 连接条件 - 值为变量
    /// </summary>
    [Fact]
    public void Test_On_5()
    {
        //结果
        var result = new StringBuilder();
        result.AppendLine("Select [a],[b] ");
        result.AppendLine("From [Sample] As [s] ");
        result.Append("Left Join [Sample2] As [s2] On [s].[IntValue]=[s2].[IntValue] And [s].[StringValue]=@_p_0");

        var a = "a";

        //执行
        _builder.Select("a,b")
            .From<Sample>("s")
            .LeftJoin<Sample2>("s2").On<Sample, Sample2>((l, r) => l.IntValue == r.IntValue && l.StringValue == a);
        _output.WriteLine(result.ToString());
        _output.WriteLine(_builder.ToSql());

        //验证
        Assert.Equal(result.ToString(), _builder.ToSql());
        _output.WriteLine(_builder.ToSql());
        Assert.Equal("a", _builder.GetParam("@_p_0"));
    }
}

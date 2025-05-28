using System.Linq.Expressions;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Sql.Tests.XUnitHelpers;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// Where子句测试
/// </summary>
public class WhereClauseTest
{
    #region 测试初始化

    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly ParameterManager _parameterManager;

    /// <summary>
    /// Where子句
    /// </summary>
    private WhereClause _clause;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public WhereClauseTest()
    {
        _parameterManager = new ParameterManager(TestDialect.Instance);
        var builder = new TestSqlBuilder(TestDialect.Instance);
        _clause = new WhereClause(builder, TestDialect.Instance, new EntityResolver(), new EntityAliasRegister(), _parameterManager);
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    private string GetSql()
    {
        return _clause.ToSql();
    }

    #endregion

    #region Default(默认输出)

    /// <summary>
    /// 默认 - 输出
    /// </summary>
    [Fact]
    public void Test_Default()
    {
        Assert.Null(GetSql());
    }

    #endregion

    #region Where(设置条件)

    /// <summary>
    /// 测试 - 设置条件 - 1个条件
    /// </summary>
    [Fact]
    public void Test_Where_1()
    {
        _clause.Where("Name", "a", Operator.Equal);
        Assert.Equal("Where [Name]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_Where_2()
    {
        _clause.Where("f.Name", "a", Operator.Equal);
        Assert.Equal("Where [f].[Name]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 2个条件
    /// </summary>
    [Fact]
    public void Test_Where_3()
    {
        _clause.Where("f.Name", "a", Operator.Equal);
        _clause.Where("s.Age", "b", Operator.Equal);
        Assert.Equal("Where [f].[Name]=@_p_0 And [s].[Age]=@_p_1", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 设置子查询条件
    /// </summary>
    [Fact]
    public void Test_Where_SubQuery_1()
    {
        var result = new StringBuilder();
        result.Append("Where [a].[b]=");
        result.AppendLine("(Select [a] ");
        result.Append("From [b])");

        var builder = new TestSqlBuilder().Select("a").From("b");
        _clause.Where("a.b", builder, Operator.Equal);
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 设置子查询条件 - 内嵌表达式
    /// </summary>
    [Fact]
    public void Test_Where_SubQuery_2()
    {
        var result = new StringBuilder();
        result.Append("Where [a].[b]=");
        result.AppendLine("(Select [a] ");
        result.Append("From [b])");

        _clause.Where("a.b", t => t.Select("a").From("b"), Operator.Equal);
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_1()
    {
        _clause.Where<Sample>(t => t.Email, "a");
        Assert.Equal("Where [Email]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名 - 设置实体解析器和实体别名注册器
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_2()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        _clause = new WhereClause(null, TestDialect.Instance, new TestEntityResolver(), new TestEntityAliasRegister(), manager);
        _clause.Where<Sample>(t => t.Email, "a");
        Assert.Equal("Where [as_Sample].[t_Email]=@_p_0", GetSql());
        Assert.Equal("a", manager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda表达式设置条件
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_1()
    {
        _clause.Where<Sample>(t => t.Email == "a");
        Assert.Equal("Where [Email]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda表达式设置条件 - 设置实体解析器和实体别名注册器
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_2()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        _clause = new WhereClause(null, TestDialect.Instance, new TestEntityResolver(), new TestEntityAliasRegister(), manager);
        _clause.Where<Sample>(t => t.Email == "a");
        Assert.Equal("Where [as_Sample].[t_Email]=@_p_0", GetSql());
        Assert.Equal("a", manager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 不相等
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_3()
    {
        _clause.Where<Sample>(t => t.Email != "a");
        Assert.Equal("Where [Email]<>@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 大于
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_4()
    {
        _clause.Where<Sample>(t => t.IntValue > 1);
        Assert.Equal("Where [IntValue]>@_p_0", GetSql());
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 小于
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_5()
    {
        _clause.Where<Sample>(t => t.IntValue < 1);
        Assert.Equal("Where [IntValue]<@_p_0", GetSql());
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 大于等于
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_6()
    {
        _clause.Where<Sample>(t => t.IntValue >= 1);
        Assert.Equal("Where [IntValue]>=@_p_0", GetSql());
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 小于等于
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_7()
    {
        _clause.Where<Sample>(t => t.IntValue <= 1);
        Assert.Equal("Where [IntValue]<=@_p_0", GetSql());
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - Contains
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_8()
    {
        _clause.Where<Sample>(t => t.Email.Contains("a"));
        Assert.Equal("Where [Email] Like @_p_0", GetSql());
        Assert.Equal("%a%", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - StartsWith
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_9()
    {
        _clause.Where<Sample>(t => t.Email.StartsWith("a"));
        Assert.Equal("Where [Email] Like @_p_0", GetSql());
        Assert.Equal("a%", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - EndsWith
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_10()
    {
        _clause.Where<Sample>(t => t.Email.EndsWith("a"));
        Assert.Equal("Where [Email] Like @_p_0", GetSql());
        Assert.Equal("%a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 设置多个条件 - 与连接
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_And_1()
    {
        _clause.Where<Sample>(t => t.Email == "a" && t.StringValue.Contains("b"));
        Assert.Equal("Where [Email]=@_p_0 And [StringValue] Like @_p_1", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("%b%", _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置条件 - 设置多个条件 - 或连接
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_Or_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where ([Email]=@_p_0 And [StringValue] Like @_p_1 Or [IntValue]=@_p_2) ");
        result.Append("And ([Email]=@_p_3 Or [IntValue]=@_p_4)");

        //执行
        _clause.Where<Sample>(t => t.Email == "a" && t.StringValue.Contains("b") || t.IntValue == 1);
        _clause.Where<Sample>(t => t.Email == "c" || t.IntValue == 2);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("%b%", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_2"));
        Assert.Equal("c", _parameterManager.GetValue("@_p_3"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_4"));
    }

    /// <summary>
    /// 测试 - 设置条件 - Is Null
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_11()
    {
        _clause.Where<Sample>(t => t.Email == null);
        Assert.Equal("Where [Email] Is Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置条件 - 空字符串使用=
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_12()
    {
        _clause.Where<Sample>(t => t.Email == "");
        Assert.Equal("Where [Email]=@_p_0", GetSql());
        Assert.Equal("", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - Is Not Null
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_13()
    {
        _clause.Where<Sample>(t => t.Email != null);
        Assert.Equal("Where [Email] Is Not Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置条件 - 空字符串使用不等号
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_14()
    {
        _clause.Where<Sample>(t => t.Email != "");
        Assert.Equal("Where [Email]<>@_p_0", GetSql());
        Assert.Equal("", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - In
    /// </summary>
    [Fact]
    public void Test_Where_WithLambda_WithOperatorResolve_15()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Where<Sample>(t => list.Contains(t.Email));

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
    }

    #endregion        

    #region WhereIfNotEmpty(设置条件)

    /// <summary>
    /// 测试 - 设置条件 - 添加条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_1()
    {
        _clause.WhereIfNotEmpty("Name", "a");
        Assert.Equal("Where [Name]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 忽略条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_2()
    {
        _clause.WhereIfNotEmpty("Name", "");
        Assert.Null(GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名  - 添加条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_3()
    {
        _clause.WhereIfNotEmpty<Sample>(t => t.Email, "a");
        Assert.Equal("Where [Email]=@_p_0", GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名  - 忽略条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_4()
    {
        _clause.WhereIfNotEmpty<Sample>(t => t.Email, "");
        Assert.Null(GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名  - 添加条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_5()
    {
        _clause.WhereIfNotEmpty<Sample>(t => t.Email.Contains("a"));
        Assert.Equal("Where [Email] Like @_p_0", GetSql());
        Assert.Equal("%a%", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 设置条件 - 通过lambda设置列名  - 忽略条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_6()
    {
        _clause.WhereIfNotEmpty<Sample>(t => t.Email == "");
        Assert.Null(GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 设置条件 - 通过lambda设置条件 - 仅允许设置一个条件
    /// </summary>
    [Fact]
    public void Test_WhereIfNotEmpty_7()
    {
        Expression<Func<Sample, bool>> condition = t => t.Email.Contains("a") && t.IntValue == 1;
        AssertHelper.Throws<InvalidOperationException>(() =>
        {
            _clause.WhereIfNotEmpty(condition);
        }, $"仅允许添加一个条件，条件：{condition}");
    }

    #endregion

    #region IsNull

    /// <summary>
    /// 测试 - 设置Is Null条件
    /// </summary>
    [Fact]
    public void Test_IsNull_1()
    {
        _clause.IsNull("Name");
        Assert.Equal("Where [Name] Is Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置Is Null条件
    /// </summary>
    [Fact]
    public void Test_IsNull_2()
    {
        _clause.IsNull<Sample>(t => t.Email);
        Assert.Equal("Where [Email] Is Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置Is Null条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_IsNull_3()
    {
        _clause.IsNull("f.Name");
        Assert.Equal("Where [f].[Name] Is Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    #endregion

    #region IsNotNull

    /// <summary>
    /// 测试 - 设置Is Not Null条件
    /// </summary>
    [Fact]
    public void Test_IsNotNull_1()
    {
        _clause.IsNotNull("Name");
        Assert.Equal("Where [Name] Is Not Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置Is Not Null条件
    /// </summary>
    [Fact]
    public void Test_IsNotNull_2()
    {
        _clause.IsNotNull<Sample>(t => t.Email);
        Assert.Equal("Where [Email] Is Not Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置Is Not Null条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_IsNotNull_3()
    {
        _clause.IsNotNull("f.Name");
        Assert.Equal("Where [f].[Name] Is Not Null", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    #endregion

    #region IsEmpty

    /// <summary>
    /// 测试 - 设置空条件
    /// </summary>
    [Fact]
    public void Test_IsEmpty_1()
    {
        _clause.IsEmpty("Name");
        Assert.Equal("Where ([Name] Is Null Or [Name]='')", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置空条件
    /// </summary>
    [Fact]
    public void Test_IsEmpty_2()
    {
        _clause.IsEmpty<Sample>(t => t.Email);
        Assert.Equal("Where ([Email] Is Null Or [Email]='')", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置空条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_IsEmpty_3()
    {
        _clause.IsEmpty("f.Name");
        Assert.Equal("Where ([f].[Name] Is Null Or [f].[Name]='')", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    #endregion

    #region IsNotEmpty

    /// <summary>
    /// 测试 - 设置空条件
    /// </summary>
    [Fact]
    public void Test_IsNotEmpty_1()
    {
        _clause.IsNotEmpty("Name");
        Assert.Equal("Where [Name] Is Not Null And [Name]<>''", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置空条件
    /// </summary>
    [Fact]
    public void Test_IsNotEmpty_2()
    {
        _clause.IsNotEmpty<Sample>(t => t.Email);
        Assert.Equal("Where [Email] Is Not Null And [Email]<>''", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 测试 - 设置空条件 - 带表别名
    /// </summary>
    [Fact]
    public void Test_IsNotEmpty_3()
    {
        _clause.IsNotEmpty("f.Name");
        Assert.Equal("Where [f].[Name] Is Not Null And [f].[Name]<>''", GetSql());
        Assert.Empty(_parameterManager.GetParams());
    }

    #endregion

    #region In

    /// <summary>
    /// 测试 - 设置In条件
    /// </summary>
    [Fact]
    public void Test_In_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [user].[Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.In("user.Email", list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 测试 - 设置In条件 - lambda列名表达式
    /// </summary>
    [Fact]
    public void Test_In_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.In<Sample>(t => t.Email, list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 测试 - 设置In条件 - 数组
    /// </summary>
    [Fact]
    public void Test_In_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [user].[Email] In (@_p_0,@_p_1)");

        //执行
        var list = new[] { "a", "b" };
        _clause.In("user.Email", list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 测试 - 设置In条件 - 枚举数组
    /// </summary>
    [Fact]
    public void Test_In_4()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [user].[Email] In (@_p_0,@_p_1,@_p_2)");

        //执行
        var list = new object[] { SampleEnum.One, SampleEnum.Two, SampleEnum.Three };
        _clause.In("user.Email", list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal(SampleEnum.One, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(SampleEnum.Two, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(SampleEnum.Three, _parameterManager.GetValue("@_p_2"));
        Assert.Equal(3, _parameterManager.GetParams().Count);
    }

    #endregion

    #region NotIn

    /// <summary>
    /// 设置Not In条件
    /// </summary>
    [Fact]
    public void Test_NotIn_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [user].[Email] Not In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.NotIn("user.Email", list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 设置Not In条件 - lambda列名表达式
    /// </summary>
    [Fact]
    public void Test_NotIn_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] Not In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.NotIn<Sample>(t => t.Email, list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 设置Not In条件 - 数组
    /// </summary>
    [Fact]
    public void Test_NotIn_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [user].[Email] Not In (@_p_0,@_p_1)");

        //执行
        var list = new[] { "a", "b" };
        _clause.NotIn("user.Email", list);

        //验证
        Assert.Equal(result.ToString(), GetSql());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_1"));
        Assert.Equal(2, _parameterManager.GetParams().Count);
    }

    #endregion

    #region Between(范围查询)

    /// <summary>
    /// 测试 - 范围查询 - 整型
    /// </summary>
    [Fact]
    public void Test_Between_Int_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0 And [a].[B]<=@_p_1");

        //执行
        _clause.Between("a.B", 1, 2, Boundary.Both);

        //验证
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 范围查询 - 整型 - 不包含边界
    /// </summary>
    [Fact]
    public void Test_Between_Int_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>@_p_0 And [a].[B]<@_p_1");

        //执行
        _clause.Between("a.B", 1, 2, Boundary.Neither);

        //验证
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 整型 - 最小值大于最大值，则交换大小值的位置
    /// </summary>
    [Fact]
    public void Test_Between_Int_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>@_p_0 And [a].[B]<@_p_1");

        //执行
        _clause.Between("a.B", 2, 1, Boundary.Neither);

        //验证
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 整型 - 最小值为空，忽略最小值条件
    /// </summary>
    [Fact]
    public void Test_Between_Int_4()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]<=@_p_0");

        //执行
        _clause.Between("a.B", null, 2, Boundary.Both);

        //验证
        Assert.Equal(2, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 整型 - 最大值为空，忽略最大值条件
    /// </summary>
    [Fact]
    public void Test_Between_Int_5()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0");

        //执行
        _clause.Between("a.B", 1, null, Boundary.Both);

        //验证
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 整型 - 最大值和最小值均为null,忽略所有条件
    /// </summary>
    [Fact]
    public void Test_Between_Int_6()
    {
        //执行
        _clause.Between("a.B", null, null, Boundary.Both);

        //验证
        Assert.Empty(_parameterManager.GetParams());
        Assert.Null(GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 整型 - lambda
    /// </summary>
    [Fact]
    public void Test_Between_Int_7()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [IntValue]>=@_p_0 And [IntValue]<=@_p_1");

        //执行
        _clause.Between<Sample>(t => t.IntValue, 1, 2, Boundary.Both);

        //验证
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - double
    /// </summary>
    [Fact]
    public void Test_Between_Double_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0 And [a].[B]<=@_p_1");

        //执行
        _clause.Between("a.B", 1.2, 3.4, Boundary.Both);

        //验证
        Assert.Equal(1.2, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(3.4, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - decimal
    /// </summary>
    [Fact]
    public void Test_Between_Decimal_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0 And [a].[B]<=@_p_1");

        //执行
        _clause.Between("a.B", 1.2M, 3.4M, Boundary.Both);

        //验证
        Assert.Equal(1.2M, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(3.4M, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 包含时间
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0 And [a].[B]<=@_p_1");

        //执行
        var min = DateTime.Parse("2000-1-1 10:10:10");
        var max = DateTime.Parse("2000-1-2 10:10:10");
        _clause.Between("a.B", min, max, true, null);

        //验证
        Assert.Equal(min, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(max, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 不包含时间
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0 And [a].[B]<@_p_1");

        //执行
        var min = DateTime.Parse("2000-1-1 10:10:10");
        var max = DateTime.Parse("2000-1-2 10:10:10");
        _clause.Between("a.B", min, max, false, null);

        //验证
        Assert.Equal(DateTime.Parse("2000-1-1"), _parameterManager.GetValue("@_p_0"));
        Assert.Equal(DateTime.Parse("2000-1-3"), _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 设置边界
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>@_p_0 And [a].[B]<@_p_1");

        //执行
        var min = DateTime.Parse("2000-1-1 10:10:10");
        var max = DateTime.Parse("2000-1-2 10:10:10");
        _clause.Between("a.B", min, max, true, Boundary.Neither);

        //验证
        Assert.Equal(min, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(max, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 不包含时间 - 最大值为空
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_4()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0");

        //执行
        var min = DateTime.Parse("2000-1-1 10:10:10");
        _clause.Between("a.B", min, null, false, null);

        //验证
        Assert.Equal(DateTime.Parse("2000-1-1"), _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 包含时间  - 最大值为空
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_5()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]>=@_p_0");

        //执行
        var min = DateTime.Parse("2000-1-1 10:10:10");
        _clause.Between("a.B", min, null, true, null);

        //验证
        Assert.Equal(min, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 不包含时间 - 最小值为空
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_6()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]<@_p_0");

        //执行
        var max = DateTime.Parse("2000-1-2 10:10:10");
        _clause.Between("a.B", null, max, false, null);

        //验证
        Assert.Equal(DateTime.Parse("2000-1-3"), _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试范围查询 - 日期 - 包含时间 - 最小值为空
    /// </summary>
    [Fact]
    public void Test_Between_DateTime_7()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [a].[B]<=@_p_0");

        //执行
        var max = DateTime.Parse("2000-1-2 10:10:10");
        _clause.Between("a.B", null, max, true, null);

        //验证
        Assert.Equal(max, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(result.ToString(), GetSql());
    }

    #endregion

    #region Exists

    /// <summary>
    /// 测试 - 设置Exists条件 - 子查询Sql生成器
    /// </summary>
    [Fact]
    public void Test_Exists_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where Exists (");
        result.AppendLine("Select [Name] ");
        result.Append("From [t]");
        result.Append(")");

        //子查询
        var subBuilder = new TestSqlBuilder();
        subBuilder.Select("Name").From("t");

        //执行
        _clause.Exists(subBuilder);

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 设置Exists条件 - 子查询操作
    /// </summary>
    [Fact]
    public void Test_Exists_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where Exists (");
        result.AppendLine("Select [Name] ");
        result.Append("From [t]");
        result.Append(")");

        //执行
        _clause.Exists(t=>t.Select("Name").From("t"));

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    #endregion

    #region NotExists

    /// <summary>
    /// 测试 - 设置Not Exists条件 - 子查询Sql生成器
    /// </summary>
    [Fact]
    public void Test_NotExists_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where Not Exists (");
        result.AppendLine("Select [Name] ");
        result.Append("From [t]");
        result.Append(")");

        //子查询
        var subBuilder = new TestSqlBuilder();
        subBuilder.Select("Name").From("t");

        //执行
        _clause.NotExists(subBuilder);

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// 测试 - 设置Not Exists条件 - 子查询操作
    /// </summary>
    [Fact]
    public void Test_NotExists_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where Not Exists (");
        result.AppendLine("Select [Name] ");
        result.Append("From [t]");
        result.Append(")");

        //执行
        _clause.NotExists(t => t.Select("Name").From("t"));

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    #endregion

    #region AppendSql(添加到Where子句)

    /// <summary>
    /// 测试 - 添加到Where子句
    /// </summary>
    [Fact]
    public void Test_AppendSql()
    {
        _clause.AppendSql("a");
        _clause.AppendSql("b");
        Assert.Equal("Where a And b", GetSql());
    }

    #endregion

    #region Clone

    /// <summary>
    /// 测试 - 复制Where子句
    /// </summary>
    [Fact]
    public void Test_Clone_1()
    {
        _clause.Where("Name", "a");

        //复制副本
        var copy = _clause.Clone(null, null, _parameterManager.Clone());
        Assert.Equal("Where [Name]=@_p_0", GetSql());
        Assert.Equal("Where [Name]=@_p_0", copy.ToSql());

        //修改副本
        copy.Where("Code", 1);
        Assert.Equal("Where [Name]=@_p_0", GetSql());
        Assert.Equal("Where [Name]=@_p_0 And [Code]=@_p_1", copy.ToSql());

        //修改原对象
        _clause.Where("Age", 1);
        Assert.Equal("Where [Name]=@_p_0 And [Age]=@_p_1", GetSql());
        Assert.Equal("Where [Name]=@_p_0 And [Code]=@_p_1", copy.ToSql());
    }

    #endregion

    #region And(连接查询条件)

    /// <summary>
    /// 连接查询条件
    /// </summary>
    [Fact]
    public void Test_And()
    {
        _clause.Where("Age", 1);
        _clause.And(new LessCondition("a", "@a"));
        Assert.Equal("Where [Age]=@_p_0 And a<@a", GetSql());
        Assert.Equal(1,_parameterManager.GetValue("@_p_0"));
    }

    #endregion

    #region Or(连接查询条件)

    /// <summary>
    /// Or查询条件
    /// </summary>
    [Fact]
    public void Test_Or()
    {
        _clause.Where("Age", 1);
        _clause.Or(new LessCondition("a", "@a"));
        Assert.Equal("Where ([Age]=@_p_0 Or a<@a)", GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 一个条件
    /// </summary>
    [Fact]
    public void Test_Or_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Or<Sample>(t => list.Contains(t.Email));

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 2个条件
    /// </summary>
    [Fact]
    public void Test_Or_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where ([Email] In (@_p_0,@_p_1) Or [Url]=@_p_2)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Or<Sample>(t => list.Contains(t.Email), t => t.Url == "a");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 2个条件 - 参数值为空时添加条件
    /// </summary>
    [Fact]
    public void Test_Or_4()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where ([Email] In (@_p_0,@_p_1) Or [Url]=@_p_2)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Or<Sample>(t => list.Contains(t.Email), t => t.Url == "");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - And和Or混合添加条件
    /// </summary>
    [Fact]
    public void Test_Or_5()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where (([Email]=@_p_0 Or ");
        result.Append("[Email] In (@_p_1,@_p_2)) Or [Url]=@_p_3) ");
        result.Append("And [Url]=@_p_4");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Where<Sample>(t => t.Email == "b");
        _clause.Or<Sample>(t => list.Contains(t.Email), t => t.Url == "a");
        _clause.Where<Sample>(t => t.Url == "c");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 一个条件
    /// </summary>
    [Fact]
    public void Test_OrIfNotEmpty_1()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.OrIfNotEmpty<Sample>(t => list.Contains(t.Email));

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 2个条件
    /// </summary>
    [Fact]
    public void Test_OrIfNotEmpty_2()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where ([Email] In (@_p_0,@_p_1) Or [Url]=@_p_2)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.OrIfNotEmpty<Sample>(t => list.Contains(t.Email), t => t.Url == "a");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - 2个条件 - 参数值为空时不添加条件
    /// </summary>
    [Fact]
    public void Test_OrIfNotEmpty_3()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where [Email] In (@_p_0,@_p_1)");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.OrIfNotEmpty<Sample>(t => list.Contains(t.Email), t => t.Url == "");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    /// <summary>
    /// Or查询条件 - lambda - And和Or混合添加条件
    /// </summary>
    [Fact]
    public void Test_OrIfNotEmpty_4()
    {
        //结果
        var result = new StringBuilder();
        result.Append("Where (([Email]=@_p_0 Or ");
        result.Append("[Email] In (@_p_1,@_p_2)) Or [Url]=@_p_3) ");
        result.Append("And [Url]=@_p_4");

        //执行
        var list = new List<string> { "a", "b" };
        _clause.Where<Sample>(t => t.Email == "b");
        _clause.OrIfNotEmpty<Sample>(t => list.Contains(t.Email), t => t.Url == "a");
        _clause.Where<Sample>(t => t.Url == "c");

        //验证
        Assert.Equal(result.ToString(), GetSql());
    }

    #endregion
}

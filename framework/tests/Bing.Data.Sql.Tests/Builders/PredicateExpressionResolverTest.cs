using Bing.Data.Sql.Builders.Conditions;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// 谓词表达式解析器测试
/// </summary>
public class PredicateExpressionResolverTest
{
    #region 测试初始化

    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 谓词表达式解析器
    /// </summary>
    private readonly PredicateExpressionResolver _resolver;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public PredicateExpressionResolverTest()
    {
        _parameterManager = new ParameterManager(TestDialect.Instance);
        _resolver = new PredicateExpressionResolver(TestDialect.Instance, new EntityResolver(), new EntityAliasRegister(), _parameterManager);
    }

    #endregion

    /// <summary>
    /// 验证空表达式
    /// </summary>
    [Fact]
    public void TestResolve_1()
    {
        Expression<Func<Sample, bool>> expression = null;
        Assert.Same(NullCondition.Instance, _resolver.Resolve(expression));
    }

    /// <summary>
    /// 1个条件
    /// </summary>
    [Fact]
    public void TestResolve_2()
    {
        Expression<Func<Sample, bool>> expression = t => t.Email == "a";
        Assert.Equal("[Email]=@_p_0", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 2个条件 - And连接
    /// </summary>
    [Fact]
    public void TestResolve_3()
    {
        Expression<Func<Sample, bool>> expression = t => t.Email == "a" && t.IntValue == 1;
        Assert.Equal("[Email]=@_p_0 And [IntValue]=@_p_1", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 2个条件 - Or连接
    /// </summary>
    [Fact]
    public void TestResolve_4()
    {
        Expression<Func<Sample, bool>> expression = t => t.Email == "a" || t.IntValue == 1;
        Assert.Equal("([Email]=@_p_0 Or [IntValue]=@_p_1)", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// And和Or连接
    /// </summary>
    [Fact]
    public void TestResolve_5()
    {
        Expression<Func<Sample, bool>> expression = t => t.Email == "a" && t.IntValue == 1 || t.DisplayValue == "b";
        Assert.Equal("([Email]=@_p_0 And [IntValue]=@_p_1 Or [DisplayValue]=@_p_2)", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_2"));
    }

    /// <summary>
    /// Or和And连接
    /// </summary>
    [Fact]
    public void TestResolve_6()
    {
        Expression<Func<Sample, bool>> expression = t => t.Email == "a" || t.IntValue == 1 && t.DisplayValue == "b";
        Assert.Equal("([Email]=@_p_0 Or [IntValue]=@_p_1 And [DisplayValue]=@_p_2)", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_2"));
    }

    /// <summary>
    /// 加括号
    /// </summary>
    [Fact]
    public void TestResolve_7()
    {
        Expression<Func<Sample, bool>> expression = t => (t.Email == "a" || t.IntValue == 1) && t.DisplayValue == "b";
        Assert.Equal("([Email]=@_p_0 Or [IntValue]=@_p_1) And [DisplayValue]=@_p_2", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_2"));
    }

    /// <summary>
    /// 两个括号
    /// </summary>
    [Fact]
    public void TestResolve_8()
    {
        Expression<Func<Sample, bool>> expression = t => (t.Email == "a" || t.IntValue == 1) || (t.Email == "b" || t.IntValue == 2);
        Assert.Equal("(([Email]=@_p_0 Or [IntValue]=@_p_1) Or ([Email]=@_p_2 Or [IntValue]=@_p_3))", _resolver.Resolve(expression).GetCondition());
        Assert.Equal("a", _parameterManager.GetValue("@_p_0"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_1"));
        Assert.Equal("b", _parameterManager.GetValue("@_p_2"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_3"));
    }
}

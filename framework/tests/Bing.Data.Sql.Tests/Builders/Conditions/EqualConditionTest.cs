using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Moq;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// Sql相等查询条件测试
/// </summary>
public class EqualConditionTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public EqualConditionTest()
    {
        _parameterManager = new ParameterManager(TestDialect.Instance);
    }

    /// <summary>
    /// 获取条件
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var condition = new EqualCondition("Name", "@Name");
        Assert.Equal("Name=@Name", condition.GetCondition());
    }

    /// <summary>
    /// 获取结果
    /// </summary>
    private string GetResult(ISqlCondition condition)
    {
        var result = new StringBuilder();
        condition.AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 测试 - 创建条件 - 验证列名为空
    /// </summary>
    [Fact]
    public void Test_Create_Validate()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            var condition = new EqualSqlCondition(_parameterManager, "", 1, true);
        });
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_1()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", 1, true);
        Assert.Equal("a=@_p_0", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试目的：同一参数化条件重复渲染应复用参数名称，避免未引用参数持续累积。
    /// </summary>
    [Fact]
    public void AppendTo_WhenRepeated_ShouldReuseSameParameter()
    {
        // Arrange
        var condition = new EqualSqlCondition(_parameterManager, "a", 1, true);

        // Act
        var first = GetResult(condition);
        var second = GetResult(condition);

        // Assert
        Assert.Equal("a=@_p_0", first);
        Assert.Equal(first, second);
        Assert.Single(_parameterManager.GetParams());
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试目的：子查询 Builder 渲染失败时，条件不得向调用方缓冲区遗留列名、括号或子查询片段。
    /// </summary>
    [Fact]
    public void AppendTo_WhenSubqueryRenderingFails_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var subquery = new Mock<ISqlBuilder>();
        subquery.Setup(item => item.AppendTo(It.IsAny<StringBuilder>()))
            .Callback<StringBuilder>(builder =>
            {
                builder.Append("Partial");
                throw new InvalidOperationException("Subquery rendering failed.");
            });
        var condition = new EqualSqlCondition(_parameterManager, "Id", subquery.Object, true);
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => condition.AppendTo(result));

        // Assert
        Assert.Equal("Subquery rendering failed.", exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：参数化条件渲染失败时不得遗留参数；后续条件与失败条件重试必须使用独立参数名称和值。
    /// </summary>
    [Fact]
    public void AppendTo_WhenParameterizedConditionFails_ShouldRestoreParameterStateBeforeRetry()
    {
        // Arrange
        var condition = new ThrowOnceSqlCondition(_parameterManager, "First", 1);
        var otherCondition = new EqualSqlCondition(_parameterManager, "Second", 2, true);
        var failedResult = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => condition.AppendTo(failedResult));
        var otherResult = GetResult(otherCondition);
        var retryResult = GetResult(condition);

        // Assert
        Assert.Equal("Condition rendering failed.", exception.Message);
        Assert.Equal("Prefix:", failedResult.ToString());
        Assert.Equal("Second=@_p_1", otherResult);
        Assert.Equal("First=@_p_2", retryResult);
        Assert.Equal(2, _parameterManager.GetParams().Count);
        Assert.Null(_parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
        Assert.Equal(1, _parameterManager.GetValue("@_p_2"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 非参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_2()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", "b", false);
        Assert.Equal("a=b", GetResult(condition));
        Assert.Empty(_parameterManager.GetParams());
    }

    /// <summary>
    /// 首次写入条件片段时故意抛出异常的参数化测试条件。
    /// </summary>
    private sealed class ThrowOnceSqlCondition : SqlConditionBase
    {
        /// <summary>
        /// 指示下次条件写入是否应抛出异常。
        /// </summary>
        private bool _shouldThrow = true;

        /// <summary>
        /// 初始化一个<see cref="ThrowOnceSqlCondition"/>类型的实例。
        /// </summary>
        /// <param name="parameterManager">参数管理器。</param>
        /// <param name="column">列名。</param>
        /// <param name="value">条件值。</param>
        public ThrowOnceSqlCondition(IParameterManager parameterManager, string column, object value)
            : base(parameterManager, column, value, true)
        {
        }

        /// <inheritdoc />
        protected override void AppendCondition(StringBuilder builder, string column, object value)
        {
            builder.Append($"{column}={value}");
            if (_shouldThrow == false)
                return;
            _shouldThrow = false;
            throw new InvalidOperationException("Condition rendering failed.");
        }
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数值为null,则输出 is null
    /// </summary>
    [Fact]
    public void Test_GetCondition_3()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", null, true);
        Assert.Equal("a Is Null", GetResult(condition));
        Assert.Empty(_parameterManager.GetParams());
    }

    ///// <summary>
    ///// 测试 - 获取条件 - 值为ISqlBuilder
    ///// </summary>
    //[Fact]
    //public void Test_GetCondition_4()
    //{
    //    var result = new StringBuilder();
    //    result.Append("a=");
    //    result.AppendLine("(Select [a] ");
    //    result.Append("From [b])");

    //    var builder = new TestSqlBuilder().Select("a").From("b");
    //    var condition = new EqualSqlCondition(_parameterManager, "a", builder, true);
    //    Assert.Equal(result.ToString(), GetResult(condition));
    //}
}

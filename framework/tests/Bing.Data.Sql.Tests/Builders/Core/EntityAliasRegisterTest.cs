using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 实体别名注册器测试
/// </summary>
public class EntityAliasRegisterTest
{
    /// <summary>
    /// 测试目的：同一查询范围内的别名应按大小写无关规则保持唯一。
    /// </summary>
    [Fact]
    public void RegisterAlias_WhenAliasAlreadyExistsIgnoringCase_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.RegisterAlias("orders");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => register.RegisterAlias("ORDERS"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"ORDERS\"。", exception.Message);
    }

    /// <summary>
    /// 测试目的：实体来源和字符串来源应共享同一别名注册表。
    /// </summary>
    [Fact]
    public void Register_WhenAliasWasRegisteredByStringSource_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.RegisterAlias("source");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => register.Register(typeof(Sample), "source"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"source\"。", exception.Message);
    }

    /// <summary>
    /// 测试目的：替换 From 实体时应释放旧 alias 并保留新 alias。
    /// </summary>
    [Fact]
    public void Replace_WhenFromAliasChanges_ShouldReleasePreviousAlias()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.Replace(typeof(Sample), "s");

        // Act
        register.Replace(typeof(Sample), "current");
        register.RegisterAlias("s");

        // Assert
        Assert.Equal("current", register.GetAlias(typeof(Sample)));
        Assert.Equal("current", register.Data[typeof(Sample)]);
    }

    /// <summary>
    /// 测试目的：同一实体多次注册时，自连接条件应按注册顺序解析来源与最新连接别名，并在克隆后保持独立。
    /// </summary>
    [Fact]
    public void GetSelfJoinAlias_WhenSameEntityRegisteredMultipleTimes_ShouldUseLastTwoAliases()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.Replace(typeof(Sample), "s");
        register.Register(typeof(Sample), "p");
        var clone = register.Clone();
        register.Register(typeof(Sample), "q");

        // Act
        var sourceAlias = register.GetSelfJoinAlias(typeof(Sample), false);
        var targetAlias = register.GetSelfJoinAlias(typeof(Sample), true);

        // Assert
        Assert.Equal("p", sourceAlias);
        Assert.Equal("q", targetAlias);
        Assert.Equal("s", clone.GetSelfJoinAlias(typeof(Sample), false));
        Assert.Equal("p", clone.GetSelfJoinAlias(typeof(Sample), true));
    }
}

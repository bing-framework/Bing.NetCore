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
    /// 测试目的：替换 From 别名发生冲突时，原别名注册与实体映射必须保持不变。
    /// </summary>
    [Fact]
    public void Replace_WhenNewAliasAlreadyExists_ShouldKeepCurrentAlias()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.Replace(typeof(Sample), "s");
        register.RegisterAlias("other");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => register.Replace(typeof(Sample), "other"));

        // Assert
        Assert.Equal("查询中已存在表别名 \"other\"。", exception.Message);
        Assert.Equal("s", register.GetAlias(typeof(Sample)));
        Assert.Equal("s", register.Data[typeof(Sample)]);
        Assert.Throws<InvalidOperationException>(() => register.RegisterAlias("s"));
    }

    /// <summary>
    /// 测试目的：克隆注册器必须保留原始表使用的别名，以便在提交前可靠预检后续组合。
    /// </summary>
    [Fact]
    public void Clone_WhenRawAliasWasRegistered_ShouldRetainRawAlias()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.RegisterAlias("raw_source");

        // Act
        var clone = register.Clone();

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(() => clone.RegisterAlias("RAW_SOURCE"));
        Assert.Equal("查询中已存在表别名 \"RAW_SOURCE\"。", exception.Message);
    }

    /// <summary>
    /// 测试目的：释放根来源别名时应保留同实体仍在查询图中的 Join 别名。
    /// </summary>
    [Fact]
    public void ReleaseAlias_WhenSelfJoinRootAliasReleased_ShouldKeepLatestJoinAlias()
    {
        // Arrange
        var register = new EntityAliasRegister();
        register.Replace(typeof(Sample), "s");
        register.Register(typeof(Sample), "p");

        // Act
        ((IEntityAliasRegisterLifecycle)register).ReleaseAlias("s");

        // Assert
        Assert.Equal("p", register.GetAlias(typeof(Sample)));
        register.RegisterAlias("s");
        Assert.Throws<InvalidOperationException>(() => register.RegisterAlias("p"));
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

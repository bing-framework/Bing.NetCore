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
    /// 测试目的：克隆后的别名集合应与来源双向隔离，并保留大小写无关的释放语义。
    /// </summary>
    [Fact]
    public void Clone_WhenAliasesAreMutated_ShouldKeepSourceAndCloneIndependent()
    {
        // Arrange
        var source = new EntityAliasRegister();
        source.Replace(typeof(Sample), "s");
        source.RegisterAlias("raw_source");
        var clone = source.Clone();

        // Act
        clone.Replace(typeof(Sample), "clone_source");
        ((IEntityAliasRegisterLifecycle)clone).ReleaseAlias("RAW_SOURCE");
        clone.RegisterAlias("raw_source");
        source.RegisterAlias("clone_source");

        // Assert
        Assert.Equal("s", source.GetAlias(typeof(Sample)));
        Assert.Equal("clone_source", clone.GetAlias(typeof(Sample)));
        Assert.Throws<InvalidOperationException>(() => source.RegisterAlias("RAW_SOURCE"));
        Assert.Throws<InvalidOperationException>(() => clone.RegisterAlias("CLONE_SOURCE"));

        source.Replace(typeof(Sample), "updated_source");
        clone.RegisterAlias("s");
        Assert.Equal("updated_source", source.GetAlias(typeof(Sample)));
        Assert.Equal("clone_source", clone.GetAlias(typeof(Sample)));
    }

    /// <summary>
    /// 测试目的：从同一空注册器创建的多个克隆应各自维护别名集合。
    /// </summary>
    [Fact]
    public void Clone_WhenCloningSameEmptyRegisterTwice_ShouldKeepAliasesIndependent()
    {
        // Arrange
        var source = new EntityAliasRegister();
        var first = source.Clone();
        var second = source.Clone();

        // Act
        first.RegisterAlias("first");
        second.RegisterAlias("FIRST");
        source.RegisterAlias("first");

        // Assert
        Assert.Throws<InvalidOperationException>(() => first.RegisterAlias("FIRST"));
        Assert.Throws<InvalidOperationException>(() => second.RegisterAlias("first"));
        Assert.Throws<InvalidOperationException>(() => source.RegisterAlias("FIRST"));
    }

    /// <summary>
    /// 测试目的：克隆应保留 From 类型和自连接别名的注册顺序。
    /// </summary>
    [Fact]
    public void Clone_WhenSelfJoinAliasesExist_ShouldRetainFromTypeAndAliasOrder()
    {
        // Arrange
        var source = new EntityAliasRegister(fromType: typeof(Sample));
        source.Replace(typeof(Sample), "s");
        source.Register(typeof(Sample), "p");
        var clone = source.Clone();

        // Act
        source.Register(typeof(Sample), "q");

        // Assert
        Assert.Equal(typeof(Sample), clone.FromType);
        Assert.Equal("s", clone.GetSelfJoinAlias(typeof(Sample), right: false));
        Assert.Equal("p", clone.GetSelfJoinAlias(typeof(Sample), right: true));
        Assert.Equal("p", source.GetSelfJoinAlias(typeof(Sample), right: false));
        Assert.Equal("q", source.GetSelfJoinAlias(typeof(Sample), right: true));
    }

    /// <summary>
    /// 测试目的：克隆单实体别名时不应再次复制已独立创建的别名集合。
    /// </summary>
    [Fact]
    public void Clone_WhenSingleEntityIsRegistered_ShouldAvoidDuplicateAliasSetAllocation()
    {
        // Arrange
        const int warmups = 128;
        const int iterations = 512;
        var data = new Dictionary<Type, string> { [typeof(Sample)] = "s" };
        var source = new EntityAliasRegister(data, typeof(Sample));
        EntityAliasRegister clone = null;
        EntityAliasRegister constructed = null;
        for (var i = 0; i < warmups; i++)
        {
            clone = (EntityAliasRegister)source.Clone();
            constructed = new EntityAliasRegister(data, typeof(Sample));
        }
        GC.KeepAlive(clone);
        GC.KeepAlive(constructed);

        // Act
        var cloneStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            clone = (EntityAliasRegister)source.Clone();
        var cloneBytes = GC.GetAllocatedBytesForCurrentThread() - cloneStart;
        GC.KeepAlive(clone);

        var constructorStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            constructed = new EntityAliasRegister(data, typeof(Sample));
        var constructorBytes = GC.GetAllocatedBytesForCurrentThread() - constructorStart;
        GC.KeepAlive(constructed);

        // Assert
        Assert.True(cloneBytes <= constructorBytes,
            $"克隆不应额外复制一份别名集合；Clone={cloneBytes / iterations} B/op，构造={constructorBytes / iterations} B/op。");
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

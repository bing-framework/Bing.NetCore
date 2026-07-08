namespace Bing.Domain.Entities;

/// <summary>
/// 实体帮助类 测试
/// </summary>
public class EntityHelperTest
{
    #region 类型检查

    /// <summary>
    /// 测试目的：IsEntity - 实体类型应返回 true。
    /// </summary>
    [Fact]
    public void IsEntity_EntityType_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        EntityHelper.IsEntity(typeof(AggregateRootSample)).ShouldBeTrue();
        EntityHelper.IsEntity(typeof(IntAggregateRootSample)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsEntity - 非实体类型应返回 false。
    /// </summary>
    [Fact]
    public void IsEntity_NonEntityType_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        EntityHelper.IsEntity(typeof(string)).ShouldBeFalse();
        EntityHelper.IsEntity(typeof(int)).ShouldBeFalse();
        EntityHelper.IsEntity(typeof(object)).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：IsEntity - type 为 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void IsEntity_NullType_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => EntityHelper.IsEntity(null));
    }

    /// <summary>
    /// 测试目的：IsEntityWithId - 带 ID 的实体类型应返回 true，且输出正确的 keyType。
    /// </summary>
    [Fact]
    public void IsEntityWithId_EntityWithGuidId_ShouldReturnTrueAndKeyType()
    {
        // Arrange & Act
        var result = EntityHelper.IsEntityWithId(typeof(AggregateRootSample), out var keyType);

        // Assert
        result.ShouldBeTrue();
        keyType.ShouldBe(typeof(Guid));
    }

    /// <summary>
    /// 测试目的：IsEntityWithId - int 主键实体应返回 true，且 keyType 为 int。
    /// </summary>
    [Fact]
    public void IsEntityWithId_EntityWithIntId_ShouldReturnIntKeyType()
    {
        // Arrange & Act
        var result = EntityHelper.IsEntityWithId(typeof(IntAggregateRootSample), out var keyType);

        // Assert
        result.ShouldBeTrue();
        keyType.ShouldBe(typeof(int));
    }

    /// <summary>
    /// 测试目的：IsEntityWithId(type) 单参数重载 - 非实体类型应返回 false。
    /// </summary>
    [Fact]
    public void IsEntityWithId_NonEntityType_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        EntityHelper.IsEntityWithId(typeof(string)).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：CheckEntity - 非实体类型应抛出 ArgumentException。
    /// </summary>
    [Fact]
    public void CheckEntity_NonEntityType_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => EntityHelper.CheckEntity(typeof(string)));
    }

    /// <summary>
    /// 测试目的：CheckEntity - 实体类型应不抛出异常。
    /// </summary>
    [Fact]
    public void CheckEntity_EntityType_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => EntityHelper.CheckEntity(typeof(AggregateRootSample)));
    }

    #endregion

    #region 主键类型查找

    /// <summary>
    /// 测试目的：FindPrimaryKeyType - Guid 主键实体应返回 typeof(Guid)。
    /// </summary>
    [Fact]
    public void FindPrimaryKeyType_GuidEntity_ShouldReturnGuidType()
    {
        // Arrange & Act
        var keyType = EntityHelper.FindPrimaryKeyType<AggregateRootSample>();

        // Assert
        keyType.ShouldBe(typeof(Guid));
    }

    /// <summary>
    /// 测试目的：FindPrimaryKeyType - int 主键实体应返回 typeof(int)。
    /// </summary>
    [Fact]
    public void FindPrimaryKeyType_IntEntity_ShouldReturnIntType()
    {
        // Arrange & Act
        var keyType = EntityHelper.FindPrimaryKeyType<IntAggregateRootSample>();

        // Assert
        keyType.ShouldBe(typeof(int));
    }

    #endregion

    #region ID 生成

    /// <summary>
    /// 测试目的：CreateGuid 应返回非空 Guid，且每次调用结果不同。
    /// </summary>
    [Fact]
    public void CreateGuid_ShouldReturnNonEmptyGuid()
    {
        // Arrange & Act
        var id1 = EntityHelper.CreateGuid();
        var id2 = EntityHelper.CreateGuid();

        // Assert
        id1.ShouldNotBe(Guid.Empty);
        id2.ShouldNotBe(Guid.Empty);
        id1.ShouldNotBe(id2);
    }

    /// <summary>
    /// 测试目的：CreateKey&lt;Guid&gt; 应返回非空 Guid。
    /// </summary>
    [Fact]
    public void CreateKey_Guid_ShouldReturnNonEmpty()
    {
        // Arrange & Act
        var id = EntityHelper.CreateKey<Guid>();

        // Assert
        id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>
    /// 测试目的：CreateKey&lt;string&gt; 应返回非空字符串（默认为 Guid 字符串）。
    /// </summary>
    [Fact]
    public void CreateKey_String_ShouldReturnNonEmpty()
    {
        // Arrange & Act
        var id = EntityHelper.CreateKey<string>();

        // Assert
        id.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：RegisterIdGenerator 传入 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void RegisterIdGenerator_NullGenerator_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            EntityHelper.RegisterIdGenerator<Guid>(null));
    }

    /// <summary>
    /// 测试目的：更改 GuidGenerateFunc 后，CreateGuid 应使用新的生成函数。
    /// </summary>
    [Fact]
    public void GuidGenerateFunc_Custom_ShouldBeUsedByCreateGuid()
    {
        // Arrange
        var fixedId = new Guid("11111111-1111-1111-1111-111111111111");
        var original = EntityHelper.GuidGenerateFunc;
        try
        {
            EntityHelper.GuidGenerateFunc = () => fixedId;

            // Act
            var id = EntityHelper.CreateGuid();

            // Assert
            id.ShouldBe(fixedId);
        }
        finally
        {
            // 恢复原始生成函数，避免影响其他测试
            EntityHelper.GuidGenerateFunc = original;
        }
    }

    #endregion

    #region 主键检查

    /// <summary>
    /// 测试目的：HasDefaultId - Guid.Empty 的实体应返回 true（视为默认值/瞬时对象）。
    /// </summary>
    [Fact]
    public void HasDefaultId_GuidEmpty_ShouldReturnTrue()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.Empty);

        // Act & Assert
        EntityHelper.HasDefaultId(sample).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：HasDefaultId - 非空 Guid 实体应返回 false。
    /// </summary>
    [Fact]
    public void HasDefaultId_NonEmptyGuid_ShouldReturnFalse()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.NewGuid());

        // Act & Assert
        EntityHelper.HasDefaultId(sample).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：HasDefaultId - int 默认值（0）的实体应返回 true。
    /// </summary>
    [Fact]
    public void HasDefaultId_IntDefault_ShouldReturnTrue()
    {
        // Arrange
        var sample = new IntAggregateRootSample(0);

        // Act & Assert
        EntityHelper.HasDefaultId(sample).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：HasDefaultId - 负数 int ID 应返回 true（数值 ≤ 0 视为默认值）。
    /// </summary>
    [Fact]
    public void HasDefaultId_NegativeInt_ShouldReturnTrue()
    {
        // Arrange
        var sample = new IntAggregateRootSample(-1);

        // Act & Assert
        EntityHelper.HasDefaultId(sample).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：HasDefaultId - 正数 int ID 应返回 false。
    /// </summary>
    [Fact]
    public void HasDefaultId_PositiveInt_ShouldReturnFalse()
    {
        // Arrange
        var sample = new IntAggregateRootSample(1);

        // Act & Assert
        EntityHelper.HasDefaultId(sample).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：HasDefaultKeys - 实体键为 Guid.Empty 时应返回 true。
    /// </summary>
    [Fact]
    public void HasDefaultKeys_GuidEmpty_ShouldReturnTrue()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.Empty);

        // Act & Assert
        EntityHelper.HasDefaultKeys(sample).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：HasDefaultKeys - 实体键为有效 Guid 时应返回 false。
    /// </summary>
    [Fact]
    public void HasDefaultKeys_NonEmptyGuid_ShouldReturnFalse()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.NewGuid());

        // Act & Assert
        EntityHelper.HasDefaultKeys(sample).ShouldBeFalse();
    }

    #endregion

    #region 实体相等性

    /// <summary>
    /// 测试目的：EntityEquals - 任意参数为 null 时均返回 false。
    /// </summary>
    [Fact]
    public void EntityEquals_NullArgument_ShouldReturnFalse()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.NewGuid());

        // Act & Assert
        EntityHelper.EntityEquals(sample, null).ShouldBeFalse();
        EntityHelper.EntityEquals(null, sample).ShouldBeFalse();
        EntityHelper.EntityEquals(null, null).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：EntityEquals - 同一引用应返回 true。
    /// </summary>
    [Fact]
    public void EntityEquals_SameReference_ShouldReturnTrue()
    {
        // Arrange
        var sample = new AggregateRootSample(Guid.NewGuid());

        // Act & Assert
        EntityHelper.EntityEquals(sample, sample).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：EntityEquals - 相同 ID 的不同实例应返回 true。
    /// </summary>
    [Fact]
    public void EntityEquals_SameId_DifferentInstance_ShouldReturnTrue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sample1 = new AggregateRootSample(id);
        var sample2 = new AggregateRootSample(id);

        // Act & Assert
        EntityHelper.EntityEquals(sample1, sample2).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：EntityEquals - 不同 ID 的实例应返回 false。
    /// </summary>
    [Fact]
    public void EntityEquals_DifferentId_ShouldReturnFalse()
    {
        // Arrange
        var sample1 = new AggregateRootSample(Guid.NewGuid());
        var sample2 = new AggregateRootSample(Guid.NewGuid());

        // Act & Assert
        EntityHelper.EntityEquals(sample1, sample2).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：EntityEquals - 两个实体 ID 均为默认值（瞬时对象）时应返回 false。
    /// </summary>
    [Fact]
    public void EntityEquals_BothDefaultId_ShouldReturnFalse()
    {
        // Arrange
        var sample1 = new AggregateRootSample(Guid.Empty);
        var sample2 = new AggregateRootSample(Guid.Empty);

        // Act & Assert
        EntityHelper.EntityEquals(sample1, sample2).ShouldBeFalse();
    }

    #endregion
}

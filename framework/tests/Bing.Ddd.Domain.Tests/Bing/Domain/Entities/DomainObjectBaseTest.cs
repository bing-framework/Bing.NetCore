using Bing.Tests.Samples;

namespace Bing.Domain.Entities;

/// <summary>
/// 测试目的：验证 DomainObjectBase.ToString() / GetChanges() 与 AddDescriptions / AddChanges 的联动行为。
/// 通过 AggregateRootSample（测试项目内已有的具体实现）进行黑盒验证。
/// </summary>
public class DomainObjectBaseTest
{
    // =====================================================================
    //  ToString — 依赖 AddDescriptions / DescriptionContext
    // =====================================================================

    /// <summary>
    /// 测试目的：新建的聚合根未设置任何字段时，ToString 不应抛异常，结果可以为空。
    /// </summary>
    [Fact]
    public void ToString_EmptyEntity_ShouldNotThrow()
    {
        // Arrange
        var entity = new AggregateRootSample();

        // Act & Assert
        Should.NotThrow(() => entity.ToString());
    }

    /// <summary>
    /// 测试目的：设置 Name 字段后，ToString 应包含 Id 和姓名信息（AggregateRootSample.AddDescriptions 实现）。
    /// </summary>
    [Fact]
    public void ToString_WithNameSet_ShouldContainIdAndName()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new AggregateRootSample(id) { Name = "Alice", EnglishName = "alice" };

        // Act
        var result = entity.ToString();

        // Assert
        result.ShouldContain(id.ToString());
        result.ShouldContain("Alice");
    }

    /// <summary>
    /// 测试目的：多次调用 ToString，每次均应重新填充描述（FlushCache + AddDescriptions），结果一致。
    /// </summary>
    [Fact]
    public void ToString_CalledTwice_ShouldReturnConsistentResult()
    {
        // Arrange
        var entity = new AggregateRootSample(Guid.NewGuid()) { Name = "Bob", EnglishName = "bob" };

        // Act
        var first = entity.ToString();
        var second = entity.ToString();

        // Assert
        first.ShouldBe(second);
    }

    /// <summary>
    /// 测试目的：Name 变更后，再次调用 ToString 应反映新值而非旧值。
    /// </summary>
    [Fact]
    public void ToString_AfterNameChange_ShouldReflectNewName()
    {
        // Arrange
        var entity = new AggregateRootSample(Guid.NewGuid()) { Name = "Old", EnglishName = "old" };
        _ = entity.ToString(); // 第一次调用

        // Act
        entity.Name = "New";
        var result = entity.ToString();

        // Assert
        result.ShouldContain("New");
        result.ShouldNotContain("Old");
    }

    // =====================================================================
    //  GetChanges — 依赖 AddChanges / ChangeTrackingContext
    // =====================================================================

    /// <summary>
    /// 测试目的：两个属性全相同时，GetChanges 应返回空集合（无变更）。
    /// </summary>
    [Fact]
    public void GetChanges_SameValues_ShouldReturnEmpty()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = AggregateRootSample.CreateSample(id);
        var other = AggregateRootSample.CreateSample(id);

        // Act
        var changes = original.GetChanges(other);

        // Assert
        changes.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：Name 不同时，GetChanges 应包含 "姓名" 这条变更记录。
    /// </summary>
    [Fact]
    public void GetChanges_NameChanged_ShouldContainNameChange()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = AggregateRootSample.CreateSample(id);    // Name = "TestName"
        var updated = AggregateRootSample.CreateSample2(id);    // Name = "TestName2"

        // Act
        var changes = original.GetChanges(updated);

        // Assert
        changes.ShouldNotBeEmpty();
        changes.Any(c => c.PropertyName == "Name").ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：GetChanges 传入 null 时应返回空集合，不抛异常。
    /// </summary>
    [Fact]
    public void GetChanges_NullOther_ShouldReturnEmpty()
    {
        // Arrange
        var original = AggregateRootSample.CreateSample();

        // Act & Assert
        var changes = Should.NotThrow(() => original.GetChanges(null));
        changes.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：多次调用 GetChanges 应返回一致结果（FlushCache 保证每次重置）。
    /// </summary>
    [Fact]
    public void GetChanges_CalledTwice_ShouldReturnConsistentCount()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = AggregateRootSample.CreateSample(id);
        var updated = AggregateRootSample.CreateSample2(id);

        // Act
        var first = original.GetChanges(updated).Count();
        var second = original.GetChanges(updated).Count();

        // Assert
        first.ShouldBe(second);
    }

    /// <summary>
    /// 测试目的：GetChanges 旧值/新值应经过 toLower + Trim 处理（ChangeTrackingContext.Add 规范）。
    /// </summary>
    [Fact]
    public void GetChanges_ValuesAreLowercasedAndTrimmed()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = AggregateRootSample.CreateSample(id);    // Name = "TestName"
        var updated = AggregateRootSample.CreateSample2(id);    // Name = "TestName2"

        // Act
        var changes = original.GetChanges(updated).ToList();
        var nameChange = changes.FirstOrDefault(c => c.PropertyName == "Name");

        // Assert
        nameChange.ShouldNotBeNull();
        nameChange.OldValue.ShouldBe("testname");
        nameChange.NewValue.ShouldBe("testname2");
    }
}

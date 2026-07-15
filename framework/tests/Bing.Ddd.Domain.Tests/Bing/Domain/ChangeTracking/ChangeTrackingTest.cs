namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 变更跟踪 测试
/// </summary>
public class ChangeTrackingTest
{
    #region ChangedValueDescriptor

    /// <summary>
    /// 测试目的：验证变更值描述符的属性赋值正确。
    /// </summary>
    [Fact]
    public void ChangedValueDescriptor_Properties_ShouldSetCorrectly()
    {
        // Arrange & Act
        var descriptor = new ChangedValueDescriptor("Name", "姓名", "Alice", "Bob");

        // Assert
        descriptor.PropertyName.ShouldBe("Name");
        descriptor.Description.ShouldBe("姓名");
        descriptor.OldValue.ShouldBe("Alice");
        descriptor.NewValue.ShouldBe("Bob");
    }

    /// <summary>
    /// 测试目的：验证变更值描述符 ToString 包含属性名、描述、旧值、新值。
    /// </summary>
    [Fact]
    public void ChangedValueDescriptor_ToString_ShouldContainKeyInfo()
    {
        // Arrange
        var descriptor = new ChangedValueDescriptor("Name", "姓名", "Alice", "Bob");

        // Act
        var result = descriptor.ToString();

        // Assert
        result.ShouldContain("Name");
        result.ShouldContain("姓名");
        result.ShouldContain("Alice");
        result.ShouldContain("Bob");
    }

    #endregion

    #region ChangedValueDescriptorCollection

    /// <summary>
    /// 测试目的：新建集合应为空。
    /// </summary>
    [Fact]
    public void Collection_New_ShouldBeEmpty()
    {
        // Arrange & Act
        var collection = new ChangedValueDescriptorCollection();

        // Assert
        collection.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：向集合添加有效记录后，应可正常遍历到该记录。
    /// </summary>
    [Fact]
    public void Collection_Add_ShouldContainItem()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();

        // Act
        collection.Add("Name", "姓名", "Alice", "Bob");

        // Assert
        collection.Count().ShouldBe(1);
        collection.First().PropertyName.ShouldBe("Name");
        collection.First().OldValue.ShouldBe("Alice");
        collection.First().NewValue.ShouldBe("Bob");
    }

    /// <summary>
    /// 测试目的：属性名为空时，不应被添加到集合（4 参数重载）。
    /// </summary>
    [Fact]
    public void Collection_Add_EmptyPropertyName_ShouldNotAdd()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();

        // Act
        collection.Add("", "描述", "old", "new");
        collection.Add("   ", "描述", "old", "new");

        // Assert
        collection.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：Descriptor 描述为空时（1 参数重载），不应被添加到集合。
    /// </summary>
    [Fact]
    public void Collection_AddDescriptor_EmptyDescription_ShouldNotAdd()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();
        var descriptor = new ChangedValueDescriptor("Name", "", "old", "new");

        // Act
        collection.Add(descriptor);

        // Assert
        collection.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：FlushCache 后集合应被清空。
    /// </summary>
    [Fact]
    public void Collection_FlushCache_ShouldClear()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();
        collection.Add("Name", "姓名", "Alice", "Bob");

        // Act
        collection.FlushCache();

        // Assert
        collection.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：拷贝构造函数应将原集合的记录复制到新实例中。
    /// </summary>
    [Fact]
    public void Collection_CopyConstructor_ShouldCopyItems()
    {
        // Arrange
        var original = new ChangedValueDescriptorCollection();
        original.Add("Name", "姓名", "Alice", "Bob");

        // Act
        var copy = new ChangedValueDescriptorCollection(original);

        // Assert
        copy.Count().ShouldBe(1);
        copy.First().PropertyName.ShouldBe("Name");
    }

    /// <summary>
    /// 测试目的：空集合的 ToString 应返回空字符串。
    /// </summary>
    [Fact]
    public void Collection_ToString_Empty_ShouldReturnEmpty()
    {
        // Arrange & Act
        var collection = new ChangedValueDescriptorCollection();

        // Assert
        collection.ToString().ShouldBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：有记录的集合，ToString 应包含变更信息。
    /// </summary>
    [Fact]
    public void Collection_ToString_WithItems_ShouldContainInfo()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();
        collection.Add("Name", "姓名", "Alice", "Bob");

        // Act
        var result = collection.ToString();

        // Assert
        result.ShouldContain("姓名");
        result.ShouldContain("Alice");
        result.ShouldContain("Bob");
    }

    #endregion

    #region ChangeTrackingContext

    /// <summary>
    /// 测试目的：值相等时，不应添加变更记录。
    /// </summary>
    [Fact]
    public void Context_Add_EqualValues_ShouldNotTrack()
    {
        // Arrange
        var ctx = new ChangeTrackingContext();

        // Act
        ctx.Add("Name", "姓名", "Alice", "Alice");

        // Assert
        ctx.GetChangedValueDescriptor().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：值不同时，应添加变更记录，且旧值/新值经过 Trim + toLower 处理。
    /// </summary>
    [Fact]
    public void Context_Add_DifferentValues_ShouldTrack()
    {
        // Arrange
        var ctx = new ChangeTrackingContext();

        // Act
        ctx.Add("Name", "姓名", "Alice", "Bob");

        // Assert
        var descriptors = ctx.GetChangedValueDescriptor().ToList();
        descriptors.Count.ShouldBe(1);
        descriptors[0].PropertyName.ShouldBe("Name");
        descriptors[0].OldValue.ShouldBe("alice");
        descriptors[0].NewValue.ShouldBe("bob");
    }

    /// <summary>
    /// 测试目的：旧值为 null、新值非空时，应记录变更。
    /// </summary>
    [Fact]
    public void Context_Add_NullToValue_ShouldTrack()
    {
        // Arrange
        var ctx = new ChangeTrackingContext();

        // Act
        ctx.Add<string>("Name", "姓名", null, "Bob");

        // Assert
        ctx.GetChangedValueDescriptor().Count().ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：大小写不同但内容相同的值应视为相等，不应记录变更。
    /// </summary>
    [Fact]
    public void Context_Add_SameValueDifferentCase_ShouldNotTrack()
    {
        // Arrange
        var ctx = new ChangeTrackingContext();

        // Act
        ctx.Add("Name", "姓名", "Alice", "ALICE");

        // Assert
        ctx.GetChangedValueDescriptor().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：Output 应包含变更信息关键字段，ToString 与 Output 输出一致。
    /// </summary>
    [Fact]
    public void Context_Output_ShouldContainChangedInfo()
    {
        // Arrange
        var ctx = new ChangeTrackingContext();
        ctx.Add("Name", "姓名", "Alice", "Bob");

        // Act
        var output = ctx.Output();

        // Assert
        output.ShouldContain("Name");
        output.ShouldContain("姓名");
        ctx.ToString().ShouldBe(output);
    }

    /// <summary>
    /// 测试目的：空变更上下文的 Output 应返回空字符串。
    /// </summary>
    [Fact]
    public void Context_Output_Empty_ShouldReturnEmpty()
    {
        // Arrange & Act
        var ctx = new ChangeTrackingContext();

        // Assert
        ctx.Output().ShouldBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：通过 ChangedValueDescriptorCollection 构造的上下文，应包含已有的变更记录。
    /// </summary>
    [Fact]
    public void Context_CopyConstructor_ShouldCopyDescriptors()
    {
        // Arrange
        var collection = new ChangedValueDescriptorCollection();
        collection.Add("Name", "姓名", "Alice", "Bob");

        // Act
        var ctx = new ChangeTrackingContext(collection);

        // Assert
        ctx.GetChangedValueDescriptor().Count().ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：null 集合作为构造参数，不应抛出异常，上下文应为空。
    /// </summary>
    [Fact]
    public void Context_NullCollection_ShouldNotThrow_AndBeEmpty()
    {
        // Arrange & Act
        var ctx = new ChangeTrackingContext(null);

        // Assert
        ctx.GetChangedValueDescriptor().ShouldBeEmpty();
    }

    #endregion
}

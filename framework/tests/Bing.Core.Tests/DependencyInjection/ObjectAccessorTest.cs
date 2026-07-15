using Bing.DependencyInjection;
using Shouldly;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// ObjectAccessor&lt;T&gt; 对象访问器测试
/// </summary>
public class ObjectAccessorTest
{
    // ==================== 默认构造函数 ====================

    /// <summary>
    /// 测试目的：无参构造函数创建后，Value 应为类型默认值（引用类型为 null）。
    /// </summary>
    [Fact]
    public void DefaultConstructor_ReferenceType_ValueIsNull()
    {
        // Arrange & Act
        var accessor = new ObjectAccessor<string>();

        // Assert
        accessor.Value.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：无参构造函数创建后，值类型 Value 应为默认值（int 为 0）。
    /// </summary>
    [Fact]
    public void DefaultConstructor_ValueType_ValueIsDefault()
    {
        // Arrange & Act
        var accessor = new ObjectAccessor<int>();

        // Assert
        accessor.Value.ShouldBe(0);
    }

    // ==================== 带参构造函数 ====================

    /// <summary>
    /// 测试目的：通过带参构造函数传入值，Value 应与传入值相同。
    /// </summary>
    [Fact]
    public void ParameterizedConstructor_SetsValueCorrectly()
    {
        // Arrange
        const string expected = "test-value";

        // Act
        var accessor = new ObjectAccessor<string>(expected);

        // Assert
        accessor.Value.ShouldBe(expected);
    }

    /// <summary>
    /// 测试目的：通过带参构造函数传入对象实例，Value 引用相同对象。
    /// </summary>
    [Fact]
    public void ParameterizedConstructor_ObjectInstance_SameReference()
    {
        // Arrange
        var obj = new List<int> { 1, 2, 3 };

        // Act
        var accessor = new ObjectAccessor<List<int>>(obj);

        // Assert
        accessor.Value.ShouldBeSameAs(obj);
    }

    // ==================== Value 属性可写 ====================

    /// <summary>
    /// 测试目的：Value 属性可以在创建后被重新赋值。
    /// </summary>
    [Fact]
    public void Value_Settable_AfterCreation()
    {
        // Arrange
        var accessor = new ObjectAccessor<string>("old");

        // Act
        accessor.Value = "new";

        // Assert
        accessor.Value.ShouldBe("new");
    }

    /// <summary>
    /// 测试目的：Value 可以被设为 null（引用类型）。
    /// </summary>
    [Fact]
    public void Value_CanBeSetToNull()
    {
        // Arrange
        var accessor = new ObjectAccessor<string>("initial");

        // Act
        accessor.Value = null;

        // Assert
        accessor.Value.ShouldBeNull();
    }

    // ==================== IObjectAccessor<T> 接口 ====================

    /// <summary>
    /// 测试目的：ObjectAccessor 可以通过 IObjectAccessor&lt;T&gt; 接口使用。
    /// </summary>
    [Fact]
    public void ObjectAccessor_Implements_IObjectAccessorInterface()
    {
        // Arrange & Act
        IObjectAccessor<string> accessor = new ObjectAccessor<string>("hello");

        // Assert
        accessor.Value.ShouldBe("hello");
        accessor.ShouldBeAssignableTo<IObjectAccessor<string>>();
    }
}

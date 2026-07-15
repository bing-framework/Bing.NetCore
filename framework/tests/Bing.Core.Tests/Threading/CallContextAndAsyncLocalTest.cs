using Bing.Threading;
using Shouldly;
using Xunit;

namespace Bing.Threading;

// =========================================================================
//  CallContext Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 CallContext 的 SetValue / GetValue / Remove / Clear 语义正确性。
/// </summary>
public class CallContextTest
{
    // 每个测试用唯一键名，避免并发/顺序干扰
    private static string Key(string suffix) => $"CallContext_Test_{suffix}_{Guid.NewGuid():N}";

    /// <summary>
    /// 测试目的：SetValue 后 GetValue 应返回同一对象。
    /// </summary>
    [Fact]
    public void SetValue_ThenGetValue_ShouldReturnSameObject()
    {
        // Arrange
        var key = Key("set");
        var value = new object();

        // Act
        CallContext.SetValue(key, value);

        // Assert
        CallContext.GetValue(key).ShouldBeSameAs(value);
    }

    /// <summary>
    /// 测试目的：未设置的 key GetValue 应返回 null。
    /// </summary>
    [Fact]
    public void GetValue_NotSet_ShouldReturnNull()
    {
        var key = Key("notset");
        CallContext.GetValue(key).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：SetValue 设置的值可以被覆盖，GetValue 应返回最新值。
    /// </summary>
    [Fact]
    public void SetValue_Overwrite_ShouldReturnLatestValue()
    {
        // Arrange
        var key = Key("overwrite");
        CallContext.SetValue(key, "first");

        // Act
        CallContext.SetValue(key, "second");

        // Assert
        CallContext.GetValue(key).ShouldBe("second");
    }

    /// <summary>
    /// 测试目的：Remove 后 GetValue 应返回 null。
    /// </summary>
    [Fact]
    public void Remove_AfterSet_ShouldMakeValueNull()
    {
        // Arrange
        var key = Key("remove");
        CallContext.SetValue(key, "data");

        // Act
        CallContext.Remove(key);

        // Assert
        CallContext.GetValue(key).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Remove 不存在的 key 时不应抛出异常。
    /// </summary>
    [Fact]
    public void Remove_NonExistentKey_ShouldNotThrow()
    {
        var key = Key("remove_nonexistent");
        Should.NotThrow(() => CallContext.Remove(key));
    }

    /// <summary>
    /// 测试目的：SetValue 支持 null 值，GetValue 应返回 null。
    /// </summary>
    [Fact]
    public void SetValue_NullValue_GetValue_ShouldReturnNull()
    {
        // Arrange
        var key = Key("null_value");

        // Act
        CallContext.SetValue(key, null);

        // Assert
        CallContext.GetValue(key).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：不同 key 的值相互独立，不会相互干扰。
    /// </summary>
    [Fact]
    public void SetValue_DifferentKeys_ShouldBeIndependent()
    {
        // Arrange
        var key1 = Key("key1");
        var key2 = Key("key2");

        // Act
        CallContext.SetValue(key1, "value1");
        CallContext.SetValue(key2, "value2");

        // Assert
        CallContext.GetValue(key1).ShouldBe("value1");
        CallContext.GetValue(key2).ShouldBe("value2");
    }

    /// <summary>
    /// 测试目的：SetValue 支持存储不同类型的对象。
    /// </summary>
    [Fact]
    public void SetValue_SupportsDifferentTypes()
    {
        // Arrange
        var intKey = Key("int");
        var listKey = Key("list");

        // Act
        CallContext.SetValue(intKey, 42);
        CallContext.SetValue(listKey, new List<string> { "a", "b" });

        // Assert
        CallContext.GetValue(intKey).ShouldBe(42);
        ((List<string>)CallContext.GetValue(listKey)).Count.ShouldBe(2);
    }
}

// =========================================================================
//  AsyncLocalExtensions Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 AsyncLocal&lt;T&gt;.SetScoped 的范围设定与恢复行为。
/// </summary>
public class AsyncLocalExtensionsTest
{
    /// <summary>
    /// 测试目的：SetScoped 设置新值后，在范围内 AsyncLocal 值应为新值。
    /// </summary>
    [Fact]
    public void SetScoped_InScope_ShouldHaveNewValue()
    {
        // Arrange
        var local = new AsyncLocal<string>();
        local.Value = "original";

        // Act & Assert
        using (local.SetScoped("scoped"))
        {
            local.Value.ShouldBe("scoped");
        }
    }

    /// <summary>
    /// 测试目的：离开 SetScoped 范围后，AsyncLocal 应恢复为原值。
    /// </summary>
    [Fact]
    public void SetScoped_AfterDispose_ShouldRestoreOriginalValue()
    {
        // Arrange
        var local = new AsyncLocal<string>();
        local.Value = "original";

        // Act
        var scope = local.SetScoped("new-value");
        scope.Dispose();

        // Assert
        local.Value.ShouldBe("original");
    }

    /// <summary>
    /// 测试目的：嵌套 SetScoped 时，内层结束后恢复外层值，外层结束后恢复原始值。
    /// </summary>
    [Fact]
    public void SetScoped_Nested_ShouldRestoreLayerByLayer()
    {
        // Arrange
        var local = new AsyncLocal<int>();
        local.Value = 0;

        // Act & Assert
        using (local.SetScoped(1))
        {
            local.Value.ShouldBe(1);

            using (local.SetScoped(2))
            {
                local.Value.ShouldBe(2);
            }

            local.Value.ShouldBe(1); // 恢复外层
        }

        local.Value.ShouldBe(0); // 恢复原始值
    }

    /// <summary>
    /// 测试目的：原始值为 null 时，SetScoped 结束后应恢复为 null。
    /// </summary>
    [Fact]
    public void SetScoped_OriginalNull_ShouldRestoreNull()
    {
        // Arrange
        var local = new AsyncLocal<string>();
        // local.Value == null by default

        // Act
        using (local.SetScoped("temp"))
        {
            local.Value.ShouldBe("temp");
        }

        // Assert
        local.Value.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：SetScoped 返回的 IDisposable 不为 null。
    /// </summary>
    [Fact]
    public void SetScoped_ReturnsNonNullDisposable()
    {
        var local = new AsyncLocal<int>();
        var scope = local.SetScoped(99);
        scope.ShouldNotBeNull();
        scope.Dispose(); // 不抛
    }
}

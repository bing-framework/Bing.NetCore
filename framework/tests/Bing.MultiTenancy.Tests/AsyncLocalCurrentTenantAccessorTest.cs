using Shouldly;

namespace Bing.MultiTenancy;

/// <summary>
/// AsyncLocalCurrentTenantAccessor 基于 AsyncLocal 的当前租户访问器测试
/// </summary>
public class AsyncLocalCurrentTenantAccessorTest
{
    // ==================== 默认值 ====================

    /// <summary>
    /// 测试目的：新建访问器后，Current 应为 null（无租户）。
    /// </summary>
    [Fact]
    public void Current_DefaultIsNull()
    {
        // Arrange & Act
        var accessor = new AsyncLocalCurrentTenantAccessor();

        // Assert
        accessor.Current.ShouldBeNull();
    }

    // ==================== 读写 ====================

    /// <summary>
    /// 测试目的：设置 Current 后，再次读取应返回相同的 BasicTenantInfo。
    /// </summary>
    [Fact]
    public void Current_Set_CanBeReadBack()
    {
        // Arrange
        var accessor = new AsyncLocalCurrentTenantAccessor();
        var info = new BasicTenantInfo("tid-001", "TenantAlpha");

        // Act
        accessor.Current = info;

        // Assert
        accessor.Current.ShouldNotBeNull();
        accessor.Current!.TenantId.ShouldBe("tid-001");
        accessor.Current!.Name.ShouldBe("TenantAlpha");
    }

    /// <summary>
    /// 测试目的：将 Current 设为 null，再读取应返回 null。
    /// </summary>
    [Fact]
    public void Current_SetToNull_ReturnsNull()
    {
        // Arrange
        var accessor = new AsyncLocalCurrentTenantAccessor();
        accessor.Current = new BasicTenantInfo("tid-001");

        // Act
        accessor.Current = null;

        // Assert
        accessor.Current.ShouldBeNull();
    }

    // ==================== 静态 Instance ====================

    /// <summary>
    /// 测试目的：AsyncLocalCurrentTenantAccessor.Instance 不为 null，且是单例。
    /// </summary>
    [Fact]
    public void Instance_IsNotNull_And_IsSingleton()
    {
        // Act
        var a = AsyncLocalCurrentTenantAccessor.Instance;
        var b = AsyncLocalCurrentTenantAccessor.Instance;

        // Assert
        a.ShouldNotBeNull();
        a.ShouldBeSameAs(b);
    }

    // ==================== AsyncLocal 隔离 ====================

    /// <summary>
    /// 测试目的：子 Task 内修改 Current，不影响父上下文（AsyncLocal 隔离语义）。
    /// </summary>
    [Fact]
    public async Task Current_ChildTask_ModificationDoesNotAffectParent()
    {
        // Arrange：每个测试用独立访问器，避免静态状态污染
        var accessor = new AsyncLocalCurrentTenantAccessor();
        accessor.Current = new BasicTenantInfo("parent-tid");

        string? childSeenId = null;

        // Act：子任务修改访问器
        await Task.Run(() =>
        {
            accessor.Current = new BasicTenantInfo("child-tid");
            childSeenId = accessor.Current?.TenantId;
        });

        // Assert：子任务能看到自己设置的值
        childSeenId.ShouldBe("child-tid");

        // Assert：父上下文仍为 parent-tid（AsyncLocal 值不回流）
        accessor.Current?.TenantId.ShouldBe("parent-tid");
    }

    /// <summary>
    /// 测试目的：并发多个 Task，每个 Task 独立设置 Current，相互之间应不干扰。
    /// </summary>
    [Fact]
    public async Task Current_MultipleConcurrentTasks_AreIsolated()
    {
        // Arrange
        var accessor = new AsyncLocalCurrentTenantAccessor();
        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, string?>();

        // Act
        var tasks = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            accessor.Current = new BasicTenantInfo($"tenant-{i}");
            await Task.Delay(5); // 给其他任务插入机会
            results[i] = accessor.Current?.TenantId;
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert：每个任务看到的是自己设置的租户
        for (var i = 0; i < 5; i++)
            results[i].ShouldBe($"tenant-{i}");
    }
}

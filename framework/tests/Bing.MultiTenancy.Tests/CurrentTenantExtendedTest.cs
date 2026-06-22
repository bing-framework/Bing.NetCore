using Shouldly;

namespace Bing.MultiTenancy;

/// <summary>
/// CurrentTenant 扩展行为测试（补充 CurrentTenantTest 中未覆盖的边界）
/// </summary>
public class CurrentTenantExtendedTest
{
    /// <summary>
    /// 创建独立的 ICurrentTenant 实例（不依赖 DI 容器）
    /// </summary>
    private static ICurrentTenant CreateCurrentTenant() =>
        new CurrentTenant(new AsyncLocalCurrentTenantAccessor());

    // ==================== IsAvailable ====================

    /// <summary>
    /// 测试目的：未设置租户时，IsAvailable 应为 false。
    /// </summary>
    [Fact]
    public void IsAvailable_WhenNoTenantSet_IsFalse()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Assert
        tenant.IsAvailable.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：设置非空租户 ID 后，IsAvailable 应为 true。
    /// </summary>
    [Fact]
    public void IsAvailable_WhenTenantIdSet_IsTrue()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Act & Assert
        using (tenant.Change("tenant-001"))
        {
            tenant.IsAvailable.ShouldBeTrue();
        }

        // 离开 scope 后恢复 false
        tenant.IsAvailable.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Change(null) 相当于切换到宿主（IsAvailable 为 false）。
    /// </summary>
    [Fact]
    public void IsAvailable_WhenChangedToNull_IsFalse()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Act：先切换到租户，再切换到宿主
        using (tenant.Change("tenant-001"))
        {
            tenant.IsAvailable.ShouldBeTrue();

            using (tenant.Change(null))
            {
                // 切换到宿主：Id = null，IsAvailable = false
                tenant.Id.ShouldBeNull();
                tenant.IsAvailable.ShouldBeFalse();
            }

            // 恢复到外层 tenant-001
            tenant.IsAvailable.ShouldBeTrue();
        }
    }

    // ==================== Name ====================

    /// <summary>
    /// 测试目的：未设置租户时，Name 应为 null。
    /// </summary>
    [Fact]
    public void Name_WhenNoTenantSet_IsNull()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Assert
        tenant.Name.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Change 传入 name 参数后，Name 属性应返回该名称。
    /// </summary>
    [Fact]
    public void Name_WhenSet_ReturnsCorrectName()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Act
        using (tenant.Change("id-001", "TenantAlpha"))
        {
            // Assert
            tenant.Name.ShouldBe("TenantAlpha");
            tenant.Id.ShouldBe("id-001");
        }
    }

    /// <summary>
    /// 测试目的：不传 name 时，Name 应为 null。
    /// </summary>
    [Fact]
    public void Name_WhenNotProvided_IsNull()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Act
        using (tenant.Change("id-001"))
        {
            // Assert
            tenant.Name.ShouldBeNull();
        }
    }

    // ==================== 嵌套 Change / 恢复 ====================

    /// <summary>
    /// 测试目的：三层嵌套 Change，内层 Dispose 后应逐层恢复到外层租户。
    /// </summary>
    [Fact]
    public void NestedChange_RestoresPreviousTenantOnDispose()
    {
        // Arrange
        var tenant = CreateCurrentTenant();

        // Assert 初始状态
        tenant.Id.ShouldBeNull();

        using (tenant.Change("T1"))
        {
            tenant.Id.ShouldBe("T1");

            using (tenant.Change("T2"))
            {
                tenant.Id.ShouldBe("T2");

                using (tenant.Change("T3"))
                {
                    tenant.Id.ShouldBe("T3");
                }

                tenant.Id.ShouldBe("T2"); // 恢复 T2
            }

            tenant.Id.ShouldBe("T1"); // 恢复 T1
        }

        tenant.Id.ShouldBeNull(); // 恢复默认
    }

    // ==================== 异步上下文隔离 ====================

    /// <summary>
    /// 测试目的：在并发 Task 中分别 Change 租户，不同任务之间不应互相干扰（AsyncLocal 隔离）。
    /// </summary>
    [Fact]
    public async Task Change_AsyncIsolation_DifferentTasksHaveDifferentTenants()
    {
        // Arrange
        var tenant = CreateCurrentTenant();
        var resultA = string.Empty;
        var resultB = string.Empty;

        // Act：两个并发任务分别设置不同的租户
        var taskA = Task.Run(async () =>
        {
            using (tenant.Change("TenantA"))
            {
                await Task.Delay(10);
                resultA = tenant.Id;
            }
        });

        var taskB = Task.Run(async () =>
        {
            using (tenant.Change("TenantB"))
            {
                await Task.Delay(10);
                resultB = tenant.Id;
            }
        });

        await Task.WhenAll(taskA, taskB);

        // Assert：每个任务看到自己设置的租户
        resultA.ShouldBe("TenantA");
        resultB.ShouldBe("TenantB");

        // 父上下文不受影响
        tenant.Id.ShouldBeNull();
    }
}

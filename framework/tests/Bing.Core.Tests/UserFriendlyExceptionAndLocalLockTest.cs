using Bing.Locks;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Bing.Tests;

// =========================================================================
//  UserFriendlyException Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 UserFriendlyException 的构造参数、属性赋值及继承链。
/// </summary>
public class UserFriendlyExceptionTest
{
    /// <summary>
    /// 测试目的：仅传入 message 时，Message 属性应正确设置，其余为默认值。
    /// </summary>
    [Fact]
    public void Constructor_MessageOnly_ShouldSetMessage()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("操作失败");

        // Assert
        ex.Message.ShouldBe("操作失败");
        ex.Code.ShouldBeNull();
        ex.Details.ShouldBeNull();
        ex.InnerException.ShouldBeNull();
        ex.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：传入 code 时，Code 应被正确赋值。
    /// </summary>
    [Fact]
    public void Constructor_WithCode_ShouldSetCode()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("错误", code: "ERR-001");

        // Assert
        ex.Code.ShouldBe("ERR-001");
        ex.Message.ShouldBe("错误");
    }

    /// <summary>
    /// 测试目的：传入 details 时，Details 应被正确赋值。
    /// </summary>
    [Fact]
    public void Constructor_WithDetails_ShouldSetDetails()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("错误", details: "详细描述");

        // Assert
        ex.Details.ShouldBe("详细描述");
    }

    /// <summary>
    /// 测试目的：传入 innerException 时，InnerException 应被正确赋值。
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_ShouldSetInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new UserFriendlyException("外层", innerException: inner);

        // Assert
        ex.InnerException.ShouldBeSameAs(inner);
    }

    /// <summary>
    /// 测试目的：自定义 logLevel 时，LogLevel 应被正确赋值。
    /// </summary>
    [Fact]
    public void Constructor_WithLogLevel_ShouldSetLogLevel()
    {
        var ex = new UserFriendlyException("错误", logLevel: LogLevel.Error);
        ex.LogLevel.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 应实现 IUserFriendlyException 接口。
    /// </summary>
    [Fact]
    public void UserFriendlyException_ShouldImplementIUserFriendlyException()
    {
        var ex = new UserFriendlyException("错误");
        ex.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 应继承自 BusinessException。
    /// </summary>
    [Fact]
    public void UserFriendlyException_ShouldInheritFromBusinessException()
    {
        var ex = new UserFriendlyException("错误");
        ex.ShouldBeAssignableTo<BusinessException>();
    }
}

// =========================================================================
//  LocalLock Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 LocalLock 的加锁/释放锁/ExecuteWithLock 逻辑。
/// </summary>
public class LocalLockTest
{
    private static string Key(string suffix) => $"LocalLock_{suffix}_{Guid.NewGuid():N}";

    // -----------------------------------------------------------------
    //  LockTake / LockRelease
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：LockTake 对未加锁的 key 应返回 true。
    /// </summary>
    [Fact]
    public void LockTake_UnlockedKey_ShouldReturnTrue()
    {
        var key = Key("take");
        var @lock = new LocalLock();
        var result = @lock.LockTake(key, "v1", TimeSpan.FromSeconds(5));

        result.ShouldBeTrue();

        // 清理
        @lock.LockRelease(key, "v1");
    }

    /// <summary>
    /// 测试目的：LockRelease 已锁定的 key 应返回 true。
    /// </summary>
    [Fact]
    public void LockRelease_AfterLockTake_ShouldReturnTrue()
    {
        var key = Key("release");
        var @lock = new LocalLock();
        @lock.LockTake(key, "v1", TimeSpan.FromSeconds(5));

        var released = @lock.LockRelease(key, "v1");

        released.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：对未加锁的 key 调用 LockRelease 应返回 true（无锁可释放，视为成功）。
    /// </summary>
    [Fact]
    public void LockRelease_NeverLocked_ShouldReturnTrue()
    {
        var key = Key("never_locked");
        var @lock = new LocalLock();

        var result = @lock.LockRelease(key, "v1");

        result.ShouldBeTrue();
    }

    // -----------------------------------------------------------------
    //  LockTakeAsync / LockReleaseAsync
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：LockTakeAsync 对未加锁 key 应返回 true。
    /// </summary>
    [Fact]
    public async Task LockTakeAsync_UnlockedKey_ShouldReturnTrue()
    {
        var key = Key("async_take");
        var @lock = new LocalLock();

        var result = await @lock.LockTakeAsync(key, "v1", TimeSpan.FromSeconds(5));

        result.ShouldBeTrue();
        await @lock.LockReleaseAsync(key, "v1");
    }

    // -----------------------------------------------------------------
    //  ExecuteWithLock
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：ExecuteWithLock 获锁成功时应执行传入的 Action。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_WhenLockAcquired_ShouldExecuteAction()
    {
        var key = Key("exec");
        var @lock = new LocalLock();
        var executed = false;

        @lock.ExecuteWithLock(key, "v1", TimeSpan.FromSeconds(5), () => { executed = true; });

        executed.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ExecuteWithLock Action 为 null 时不应抛出异常。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_NullAction_ShouldNotThrow()
    {
        var key = Key("null_action");
        var @lock = new LocalLock();

        Should.NotThrow(() => @lock.ExecuteWithLock(key, "v1", TimeSpan.FromSeconds(5), (Action)null));
    }

    /// <summary>
    /// 测试目的：ExecuteWithLock 泛型重载 — Action 为 null 时应返回 defaultValue。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_Generic_NullAction_ShouldReturnDefault()
    {
        var key = Key("generic_null");
        var @lock = new LocalLock();

        var result = @lock.ExecuteWithLock<int>(key, "v1", TimeSpan.FromSeconds(5), (Func<int>)null, -1);

        result.ShouldBe(-1);
    }

    /// <summary>
    /// 测试目的：ExecuteWithLock 泛型重载 — 锁成功时应执行 Func 并返回其结果。
    /// </summary>
    [Fact]
    public void ExecuteWithLock_Generic_WhenLockAcquired_ShouldReturnFuncResult()
    {
        var key = Key("generic_exec");
        var @lock = new LocalLock();

        var result = @lock.ExecuteWithLock(key, "v1", TimeSpan.FromSeconds(5), () => 42);

        result.ShouldBe(42);
    }

    // -----------------------------------------------------------------
    //  ExecuteWithLockAsync
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：ExecuteWithLockAsync 获锁成功时应执行异步 Action。
    /// </summary>
    [Fact]
    public async Task ExecuteWithLockAsync_WhenLockAcquired_ShouldExecuteAction()
    {
        var key = Key("async_exec");
        var @lock = new LocalLock();
        var executed = false;

        await @lock.ExecuteWithLockAsync(key, "v1", TimeSpan.FromSeconds(5),
            async () => { executed = true; await Task.CompletedTask; });

        executed.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ExecuteWithLockAsync Action 为 null 时不应抛出异常。
    /// </summary>
    [Fact]
    public async Task ExecuteWithLockAsync_NullAction_ShouldNotThrow()
    {
        var key = Key("async_null");
        var @lock = new LocalLock();

        await Should.NotThrowAsync(() => @lock.ExecuteWithLockAsync(key, "v1", TimeSpan.FromSeconds(5), (Func<Task>)null));
    }
}

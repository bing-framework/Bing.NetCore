using Microsoft.Extensions.Logging;
using Bing.Logging;
using Shouldly;
using Xunit;

namespace Bing.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 测试用 Enumeration 子类
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 枚举类型测试使用的订单状态。
/// </summary>
public class OrderStatus : Enumeration
{
    /// <summary>
    /// 表示待处理状态。
    /// </summary>
    public static readonly OrderStatus Pending   = new OrderStatus("1", "待处理");

    /// <summary>
    /// 表示已确认状态。
    /// </summary>
    public static readonly OrderStatus Confirmed = new OrderStatus("2", "已确认");

    /// <summary>
    /// 表示已发货状态。
    /// </summary>
    public static readonly OrderStatus Shipped   = new OrderStatus("3", "已发货");

    /// <summary>
    /// 初始化一个 <see cref="OrderStatus"/> 类型的实例。
    /// </summary>
    /// <param name="id">状态标识。</param>
    /// <param name="name">状态名称。</param>
    private OrderStatus(string id, string name) : base(id, name) { }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="Enumeration"/> 单元测试
/// </summary>
public class EnumerationTest
{
    // ═══════════════════════════════════════════════════════════
    // 基本属性
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Id 与 Name 属性应与构造时传入的值一致。
    /// </summary>
    [Fact]
    public void Properties_ShouldMatchConstructorArgs()
    {
        OrderStatus.Pending.Id.ShouldBe("1");
        OrderStatus.Pending.Name.ShouldBe("待处理");
    }

    // ═══════════════════════════════════════════════════════════
    // ToString
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ToString 应输出 "[TypeName] Id = x, Name = y" 格式。
    /// </summary>
    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        OrderStatus.Pending.ToString().ShouldBe("[OrderStatus] Id = 1, Name = 待处理");
    }

    // ═══════════════════════════════════════════════════════════
    // Equals / GetHashCode
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：相同 Id 的同类型枚举项应相等。
    /// </summary>
    [Fact]
    public void Equals_SameIdSameType_ShouldBeTrue()
    {
        var a = OrderStatus.Pending;
        var b = OrderStatus.Pending;
        a.Equals(b).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：不同 Id 的枚举项应不等。
    /// </summary>
    [Fact]
    public void Equals_DifferentId_ShouldBeFalse()
    {
        OrderStatus.Pending.Equals(OrderStatus.Confirmed).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：与 null 比较应返回 false。
    /// </summary>
    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        OrderStatus.Pending.Equals(null).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：相同 Id 的项 GetHashCode 应相等。
    /// </summary>
    [Fact]
    public void GetHashCode_SameId_ShouldBeEqual()
    {
        OrderStatus.Pending.GetHashCode().ShouldBe(OrderStatus.Pending.GetHashCode());
    }

    // ═══════════════════════════════════════════════════════════
    // CompareTo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Id="1" 应小于 Id="2"，CompareTo 返回负数。
    /// </summary>
    [Fact]
    public void CompareTo_SmallerIdFirst_ShouldReturnNegative()
    {
        OrderStatus.Pending.CompareTo(OrderStatus.Confirmed).ShouldBeLessThan(0);
    }

    /// <summary>
    /// 测试目的：相同 Id 的项 CompareTo 应返回 0。
    /// </summary>
    [Fact]
    public void CompareTo_SameId_ShouldReturnZero()
    {
        OrderStatus.Pending.CompareTo(OrderStatus.Pending).ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════
    // GetAll
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetAll 应返回所有声明的静态枚举项。
    /// </summary>
    [Fact]
    public void GetAll_ShouldReturnAllItems()
    {
        var all = Enumeration.GetAll<OrderStatus>().ToList();
        all.Count.ShouldBe(3);
        all.ShouldContain(OrderStatus.Pending);
        all.ShouldContain(OrderStatus.Confirmed);
        all.ShouldContain(OrderStatus.Shipped);
    }

    // ═══════════════════════════════════════════════════════════
    // FromValue / FromDisplayName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FromValue 对存在的 Id 应返回对应枚举项。
    /// </summary>
    [Fact]
    public void FromValue_ExistingId_ShouldReturnMatchingItem()
    {
        var item = Enumeration.FromValue<OrderStatus>("2");
        item.ShouldBe(OrderStatus.Confirmed);
    }

    /// <summary>
    /// 测试目的：FromValue 对不存在的 Id 应抛出 InvalidOperationException。
    /// </summary>
    [Fact]
    public void FromValue_NonExistingId_ShouldThrowInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => Enumeration.FromValue<OrderStatus>("99"));
    }

    /// <summary>
    /// 测试目的：FromDisplayName 对存在的 Name 应返回对应枚举项。
    /// </summary>
    [Fact]
    public void FromDisplayName_ExistingName_ShouldReturnMatchingItem()
    {
        var item = Enumeration.FromDisplayName<OrderStatus>("已发货");
        item.ShouldBe(OrderStatus.Shipped);
    }

    /// <summary>
    /// 测试目的：FromDisplayName 对不存在的 Name 应抛出 InvalidOperationException。
    /// </summary>
    [Fact]
    public void FromDisplayName_NonExistingName_ShouldThrowInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() => Enumeration.FromDisplayName<OrderStatus>("不存在"));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Disposable 测试
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="System.Disposable"/> 单元测试
/// </summary>
public class DisposableTest
{
    /// <summary>
    /// 记录托管资源释放调用并暴露释放状态的测试实现。
    /// </summary>
    private class ConcreteDisposable : System.Disposable
    {
        /// <summary>
        /// 获取或设置托管资源释放路径是否已调用。
        /// </summary>
        public bool ManagedDisposeCalled { get; private set; }

        /// <summary>
        /// 获取对象是否已释放。
        /// </summary>
        public bool IsDisposed => Disposed;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ManagedDisposeCalled = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 测试目的：调用 Dispose() 后，Disposed 应为 true。
    /// </summary>
    [Fact]
    public void Dispose_ShouldSetDisposedTrue()
    {
        var obj = new ConcreteDisposable();
        obj.Dispose();
        obj.IsDisposed.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：调用 Dispose() 时，managed dispose 路径应被触发。
    /// </summary>
    [Fact]
    public void Dispose_ShouldCallManagedDispose()
    {
        var obj = new ConcreteDisposable();
        obj.Dispose();
        obj.ManagedDisposeCalled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：using 语句结束后 Disposed 应自动置为 true。
    /// </summary>
    [Fact]
    public void Using_ShouldAutoDispose()
    {
        ConcreteDisposable obj;
        using (obj = new ConcreteDisposable())
        {
            obj.IsDisposed.ShouldBeFalse();
        }
        obj.IsDisposed.ShouldBeTrue();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BingExceptionExtensions 测试
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="BingExceptionExtensions"/> 单元测试
/// </summary>
public class BingExceptionExtensionsTest
{
    /// <summary>
    /// 携带日志级别属性的异常测试实现。
    /// </summary>
    private class HasLogLevelException : Exception, IHasLogLevel
    {
        /// <inheritdoc />
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// 初始化一个 <see cref="HasLogLevelException"/> 类型的实例。
        /// </summary>
        /// <param name="level">该异常关联的日志级别。</param>
        public HasLogLevelException(LogLevel level) : base("test") => LogLevel = level;
    }

    /// <summary>
    /// 测试目的：实现了 IHasLogLevel 的异常，GetLogLevel 应返回其 LogLevel 属性值。
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Critical)]
    public void GetLogLevel_WhenExceptionImplementsInterface_ShouldReturnItsLevel(LogLevel level)
    {
        var ex = new HasLogLevelException(level);
        ex.GetLogLevel().ShouldBe(level);
    }

    /// <summary>
    /// 测试目的：普通异常（不实现 IHasLogLevel）使用默认值时应返回 Error。
    /// </summary>
    [Fact]
    public void GetLogLevel_PlainException_ShouldReturnDefaultError()
    {
        var ex = new Exception("plain");
        ex.GetLogLevel().ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：普通异常可自定义默认级别，应返回指定的 defaultLevel。
    /// </summary>
    [Fact]
    public void GetLogLevel_PlainExceptionWithCustomDefault_ShouldReturnCustomDefault()
    {
        var ex = new Exception("plain");
        ex.GetLogLevel(LogLevel.Warning).ShouldBe(LogLevel.Warning);
    }
}

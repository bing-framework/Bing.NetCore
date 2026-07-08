using Bing.Logging.ExtraSupports;
using Bing.Logging.Core.Callers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LogContext"/>、<see cref="LogContextAccessor"/>、<see cref="LogFactory"/>、
/// <see cref="BingLoggingBuilder"/>、<see cref="ContextDataTypes"/>、<see cref="NullLogCallerInfo"/>
/// 单元测试
/// </summary>
public class LogContextAndFactoryTest
{
    // ═══════════════════════════════════════════════════════════
    // LogContext — 属性与集合初始化
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Data 字典不为 null，Tags 列表不为 null，
    /// 防止使用方直接访问时触发 NullReferenceException。
    /// </summary>
    [Fact]
    public void LogContext_Default_DataAndTagsShouldNotBeNull()
    {
        // Arrange & Act
        var ctx = new LogContext();

        // Assert
        ctx.Data.ShouldNotBeNull();
        ctx.Data.Count.ShouldBe(0);
        ctx.Tags.ShouldNotBeNull();
        ctx.Tags.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：所有 string 属性默认应为 null，IsWebEnv 默认应为 false，
    /// 确保不携带意外默认值影响上游逻辑。
    /// </summary>
    [Fact]
    public void LogContext_Default_StringPropertiesShouldBeNullAndIsWebEnvFalse()
    {
        // Arrange & Act
        var ctx = new LogContext();

        // Assert
        ctx.TraceId.ShouldBeNull();
        ctx.UserId.ShouldBeNull();
        ctx.TenantId.ShouldBeNull();
        ctx.Application.ShouldBeNull();
        ctx.Environment.ShouldBeNull();
        ctx.Ip.ShouldBeNull();
        ctx.Host.ShouldBeNull();
        ctx.Browser.ShouldBeNull();
        ctx.Url.ShouldBeNull();
        ctx.SessionId.ShouldBeNull();
        ctx.IsWebEnv.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：属性赋值后应能正确读取，确保 LogContext 的属性是可读写的 POCO。
    /// </summary>
    [Fact]
    public void LogContext_SetProperties_ShouldReturnAssignedValues()
    {
        // Arrange & Act
        var ctx = new LogContext
        {
            TraceId = "trace-001",
            UserId = "user-001",
            TenantId = "tenant-a",
            Application = "MyApp",
            Environment = "Prod",
            IsWebEnv = true
        };

        // Assert
        ctx.TraceId.ShouldBe("trace-001");
        ctx.UserId.ShouldBe("user-001");
        ctx.TenantId.ShouldBe("tenant-a");
        ctx.Application.ShouldBe("MyApp");
        ctx.Environment.ShouldBe("Prod");
        ctx.IsWebEnv.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：LogContext.Current 是 AsyncLocal，在主线程设置后可读取到相同引用。
    /// </summary>
    [Fact]
    public void LogContext_Current_SetAndGet_ShouldReturnSameInstance()
    {
        // Arrange
        var original = LogContext.Current;
        var ctx = new LogContext { TraceId = "async-trace" };
        try
        {
            // Act
            LogContext.Current = ctx;

            // Assert
            LogContext.Current.ShouldBeSameAs(ctx);
            LogContext.Current.TraceId.ShouldBe("async-trace");
        }
        finally
        {
            LogContext.Current = original;
        }
    }

    /// <summary>
    /// 测试目的：Tags 可以正常添加条目并读取，确保标签功能可用。
    /// </summary>
    [Fact]
    public void LogContext_Tags_AddTag_ShouldBeReadable()
    {
        // Arrange
        var ctx = new LogContext();

        // Act
        ctx.Tags.Add("tag1");
        ctx.Tags.Add("tag2");

        // Assert
        ctx.Tags.Count.ShouldBe(2);
        ctx.Tags.ShouldContain("tag1");
        ctx.Tags.ShouldContain("tag2");
    }

    /// <summary>
    /// 测试目的：Data 字典可以正常添加 KV 并读取，确保扩展数据功能可用。
    /// </summary>
    [Fact]
    public void LogContext_Data_AddEntry_ShouldBeReadable()
    {
        // Arrange
        var ctx = new LogContext();

        // Act
        ctx.Data["key1"] = "value1";

        // Assert
        ctx.Data["key1"].ShouldBe("value1");
    }

    // ═══════════════════════════════════════════════════════════
    // LogContextAccessor
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 LogContext.Current 为 null 时，LogContextAccessor.Context 应创建并返回新实例，
    /// 确保不返回 null。
    /// </summary>
    [Fact]
    public void LogContextAccessor_Context_WhenCurrentIsNull_ShouldCreateNew()
    {
        // Arrange
        var original = LogContext.Current;
        LogContext.Current = null;
        try
        {
            var accessor = new LogContextAccessor();

            // Act
            var ctx = accessor.Context;

            // Assert
            ctx.ShouldNotBeNull();
        }
        finally
        {
            LogContext.Current = original;
        }
    }

    /// <summary>
    /// 测试目的：新创建的 LogContext 应包含非空的 TraceId，
    /// 确保分布式追踪字段不为空。
    /// </summary>
    [Fact]
    public void LogContextAccessor_Context_ShouldHaveNonNullTraceId()
    {
        // Arrange
        var original = LogContext.Current;
        LogContext.Current = null;
        try
        {
            var accessor = new LogContextAccessor();

            // Act
            var ctx = accessor.Context;

            // Assert
            ctx.TraceId.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            LogContext.Current = original;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // LogFactory
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入 null ILoggerFactory 时，构造器应抛出 ArgumentNullException，
    /// 防止在运行时遇到意外 NRE。
    /// </summary>
    [Fact]
    public void LogFactory_Constructor_NullLoggerFactory_ShouldThrow()
    {
        // Arrange
        var mockAccessor = new Mock<ILogContextAccessor>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new LogFactory(null, mockAccessor.Object));
    }

    /// <summary>
    /// 测试目的：CreateLog(string) 应返回非 null 的 ILog 实例，
    /// 确保工厂方法按 categoryName 正确创建日志操作对象。
    /// </summary>
    [Fact]
    public void LogFactory_CreateLog_WithCategoryName_ShouldReturnNonNullLog()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);
        var mockAccessor = new Mock<ILogContextAccessor>();
        var factory = new LogFactory(mockLoggerFactory.Object, mockAccessor.Object);

        // Act
        var log = factory.CreateLog("TestCategory");

        // Assert
        log.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：CreateLog(Type) 应返回非 null 的 ILog 实例，
    /// 确保按类型创建日志对象的路径正确。
    /// </summary>
    [Fact]
    public void LogFactory_CreateLog_WithType_ShouldReturnNonNullLog()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(mockLogger.Object);
        var mockAccessor = new Mock<ILogContextAccessor>();
        var factory = new LogFactory(mockLoggerFactory.Object, mockAccessor.Object);

        // Act
        var log = factory.CreateLog(typeof(LogFactory));

        // Assert
        log.ShouldNotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // BingLoggingBuilder
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造器应将 IServiceCollection 正确存储到 Services 属性，
    /// 确保扩展方法可以通过 builder.Services 注册依赖。
    /// </summary>
    [Fact]
    public void BingLoggingBuilder_Constructor_ShouldSetServicesProperty()
    {
        // Arrange
        var mockServices = new Mock<Microsoft.Extensions.DependencyInjection.IServiceCollection>();

        // Act
        var builder = new BingLoggingBuilder(mockServices.Object);

        // Assert
        builder.Services.ShouldBeSameAs(mockServices.Object);
    }

    // ═══════════════════════════════════════════════════════════
    // ContextDataTypes — 常量值验证
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ContextDataTypes 中的常量前缀应保持稳定，
    /// 防止意外重命名导致 Serilog/日志提供程序的扩展属性键失配。
    /// </summary>
    [Fact]
    public void ContextDataTypes_Constants_ShouldMatchExpectedValues()
    {
        // Assert
        ContextDataTypes.ExtraProperty.ShouldBe("__BING_EXTRA_PROPERTY_");
        ContextDataTypes.Tags.ShouldBe("__BING_TAGS");
        ContextDataTypes.CallerInfo.ShouldBe("__BING_CALLER_IFNO");
    }

    // ═══════════════════════════════════════════════════════════
    // NullLogCallerInfo — 结构体 null 值语义
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：NullLogCallerInfo.Instance 所有属性应返回 null/0/null，
    /// 用于调用者信息不可用时的安全兜底。
    /// </summary>
    [Fact]
    public void NullLogCallerInfo_Instance_AllPropertiesShouldBeNullOrDefault()
    {
        // Arrange & Act
        var info = NullLogCallerInfo.Instance;

        // Assert
        info.MemberName.ShouldBeNull();
        info.FilePath.ShouldBeNull();
        info.LineNumber.ShouldBe(0);
        ((object)info.ToParams()).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：NullLogCallerInfo.Instance 多次访问应返回值语义相同的结构，
    /// 确保 struct 使用安全一致。
    /// </summary>
    [Fact]
    public void NullLogCallerInfo_Instance_MultipleAccess_ShouldBeConsistent()
    {
        // Arrange & Act
        var a = NullLogCallerInfo.Instance;
        var b = NullLogCallerInfo.Instance;

        // Assert
        a.MemberName.ShouldBe(b.MemberName);
        a.FilePath.ShouldBe(b.FilePath);
        a.LineNumber.ShouldBe(b.LineNumber);
    }
}

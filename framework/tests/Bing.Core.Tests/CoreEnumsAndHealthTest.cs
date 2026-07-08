using Bing.Core.Enums;
using Bing.Core.Modularity;
using Bing.Exceptions;
using Bing.Exceptions.Prompts;
using Bing.Monitoring.Health;
using Bing.Trees;

namespace Bing.Tests;

/// <summary>
/// <see cref="LoadMode"/>、<see cref="LoadOperation"/>、<see cref="ModuleLevel"/>、
/// <see cref="EnvironmentType"/> 枚举，以及
/// <see cref="BusHealthResult"/>、<see cref="BusHealthStatus"/> 结构体，
/// <see cref="ExceptionPrompt"/> 静态工具类 的单元测试。
/// </summary>
public class CoreEnumsAndHealthTest
{
    // ═══════════════════════════════════════════════════════════
    // LoadMode
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：枚举包含 Sync、Async、OnlyRootAsync 三个成员，
    /// 防止意外增删导致树形组件加载策略错乱。
    /// </summary>
    [Fact]
    public void LoadMode_Count_ShouldBeThree()
    {
        Enum.GetValues(typeof(LoadMode)).Length.ShouldBe(3);
    }

    /// <summary>
    /// 测试目的：LoadMode 各成员可被正常声明与比较，枚举解析不抛异常。
    /// </summary>
    [Fact]
    public void LoadMode_Values_ShouldContainExpectedMembers()
    {
        // Assert
        Enum.IsDefined(typeof(LoadMode), LoadMode.Sync).ShouldBeTrue();
        Enum.IsDefined(typeof(LoadMode), LoadMode.Async).ShouldBeTrue();
        Enum.IsDefined(typeof(LoadMode), LoadMode.OnlyRootAsync).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // LoadOperation
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：枚举值 FirstLoad=1、LoadChild=2、Search=3，
    /// 确保与前端或配置中的整数约定一致。
    /// </summary>
    [Fact]
    public void LoadOperation_Values_ShouldMatchExpected()
    {
        ((int)LoadOperation.FirstLoad).ShouldBe(1);
        ((int)LoadOperation.LoadChild).ShouldBe(2);
        ((int)LoadOperation.Search).ShouldBe(3);
    }

    /// <summary>
    /// 测试目的：枚举共有 3 个成员，防止意外变更。
    /// </summary>
    [Fact]
    public void LoadOperation_Count_ShouldBeThree()
    {
        Enum.GetValues(typeof(LoadOperation)).Length.ShouldBe(3);
    }

    // ═══════════════════════════════════════════════════════════
    // ModuleLevel
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：ModuleLevel 各级别的整数值应符合规范（Core=1, Framework=10, Application=20, Business=30），
    /// 数值越小优先级越高，确保模块启动顺序正确。
    /// </summary>
    [Fact]
    public void ModuleLevel_Values_ShouldMatchExpected()
    {
        ((int)ModuleLevel.Core).ShouldBe(1);
        ((int)ModuleLevel.Framework).ShouldBe(10);
        ((int)ModuleLevel.Application).ShouldBe(20);
        ((int)ModuleLevel.Business).ShouldBe(30);
    }

    /// <summary>
    /// 测试目的：ModuleLevel 共 4 个成员，防止误增/误删导致模块加载顺序错乱。
    /// </summary>
    [Fact]
    public void ModuleLevel_Count_ShouldBeFour()
    {
        Enum.GetValues(typeof(ModuleLevel)).Length.ShouldBe(4);
    }

    // ═══════════════════════════════════════════════════════════
    // EnvironmentType
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：枚举值 Development=1、Test=2、Preview=3、Prod=4，
    /// 确保环境配置切换时数值映射正确。
    /// </summary>
    [Fact]
    public void EnvironmentType_Values_ShouldMatchExpected()
    {
        ((byte)EnvironmentType.Development).ShouldBe((byte)1);
        ((byte)EnvironmentType.Test).ShouldBe((byte)2);
        ((byte)EnvironmentType.Preview).ShouldBe((byte)3);
        ((byte)EnvironmentType.Prod).ShouldBe((byte)4);
    }

    /// <summary>
    /// 测试目的：EnvironmentType 共 4 个成员，防止遗漏环境类型导致判断逻辑缺陷。
    /// </summary>
    [Fact]
    public void EnvironmentType_Count_ShouldBeFour()
    {
        Enum.GetValues(typeof(EnvironmentType)).Length.ShouldBe(4);
    }

    // ═══════════════════════════════════════════════════════════
    // BusHealthStatus
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：BusHealthStatus 各状态值应符合规范（Unhealthy=0, Degraded=1, Healthy=2），
    /// 确保健康检查端点返回的整数与状态语义一致。
    /// </summary>
    [Fact]
    public void BusHealthStatus_Values_ShouldMatchExpected()
    {
        ((int)BusHealthStatus.Unhealthy).ShouldBe(0);
        ((int)BusHealthStatus.Degraded).ShouldBe(1);
        ((int)BusHealthStatus.Healthy).ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：BusHealthStatus 默认值应为 Unhealthy（0），
    /// 防止未初始化时误判为健康状态。
    /// </summary>
    [Fact]
    public void BusHealthStatus_Default_ShouldBeUnhealthy()
    {
        default(BusHealthStatus).ShouldBe(BusHealthStatus.Unhealthy);
    }

    // ═══════════════════════════════════════════════════════════
    // BusHealthResult
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：BusHealthResult.Healthy() 应创建状态为 Healthy 的结果，
    /// 描述与 Data 按传入值正确赋值。
    /// </summary>
    [Fact]
    public void BusHealthResult_Healthy_ShouldSetStatusAndDescription()
    {
        // Act
        var result = BusHealthResult.Healthy("all systems go");

        // Assert
        result.Status.ShouldBe(BusHealthStatus.Healthy);
        result.Description.ShouldBe("all systems go");
        result.Exception.ShouldBeNull();
        result.Data.ShouldNotBeNull();
        result.Data.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：BusHealthResult.Degraded() 应创建状态为 Degraded 的结果，
    /// 异常信息被正确保留。
    /// </summary>
    [Fact]
    public void BusHealthResult_Degraded_ShouldSetStatusAndException()
    {
        // Arrange
        var ex = new InvalidOperationException("degraded cause");

        // Act
        var result = BusHealthResult.Degraded("partial failure", ex);

        // Assert
        result.Status.ShouldBe(BusHealthStatus.Degraded);
        result.Description.ShouldBe("partial failure");
        result.Exception.ShouldBeSameAs(ex);
    }

    /// <summary>
    /// 测试目的：BusHealthResult.Unhealthy() 应创建状态为 Unhealthy 的结果，
    /// 无参调用时 Description 和 Exception 均为 null。
    /// </summary>
    [Fact]
    public void BusHealthResult_Unhealthy_DefaultArgs_ShouldHaveNullDescriptionAndException()
    {
        // Act
        var result = BusHealthResult.Unhealthy();

        // Assert
        result.Status.ShouldBe(BusHealthStatus.Unhealthy);
        result.Description.ShouldBeNull();
        result.Exception.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：BusHealthResult.Healthy() 可传入自定义 Data 字典，
    /// 结果中 Data 应与传入引用相同。
    /// </summary>
    [Fact]
    public void BusHealthResult_Healthy_WithData_ShouldReturnData()
    {
        // Arrange
        var data = new Dictionary<string, object> { { "version", "1.0.0" } };

        // Act
        var result = BusHealthResult.Healthy(data: data);

        // Assert
        result.Data.ContainsKey("version").ShouldBeTrue();
        result.Data["version"].ShouldBe("1.0.0");
    }

    // ═══════════════════════════════════════════════════════════
    // ExceptionPrompt
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 exception 为 null 时，GetPrompt 应返回 null，
    /// 防止调用方收到误导性的错误信息。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_NullException_ShouldReturnNull()
    {
        // Act & Assert
        ExceptionPrompt.GetPrompt(null, false).ShouldBeNull();
        ExceptionPrompt.GetPrompt(null, true).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：非生产环境下，GetPrompt 应返回异常的 Message 文本（原始信息直接呈现）。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_NonProduction_ShouldReturnExceptionMessage()
    {
        // Arrange
        var ex = new InvalidOperationException("raw error detail");

        // Act
        var prompt = ExceptionPrompt.GetPrompt(ex, false);

        // Assert
        prompt.ShouldBe("raw error detail");
    }

    /// <summary>
    /// 测试目的：生产环境下，GetPrompt 对普通异常应返回系统通用错误文案（不暴露内部细节）。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetPrompt_ProductionMode_ShouldReturnSystemError()
    {
        // Arrange
        var ex = new InvalidOperationException("internal detail");

        // Act
        var prompt = ExceptionPrompt.GetPrompt(ex, true);

        // Assert — 生产环境返回通用文案，不应等于原始异常消息
        prompt.ShouldNotBe("internal detail");
        prompt.ShouldNotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试目的：AddPrompt(null) 应抛出 ArgumentNullException，保护静态列表的完整性。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_AddPrompt_NullPrompt_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ExceptionPrompt.AddPrompt(null));
    }

    /// <summary>
    /// 测试目的：GetException(null) 应返回 null，确保安全调用无异常。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetException_NullInput_ShouldReturnNull()
    {
        // Act & Assert
        ExceptionPrompt.GetException(null).ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：GetException(exception) 当无注册的 Prompt 时，应原样返回传入的异常对象。
    /// </summary>
    [Fact]
    public void ExceptionPrompt_GetException_WithNoPrompts_ShouldReturnSameException()
    {
        // Arrange
        var ex = new ArgumentException("test");

        // Act
        var result = ExceptionPrompt.GetException(ex);

        // Assert
        result.ShouldBeSameAs(ex);
    }
}

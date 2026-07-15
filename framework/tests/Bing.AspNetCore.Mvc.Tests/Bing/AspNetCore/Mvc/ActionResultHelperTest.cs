using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// <see cref="ActionResultHelper"/> 单元测试
/// </summary>
public class ActionResultHelperTest
{
    // ═══════════════════════════════════════════════════════════
    // ObjectResultTypes 初始值
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：静态初始化后 ObjectResultTypes 应包含三个预定义类型，
    /// 确保框架内置的对象结果类型映射不被意外修改。
    /// </summary>
    [Fact]
    public void ObjectResultTypes_ShouldContainThreeDefaultTypes()
    {
        // Assert
        ActionResultHelper.ObjectResultTypes.ShouldNotBeNull();
        ActionResultHelper.ObjectResultTypes.ShouldContain(typeof(JsonResult));
        ActionResultHelper.ObjectResultTypes.ShouldContain(typeof(ObjectResult));
        ActionResultHelper.ObjectResultTypes.ShouldContain(typeof(NoContentResult));
    }

    // ═══════════════════════════════════════════════════════════
    // IsObjectResult — 非 IActionResult 返回类型
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：string 类型不是 IActionResult，IsObjectResult 应返回 true，
    /// 确保普通 POCO 返回类型被识别为对象结果（需要序列化输出）。
    /// </summary>
    [Fact]
    public void IsObjectResult_StringReturnType_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(string)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：int 类型不是 IActionResult，IsObjectResult 应返回 true。
    /// </summary>
    [Fact]
    public void IsObjectResult_IntReturnType_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(int)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Task&lt;string&gt; 应被解包为 string，仍返回 true，
    /// 确保异步方法返回类型可正常处理。
    /// </summary>
    [Fact]
    public void IsObjectResult_TaskOfString_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(Task<string>)).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // IsObjectResult — IActionResult 子类
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：JsonResult（ObjectResultTypes 成员）应返回 true，
    /// 确保框架认为 JsonResult 是"对象结果"类型。
    /// </summary>
    [Fact]
    public void IsObjectResult_JsonResult_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(JsonResult)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ObjectResult（ObjectResultTypes 成员）应返回 true。
    /// </summary>
    [Fact]
    public void IsObjectResult_ObjectResult_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(ObjectResult)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：NoContentResult（ObjectResultTypes 成员）应返回 true。
    /// </summary>
    [Fact]
    public void IsObjectResult_NoContentResult_ShouldBeTrue()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(NoContentResult)).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ContentResult 不在 ObjectResultTypes 中，应返回 false，
    /// 确保非对象结果类型被正确排除。
    /// </summary>
    [Fact]
    public void IsObjectResult_ContentResult_ShouldBeFalse()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(ContentResult)).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：RedirectResult 不在 ObjectResultTypes 中，应返回 false。
    /// </summary>
    [Fact]
    public void IsObjectResult_RedirectResult_ShouldBeFalse()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(RedirectResult)).ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // IsObjectResult — excludeTypes 参数
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入 excludeTypes 包含 string 时，string 应被排除并返回 false，
    /// 确保 excludeTypes 参数能正确覆盖默认对象结果判断。
    /// </summary>
    [Fact]
    public void IsObjectResult_WithExcludeType_ShouldReturnFalse()
    {
        // Act & Assert
        ActionResultHelper.IsObjectResult(typeof(string), typeof(string)).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：excludeTypes 为基类时，子类也应被排除，
    /// 确保 excludeTypes 使用 IsAssignableFrom 语义。
    /// </summary>
    [Fact]
    public void IsObjectResult_ExcludeBaseType_SubClassAlsoExcluded()
    {
        // Act — 排除 IActionResult，则 ObjectResult 也应被排除
        var result = ActionResultHelper.IsObjectResult(typeof(ObjectResult), typeof(IActionResult));

        // Assert
        result.ShouldBeFalse();
    }
}

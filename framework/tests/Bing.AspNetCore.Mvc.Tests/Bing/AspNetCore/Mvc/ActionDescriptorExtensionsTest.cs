using Bing;
using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shouldly;
using Xunit;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 验证 <c>ActionDescriptorExtensions</c> 的操作描述符扩展行为。
/// </summary>
/// <remarks>测试覆盖控制器操作、页面操作、方法信息、返回类型及对象结果判断。</remarks>
public class ActionDescriptorExtensionsTest
{
    // ── 辅助方法：创建 ControllerActionDescriptor ────────────────

    /// <summary>
    /// 创建指向测试控制器方法的操作描述符。
    /// </summary>
    /// <param name="methodName">控制器方法名称。</param>
    /// <returns>控制器操作描述符。</returns>
    private static ControllerActionDescriptor CreateControllerDescriptor(
        string methodName = nameof(FakeController.ReturnsString))
    {
        return new ControllerActionDescriptor
        {
            MethodInfo = typeof(FakeController).GetMethod(methodName)!
        };
    }

    // ── IsControllerAction ────────────────────────────────────────

    /// <summary>
    /// 测试目的：ControllerActionDescriptor 应被识别为控制器操作，返回 true。
    /// </summary>
    [Fact]
    public void IsControllerAction_WithControllerDescriptor_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor();

        // Act & Assert
        descriptor.IsControllerAction().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：PageActionDescriptor 不是控制器操作，IsControllerAction 应返回 false。
    /// </summary>
    [Fact]
    public void IsControllerAction_WithPageDescriptor_ShouldReturnFalse()
    {
        // Arrange
        var descriptor = new PageActionDescriptor();

        // Act & Assert
        descriptor.IsControllerAction().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：普通 ActionDescriptor 基类实例不是控制器操作，应返回 false。
    /// </summary>
    [Fact]
    public void IsControllerAction_WithBaseDescriptor_ShouldReturnFalse()
    {
        // Arrange
        var descriptor = new ActionDescriptor();

        // Act & Assert
        descriptor.IsControllerAction().ShouldBeFalse();
    }

    // ── IsPageAction ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：PageActionDescriptor 应被识别为 Razor Page 操作，返回 true。
    /// </summary>
    [Fact]
    public void IsPageAction_WithPageDescriptor_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = new PageActionDescriptor();

        // Act & Assert
        descriptor.IsPageAction().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ControllerActionDescriptor 不是 Razor Page 操作，IsPageAction 应返回 false。
    /// </summary>
    [Fact]
    public void IsPageAction_WithControllerDescriptor_ShouldReturnFalse()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor();

        // Act & Assert
        descriptor.IsPageAction().ShouldBeFalse();
    }

    // ── AsControllerActionDescriptor ──────────────────────────────

    /// <summary>
    /// 测试目的：对 ControllerActionDescriptor 调用 AsControllerActionDescriptor 应成功， 返回同一引用。
    /// </summary>
    [Fact]
    public void AsControllerActionDescriptor_WithControllerDescriptor_ShouldReturnSameInstance()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor();

        // Act
        var result = descriptor.AsControllerActionDescriptor();

        // Assert
        result.ShouldNotBeNull();
        ReferenceEquals(result, descriptor).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：对 PageActionDescriptor 调用 AsControllerActionDescriptor 应抛出 BingFrameworkException。
    /// </summary>
    [Fact]
    public void AsControllerActionDescriptor_WithPageDescriptor_ShouldThrow()
    {
        // Arrange
        var descriptor = new PageActionDescriptor();

        // Act & Assert
        Should.Throw<BingFrameworkException>(() => descriptor.AsControllerActionDescriptor());
    }

    /// <summary>
    /// 测试目的：对普通 ActionDescriptor 调用 AsControllerActionDescriptor 应抛出 BingFrameworkException。
    /// </summary>
    [Fact]
    public void AsControllerActionDescriptor_WithBaseDescriptor_ShouldThrow()
    {
        // Arrange
        var descriptor = new ActionDescriptor();

        // Act & Assert
        Should.Throw<BingFrameworkException>(() => descriptor.AsControllerActionDescriptor());
    }

    // ── AsPageAction ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：对 PageActionDescriptor 调用 AsPageAction 应成功，返回同一引用。
    /// </summary>
    [Fact]
    public void AsPageAction_WithPageDescriptor_ShouldReturnSameInstance()
    {
        // Arrange
        var descriptor = new PageActionDescriptor();

        // Act
        var result = descriptor.AsPageAction();

        // Assert
        result.ShouldNotBeNull();
        ReferenceEquals(result, descriptor).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：对 ControllerActionDescriptor 调用 AsPageAction 应抛出 BingFrameworkException。
    /// </summary>
    [Fact]
    public void AsPageAction_WithControllerDescriptor_ShouldThrow()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor();

        // Act & Assert
        Should.Throw<BingFrameworkException>(() => descriptor.AsPageAction());
    }

    // ── GetMethodInfo ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetMethodInfo 应返回 ControllerActionDescriptor.MethodInfo，与直接访问一致。
    /// </summary>
    [Fact]
    public void GetMethodInfo_ShouldReturnMethodInfoFromDescriptor()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsString));

        // Act
        var methodInfo = descriptor.GetMethodInfo();

        // Assert
        methodInfo.ShouldNotBeNull();
        methodInfo.Name.ShouldBe(nameof(FakeController.ReturnsString));
    }

    // ── GetReturnType ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：返回 string 类型的方法，GetReturnType 应返回 typeof(string)。
    /// </summary>
    [Fact]
    public void GetReturnType_ForStringMethod_ShouldReturnStringType()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsString));

        // Act
        var returnType = descriptor.GetReturnType();

        // Assert
        returnType.ShouldBe(typeof(string));
    }

    /// <summary>
    /// 测试目的：返回 IActionResult 类型的方法，GetReturnType 应返回 typeof(IActionResult)。
    /// </summary>
    [Fact]
    public void GetReturnType_ForIActionResultMethod_ShouldReturnIActionResultType()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsIActionResult));

        // Act
        var returnType = descriptor.GetReturnType();

        // Assert
        returnType.ShouldBe(typeof(IActionResult));
    }

    // ── HasObjectResult ───────────────────────────────────────────

    /// <summary>
    /// 测试目的：返回 JsonResult 的方法，HasObjectResult 应返回 true。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForJsonResultMethod_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsJsonResult));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：返回 ObjectResult 的方法，HasObjectResult 应返回 true。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForObjectResultMethod_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsObjectResult));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：返回 string（非 ActionResult）的方法，按当前实现应视为对象结果。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForStringMethod_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsString));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：返回 void 的方法，按当前实现也会被视为对象结果。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForVoidMethod_ShouldReturnTrue()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsVoid));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeTrue();
    }
}

// ── 测试用辅助 Controller ─────────────────────────────────────────────────

/// <summary>
/// 提供操作描述符测试使用的控制器方法。
/// </summary>
internal class FakeController
{
    /// <summary>
    /// 返回字符串结果。
    /// </summary>
    /// <returns>空字符串。</returns>
    public string ReturnsString() => string.Empty;

    /// <summary>
    /// 执行无返回值的操作。
    /// </summary>
    public void ReturnsVoid() { }

    /// <summary>
    /// 返回操作结果接口。
    /// </summary>
    /// <returns>空的 <see cref="IActionResult" /> 结果。</returns>
    public IActionResult ReturnsIActionResult() => null!;

    /// <summary>
    /// 返回 JSON 操作结果。
    /// </summary>
    /// <returns>空的 <see cref="JsonResult" /> 实例。</returns>
    public JsonResult ReturnsJsonResult() => null!;

    /// <summary>
    /// 返回对象操作结果。
    /// </summary>
    /// <returns>空的 <see cref="ObjectResult" /> 实例。</returns>
    public ObjectResult ReturnsObjectResult() => null!;
}

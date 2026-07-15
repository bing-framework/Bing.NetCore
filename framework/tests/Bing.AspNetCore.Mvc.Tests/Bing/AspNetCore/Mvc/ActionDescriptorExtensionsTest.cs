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
/// <see cref="ActionDescriptorExtensions"/> 单元测试
/// 覆盖所有扩展方法：IsControllerAction/IsPageAction/AsControllerActionDescriptor/
/// AsPageAction/GetMethodInfo/GetReturnType/HasObjectResult
/// </summary>
public class ActionDescriptorExtensionsTest
{
    // ── 辅助方法：创建 ControllerActionDescriptor ────────────────

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
    /// 测试目的：对 ControllerActionDescriptor 调用 AsControllerActionDescriptor 应成功，
    /// 返回同一引用。
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
    /// 测试目的：返回 string（非 ActionResult）的方法，HasObjectResult 应返回 false。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForStringMethod_ShouldReturnFalse()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsString));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：返回 void 的方法，HasObjectResult 应返回 false。
    /// </summary>
    [Fact]
    public void HasObjectResult_ForVoidMethod_ShouldReturnFalse()
    {
        // Arrange
        var descriptor = CreateControllerDescriptor(nameof(FakeController.ReturnsVoid));

        // Act & Assert
        descriptor.HasObjectResult().ShouldBeFalse();
    }
}

// ── 测试用辅助 Controller ─────────────────────────────────────────────────

internal class FakeController
{
    public string ReturnsString() => string.Empty;
    public void ReturnsVoid() { }
    public IActionResult ReturnsIActionResult() => null!;
    public JsonResult ReturnsJsonResult() => null!;
    public ObjectResult ReturnsObjectResult() => null!;
}

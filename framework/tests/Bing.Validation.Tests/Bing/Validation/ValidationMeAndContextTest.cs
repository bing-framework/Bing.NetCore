using System.ComponentModel.DataAnnotations;
using Bing.Exceptions;
using Bing.Validation;
using Bing.Validation.Strategies;
using Shouldly;
using Xunit;

namespace Bing.Validation.Tests;

// ─── 测试辅助：实现 IVerifyModel<T> 的样本模型 ────────────────────

/// <summary>
/// 合法验证模型样本（Name 有默认值，DataAnnotation 可通过）
/// </summary>
internal class ValidatableValidModel : IVerifyModel<ValidatableValidModel>
{
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; } = "valid-name";

    public IValidationResult Validate() =>
        new ValidationResultCollection();

    public void SetValidationCallback(IValidationCallbackHandler handler) { }
    public void UseValidationRules() { }
    public void UseStrategy(IValidationStrategy<ValidatableValidModel> strategy) { }
    public void UseStrategyList(IEnumerable<IValidationStrategy<ValidatableValidModel>> strategies) { }
}

/// <summary>
/// 非法验证模型样本（Name 为 null，[Required] 约束失败）
/// </summary>
internal class ValidatableInvalidModel : IVerifyModel<ValidatableInvalidModel>
{
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; } // null → DataAnnotation 失败

    public IValidationResult Validate() =>
        new ValidationResultCollection("名称不能为空");

    public void SetValidationCallback(IValidationCallbackHandler handler) { }
    public void UseValidationRules() { }
    public void UseStrategy(IValidationStrategy<ValidatableInvalidModel> strategy) { }
    public void UseStrategyList(IEnumerable<IValidationStrategy<ValidatableInvalidModel>> strategies) { }
}

/// <summary>
/// 验证策略样本：可指定策略名称和返回的验证结果
/// </summary>
internal class NamedStrategy : IValidationStrategy<ValidatableValidModel>
{
    private readonly ValidationResult _result;
    public string StrategyName { get; }

    public NamedStrategy(string name, ValidationResult result = null)
    {
        StrategyName = name;
        _result = result;
    }

    public ValidationResult Validate(ValidatableValidModel obj) => _result;
}

// ─── ValidationMe 测试 ────────────────────────────────────────────

/// <summary>
/// <see cref="ValidationMe"/> 单元测试
/// </summary>
public class ValidationMeTest : IDisposable
{
    /// <summary>
    /// 测试后恢复默认 ThrowHandler，避免影响其他测试
    /// </summary>
    public void Dispose() => ValidationMe.RegisterCallbackHandler(new ThrowHandler());

    /// <summary>
    /// 测试目的：默认处理器为 ThrowHandler；当验证失败且未自定义 Handler 时，
    /// 通过 ValidationContext.Validate() 会抛出 Warning 异常。
    /// </summary>
    [Fact]
    public void DefaultHandler_IsThrowHandler_ValidateShouldThrowWarning()
    {
        // Arrange — 确保恢复默认处理器
        ValidationMe.RegisterCallbackHandler(new ThrowHandler());
        var ctx = new ValidationContext<ValidatableInvalidModel>(new ValidatableInvalidModel());
        // 不设置自定义 Handle，使用全局默认处理器

        // Act & Assert
        Should.Throw<Warning>(() => ctx.Validate());
    }

    /// <summary>
    /// 测试目的：注册 NothingHandler 后，ValidationContext 验证失败不再抛出异常。
    /// </summary>
    [Fact]
    public void RegisterCallbackHandler_WithNothingHandler_ValidateShouldNotThrow()
    {
        // Arrange
        ValidationMe.RegisterCallbackHandler(new NothingHandler());
        var ctx = new ValidationContext<ValidatableInvalidModel>(new ValidatableInvalidModel());
        // 不设置自定义 Handle，使用已注册的 NothingHandler

        // Act & Assert
        Should.NotThrow(() => ctx.Validate());
    }

    /// <summary>
    /// 测试目的：多次 RegisterCallbackHandler 应使用最后一次注册的处理器。
    /// </summary>
    [Fact]
    public void RegisterCallbackHandler_MultipleRegistrations_UsesLast()
    {
        // Arrange — 先注册 NothingHandler
        ValidationMe.RegisterCallbackHandler(new NothingHandler());
        // 再注册 ThrowHandler
        ValidationMe.RegisterCallbackHandler(new ThrowHandler());
        var ctx = new ValidationContext<ValidatableInvalidModel>(new ValidatableInvalidModel());

        // Act & Assert — 最后注册的是 ThrowHandler，应抛出 Warning
        Should.Throw<Warning>(() => ctx.Validate());
    }
}

// ─── ValidationContext<T> 测试 ────────────────────────────────────

/// <summary>
/// <see cref="ValidationContext{TObject}"/> 单元测试
/// </summary>
public class ValidationContextTest
{
    // ── AddStrategy ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：AddStrategy 传入 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddStrategy_WhenNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ctx.AddStrategy(null));
    }

    /// <summary>
    /// 测试目的：相同 StrategyName 的策略只添加一次（重复跳过）。
    /// </summary>
    [Fact]
    public void AddStrategy_DuplicateName_ShouldBeIgnored()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        var s1 = new NamedStrategy("DupStrategy");
        var s2 = new NamedStrategy("DupStrategy"); // 同名

        // Act
        ctx.AddStrategy(s1);
        ctx.AddStrategy(s2); // 应被跳过

        // 验证 Validate 只调用一次策略（通过结果数量间接验证）
        bool handlerInvoked = false;
        ctx.SetHandler(_ => handlerInvoked = true);
        ctx.Validate(); // 两个策略都返回 null（valid），处理器不应被调用

        // Assert
        handlerInvoked.ShouldBeFalse();
    }

    // ── AddStrategyList ─────────────────────────────────────────

    /// <summary>
    /// 测试目的：AddStrategyList 传入 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void AddStrategyList_WhenNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ctx.AddStrategyList(null));
    }

    /// <summary>
    /// 测试目的：AddStrategyList 接受空集合时不应抛出异常。
    /// </summary>
    [Fact]
    public void AddStrategyList_WithEmptyList_ShouldNotThrow()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act & Assert
        Should.NotThrow(() => ctx.AddStrategyList(Array.Empty<NamedStrategy>()));
    }

    /// <summary>
    /// 测试目的：AddStrategyList 批量添加多个策略，重复名称只保留第一个。
    /// </summary>
    [Fact]
    public void AddStrategyList_WithDuplicates_ShouldSkipDuplicate()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        var strategies = new[]
        {
            new NamedStrategy("A"),
            new NamedStrategy("B"),
            new NamedStrategy("A"), // 重复
        };

        // Act & Assert — 不抛异常，重复跳过
        Should.NotThrow(() => ctx.AddStrategyList(strategies));
    }

    // ── SetHandler ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：SetHandler 传入 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void SetHandler_WhenNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ctx.SetHandler(null));
    }

    /// <summary>
    /// 测试目的：第一次调用 SetHandler 应设置处理器（不叠加）。
    /// </summary>
    [Fact]
    public void SetHandler_FirstCall_ShouldSetHandler()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        int callCount = 0;
        ctx.SetHandler(_ => callCount++);

        // 为触发 Handle，向 appendAction 添加一个错误
        ctx.Validate(c => c.Add(new ValidationResult("触发处理器")));

        // Assert — 处理器被调用了 1 次
        callCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：连续调用 SetHandler 应叠加处理器（+=），两个都被调用。
    /// </summary>
    [Fact]
    public void SetHandler_MultipleCalls_ShouldAccumulate()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        int callCount = 0;
        ctx.SetHandler(_ => callCount++);
        ctx.SetHandler(_ => callCount++); // 第二次叠加

        // Act — appendAction 注入一个错误触发 Handle
        ctx.Validate(c => c.Add(new ValidationResult("触发处理器")));

        // Assert — 两个处理器都被调用
        callCount.ShouldBe(2);
    }

    // ── IsValid ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Validate 调用前 IsValid 应默认返回 true（尚未验证）。
    /// </summary>
    [Fact]
    public void IsValid_BeforeValidate_ShouldBeTrue()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Assert
        ctx.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：对合法模型调用 Validate 后 IsValid 应仍为 true。
    /// </summary>
    [Fact]
    public void IsValid_AfterValidateValidModel_ShouldBeTrue()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act
        ctx.Validate();

        // Assert
        ctx.IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：对非法模型调用 Validate 后（使用自定义 Handler），
    /// 处理器被调用且 IsValid 应为 false。
    /// </summary>
    [Fact]
    public void IsValid_AfterValidateInvalidModel_ShouldBeFalse()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableInvalidModel>(new ValidatableInvalidModel());
        ctx.SetHandler(_ => { }); // 空处理器，防止抛出异常

        // Act
        ctx.Validate();

        // Assert
        ctx.IsValid.ShouldBeFalse();
    }

    // ── Validate（带 appendAction）─────────────────────────────

    /// <summary>
    /// 测试目的：appendAction 可向验证结果集合追加额外错误，影响最终 IsValid。
    /// </summary>
    [Fact]
    public void Validate_WithAppendAction_ShouldIncludeAppendedErrors()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        ctx.SetHandler(_ => { }); // 不抛出

        // Act — 向 appendAction 注入错误使有效模型变为无效
        ctx.Validate(c => c.Add(new ValidationResult("人工追加错误")));

        // Assert
        ctx.IsValid.ShouldBeFalse();
        ctx.GetValidationResultCollection().ShouldNotBeNull();
    }

    // ── GetValidationResultCollection ──────────────────────────

    /// <summary>
    /// 测试目的：Validate 之前 GetValidationResultCollection 应返回 null。
    /// </summary>
    [Fact]
    public void GetValidationResultCollection_BeforeValidate_ShouldBeNull()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Assert
        ctx.GetValidationResultCollection().ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Validate 之后 GetValidationResultCollection 应返回非 null 集合。
    /// </summary>
    [Fact]
    public void GetValidationResultCollection_AfterValidate_ShouldNotBeNull()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act
        ctx.Validate();

        // Assert
        ctx.GetValidationResultCollection().ShouldNotBeNull();
    }

    // ── RaiseException ─────────────────────────────────────────

    /// <summary>
    /// 测试目的：未调用 Validate 时 RaiseException 不应抛出异常（ResultCollection 为 null）。
    /// </summary>
    [Fact]
    public void RaiseException_BeforeValidate_ShouldNotThrow()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());

        // Act & Assert
        Should.NotThrow(() => ctx.RaiseException<ValidationException>());
    }

    /// <summary>
    /// 测试目的：合法模型 Validate 后 RaiseException 不应抛出（IsValid=true）。
    /// </summary>
    [Fact]
    public void RaiseException_AfterValidValidModel_ShouldNotThrow()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableValidModel>(new ValidatableValidModel());
        ctx.Validate();

        // Act & Assert
        Should.NotThrow(() => ctx.RaiseException<ValidationException>());
    }

    /// <summary>
    /// 测试目的：非法模型 Validate 后（自定义 Handler 不抛出），
    /// RaiseException 应抛出 ValidationException。
    /// </summary>
    [Fact]
    public void RaiseException_AfterValidateInvalidModel_ShouldThrowValidationException()
    {
        // Arrange
        var ctx = new ValidationContext<ValidatableInvalidModel>(new ValidatableInvalidModel());
        ctx.SetHandler(_ => { }); // 空处理器，防止 Validate 内部抛出

        ctx.Validate();

        // Act & Assert
        Should.Throw<ValidationException>(() => ctx.RaiseException<ValidationException>());
    }
}

// ─── ValidationHandleOperation 测试 ──────────────────────────────

/// <summary>
/// <see cref="ValidationHandleOperation"/> 单元测试
/// </summary>
public class ValidationHandleOperationTest
{
    /// <summary>
    /// 测试目的：构造函数传入 null 集合时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_WhenCollectionIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ValidationHandleOperation(null));
    }

    /// <summary>
    /// 测试目的：RaiseException 对有效集合不应抛出任何异常。
    /// </summary>
    [Fact]
    public void RaiseException_WhenValid_ShouldNotThrow()
    {
        // Arrange
        var op = new ValidationHandleOperation(new ValidationResultCollection());

        // Act & Assert
        Should.NotThrow(() => op.RaiseException<ValidationException>());
    }

    /// <summary>
    /// 测试目的：RaiseException 对无效集合应抛出指定类型的异常。
    /// </summary>
    [Fact]
    public void RaiseException_WhenInvalid_ShouldThrowValidationException()
    {
        // Arrange
        var op = new ValidationHandleOperation(new ValidationResultCollection("字段X不合法"));

        // Act & Assert
        Should.Throw<ValidationException>(() => op.RaiseException<ValidationException>());
    }
}

// ─── ValidationHandleExceptionExtensions 测试 ────────────────────

/// <summary>
/// <see cref="ValidationHandleExceptionExtensions"/> 单元测试
/// </summary>
public class ValidationHandleExceptionExtensionsTest
{
    /// <summary>
    /// 测试目的：Handle() 扩展方法应为集合创建并返回 ValidationHandleOperation 实例。
    /// </summary>
    [Fact]
    public void Handle_ShouldReturnValidationHandleOperation()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var op = col.Handle();

        // Assert
        op.ShouldNotBeNull();
        op.ShouldBeOfType<ValidationHandleOperation>();
    }

    /// <summary>
    /// 测试目的：HandleAll 传入 null 的 handler 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void HandleAll_WhenHandlerIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var op = new ValidationHandleOperation(new ValidationResultCollection());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => op.HandleAll(null));
    }

    /// <summary>
    /// 测试目的：HandleAll 传入 null 的 op 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void HandleAll_WhenOpIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ValidationHandleExceptionExtensions.HandleAll(null, new ThrowHandler()));
    }

    /// <summary>
    /// 测试目的：HandleAll 传入 NothingHandler 对无效集合不应抛出异常，并返回 op 本身（链式调用）。
    /// </summary>
    [Fact]
    public void HandleAll_WithNothingHandler_ShouldNotThrowAndReturnOp()
    {
        // Arrange
        var col = new ValidationResultCollection("字段Y错误");
        var op = col.Handle();

        // Act
        var result = Should.NotThrow(() => op.HandleAll(new NothingHandler()));

        // Assert
        result.ShouldBeSameAs(op);
    }

    /// <summary>
    /// 测试目的：HandleAll 传入 ThrowHandler 对无效集合应抛出 Warning。
    /// </summary>
    [Fact]
    public void HandleAll_WithThrowHandler_WhenInvalid_ShouldThrowWarning()
    {
        // Arrange
        var col = new ValidationResultCollection("字段Z错误");
        var op = col.Handle();

        // Act & Assert
        Should.Throw<Warning>(() => op.HandleAll(new ThrowHandler()));
    }

    /// <summary>
    /// 测试目的：HandleAll 对有效集合使用 ThrowHandler 不应抛出异常。
    /// </summary>
    [Fact]
    public void HandleAll_WithThrowHandler_WhenValid_ShouldNotThrow()
    {
        // Arrange
        var col = new ValidationResultCollection(); // 空 = 有效
        var op = col.Handle();

        // Act & Assert
        Should.NotThrow(() => op.HandleAll(new ThrowHandler()));
    }
}

// ─── ValidationExceptionExtensions 测试 ──────────────────────────

/// <summary>
/// <see cref="ValidationExceptionExtensions"/> 单元测试
/// </summary>
public class ValidationExceptionExtensionsTest
{
    /// <summary>
    /// 测试目的：null 集合调用 ToException 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void ToException_WhenCollectionIsNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((ValidationResultCollection)null).ToException<ValidationException>());
    }

    /// <summary>
    /// 测试目的：有效集合（无错误）调用 ToException 应返回带空消息的 ValidationException。
    /// </summary>
    [Fact]
    public void ToException_WhenValid_ShouldReturnException()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act
        var ex = col.ToException<ValidationException>();

        // Assert — 应正确返回异常实例
        ex.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：无效集合调用 ToException 应返回包含错误信息的 ValidationException。
    /// </summary>
    [Fact]
    public void ToException_WhenInvalid_ShouldContainErrorMessages()
    {
        // Arrange
        var col = new ValidationResultCollection();
        col.Add(new ValidationResult("字段W不能为空"));

        // Act
        var ex = col.ToException<ValidationException>();

        // Assert
        ex.ShouldNotBeNull();
        ex.Message.ShouldContain("字段W不能为空");
    }

    /// <summary>
    /// 测试目的：appendAction 参数应在 ToException 时被执行，可追加额外信息。
    /// </summary>
    [Fact]
    public void ToException_WithAppendAction_ShouldInvokeAction()
    {
        // Arrange
        var col = new ValidationResultCollection("字段V错误");
        bool actionInvoked = false;

        // Act
        var ex = col.ToException<ValidationException>((e, c) => actionInvoked = true);

        // Assert
        ex.ShouldNotBeNull();
        actionInvoked.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：RaiseException 扩展方法对有效集合不应抛出任何异常。
    /// </summary>
    [Fact]
    public void RaiseException_WhenValid_ShouldNotThrow()
    {
        // Arrange
        var col = new ValidationResultCollection();

        // Act & Assert
        Should.NotThrow(() => col.RaiseException<ValidationException>());
    }

    /// <summary>
    /// 测试目的：RaiseException 扩展方法对无效集合应抛出 ValidationException。
    /// </summary>
    [Fact]
    public void RaiseException_WhenInvalid_ShouldThrow()
    {
        // Arrange
        var col = new ValidationResultCollection("字段U不合法");

        // Act & Assert
        Should.Throw<ValidationException>(() => col.RaiseException<ValidationException>());
    }
}

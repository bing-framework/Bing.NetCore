using System.Linq.Expressions;
using Bing.Domain.ChangeTracking;
using Bing.Validation;
using Bing.Validation.Strategies;

namespace Bing.Domain.Entities;

/// <summary>
/// 为领域对象提供验证、变更跟踪和描述输出的基础实现。
/// </summary>
public abstract class DomainObjectBase : IDomainObject, IChangeTrackable
{
    /// <summary>
    /// 存储当前对象与其他对象比较后生成的变更描述。
    /// </summary>
    protected internal readonly ChangeTrackingContext ChangeTrackingContext;

    /// <summary>
    /// 收集当前对象的文本描述片段。
    /// </summary>
    protected internal readonly DescriptionContext DescriptionContext;

    /// <summary>
    /// 初始化领域对象的验证辅助上下文。
    /// </summary>
    protected DomainObjectBase()
    {
        ChangeTrackingContext = new ChangeTrackingContext();
        DescriptionContext = new DescriptionContext();
    }

    /// <inheritdoc />
    public abstract IValidationResult Validate();

    #region ChangeTracking(变更跟踪)

    /// <inheritdoc />
    public ChangedValueDescriptorCollection GetChanges(object otherObject)
    {
        ChangeTrackingContext.FlushCache();
        if (otherObject == null)
            return ChangeTrackingContext.GetChangedValueDescriptor();
        // 使用内部方法，保证对象类型正确
        AddChangesInternal(otherObject);
        return ChangeTrackingContext.GetChangedValueDescriptor();
    }

    /// <summary>
    /// 将对象转换为具体领域类型并记录其变更。
    /// </summary>
    /// <param name="newObj">作为比较目标的新对象实例。</param>
    protected abstract void AddChangesInternal(object newObj);

    /// <summary>
    /// 添加单个属性的变更描述。
    /// </summary>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="propertyName">发生变更的属性名称。</param>
    /// <param name="description">属性的显示描述。</param>
    /// <param name="oldValue">变更前的属性值。</param>
    /// <param name="newValue">变更后的属性值。</param>
    protected void AddChange<TValue>(string propertyName, string description, TValue oldValue, TValue newValue) =>
        ChangeTrackingContext.Add(propertyName, description, oldValue, newValue);

    #endregion

    #region Descriptin(描述)

    /// <summary>
    /// 向描述上下文添加当前对象的描述信息。
    /// </summary>
    /// <remarks>基类不添加描述；派生类可重写该方法写入自身属性。</remarks>
    protected virtual void AddDescriptions() { }

    /// <summary>
    /// 添加原始描述文本。
    /// </summary>
    /// <param name="description">要添加的描述文本。</param>
    protected void AddDescription(string description) => DescriptionContext.Add(description);

    /// <summary>
    /// 添加具有显示名称和值的描述信息。
    /// </summary>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="name">属性显示名称。</param>
    /// <param name="value">要描述的属性值。</param>
    protected void AddDescription<TValue>(string name, TValue value) => DescriptionContext.Add(name, value);

    #endregion

    /// <summary>
    /// 输出当前对象的描述信息。
    /// </summary>
    /// <returns>基于当前对象状态重新生成的描述文本。</returns>
    public override string ToString()
    {
        DescriptionContext.FlushCache();
        AddDescriptions();
        return DescriptionContext.Output();
    }
}

/// <summary>
/// 为指定领域对象类型提供强类型验证和变更跟踪支持。
/// </summary>
/// <typeparam name="TObject">具体领域对象类型。</typeparam>
public abstract class DomainObjectBase<TObject> : DomainObjectBase, IVerifyModel<TObject>
    where TObject : class, IDomainObject, IVerifyModel<TObject>
{
    /// <summary>
    /// 延迟创建的强类型验证上下文。
    /// </summary>
    private readonly Lazy<ValidationContext<TObject>> _validationContext;

    /// <summary>
    /// 初始化 <see cref="DomainObjectBase{TObject}"/> 的实例。
    /// </summary>
    protected DomainObjectBase()
    {
        _validationContext = new Lazy<ValidationContext<TObject>>(() => new ValidationContext<TObject>(AssignableType(this)));
    }

    #region Validation(验证)

    /// <summary>
    /// 设置验证回调处理器。
    /// </summary>
    /// <param name="handler">处理验证回调的处理器。</param>
    public void SetValidationCallback(IValidationCallbackHandler handler) => 
        _validationContext.Value.SetHandler(op => op.HandleAll(handler));

    /// <summary>
    /// 启用全局验证规则。
    /// </summary>
    /// <exception cref="NotImplementedException">当前版本尚未实现全局验证规则配置时抛出。</exception>
    public void UseValidationRules()
    {
        // 全局规则的注册入口尚未实现，保留现有失败契约以避免静默跳过验证。
        throw new NotImplementedException();
    }

    /// <summary>
    /// 应用单个验证策略。
    /// </summary>
    /// <param name="strategy">要添加的验证策略。</param>
    public void UseStrategy(IValidationStrategy<TObject> strategy) => _validationContext.Value.AddStrategy(strategy);

    /// <summary>
    /// 应用多个验证策略。
    /// </summary>
    /// <param name="strategies">要添加的验证策略集合。</param>
    public void UseStrategyList(IEnumerable<IValidationStrategy<TObject>> strategies) => _validationContext.Value.AddStrategyList(strategies);

    /// <summary>
    /// 执行验证并返回结果集合。
    /// </summary>
    /// <returns>当前验证上下文收集的验证结果。</returns>
    public override IValidationResult Validate()
    {
        _validationContext.Value.Validate(Validate);
        return _validationContext.Value.GetValidationResultCollection();
    }

    /// <summary>
    /// 执行派生对象定义的自定义验证逻辑。
    /// </summary>
    /// <param name="results">用于收集验证结果的集合。</param>
    protected virtual void Validate(ValidationResultCollection results) { }

    #endregion

    #region ChangeTracking(变更跟踪)

    /// <inheritdoc />
    protected override void AddChangesInternal(object newObj)
    {
        if (newObj is not TObject typedObj)
            throw new InvalidOperationException($"对象类型不匹配: {newObj.GetType().FullName} ≠ {typeof(TObject).FullName}");
        AddChanges(typedObj);
    }

    /// <summary>
    /// 添加当前对象与指定对象之间的变更信息。
    /// </summary>
    /// <param name="newObj">作为比较目标的新对象实例。</param>
    protected virtual void AddChanges(TObject newObj) { }

    /// <summary>
    /// 记录当前对象指定属性与新值之间的变更。
    /// </summary>
    /// <typeparam name="TProperty">目标属性类型。</typeparam>
    /// <typeparam name="TValue">新值类型。</typeparam>
    /// <param name="expression">用于选择待比较属性的成员访问表达式。</param>
    /// <param name="newValue">属性变更后的值。</param>
    protected void AddChange<TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TValue newValue) => 
        ChangeTrackingContext.Add(expression, AssignableType(this), newValue);

    /// <summary>
    /// 记录两个可跟踪领域对象之间的变更。
    /// </summary>
    /// <typeparam name="TDomainObject">变更后的领域对象类型。</typeparam>
    /// <param name="beforeChange">变更前的可跟踪对象。</param>
    /// <param name="afterChange">变更后的领域对象。</param>
    protected void AddChange<TDomainObject>(IChangeTrackable beforeChange, TDomainObject afterChange) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(beforeChange, afterChange);

    /// <summary>
    /// 记录两个领域对象集合之间的变更。
    /// </summary>
    /// <typeparam name="TDomainObject">变更后领域对象的类型。</typeparam>
    /// <param name="leftObjs">变更前的可跟踪对象集合。</param>
    /// <param name="rightObjs">变更后的领域对象集合。</param>
    protected void AddChange<TDomainObject>(IEnumerable<IChangeTrackable> leftObjs, IEnumerable<TDomainObject> rightObjs) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(leftObjs, rightObjs);

    #endregion

    #region Description(描述)

    /// <summary>
    /// 根据当前对象的指定属性添加描述信息。
    /// </summary>
    /// <typeparam name="TProperty">属性值类型。</typeparam>
    /// <param name="expression">用于选择描述属性的成员访问表达式。</param>
    protected void AddDescription<TProperty>(Expression<Func<TObject, TProperty>> expression) =>
        DescriptionContext.Add(expression);

    #endregion

    #region Misc(杂项)

    /// <summary>
    /// 将当前基类实例转换为具体领域对象类型。
    /// </summary>
    /// <param name="me">要转换的领域对象基类实例。</param>
    /// <returns>具体领域对象实例。</returns>
    /// <exception cref="InvalidCastException">实例无法转换为 <typeparamref name="TObject"/> 时抛出。</exception>
    private TObject AssignableType(DomainObjectBase<TObject> me) => me as TObject ?? throw new InvalidCastException($"无法转换为 {typeof(TObject).FullName}");

    #endregion
}

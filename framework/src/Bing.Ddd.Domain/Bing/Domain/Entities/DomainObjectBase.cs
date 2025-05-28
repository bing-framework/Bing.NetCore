using System.Linq.Expressions;
using Bing.Domain.ChangeTracking;
using Bing.Validation;
using Bing.Validation.Strategies;

namespace Bing.Domain.Entities;

/// <summary>
/// 领域对象基类
/// </summary>
public abstract class DomainObjectBase : IDomainObject, IChangeTrackable
{
    /// <summary>
    /// 变更跟踪上下文
    /// </summary>
    protected internal readonly ChangeTrackingContext ChangeTrackingContext;

    /// <summary>
    /// 描述上下文
    /// </summary>
    protected internal readonly DescriptionContext DescriptionContext;

    /// <summary>
    /// 初始化一个<see cref="DomainObjectBase"/>类型的实例
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
    /// 内部方法，供泛型 <see cref="DomainObjectBase{TObject}"/> 处理对象类型。
    /// </summary>
    /// <param name="newObj">新的对象实例</param>
    protected abstract void AddChangesInternal(object newObj);

    /// <summary>
    /// 添加单个属性的变更信息。
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="description">描述</param>
    /// <param name="oldValue">变更前的值。范例：this.Name</param>
    /// <param name="newValue">变更后的值。范例：newEntity.Name</param>
    protected void AddChange<TValue>(string propertyName, string description, TValue oldValue, TValue newValue) =>
        ChangeTrackingContext.Add(propertyName, description, oldValue, newValue);

    #endregion

    #region Descriptin(描述)

    /// <summary>
    /// 添加对象描述信息。
    /// </summary>
    protected virtual void AddDescriptions() { }

    /// <summary>
    /// 添加描述文本。
    /// </summary>
    /// <param name="description">描述文本</param>
    protected void AddDescription(string description) => DescriptionContext.Add(description);

    /// <summary>
    /// 添加带名称的描述信息。
    /// </summary>
    /// <typeparam name="TValue">属性类型</typeparam>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    protected void AddDescription<TValue>(string name, TValue value) => DescriptionContext.Add(name, value);

    #endregion

    /// <summary>
    /// 输出对象的描述信息。
    /// </summary>
    /// <returns>对象描述信息</returns>
    public override string ToString()
    {
        DescriptionContext.FlushCache();
        AddDescriptions();
        return DescriptionContext.Output();
    }
}

/// <summary>
/// 领域对象基类
/// </summary>
/// <typeparam name="TObject">领域对象</typeparam>
public abstract class DomainObjectBase<TObject> : DomainObjectBase, IVerifyModel<TObject>
    where TObject : class, IDomainObject, IVerifyModel<TObject>
{
    /// <summary>
    /// 验证上下文
    /// </summary>
    private readonly Lazy<ValidationContext<TObject>> _validationContext;

    /// <summary>
    /// 初始化一个<see cref="DomainObjectBase{TObject}"/>类型的实例
    /// </summary>
    protected DomainObjectBase()
    {
        _validationContext = new Lazy<ValidationContext<TObject>>(() => new ValidationContext<TObject>(AssignableType(this)));
    }

    #region Validation(验证)

    /// <summary>
    /// 设置验证回调处理器。
    /// </summary>
    /// <param name="handler">验证回调处理器</param>
    public void SetValidationCallback(IValidationCallbackHandler handler) => 
        _validationContext.Value.SetHandler(op => op.HandleAll(handler));

    /// <summary>
    /// 启用全局验证规则。
    /// </summary>
    public void UseValidationRules()
    {
        // TODO: 具体实现逻辑
        throw new NotImplementedException();
    }

    /// <summary>
    /// 应用单个验证策略。
    /// </summary>
    /// <param name="strategy">验证策略</param>
    public void UseStrategy(IValidationStrategy<TObject> strategy) => _validationContext.Value.AddStrategy(strategy);

    /// <summary>
    /// 应用多个验证策略。
    /// </summary>
    /// <param name="strategies">验证策略集合</param>
    public void UseStrategyList(IEnumerable<IValidationStrategy<TObject>> strategies) => _validationContext.Value.AddStrategyList(strategies);

    /// <summary>
    /// 执行验证并返回验证结果。
    /// </summary>
    /// <returns>验证结果集合</returns>
    public override IValidationResult Validate()
    {
        _validationContext.Value.Validate(Validate);
        return _validationContext.Value.GetValidationResultCollection();
    }

    /// <summary>
    /// 执行自定义验证逻辑。
    /// </summary>
    /// <param name="results">验证结果集合</param>
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
    /// 添加对象变更信息。
    /// </summary>
    /// <param name="newObj">新的对象实例</param>
    protected virtual void AddChanges(TObject newObj) { }

    /// <summary>
    /// 添加对象属性值的变更。
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="expression">属性表达式。范例：t => t.Name</param>
    /// <param name="newValue">新值。范例：newEntity.Name</param>
    protected void AddChange<TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TValue newValue) => 
        ChangeTrackingContext.Add(expression, AssignableType(this), newValue);

    /// <summary>
    /// 添加对象间的变更。
    /// </summary>
    /// <param name="beforeChange">对象变更前跟踪</param>
    /// <param name="afterChange">变更后的对象</param>
    protected void AddChange<TDomainObject>(IChangeTrackable beforeChange, TDomainObject afterChange) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(beforeChange, afterChange);

    /// <summary>
    /// 添加对象集合的变更。
    /// </summary>
    /// <param name="leftObjs">左对象列表</param>
    /// <param name="rightObjs">右对象列表</param>
    protected void AddChange<TDomainObject>(IEnumerable<IChangeTrackable> leftObjs, IEnumerable<TDomainObject> rightObjs) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(leftObjs, rightObjs);

    #endregion

    #region Descriptin(描述)

    /// <summary>
    /// 添加基于属性的描述信息。
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="expression">属性表达式。范例：t => t.Name</param>
    protected void AddDescription<TProperty>(Expression<Func<TObject, TProperty>> expression) =>
        DescriptionContext.Add(expression);

    #endregion

    #region Misc(杂项)

    /// <summary>
    /// 获取当前对象的可分配类型、
    /// </summary>
    /// <param name="me">领域对象基类</param>
    /// <returns>可分配的对象</returns>
    private TObject AssignableType(DomainObjectBase<TObject> me) => me as TObject ?? throw new InvalidCastException($"无法转换为 {typeof(TObject).FullName}");

    #endregion
}

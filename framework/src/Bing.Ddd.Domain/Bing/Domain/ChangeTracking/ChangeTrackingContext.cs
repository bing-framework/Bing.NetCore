using System.Linq.Expressions;
using Bing.Domain.ChangeTracking.Internal;
using Bing.Domain.Entities;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 变更跟踪上下文
/// </summary>
public sealed class ChangeTrackingContext
{
    /// <summary>
    /// 变更值集合
    /// </summary>
    private readonly ChangedValueDescriptorCollection _changedValueCollection;

    /// <summary>
    /// 初始化一个 <see cref="ChangeTrackingContext"/> 类型的实例。
    /// </summary>
    public ChangeTrackingContext() => _changedValueCollection = new ChangedValueDescriptorCollection();

    /// <summary>
    /// 初始化一个 <see cref="ChangeTrackingContext"/> 类型的实例。
    /// </summary>
    /// <param name="collection">变更值集合</param>
    public ChangeTrackingContext(ChangedValueDescriptorCollection collection) =>
        _changedValueCollection = collection == null
            ? new ChangedValueDescriptorCollection()
            : new ChangedValueDescriptorCollection(collection);

    /// <summary>
    /// 获取当前调用真正归属本上下文的 frame 结果缓冲。
    /// </summary>
    /// <remarks>只有 frame 的所属上下文与当前实例一致时才写入 frame；独立 Context 继续使用自身缓冲。</remarks>
    private ChangedValueDescriptorCollection ActiveTarget =>
        ChangeTrackingSession.Current is { } frame && ReferenceEquals(frame.Owner, this)
            ? frame.Result
            : _changedValueCollection;

    /// <summary>
    /// 记录属性值变更。
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="description">描述</param>
    /// <param name="valueBeforeChange">变更前的值。范例：this.Name</param>
    /// <param name="valueAfterChange">变更后的值。范例：newEntity.Name</param>
    /// <remarks>
    /// 使用类型值语义判定差异；字符串区分大小写、不去空格，展示时保留源值内容。
    /// </remarks>
    public void Add<TValue>(string propertyName, string description, TValue valueBeforeChange, TValue valueAfterChange)
    {
        if (EqualityComparer<TValue>.Default.Equals(valueBeforeChange, valueAfterChange))
            return;
        var oldText = ScalarValueFormatter.Format(valueBeforeChange);
        var newText = ScalarValueFormatter.Format(valueAfterChange);
        ActiveTarget.Add(propertyName, description, oldText, newText);
    }

    /// <summary>
    /// 记录对象变更。
    /// </summary>
    /// <param name="leftObj">左对象</param>
    /// <param name="rightObj">右对象</param>
    public void Add(IChangeTrackable leftObj, object rightObj)
    {
        if (Equals(leftObj, null))
            return;
        if (Equals(rightObj, null))
            return;
        if (leftObj.GetType() != rightObj.GetType())
            throw new InvalidOperationException($"无法进行对象比较，类型不匹配: {leftObj.GetType().FullName} ≠ {rightObj.GetType().FullName}");

        var changes = leftObj.GetChanges(rightObj);
        var target = ChangeTrackingSession.Current;
        if (target == null || !ReferenceEquals(target.Owner, this))
        {
            foreach (var descriptor in changes)
                _changedValueCollection.Add(descriptor);
        }
        else
            ChangeTrackingSession.Merge(target, string.Empty, changes);
    }

    /// <summary>
    /// 记录对象集合变更。
    /// </summary>
    /// <typeparam name="TObject">领域对象类型</typeparam>
    /// <param name="leftObjs">左对象列表</param>
    /// <param name="rightObjs">右对象列表</param>
    public void Add<TObject>(IEnumerable<IChangeTrackable> leftObjs, IEnumerable<TObject> rightObjs)
        where TObject : IDomainObject
    {
        if (Equals(leftObjs, null))
            return;
        if (Equals(rightObjs, null))
            return;

        // 转换为列表，避免重复遍历
        var leftObjList = leftObjs.ToList();
        var rightObjList = rightObjs.ToList();

        // 手动集合入口保持按位置配对语义；自动生成入口使用带身份匹配的关系 helper。
        var length = Math.Min(leftObjList.Count, rightObjList.Count);

        for (var i = 0; i < length; i++)
            Add(leftObjList[i], rightObjList[i]);
    }

    /// <summary>
    /// 记录成员值变更。
    /// </summary>
    /// <typeparam name="TObject">领域对象类型</typeparam>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="expression">属性表达式。范例：t => t.Name</param>
    /// <param name="obj">领域对象</param>
    /// <param name="newValue">新值。范例：newEntity.Name</param>
    public void Add<TObject, TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TObject obj, TValue newValue) 
        where TObject : IDomainObject
    {
        var member = Lambdas.GetMemberExpression(expression);
        var name = Lambdas.GetMemberName(member);
        var desc = Reflection.Reflections.GetDisplayNameOrDescription(member.Member);
        var value = member.Member.GetPropertyValue(obj);
        Add(name, desc, Conv.To<TValue>(value), newValue);
    }

    /// <summary>
    /// 用变更描述符集合填充变更跟踪结果。
    /// </summary>
    /// <param name="collection">变更值集合</param>
    public void Populate(ChangedValueDescriptorCollection collection) => ActiveTarget.Populate(collection);

    /// <summary>
    /// 刷新缓存
    /// </summary>
    public void FlushCache() => ActiveTarget.FlushCache();

    /// <summary>
    /// 获取变更值集合
    /// </summary>
    /// <returns>当前调用 frame 或上下文自身持有的变更值描述符集合。</returns>
    public ChangedValueDescriptorCollection GetChangedValueDescriptor() => ActiveTarget;

    /// <summary>
    /// 输出变更描述集合的文本表示。
    /// </summary>
    /// <returns>当前上下文中的变更信息文本。</returns>
    public string Output() => GetChangedValueDescriptor().ToString();

    /// <inheritdoc />
    public override string ToString() => Output();
}

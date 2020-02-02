using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bing.Domains.Entities;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Domains.ChangeTracking
{
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
        /// 初始化一个<see cref="ChangeTrackingContext"/>类型的实例
        /// </summary>
        public ChangeTrackingContext() => _changedValueCollection = new ChangedValueDescriptorCollection();

        /// <summary>
        /// 初始化一个<see cref="ChangeTrackingContext"/>类型的实例
        /// </summary>
        /// <param name="collection">变更值集合</param>
        public ChangeTrackingContext(ChangedValueDescriptorCollection collection) =>
            _changedValueCollection = collection == null
                ? new ChangedValueDescriptorCollection()
                : new ChangedValueDescriptorCollection(collection);

        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="propertyName">属性名</param>
        /// <param name="description">描述</param>
        /// <param name="valueBeforeChange">变更前的值。范例：this.Name</param>
        /// <param name="valueAfterChange">变更后的值。范例：newEntity.Name</param>
        public void Add<TValue>(string propertyName, string description, TValue valueBeforeChange,
            TValue valueAfterChange)
        {
            if (Equals(valueBeforeChange, valueAfterChange))
                return;
            var stringBeforeChange = valueBeforeChange.SafeString().ToLower().Trim();
            var stringAfterChange = valueAfterChange.SafeString().ToLower().Trim();
            if (stringBeforeChange == stringAfterChange)
                return;
            _changedValueCollection.Add(propertyName, description, stringBeforeChange, stringAfterChange);
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="TObject">领域对象类型</typeparam>
        /// <param name="leftObj">左对象</param>
        /// <param name="rightObj">右对象</param>
        public void Add<TObject>(IChangeTrackable<TObject> leftObj, TObject rightObj) where TObject : IDomainObject
        {
            if (Equals(leftObj, null))
                return;
            if (Equals(rightObj, null))
                return;
            _changedValueCollection.AddRange(leftObj.GetChanges(rightObj));
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="TObject">领域对象类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="expression">属性表达式。范例：t => t.Name</param>
        /// <param name="newValue">新值。范例：newEntity.Name</param>
        public void Add<TObject, TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TValue newValue) where TObject : IDomainObject
        {
            var member = Lambda.GetMemberExpression(expression);
            var name = Lambda.GetMemberName(member);
            var desc = Reflection.GetDisplayNameOrDescription(member.Member);
            var value = Lambda.GetValue(expression);
            Add(name, desc, Conv.To<TValue>(value), newValue);
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="TObject">领域对象类型</typeparam>
        /// <param name="leftObjs">左对象列表</param>
        /// <param name="rightObjs">右对象列表</param>
        public void Add<TObject>(IEnumerable<IChangeTrackable<TObject>> leftObjs, IEnumerable<TObject> rightObjs)
            where TObject : IDomainObject
        {
            if (Equals(leftObjs, null))
                return;
            if (Equals(rightObjs, null))
                return;
            var leftObjList = leftObjs.ToList();
            var rightObjList = rightObjs.ToList();

            var length = leftObjList.Capacity > rightObjList.Count
                ? rightObjList.Count
                : leftObjList.Count;
            for (var i = 0; i < length; i++)
                Add(leftObjList[i], rightObjList[i]);
        }

        /// <summary>
        /// 填充
        /// </summary>
        /// <param name="collection">变更值集合</param>
        public void Populate(ChangedValueDescriptorCollection collection) => _changedValueCollection.Populate(collection);

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void FlushCache() => _changedValueCollection.FlushCache();

        /// <summary>
        /// 获取变更值集合
        /// </summary>
        public ChangedValueDescriptorCollection GetChangedValueDescriptor() => _changedValueCollection;

        /// <summary>
        /// 输出
        /// </summary>
        public string Output() => _changedValueCollection.ToString();

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString() => Output();
    }
}

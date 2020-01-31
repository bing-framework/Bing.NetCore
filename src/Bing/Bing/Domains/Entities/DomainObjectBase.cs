using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Domains.ChangeTracking;
using Bing.Domains.Core;
using Bing.Validations;
using Bing.Validations.Abstractions;
using Bing.Validations.Handlers;
using IValidationHandler = Bing.Validations.Abstractions.IValidationHandler;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 领域对象基类
    /// </summary>
    /// <typeparam name="TObject">领域对象</typeparam>
    public abstract class DomainObjectBase<TObject> : IDomainObject, IValidatable<TObject>, IChangeTrackable<TObject>
        where TObject : class, IDomainObject
    {
        /// <summary>
        /// 验证上下文
        /// </summary>
        private readonly ValidationContext<TObject> _validationContext;

        /// <summary>
        /// 描述上下文
        /// </summary>
        private readonly DescriptionContext _descriptionContext;

        /// <summary>
        /// 变更跟踪上下文
        /// </summary>
        private readonly ChangeTrackingContext _changeTrackingContext;

        /// <summary>
        /// 初始化一个<see cref="DomainObjectBase{TObject}"/>类型的实例
        /// </summary>
        protected DomainObjectBase()
        {
            _validationContext = new ValidationContext<TObject>(AssignableType(this));
            _descriptionContext = new DescriptionContext();
            _changeTrackingContext = new ChangeTrackingContext();
            SetValidateHandler(new ThrowHandler());
        }

        #region Validation(验证)

        /// <summary>
        /// 设置验证处理器
        /// </summary>
        /// <param name="handler">验证处理器</param>
        public void SetValidateHandler(IValidationHandler handler) => _validationContext.SetHandler(op => op.HandleAll(handler));

        /// <summary>
        /// 添加验证策略
        /// </summary>
        /// <param name="strategy">验证策略</param>
        public void AddStrategy(IValidateStrategy<TObject> strategy) => _validationContext.AddStrategy(strategy);

        /// <summary>
        /// 添加验证策略集合
        /// </summary>
        /// <param name="strategies">验证策略集合</param>
        public void AddStrategyList(IEnumerable<IValidateStrategy<TObject>> strategies) => _validationContext.AddStrategyList(strategies);

        /// <summary>
        /// 验证
        /// </summary>
        public virtual ValidationResultCollection Validate()
        {
            _validationContext.Validate(Validate);
            return _validationContext.GetValidationResultCollection();
        }

        /// <summary>
        /// 验证并添加到验证结果集合
        /// </summary>
        /// <param name="results"></param>
        protected virtual void Validate(ValidationResultCollection results) { }

        #endregion

        #region ChangeTracking(变更跟踪)

        /// <summary>
        /// 添加变更列表
        /// </summary>
        /// <param name="newObj">新对象</param>
        protected virtual void AddChanges(TObject newObj) { }

        /// <summary>
        /// 添加变更
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="expression">属性表达式。范例：t => t.Name</param>
        /// <param name="newValue">新值。范例：newEntity.Name</param>
        protected void AddChange<TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TValue newValue) => _changeTrackingContext.Add(expression,newValue);

        /// <summary>
        /// 添加变更
        /// </summary>
        /// <param name="objectBeforeChangeTrackable">对象变更前跟踪</param>
        /// <param name="objectAfterChange">变更后的对象</param>
        protected void AddChange(IChangeTrackable<TObject> objectBeforeChangeTrackable, TObject objectAfterChange) => _changeTrackingContext.Add(objectBeforeChangeTrackable,objectAfterChange);

        /// <summary>
        /// 添加变更
        /// </summary>
        /// <param name="leftObjs">左对象列表</param>
        /// <param name="rightObjs">右对象列表</param>
        protected void AddChange(IEnumerable<IChangeTrackable<TObject>> leftObjs, IEnumerable<TObject> rightObjs) => _changeTrackingContext.Add(leftObjs,rightObjs);

        /// <summary>
        /// 添加变更
        /// </summary>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="propertyName">属性名</param>
        /// <param name="description">描述</param>
        /// <param name="valueBeforeChange">变更前的值。范例：this.Name</param>
        /// <param name="valueAfterChange">变更后的值。范例：newEntity.Name</param>
        protected void AddChange<TValue>(string propertyName, string description, TValue valueBeforeChange,
            TValue valueAfterChange) =>
            _changeTrackingContext.Add(propertyName,description,valueBeforeChange,valueAfterChange);

        /// <summary>
        /// 获取变更值集合
        /// </summary>
        /// <param name="otherObj">其它对象</param>
        public ChangedValueDescriptorCollection GetChanges(TObject otherObj)
        {
            _changeTrackingContext.FlushCache();
            if (Equals(otherObj, null))
                return _changeTrackingContext.GetChangedValueDescriptor();
            AddChanges(otherObj);
            return _changeTrackingContext.GetChangedValueDescriptor();
        }

        #endregion

        #region Descriptin(描述)

        /// <summary>
        /// 添加描述
        /// </summary>
        protected virtual void AddDescriptions() { }

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <param name="description">描述</param>
        protected void AddDescription(string description) => _descriptionContext.Add(description);

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <typeparam name="TValue">属性类型</typeparam>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        protected void AddDescription<TValue>(string name, TValue value) => _descriptionContext.Add(name, value);

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="expression">属性表达式。范例：t => t.Name</param>
        protected void AddDescription<TProperty>(Expression<Func<TObject, TProperty>> expression) =>
            _descriptionContext.Add(expression);

        #endregion

        #region Misc(杂项)

        /// <summary>
        /// 分配类型
        /// </summary>
        /// <param name="me">领域对象基类</param>
        private TObject AssignableType(DomainObjectBase<TObject> me) => me as TObject;

        #endregion

        /// <summary>
        /// 输出对象状态
        /// </summary>
        public override string ToString()
        {
            _descriptionContext.FlushCache();
            AddDescriptions();
            return _descriptionContext.Output();
        }
    }
}

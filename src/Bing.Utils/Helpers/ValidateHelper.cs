using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Class ValidateHelper.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ValidateHelper<T> where T : class
    {
        /// <summary>
        /// The dic validations
        /// </summary>
        private Dictionary<string, List<ValidationAttribute>> dicValidations = new Dictionary<string, List<ValidationAttribute>>();

        /// <summary>
        /// 错误列表
        /// </summary>
        private Dictionary<string, string> dErrors = new Dictionary<string, string>();

        /// <summary>
        /// 是否实现IDataErrorInfo接口
        /// </summary>
        private bool isIDataErrorInfo;

        /// <summary>
        /// 需要验证对象的实例
        /// </summary>
        private T validateObj;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="obj">The object.</param>
        public ValidateHelper(T obj)
        {
            RegisterEvent(obj);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="errors">The errors.</param>
        public ValidateHelper(T obj, Dictionary<string, string> errors)
        {
            RegisterEvent(obj);
            Register(errors);
        }

        /// <summary>
        /// 注册需要验证的属性和验证特性
        /// </summary>
        /// <param name="expr">属性</param>
        /// <param name="metadatas">特性</param>
        /// <returns>ValidateHelper{`0}.</returns>
        public ValidateHelper<T> Register(Expression<Func<T, object>> expr, List<ValidationAttribute> metadatas)
        {
            var prpName = GetExprName(expr);
            if (string.IsNullOrEmpty(prpName)) return this;
            if (dicValidations.ContainsKey(prpName))
            {
                dicValidations[prpName].AddRange(metadatas);
            }
            else
            {
                dicValidations.Add(prpName, metadatas);
            }
            return this;
        }

        /// <summary>
        /// 注册需要验证的属性和验证特性
        /// </summary>
        /// <param name="expr">属性</param>
        /// <param name="metadata">特性</param>
        /// <returns>ValidateHelper{`0}.</returns>
        public ValidateHelper<T> Register(Expression<Func<T, object>> expr, ValidationAttribute metadata)
        {
            var prpName = GetExprName(expr);
            if (string.IsNullOrEmpty(prpName))
                return this;
            var key = prpName;
            if (dicValidations.ContainsKey(key))
            {
                var metadatas = dicValidations[key];
                metadatas.Add(metadata);
            }
            else
            {
                var list = new List<ValidationAttribute>();
                list.Add(metadata);
                dicValidations.Add(key, list);
            }
            return this;
        }

        /// <summary>
        /// 如果是IDataErrorInfo接口，注册错误字典
        /// </summary>
        /// <param name="dataErrors">错误字典</param>
        /// <returns>ValidateHelper{`0}.</returns>
        public ValidateHelper<T> Register(Dictionary<string, string> dataErrors)
        {
            dErrors = dataErrors;
            return this;
        }

        /// <summary>
        /// 注册，自动提取特性注册
        /// </summary>
        /// <returns>ValidateHelper{`0}.</returns>
        public ValidateHelper<T> Register()
        {
            var properties = typeof(T).GetProperties();
            var baseType = typeof(ValidationAttribute);
            var metadatas = new List<ValidationAttribute>();
            foreach (var property in properties)
            {
                var atts = property.GetCustomAttributes(false);
                foreach (var att in atts)
                {
                    if (att.GetType().IsSubclassOf(baseType))
                    {
                        metadatas.Add((ValidationAttribute)att);
                    }
                }
                dicValidations.Add(property.Name, metadatas);
            }
            return this;
        }

        /// <summary>
        /// 反注册
        /// </summary>
        public void UnRegister()
        {
            if (validateObj != null)
            {
                var type = validateObj.GetType();
                if (typeof(INotifyPropertyChanged).IsAssignableFrom(type))
                {
                    ((INotifyPropertyChanged)validateObj).PropertyChanged -= PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Validates this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public bool Validate()
        {
            if (isIDataErrorInfo && dErrors != null)
            {
                if (dErrors.Count > 0)
                    return false;
            }

            //获取silverlight获取wpf的change事件
            var notify = GetPropertyChangedMethod(validateObj);

            var erroCount = 0;
            //触发所有的属性验证
            foreach (var key in dicValidations.Keys)
            {
                if (dErrors != null)
                    erroCount = dErrors.Count;
                if (notify != null)
                {
                    notify.Invoke(validateObj, new object[] { key });

                    //手动触发验证事件时，IDataError的验证事件会先触发
                    //然后才触发当前验证内的PropertyChanged属性，所以需要2次触发
                    if (dErrors != null && dErrors.Count != erroCount)
                    {
                        notify.Invoke(validateObj, new object[] { key });
                    }
                }
                else
                {
                    PropertyChanged(validateObj, new PropertyChangedEventArgs(key));
                }
            }

            if (isIDataErrorInfo && dErrors != null)
            {
                if (dErrors.Count > 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 处理属性变化事件
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs" /> instance containing the event data.</param>
        protected virtual void PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var value = GetPropertyValue(sender, e.PropertyName);
            if (dicValidations.ContainsKey(e.PropertyName))
            {
                var context = new ValidationContext(validateObj, null, null) { MemberName = e.PropertyName };
                var metadatas = dicValidations[e.PropertyName];
                foreach (var metadata in metadatas)
                {
                    try
                    {
                        metadata.Validate(value, context);
                    }
                    catch (Exception ex)
                    {
                        if (isIDataErrorInfo && dErrors != null)
                        {
                            if (dErrors.ContainsKey(e.PropertyName))
                            {
                                dErrors[e.PropertyName] = ex.Message;
                            }
                            else
                            {
                                dErrors.Add(e.PropertyName, ex.Message);
                            }
                            return;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                if (isIDataErrorInfo && dErrors != null && dErrors.ContainsKey(e.PropertyName))
                {
                    dErrors.Remove(e.PropertyName);
                }
            }
        }

        /// <summary>
        /// 注册属性变更事件.
        /// </summary>
        /// <param name="obj">The object.</param>
        private void RegisterEvent(T obj)
        {
            validateObj = obj;
            var type = obj.GetType();
            if (typeof(INotifyPropertyChanged).IsAssignableFrom(type))
            {
                ((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged;
            }
            isIDataErrorInfo = typeof(IDataErrorInfo).IsAssignableFrom(type);
        }

        /// <summary>
        /// 获取属性，名称.
        /// </summary>
        /// <param name="expr">The expr.</param>
        /// <returns>System.String.</returns>
        private string GetExprName(Expression<Func<T, object>> expr)
        {
            string prpName;
            switch (expr.Body.NodeType)
            {
                case ExpressionType.MemberAccess:
                    prpName = ((MemberExpression)expr.Body).Member.Name;
                    break;

                case ExpressionType.Convert:
                    prpName = ((MemberExpression)((UnaryExpression)expr.Body).Operand).Member.Name;
                    break;

                default:
                    prpName = string.Empty;
                    break;
            }
            return prpName;
        }

        /// <summary>
        /// 获取属性值.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>System.Object.</returns>
        private object GetPropertyValue(object sender, string propertyName)
        {
            var property = sender.GetType().GetProperty(propertyName);
            if (property != null)
            {
                return property.GetValue(sender, null);
            }
            return null;
        }

        /// <summary>
        /// 获取触发事件的methodinfo.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>MethodInfo.</returns>
        private MethodInfo GetPropertyChangedMethod(object sender)
        {
            //因为silverlight的安全模型中，对public以外的方法都不运行反射调用
            //只能要求需要验证的类都提供一个NotifyPropertyChanged的public 方法
            var methods = sender.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                //查找包含PropertyChanged的方法
                if (method.Name.Contains("PropertyChanged") && !method.Name.Contains("add_") && !method.Name.Contains("remove_"))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the event member,silverlight下 GetValue抛出fiedlaccessexception，不能使用这类方法来处理.
        /// </summary>
        /// <param name="bindableObject">The bindable object.</param>
        /// <returns>System.Object.</returns>
        private object GetEventMember(object bindableObject)
        {
            // get the internal eventDelegate
            var bindableObjectType = bindableObject.GetType();
            FieldInfo propChangedFieldInfo = null;
            while (bindableObjectType != null)
            {
                propChangedFieldInfo = bindableObjectType.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                if (propChangedFieldInfo != null)
                    break;
                bindableObjectType = bindableObjectType.BaseType;
            }

            if (propChangedFieldInfo == null) return null;
            // get prop changed event field value
            //silverlight下 GetValue抛出fiedlaccessexception，不能使用这类方法来处理
            var fieldValue = propChangedFieldInfo.GetValue(bindableObject);
            if (fieldValue == null)
                return null;

            return null;
        }
    }
}

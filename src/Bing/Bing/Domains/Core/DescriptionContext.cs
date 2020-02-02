using System;
using System.Linq.Expressions;
using System.Text;
using Bing.Helpers;
using Bing.Utils.Extensions;

namespace Bing.Domains.Core
{
    /// <summary>
    /// 描述上下文
    /// </summary>
    public sealed class DescriptionContext
    {
        /// <summary>
        /// 字符串拼接器
        /// </summary>
        private readonly StringBuilder _stringBuilder;

        /// <summary>
        /// 初始化一个<see cref="DescriptionContext"/>类型的实例
        /// </summary>
        public DescriptionContext() => _stringBuilder = new StringBuilder();

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <param name="description">描述</param>
        public void Add(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return;
            _stringBuilder.Append(description);
        }

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        public void Add<TValue>(string name, TValue value)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (value == null || value.Equals(default(TValue)) || string.IsNullOrWhiteSpace(value.ToString()))
                return;
            _stringBuilder.Append($"{name}:{value},");
        }

        /// <summary>
        /// 添加描述
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <typeparam name="TProperty">属性类型</typeparam>
        /// <param name="expression">属性表达式，范例：t => t.Name</param>
        public void Add<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            var member = Lambda.GetMember(expression);
            var description = Reflection.GetDisplayNameOrDescription(member);
            var value = member.GetPropertyValue(this);
            if (Reflection.IsBool(member))
                value = Conv.ToBool(value).Description();
            Add(description, value);
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        public void FlushCache() => _stringBuilder.Clear();

        /// <summary>
        /// 输出
        /// </summary>
        public string Output()
        {
            if (_stringBuilder.Length == 0)
                return string.Empty;
            return _stringBuilder.ToString().TrimEnd().TrimEnd(',');
        }

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString() => Output();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// 名称项，处理名称中包含符号
    /// </summary>
    public class NameItem
    {
        /// <summary>
        /// 前缀
        /// </summary>
        private string _prefix;

        /// <summary>
        /// 前缀
        /// </summary>
        public string Prefix
        {
            get => _prefix.SafeString();
            set => _prefix = value;
        }

        /// <summary>
        /// 名称
        /// </summary>
        private string _name;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get => _name.SafeString();
            set => _name = value;
        }

        /// <summary>
        /// 初始化一个<see cref="NameItem"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        public NameItem(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var list = IsComplex(name) ? ResolveByPattern(name) : ResolveSplit(name);
            if (list.Count == 1)
            {
                Name = list[0];
                return;
            }

            if (list.Count == 2)
            {
                Prefix = list[0];
                Name = list[1];
            }
        }

        /// <summary>
        /// 是否复杂名称。包含多个句点的名称为复杂名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        private bool IsComplex(string name)
        {
            return name.IndexOf(".", StringComparison.CurrentCulture) !=
                   name.LastIndexOf(".", StringComparison.CurrentCulture);
        }

        /// <summary>
        /// 分割句点
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        private List<string> ResolveSplit(string name)
        {
            return name.Split('.').ToList();
        }

        /// <summary>
        /// 通过正则表达式进行解析
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        private List<string> ResolveByPattern(string name)
        {
            var pattern = "^([\\[`\"]\\S+[\\]`\"]).([\\[`\"]\\S+[\\]`\"])$";
            return Regexs.GetValues(name, pattern, new[] {"$1", "$2"}).Select(t => t.Value).ToList();
        }

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        public string ToSql(IDialect dialect, string prefix)
        {
            Prefix = GetPrefix(prefix);
            return string.IsNullOrWhiteSpace(Prefix)
                ? dialect.SafeName(Name)
                : $"{dialect.SafeName(Prefix)}.{dialect.SafeName(Name)}";
        }

        /// <summary>
        /// 获取前缀
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        private string GetPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(Prefix))
            {
                return prefix;
            }
            return Prefix;
        }
    }
}

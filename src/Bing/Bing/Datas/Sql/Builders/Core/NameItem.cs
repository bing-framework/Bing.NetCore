using System.Collections.Generic;
using System.Linq;
using Bing.Datas.Sql.Matedatas;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// 名称项，处理名称中包含符号.
    /// </summary>
    public class NameItem
    {
        #region 属性

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DatabaseName { get; private set; }

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

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="NameItem"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        public NameItem(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            var list = IsComplex(name) ? ResolveByPattern(name) : ResolveBySplit(name);
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
            if (list.Count == 3)
            {
                DatabaseName = list[0];
                Prefix = list[1];
                Name = list[2];
            }
        }

        /// <summary>
        /// 是否复杂名称
        /// </summary>
        /// <param name="name">名称</param>
        private bool IsComplex(string name) => name.Contains("[") || name.Contains("`") || name.Contains("\"");

        /// <summary>
        /// 分割句点
        /// </summary>
        /// <param name="name">名称</param>
        private List<string> ResolveBySplit(string name) => name.Split('.').ToList();

        /// <summary>
        /// 通过正则表达式进行解析
        /// </summary>
        /// <param name="name">名称</param>
        private List<string> ResolveByPattern(string name)
        {
            var pattern = "^(([\\[`\"]\\S+?[\\]`\"]).)?(([\\[`\"]\\S+[\\]`\"]).)?([\\[`\"]\\S+[\\]`\"])$";
            var list = Regexs.GetValues(name, pattern, new[] { "$1", "$2", "$3", "$4", "$5" }).Select(t => t.Value).ToList();
            return list.Where(t => string.IsNullOrWhiteSpace(t) == false && t.EndsWith(".") == false).ToList();
        }

        #endregion

        #region ToSql(获取Sql)

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="prefix">前缀</param>
        /// <param name="tableDatabase">表数据库</param>
        public string ToSql(IDialect dialect, string prefix = null, ITableDatabase tableDatabase = null)
        {
            var name = GetName(dialect, prefix);
            var database = GetDatabase(dialect, tableDatabase, prefix);
            return string.IsNullOrWhiteSpace(database) ? name : $"{database}.{name}";
        }

        /// <summary>
        /// 获取前缀
        /// </summary>
        /// <param name="prefix">前缀</param>
        private string GetPrefix(string prefix) => string.IsNullOrWhiteSpace(Prefix) ? prefix : Prefix;

        /// <summary>
        /// 获取前缀
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="tableDatabase">表数据库</param>
        /// <param name="prefix">前缀</param>
        private string GetDatabase(IDialect dialect, ITableDatabase tableDatabase, string prefix)
        {
            if (string.IsNullOrWhiteSpace(DatabaseName) == false)
                return dialect.SafeName(DatabaseName);
            return tableDatabase == null ? null : dialect.SafeName(tableDatabase.GetDatabase(GetName(prefix)));
        }

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="prefix">前缀</param>
        private string GetName(IDialect dialect, string prefix)
        {
            prefix = GetPrefix(prefix);
            return string.IsNullOrWhiteSpace(prefix) ? dialect.SafeName(Name) : $"{dialect.SafeName(prefix)}.{dialect.SafeName(Name)}";
        }
        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="prefix">前缀</param>
        private string GetName(string prefix)
        {
            prefix = GetPrefix(prefix);
            return string.IsNullOrWhiteSpace(prefix) ? Name : $"{prefix}.{Name}";
        }

        #endregion

    }
}

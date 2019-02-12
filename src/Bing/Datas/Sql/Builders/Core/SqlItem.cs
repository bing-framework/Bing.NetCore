using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// Sql项
    /// </summary>
    public class SqlItem
    {
        /// <summary>
        /// 是否使用原始值
        /// </summary>
        public bool Raw { get; }

        /// <summary>
        /// 前缀
        /// </summary>
        private string _prefix;

        /// <summary>
        /// 前缀，范例：t.a As b，值为 t
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
        /// 名称，范例：t.a As b，值为 a
        /// </summary>
        public string Name
        {
            get => Raw ? _name : _name.SafeString();
            set => _name = value;
        }

        /// <summary>
        /// 别名
        /// </summary>
        private string _alias;

        /// <summary>
        /// 别名，范例：t.a As b，值为 b
        /// </summary>
        public string Alias
        {
            get => _alias.SafeString();
            set => _alias = value;
        }

        /// <summary>
        /// 初始化一个<see cref="SqlItem"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="prefix">前缀</param>
        /// <param name="alias">别名</param>
        /// <param name="raw">是否使用原始值</param>
        /// <param name="isSplit">是否用句点分割名称</param>
        public SqlItem(string name, string prefix = null, string alias = null, bool raw = false , bool isSplit = true)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _prefix = prefix;
            _alias = alias;
            Raw = raw;
            if (raw)
            {
                _name = name;
                return;
            }

            Resolve(name, isSplit);
        }

        /// <summary>
        /// 初始化一个<see cref="SqlItem"/>类型的实例
        /// </summary>
        /// <param name="sqlItem">Sql项</param>
        protected SqlItem(SqlItem sqlItem):this(sqlItem.Name,sqlItem.Prefix,sqlItem.Alias,sqlItem.Raw,false)
        {
        }

        /// <summary>
        /// 设置别名，返回前缀和名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="isSplit">是否用句点分割名称</param>
        private void Resolve(string name, bool isSplit)
        {
            var pattern = @"\s+[aA][sS]\s+";
            var list = Regexs.Split(name, pattern);
            if (list == null || list.Length == 0)
            {
                return;
            }

            if (list.Length == 2)
            {
                _alias = list[1];
            }

            if (isSplit)
            {
                SplitName(list[0]);
                return;
            }

            _name = name;
        }

        /// <summary>
        /// 分割名称
        /// </summary>
        /// <param name="name">名称</param>
        private void SplitName(string name)
        {
            var result = new NameItem(name);
            if (string.IsNullOrWhiteSpace(result.Prefix) == false)
            {
                _prefix = result.Prefix;
            }

            if (string.IsNullOrWhiteSpace(result.Name) == false)
            {
                _name = result.Name;
            }
        }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        public SqlItem Clone()
        {
            return new SqlItem(this);
        }

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <returns></returns>
        public string ToSql(IDialect dialect = null)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return null;
            }

            if (Raw)
            {
                return Name;
            }

            var column = string.IsNullOrWhiteSpace(Prefix)
                ? GetSafeName(dialect, Name)
                : $"{GetSafeName(dialect, Prefix)}.{GetSafeName(dialect, Name)}";
            return string.IsNullOrWhiteSpace(Alias) ? column : $"{column} As {GetSafeName(dialect, Alias)}";
        }

        /// <summary>
        /// 获取安全名称
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="name">名称</param>
        /// <returns></returns>
        private string GetSafeName(IDialect dialect, string name)
        {
            if (dialect == null)
            {
                return name;
            }
            return dialect.SafeName(name);
        }
    }
}

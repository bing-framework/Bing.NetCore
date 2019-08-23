using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bing.Datas.Sql.Builders.Extensions;
using Bing.Datas.Sql.Matedatas;
using Bing.Utils.Extensions;

namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// Sql项
    /// </summary>
    public class SqlItem
    {
        #region 字段

        /// <summary>
        /// 名称
        /// </summary>
        private string _name;

        /// <summary>
        /// 前缀
        /// </summary>
        private string _prefix;

        /// <summary>
        /// 别名
        /// </summary>
        private string _alias;

        #endregion

        #region 属性

        /// <summary>
        /// 是否使用原始值
        /// </summary>
        public bool Raw { get; }

        /// <summary>
        /// 前缀，范例：t.a As b，值为 t
        /// </summary>
        public string Prefix => _prefix.SafeString();

        /// <summary>
        /// 名称，范例：t.a As b，值为 a
        /// </summary>
        public string Name => Raw ? _name : _name.SafeString();

        /// <summary>
        /// 别名，范例：t.a As b，值为 b
        /// </summary>
        public string Alias => _alias.SafeString();

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DatabaseName { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="SqlItem"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="prefix">前缀</param>
        /// <param name="alias">别名</param>
        /// <param name="raw">是否使用原始值</param>
        /// <param name="isSplit">是否用句点分割名称</param>
        /// <param name="isResolve">是否解析名称</param>
        public SqlItem(string name, string prefix = null, string alias = null, bool raw = false, bool isSplit = true,bool isResolve = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            _prefix = prefix;
            _alias = alias;
            Raw = raw;
            if (raw)
            {
                _name = name;
                return;
            }
            Resolve(name, isSplit, isResolve);
        }

        /// <summary>
        /// 设置别名，返回前缀和名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="isSplit">是否用句点分割名称</param>
        /// <param name="isResolve">是否解析名称</param>
        private void Resolve(string name, bool isSplit, bool isResolve)
        {
            name = name.Trim();
            if (isResolve == false)
            {
                _name = name;
                return;
            }
            var pattern = @"\s+[aA][sS]\s+";
            name = Regex.Replace(name, pattern, " ");
            if (name.Contains("."))
            {
                pattern = @"\s+.\s+";
                name = Regex.Replace(name, pattern, ".");
            }
            var list = name.Split(' ').Where(t => t.IsEmpty() == false).ToList();
            if (list.Count==0)
                return;
            if (list.Count == 2)
                _alias = list[1].Trim();
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
            if (string.IsNullOrWhiteSpace(result.Name) == false)
                _name = result.Name;
            if (string.IsNullOrWhiteSpace(result.Prefix) == false)
                _prefix = result.Prefix;
            if (string.IsNullOrWhiteSpace(result.DatabaseName) == false)
                DatabaseName = result.DatabaseName;
        }

        #endregion

        #region Clone(克隆)

        /// <summary>
        /// 克隆
        /// </summary>
        public SqlItem Clone() => new SqlItem(Name, Prefix, Alias, Raw, false);

        #endregion

        #region ToSql(获取Sql)

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="tableDatabase">表数据库</param>
        public virtual string ToSql(IDialect dialect = null, ITableDatabase tableDatabase = null)
        {
            if (string.IsNullOrWhiteSpace(Name))
                return null;
            if (Raw)
                return Name;
            var column = GetColumn(dialect, tableDatabase);
            var columnAlias = GetSafeName(dialect, Alias);
            return dialect.GetColumn(column, columnAlias);
        }

        /// <summary>
        /// 获取列
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="tableDatabase">表数据库</param>
        protected string GetColumn(IDialect dialect, ITableDatabase tableDatabase)
        {
            var result = new StringBuilder();
            var database = DatabaseName;
            if (string.IsNullOrWhiteSpace(DatabaseName) && tableDatabase != null)
                database = tableDatabase.GetDatabase(GetName());
            if (string.IsNullOrWhiteSpace(database) == false)
                result.Append($"{GetSafeName(dialect, database)}.");
            if (string.IsNullOrWhiteSpace(Prefix) == false)
                result.Append($"{GetSafeName(dialect, Prefix)}.");
            result.Append(GetSafeName(dialect, Name));
            return result.ToString();
        }

        /// <summary>
        /// 获取名称
        /// </summary>
        protected string GetName() => string.IsNullOrWhiteSpace(Prefix) ? Name : $"{Prefix}.{Name}";

        /// <summary>
        /// 获取安全名称
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="name">名称</param>
        protected string GetSafeName(IDialect dialect, string name) => dialect.GetSafeName(name);

        #endregion
    }
}

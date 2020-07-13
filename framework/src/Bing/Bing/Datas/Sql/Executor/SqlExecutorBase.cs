using System;
using System.Data;
using System.Threading.Tasks;
using Bing.Datas.Transactions;

namespace Bing.Datas.Sql.Executor
{
    /// <summary>
    /// Sql执行对象基类
    /// </summary>
    public abstract class SqlExecutorBase : ISqlExecutor
    {
        /// <summary>
        /// 数据库
        /// </summary>
        protected IDatabase Database { get; private set; }

        /// <summary>
        /// 数据库连接
        /// </summary>
        protected IDbConnection Connection { get; private set; }

        /// <summary>
        /// 事务操作管理器
        /// </summary>
        protected ITransactionActionManager TransactionActionManager { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="SqlExecutorBase"/>类型的实例
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="database">数据库</param>
        protected SqlExecutorBase(ITransactionActionManager transactionActionManager, IDatabase database = null)
        {
            TransactionActionManager = transactionActionManager ?? throw new ArgumentNullException(nameof(transactionActionManager));
            Database = database;
            Connection = database?.GetConnection();
        }

        /// <summary>
        /// 设置数据库连接
        /// </summary>
        /// <param name="connection">数据库连接</param>
        public ISqlExecutor SetConnection(IDbConnection connection)
        {
            Connection = connection;
            return this;
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        /// <param name="connection">数据库连接</param>
        protected IDbConnection GetConnection(IDbConnection connection)
        {
            if (connection != null)
                return connection;
            if (Connection == null)
                throw new ArgumentNullException(nameof(Connection));
            return Connection;
        }

        /// <summary>
        /// 执行Sql语句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="param">参数</param>
        public abstract int ExecuteSql(string sql, object param = null);

        /// <summary>
        /// 执行Sql语句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="param">参数</param>
        public abstract Task<int> ExecuteSqlAsync(string sql, object param = null);
    }
}

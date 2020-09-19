using System.Threading.Tasks;
using Bing.Data.Sql;
using Bing.Data.Sql.Executor;
using Bing.Data.Transaction;
using Dapper;

namespace Bing.Datas.Dapper
{
    /// <summary>
    /// Sql执行对象
    /// </summary>
    public class SqlExecutor : SqlExecutorBase, ISqlExecutor
    {
        /// <summary>
        /// 跟踪日志名称
        /// </summary>
        public const string TraceLogName = "SqlExecuteLog";

        /// <summary>
        /// 初始化一个<see cref="SqlExecutor"/>类型的实例
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="database">数据库</param>
        public SqlExecutor(ITransactionActionManager transactionActionManager, IDatabase database = null) : base(transactionActionManager, database)
        {
        }

        /// <summary>
        /// 执行Sql语句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="param">参数</param>
        public override int ExecuteSql(string sql, object param = null)
        {
            TransactionActionManager.Register(async transaction =>
            {
                await Connection.ExecuteAsync(sql, param, transaction);
            });
            return 1;
        }

        /// <summary>
        /// 执行Sql语句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="param">参数</param>
        public override Task<int> ExecuteSqlAsync(string sql, object param = null)
        {
            TransactionActionManager.Register(async transaction =>
            {
                await Connection.ExecuteAsync(sql, param, transaction);
            });
            return Task.FromResult(1);
        }
    }
}

using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Data.Stores;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Admin.Data.Stores.Implements.Systems
{
    /// <summary>
    /// 应用程序存储器
    /// </summary>
    public class ApplicationPoStore : StoreBase<ApplicationPo>, IApplicationPoStore
    {
        /// <summary>
        /// 初始化一个<see cref="ApplicationPoStore"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
        /// <param name="databaseContextAccessor">数据库上下文访问器</param>
        /// <param name="metadataOptions">SQL 元数据配置</param>
        /// <param name="typeConverterResolver">数据类型转换器解析器</param>
        public ApplicationPoStore(IAdminUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
            metadataOptions, typeConverterResolver)
        {
        }
    }
}

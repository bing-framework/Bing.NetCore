using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 资源 仓储
    /// </summary>
    public class ResourceRepository : RepositoryBase<Resource>, IResourceRepository
    {
        /// <summary>
        /// 初始化一个<see cref="ResourceRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="sqlQueryFactory">SQL 查询对象工厂</param>
        /// <param name="databaseContextAccessor">数据库上下文访问器</param>
        /// <param name="metadataOptions">SQL 元数据配置</param>
        /// <param name="typeConverterResolver">数据类型转换器解析器</param>
        public ResourceRepository(IAdminUnitOfWork unitOfWork, ISqlQueryFactory sqlQueryFactory,
            IDatabaseContextAccessor databaseContextAccessor, SqlMetadataOptions metadataOptions,
            ITypeConverterResolver typeConverterResolver) : base(unitOfWork, sqlQueryFactory, databaseContextAccessor,
            metadataOptions, typeConverterResolver) { }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Aspects;
using Bing.Auditing;

namespace Bing.Datas.UnitOfWorks
{
    /// <summary>
    /// 工作单元
    /// </summary>
    [Ignore]
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        int Commit();

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        Task<int> CommitAsync();

        /// <summary>
        /// 获取审计实体集合
        /// </summary>
        IEnumerable<AuditEntityEntry> GetAuditEntities();
    }
}

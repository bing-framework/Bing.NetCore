using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Aspects;

namespace Bing.Datas.UnitOfWorks
{
    /// <summary>
    /// 工作单元
    /// </summary>
    [Ignore]
    public interface IUnitOfWork:IDisposable
    {
        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        /// <returns></returns>
        int Commit();

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        /// <returns></returns>
        Task<int> CommitAsync();
    }
}

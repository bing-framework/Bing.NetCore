using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Domains.Repositories;

namespace Bing.Datas.Queries
{
    /// <summary>
    /// 查询参数
    /// </summary>
    public interface IQueryParameter : IPager
    {
        /// <summary>
        /// 搜索关键字
        /// </summary>
        string Keyword { get; set; }
    }
}

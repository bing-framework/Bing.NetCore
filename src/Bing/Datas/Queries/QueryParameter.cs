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
    public class QueryParameter : Pager, IQueryParameter
    {
        /// <summary>
        /// 搜索关键字
        /// </summary>
        public string Keyword { get; set; }

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            base.AddDescriptions();
            AddDescription("Keyword", Keyword);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Applications.Aspects
{
    /// <summary>
    /// 提交工作单元后操作
    /// </summary>
    public interface ICommitAfter
    {
        /// <summary>
        /// 提交后操作
        /// </summary>
        void CommitAfter();
    }
}

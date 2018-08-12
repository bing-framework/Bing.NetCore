using System;
using System.Collections.Generic;
using System.Text;
using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Core
{
    /// <summary>
    /// 邮件
    /// </summary>
    public class MailBox
    {
        /// <summary>
        /// 附件列表
        /// </summary>
        public IEnumerable<IAttachment> Attachments { get; set; }

        /// <summary>
        /// 正文
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 抄送人
        /// </summary>
        public IEnumerable<string> Cc { get; set; }

        /// <summary>
        /// 是否Html内容
        /// </summary>
        public bool IsHtml { get; set; }

        /// <summary>
        /// 主题
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// 收件人
        /// </summary>
        public IEnumerable<string> To { get; set; }
    }
}

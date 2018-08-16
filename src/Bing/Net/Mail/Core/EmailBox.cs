using System.Collections.Generic;
using Bing.Net.Mail.Abstractions;

namespace Bing.Net.Mail.Core
{
    /// <summary>
    /// 电子邮件
    /// </summary>
    public class EmailBox
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
        public bool IsBodyHtml { get; set; }

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

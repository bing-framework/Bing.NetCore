using System.Net.Mail;
using System.Threading.Tasks;
using Bing.EmailCenter.Core;

namespace Bing.EmailCenter.Abstractions
{
    /// <summary>
    /// 电子邮件发送器
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        void Send(string to, string subject, string body, bool isBodyHtml = true);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        Task SendAsync(string to, string subject, string body, bool isBodyHtml = true);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="from">发件人</param>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        void Send(string from, string to, string subject, string body, bool isBodyHtml = true);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="from">发件人</param>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        Task SendAsync(string from, string to, string subject, string body, bool isBodyHtml = true);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">邮件</param>
        void Send(MailBox box);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">邮件</param>
        /// <returns></returns>
        Task SendAsync(MailBox box);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        void Send(MailMessage mail, bool normalize = true);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        Task SendAsync(MailMessage mail, bool normalize = true);
    }
}

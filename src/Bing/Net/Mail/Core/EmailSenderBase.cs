using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Bing.Net.Mail.Abstractions;
using Bing.Net.Mail.Configs;
using Bing.Utils.Extensions;

namespace Bing.Net.Mail.Core
{
    /// <summary>
    /// 电子邮件发送器基类
    /// </summary>
    public abstract class EmailSenderBase:IEmailSender
    {
        /// <summary>
        /// 电子邮件配置提供器
        /// </summary>
        public IEmailConfigProvider ConfigProvider { get; }

        /// <summary>
        /// 初始化一个<see cref="EmailSenderBase"/>类型的实例
        /// </summary>
        /// <param name="provider">电子邮件配置提供器</param>
        protected EmailSenderBase(IEmailConfigProvider provider)
        {
            ConfigProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        public virtual void Send(string to, string subject, string body, bool isBodyHtml = true)
        {
            Send(new MailMessage()
            {
                To = { to},
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            });
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        public virtual async Task SendAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            await SendAsync(new MailMessage()
            {
                To = {to},
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            });
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="from">发件人</param>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        public virtual void Send(string @from, string to, string subject, string body, bool isBodyHtml = true)
        {
            Send(new MailMessage(from, to, subject, body) {IsBodyHtml = isBodyHtml});
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="from">发件人</param>
        /// <param name="to">收件人</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">正文</param>
        /// <param name="isBodyHtml">是否html内容</param>
        public virtual async Task SendAsync(string @from, string to, string subject, string body, bool isBodyHtml = true)
        {
            await SendAsync(new MailMessage(from, to, subject, body) { IsBodyHtml = isBodyHtml });
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">邮件</param>
        public void Send(EmailBox box)
        {            
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">邮件</param>
        /// <returns></returns>
        public async Task SendAsync(EmailBox box)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        public void Send(MailMessage mail, bool normalize = true)
        {
            if (normalize)
            {
                NormalizeMail(mail);
            }
            SendEmail(mail);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        public async Task SendAsync(MailMessage mail, bool normalize = true)
        {
            if (normalize)
            {
                NormalizeMail(mail);
            }

            await SendEmailAsync(mail);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        protected abstract void SendEmail(MailMessage mail);

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        /// <returns></returns>
        protected abstract Task SendEmailAsync(MailMessage mail);

        /// <summary>
        /// 获取附件集合
        /// </summary>
        /// <param name="attachments">附件集合</param>
        /// <returns></returns>
        protected virtual AttachmentCollection GetAttachments(IEnumerable<IAttachment> attachments)
        {
            if (attachments == null)
            {
                throw new ArgumentNullException(nameof(attachments));
            }

            AttachmentCollection collection = null;
            foreach (var item in attachments)
            {
                var fileName = item.GetName();
                Attachment attachment;
                Stream stream = null;
                try
                {
                    stream = item.GetFileStream();
                    attachment=new Attachment(stream,fileName);
                }
                catch (Exception ex)
                {
                    stream?.Dispose();
                    continue;
                }
            }
            return null;
        }

        /// <summary>
        /// 规范化邮件，设置发件人地址/名称并使邮件编码为UTF-8
        /// </summary>
        /// <param name="mail"></param>
        protected virtual void NormalizeMail(MailMessage mail)
        {
            if (mail.From == null || mail.From.Address.IsEmpty())
            {
                var config = ConfigProvider.GetConfig();
                mail.From = new MailAddress(config.FromAddress, config.DisplayName, Encoding.UTF8);
            }

            if (mail.HeadersEncoding == null)
            {
                mail.HeadersEncoding=Encoding.UTF8;
            }

            if (mail.SubjectEncoding == null)
            {
                mail.SubjectEncoding=Encoding.UTF8;
            }

            if (mail.BodyEncoding == null)
            {
                mail.BodyEncoding=Encoding.UTF8;
            }
        }
    }
}

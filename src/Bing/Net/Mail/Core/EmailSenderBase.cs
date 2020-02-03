using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Net.Mail.Abstractions;
using Bing.Net.Mail.Configs;
using Bing.Extensions;

namespace Bing.Net.Mail.Core
{
    /// <summary>
    /// 电子邮件发送器基类
    /// </summary>
    public abstract class EmailSenderBase : IEmailSender
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
                To = { to },
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
                To = { to },
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
            Send(new MailMessage(from, to, subject, body) { IsBodyHtml = isBodyHtml });
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
        public virtual void Send(EmailBox box)
        {
            var mail = new MailMessage();
            var config = ConfigProvider.GetConfig();
            mail.From = new MailAddress(config.FromAddress);
            PaserMailAddress(box.To, mail.To);
            PaserMailAddress(box.Cc, mail.CC);
            PaserMailAddress(box.Bcc, mail.Bcc);
            PaserMailAddress(config.FromAddress, mail.ReplyToList);
            mail.Subject = box.Subject;
            mail.Body = box.Body;
            mail.IsBodyHtml = box.IsBodyHtml;
            HandlerAttachments(box.Attachments, mail.Attachments);
            Send(mail);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">邮件</param>
        /// <returns></returns>
        public virtual async Task SendAsync(EmailBox box)
        {
            var mail = new MailMessage();
            var config = await ConfigProvider.GetConfigAsync();
            mail.From = new MailAddress(config.FromAddress, config.DisplayName);
            PaserMailAddress(box.To, mail.To);
            PaserMailAddress(box.Cc, mail.CC);
            PaserMailAddress(box.Bcc, mail.Bcc);
            PaserMailAddress(config.FromAddress, mail.ReplyToList);
            mail.Subject = box.Subject;
            mail.Body = box.Body;
            mail.IsBodyHtml = box.IsBodyHtml;
            HandlerAttachments(box.Attachments, mail.Attachments);
            await SendAsync(mail);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        public virtual void Send(MailMessage mail, bool normalize = true)
        {
            if (normalize)
                NormalizeMail(mail);
            SendEmail(mail);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件消息</param>
        /// <param name="normalize">是否规范化邮件，如果是，则设置发件人地址/名称并使邮件编码为UTF-8</param>
        public virtual async Task SendAsync(MailMessage mail, bool normalize = true)
        {
            if (normalize)
                NormalizeMail(mail);
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
        protected abstract Task SendEmailAsync(MailMessage mail);

        /// <summary>
        /// 处理附件
        /// </summary>
        /// <param name="attachments">附件集合</param>
        /// <param name="attachmentCollection">附件集合对象</param>
        protected virtual void HandlerAttachments(IList<IAttachment> attachments, AttachmentCollection attachmentCollection)
        {
            if (attachments == null || !attachments.Any())
                return;
            foreach (var item in attachments)
            {
                Attachment attachment = new Attachment(item.GetFileStream(), item.GetName());
                attachmentCollection.Add(attachment);
            }
        }

        /// <summary>
        /// 规范化邮件，设置发件人地址/名称并使邮件编码为UTF-8
        /// </summary>
        /// <param name="mail">邮件</param>
        protected virtual void NormalizeMail(MailMessage mail)
        {
            if (mail.From == null || mail.From.Address.IsEmpty())
            {
                var config = ConfigProvider.GetConfig();
                mail.From = new MailAddress(config.FromAddress, config.DisplayName, Encoding.UTF8);
            }
            if (mail.HeadersEncoding == null)
                mail.HeadersEncoding = Encoding.UTF8;
            if (mail.SubjectEncoding == null)
                mail.SubjectEncoding = Encoding.UTF8;
            if (mail.BodyEncoding == null)
                mail.BodyEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// 解析分解邮件地址
        /// </summary>
        /// <param name="mailAddress">邮件地址</param>
        /// <param name="mailAddressCollection">邮件地址对象</param>
        protected static void PaserMailAddress(string mailAddress, MailAddressCollection mailAddressCollection)
        {
            if (mailAddress.IsEmpty())
                return;
            char[] separator = new char[2] { ',', ';' };
            string[] addressArray = mailAddress.Split(separator);
            PaserMailAddress(addressArray.ToList(), mailAddressCollection);
        }

        /// <summary>
        /// 解析分解邮件地址
        /// </summary>
        /// <param name="mailAddress">邮件地址列表</param>
        /// <param name="mailAddressCollection">邮件地址对象</param>
        protected static void PaserMailAddress(List<string> mailAddress,
            MailAddressCollection mailAddressCollection)
        {
            if (mailAddress == null || mailAddress.Count == 0)
                return;
            foreach (var address in mailAddress)
            {
                if (address.Trim() == string.Empty)
                    continue;
                mailAddressCollection.Add(new MailAddress(address));
            }
        }
    }
}

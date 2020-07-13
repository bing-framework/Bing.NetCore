using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using MimeKit;
using MimeKit.IO;

namespace Bing.MailKit.Extensions
{
    /// <summary>
    /// 电子邮件扩展
    /// </summary>
    public static partial class EmailExtensions
    {
        /// <summary>
        /// 转换成MimeMessage
        /// </summary>
        /// <param name="mail">邮件消息</param>
        public static MimeMessage ToMimeMessage(this MailMessage mail)
        {
            if (mail == null)
                throw new ArgumentNullException(nameof(mail));
            var headers = new List<Header>();
            foreach (var key in mail.Headers.AllKeys)
            {
                var values = mail.Headers.GetValues(key);
                if (values == null)
                    continue;
                foreach (var value in values) 
                    headers.Add(new Header(key, value));
            }

            var message = new MimeMessage(headers.ToArray());
            MimeEntity body = null;
            if (mail.Sender != null)
                message.Sender = mail.Sender.ToMailboxAddress();
            if (mail.From != null)
            {
                message.Headers.Replace(HeaderId.From, string.Empty);
                message.From.Add(mail.From.ToMailboxAddress());
            }
            if (mail.ReplyToList.Count > 0)
            {
                message.Headers.Replace(HeaderId.ReplyTo, string.Empty);
                message.ReplyTo.AddRange(mail.ReplyToList.ToInternetAddressList());
            }

            if (mail.To.Count > 0)
            {
                message.Headers.Replace(HeaderId.To, string.Empty);
                message.To.AddRange(mail.To.ToInternetAddressList());
            }

            if (mail.CC.Count > 0)
            {
                message.Headers.Replace(HeaderId.Cc, string.Empty);
                message.Cc.AddRange(mail.CC.ToInternetAddressList());
            }

            if (mail.Bcc.Count > 0)
            {
                message.Headers.Replace(HeaderId.Bcc, string.Empty);
                message.Bcc.AddRange(mail.Bcc.ToInternetAddressList());
            }

            if (mail.SubjectEncoding != null)
                message.Headers.Replace(HeaderId.Subject, mail.SubjectEncoding, mail.Subject ?? string.Empty);
            else
                message.Subject = mail.Subject ?? string.Empty;

            switch (mail.Priority)
            {
                case MailPriority.Normal:
                    message.Headers.RemoveAll(HeaderId.XMSMailPriority);
                    message.Headers.RemoveAll(HeaderId.Importance);
                    message.Headers.RemoveAll(HeaderId.XPriority);
                    message.Headers.RemoveAll(HeaderId.Priority);
                    break;

                case MailPriority.High:
                    message.Headers.Replace(HeaderId.Priority, "urgent");
                    message.Headers.Replace(HeaderId.Importance, "high");
                    message.Headers.Replace(HeaderId.XPriority, "2 (High)");
                    break;

                case MailPriority.Low:
                    message.Headers.Replace(HeaderId.Priority, "non-urgent");
                    message.Headers.Replace(HeaderId.Importance, "low");
                    message.Headers.Replace(HeaderId.XPriority, "4 (Low)");
                    break;
            }

            if (!string.IsNullOrEmpty(mail.Body))
            {
                var text = new TextPart(mail.IsBodyHtml ? "html" : "plain");
                text.SetText(mail.BodyEncoding ?? Encoding.UTF8, mail.Body);
                body = text;
            }

            if (mail.AlternateViews.Count > 0)
            {
                var alternative = new MultipartAlternative();

                if (body != null)
                {
                    alternative.Add(body);
                }

                foreach (var view in mail.AlternateViews)
                {
                    var part = GetMimePart(view);

                    if (view.BaseUri != null)
                    {
                        part.ContentLocation = view.BaseUri;
                    }

                    if (view.LinkedResources.Count > 0)
                    {
                        var type = part.ContentType.MediaType + "/" + part.ContentType.MediaSubtype;
                        var related = new MultipartRelated();

                        related.ContentType.Parameters.Add("type", type);

                        if (view.BaseUri != null)
                        {
                            related.ContentLocation = view.BaseUri;
                        }

                        related.Add(part);

                        foreach (var resource in view.LinkedResources)
                        {
                            part = GetMimePart(resource);

                            if (resource.ContentLink != null)
                                part.ContentLocation = resource.ContentLink;

                            related.Add(part);
                        }

                        alternative.Add(related);
                    }
                    else
                    {
                        alternative.Add(part);
                    }
                }

                body = alternative;
            }

            if (body == null)
                body = new TextPart(mail.IsBodyHtml ? "html" : "plain");
            if (mail.Attachments.Count > 0)
            {
                var mixed = new Multipart("mixed");

                if (body != null)
                {
                    mixed.Add(body);
                }

                foreach (var attachment in mail.Attachments)
                {
                    mixed.Add(GetMimePart(attachment));
                }

                body = mixed;
            }

            message.Body = body;

            return message;
        }

        /// <summary>
        /// 获取MimePart
        /// </summary>
        /// <param name="item">附件基类</param>
        private static MimePart GetMimePart(AttachmentBase item)
        {
            var mimeType = item.ContentType.ToString();
            var contentType = ContentType.Parse(mimeType);
            var attachemt = item as Attachment;
            MimePart part;

            if (contentType.MediaType.Equals("text", StringComparison.OrdinalIgnoreCase))
            {
                part = new TextPart();
            }
            else
            {
                part = new MimePart(contentType);
            }

            if (attachemt != null)
            {
                //var disposition = attachemt.ContentDisposition.ToString();
                //part.ContentDisposition = ContentDisposition.Parse(disposition);
                part.ContentDisposition = new ContentDisposition(ContentDisposition.Attachment);
            }

            switch (item.TransferEncoding)
            {
                case System.Net.Mime.TransferEncoding.QuotedPrintable:
                    part.ContentTransferEncoding = ContentEncoding.QuotedPrintable;
                    break;

                case System.Net.Mime.TransferEncoding.Base64:
                    part.ContentTransferEncoding = ContentEncoding.Base64;
                    break;

                case System.Net.Mime.TransferEncoding.SevenBit:
                    part.ContentTransferEncoding = ContentEncoding.SevenBit;
                    break;

                case System.Net.Mime.TransferEncoding.EightBit:
                    part.ContentTransferEncoding = ContentEncoding.EightBit;
                    break;
            }

            if (item.ContentId != null)
            {
                part.ContentId = item.ContentId;
            }

            var stream = new MemoryBlockStream();
            item.ContentStream.CopyTo(stream);
            stream.Position = 0;

            part.Content = new MimeContent(stream);
            if (attachemt != null)
            {
                // 解决中文文件名乱码
                var charset = "GB18030";
                part.ContentType.Parameters.Clear();
                part.ContentDisposition.Parameters.Clear();
                var fileName = attachemt.Name;
                part.ContentType.Parameters.Add(charset, "name", fileName);
                part.ContentDisposition.Parameters.Add(charset, "filename", fileName);
                // 解决文件名不能超过41字符
                foreach (var parameter in part.ContentDisposition.Parameters)
                {
                    parameter.EncodingMethod = ParameterEncodingMethod.Rfc2047;
                }

                foreach (var parameter in part.ContentType.Parameters)
                {
                    parameter.EncodingMethod = ParameterEncodingMethod.Rfc2047;
                }
            }
            return part;
        }

        /// <summary>
        /// 转换成邮箱地址
        /// </summary>
        /// <param name="address">邮箱地址</param>
        private static MailboxAddress ToMailboxAddress(this MailAddress address) => address == null ? null : new MailboxAddress(address.DisplayName, address.Address);

        /// <summary>
        /// 转换成Internet地址列表
        /// </summary>
        /// <param name="addresses">邮箱地址集合</param>
        private static InternetAddressList ToInternetAddressList(this MailAddressCollection addresses)
        {
            if (addresses == null)
                return null;
            var list = new InternetAddressList();
            foreach (var address in addresses) 
                list.Add(address.ToMailboxAddress());
            return list;
        }
    }
}

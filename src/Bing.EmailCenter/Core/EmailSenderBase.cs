using System.Net.Mail;
using System.Threading.Tasks;
using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Core
{
    public abstract class EmailSenderBase:IEmailSender
    {

        public virtual void Send(string to, string subject, string body, bool isBodyHtml = true)
        {
            throw new System.NotImplementedException();
        }

        public virtual async Task SendAsync(string to, string subject, string body, bool isBodyHtml = true)
        {
            throw new System.NotImplementedException();
        }

        public virtual void Send(string @from, string to, string subject, string body, bool isBodyHtml = true)
        {
            throw new System.NotImplementedException();
        }

        public virtual async Task SendAsync(string @from, string to, string subject, string body, bool isBodyHtml = true)
        {
            throw new System.NotImplementedException();
        }

        public virtual void Send(MailBox box)
        {
            throw new System.NotImplementedException();
        }

        public virtual async Task SendAsync(MailBox box)
        {
            throw new System.NotImplementedException();
        }

        public virtual void Send(MailMessage mail, bool normalize = true)
        {
            throw new System.NotImplementedException();
        }

        public virtual async Task SendAsync(MailMessage mail, bool normalize = true)
        {
            throw new System.NotImplementedException();
        }
    }
}

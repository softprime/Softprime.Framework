using System.Net.Mail;
using System.Threading.Tasks;
using Softprime.Framework.Mailer.Models;

namespace Softprime.Framework.Mailer
{
    public interface ISoftprimeMailer
    {
        Task<MailerResponse> Send<T>(string templateKey, T model, string subject, string plainTextContent = null, Attachment[] attachments = null) where T : MailerModelBase;
    }
}

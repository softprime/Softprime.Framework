using System.Net.Mail;
using System.Threading.Tasks;
using Softprime.Framework.Mailer.Models;

namespace Softprime.Framework.Mailer
{
    public interface ISoftprimeMailerDispatcher
    {
        Task<MailerResponse> Send(MailAddress from, MailAddress to, string subject, string htmlContent, string plainTextContent = null, Attachment[] attachments = null);
    }
}

using System.Collections.Generic;

namespace Softprime.Framework.Mailer.Models
{
    public class MailerResponse
    {
        public bool Sent { get; set; }

        public IList<string> ErrorMessages { get; set; }
    }
}

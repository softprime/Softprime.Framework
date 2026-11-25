using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Softprime.Framework.Mailer.Models;

namespace Softprime.Framework.Mailer.AWS
{
    public class AWSDispatcher : ISoftprimeMailerDispatcher
    {
        private readonly AWSSettings _settings;
        private readonly Func<AWSSettings, IAmazonSimpleEmailServiceV2> _createClient;

        public AWSDispatcher(AWSSettings settings)
            : this(settings, current => new AmazonSimpleEmailServiceV2Client(
                new BasicAWSCredentials(current.AccessKey, current.SecretKey)))
        {
        }

        internal AWSDispatcher(AWSSettings settings, Func<AWSSettings, IAmazonSimpleEmailServiceV2> createClient)
        {
            _settings = settings;
            _createClient = createClient;
        }

        public async Task<MailerResponse> Send(MailAddress @from, 
            MailAddress to, 
            string subject, 
            string htmlContent, 
            string plainTextContent = null,
            Attachment[] attachments = null)
        {
            try
            {
                var client = _createClient(_settings);

                var response = await client.SendEmailAsync(new SendEmailRequest
                {
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { to.Address }
                    },
                    Content = new EmailContent
                    {
                        Simple = new Message
                        {
                            Body = new Body
                            {
                                Html = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = htmlContent,
                                },
                                Text = new Content
                                {
                                    Charset = "UTF-8",
                                    Data = plainTextContent,
                                }
                            },
                            Subject = new Content
                            {
                                Charset = "UTF-8",
                                Data = subject
                            }
                        }
                    },
                    ReplyToAddresses = new List<string> { from.Address },
                    FromEmailAddress = from.Address
                });

                string messageId = response.MessageId;

                return new MailerResponse
                {
                    Sent = response.HttpStatusCode == HttpStatusCode.Accepted
                };
            }
            catch (Exception e)
            {
                return new MailerResponse
                {
                    Sent = false,
                    ErrorMessages = new List<string>(new[] { e.Message })
                };
            }
        }
    }
}

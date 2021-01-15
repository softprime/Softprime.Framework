using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Softprime.Framework.Mailer.Models;
using RazorLight;

namespace Softprime.Framework.Mailer
{
    public class SoftprimeMailer : ISoftprimeMailer
    {
        private readonly ISoftprimeMailerDispatcher _dispatcher;
        private readonly SoftprimeMailerSettings _settings;
        private readonly RazorLightEngine _engine;

        protected SoftprimeMailer(SoftprimeMailerSettings settings, ISoftprimeMailerDispatcher dispatcher, Type templateRootType)
        {
            _settings = settings;
            _dispatcher = dispatcher;

            _engine = new RazorLightEngineBuilder()
                .UseEmbeddedResourcesProject(templateRootType)
                .UseMemoryCachingProvider()
                .Build();
        }
        
            
        public virtual async Task<MailerResponse> Send<T>(string templateKey, T model, string subject, string plainTextContent = null, Attachment[] attachments = null) where T : MailerModelBase
        {
            var result = await _engine.CompileRenderAsync(templateKey, model);
            return await _dispatcher.Send(new MailAddress(_settings.DefaultFromEmail, _settings.DefaultFromName), model.To, subject, result, plainTextContent, attachments);
        }

    }
}

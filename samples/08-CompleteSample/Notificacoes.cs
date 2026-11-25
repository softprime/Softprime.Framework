using System.Collections.Concurrent;
using System.Net.Mail;
using Softprime.Framework.Mailer;
using Softprime.Framework.Mailer.Models;

namespace Softprime.Samples.CompleteSample;

internal sealed class SampleMailer(SoftprimeMailerSettings settings, ISoftprimeMailerDispatcher dispatcher)
    : SoftprimeMailer(settings, dispatcher, typeof(SampleMailer))
{
}

public sealed class BoasVindasModel : MailerModelBase
{
    public required string Nome { get; init; }
    public required string Empresa { get; init; }
}

internal sealed class FakeMailerDispatcher : ISoftprimeMailerDispatcher
{
    private readonly ConcurrentQueue<Mensagem> _mensagens = new();

    public Mensagem[] Listar() => _mensagens.ToArray();

    public Task<MailerResponse> Send(MailAddress from, MailAddress to, string subject, string htmlContent,
        string? plainTextContent = null, Attachment[]? attachments = null)
    {
        _mensagens.Enqueue(new Mensagem(to.Address, subject, htmlContent));
        return Task.FromResult(new MailerResponse { Sent = true });
    }

    internal sealed record Mensagem(string Destinatario, string Assunto, string Html);
}

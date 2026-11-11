using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Softprime.Framework.Mailer;
using Softprime.Framework.Mailer.Helpers;
using Softprime.Framework.Mailer.Models;
using Softprime.Samples.Mailer.Templates;

namespace Softprime.Samples.Mailer
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));

            try
            {
                if (!isSmokeTest)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("================================================================================");
                    Console.WriteLine(" Softprime.Framework — Sample 06-Mailer (Templates Razor + FakeDispatcher)");
                    Console.WriteLine("================================================================================");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("[INFO] Modo 100% offline: nenhum e-mail real e enviado pela rede (FakeMailerDispatcher em memoria).");
                    Console.WriteLine();
                }

                var settings = new SoftprimeMailerSettings
                {
                    DefaultFromName = "Softprime Notificacoes",
                    DefaultFromEmail = "nao-responda@softprime.example"
                };

                var dispatcher = new FakeMailerDispatcher();
                ISoftprimeMailer mailer = new SampleMailer(settings, dispatcher);

                await RunWelcomeEmailScenarioAsync(mailer, dispatcher, isSmokeTest);
                await RunOrderSummaryWithAttachmentScenarioAsync(mailer, dispatcher, isSmokeTest);
                await RunSimulatedDispatchFailureScenarioAsync(mailer, dispatcher, isSmokeTest);
                await RunMissingTemplateScenarioAsync(mailer, isSmokeTest);

                if (isSmokeTest)
                {
                    Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.Mailer: Renderizacao Razor, anexos, FakeMailerDispatcher e cenarios negativos validados.");
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCESSO] Demonstracao Mailer concluida com 100% de exito! ({dispatcher.SentEmails.Count} e-mails registrados em memoria)");
                    Console.ResetColor();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERRO] Falha no Mailer Sample: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"[ERRO_DETALHE] {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                return 1;
            }
        }

        private static async Task RunWelcomeEmailScenarioAsync(ISoftprimeMailer mailer, FakeMailerDispatcher dispatcher, bool isSmokeTest)
        {
            var model = new BoasVindasEmailModel
            {
                To = new MailAddress("ana.silva@cliente.example", "Ana Silva"),
                NomeUsuario = "Ana Silva",
                Empresa = "Softprime Corporativo",
                UrlAtivacao = "https://portal.softprime.example/ativar/token-demo-01"
            };

            var response = await mailer.Send(
                templateKey: "BoasVindas",
                model: model,
                subject: "Bem-vinda ao Softprime Portal",
                plainTextContent: "Ola Ana Silva, ative sua conta em https://portal.softprime.example/ativar/token-demo-01");

            if (!response.Sent || dispatcher.SentEmails.Count != 1)
                throw new InvalidOperationException("Falha ao enviar template BoasVindas para o FakeMailerDispatcher.");

            var sent = dispatcher.SentEmails[0];
            if (!sent.HtmlContent.Contains("Bem-vindo(a), Ana Silva!", StringComparison.Ordinal) ||
                !sent.HtmlContent.Contains("Softprime Corporativo", StringComparison.Ordinal) ||
                sent.From.Address != "nao-responda@softprime.example")
            {
                throw new InvalidOperationException("HTML renderizado de BoasVindas nao contem os dados esperados do modelo.");
            }

            LogStep(isSmokeTest, "1. Template Razor Tipado (BoasVindas.cshtml)",
                $"Para: {sent.To.DisplayName} <{sent.To.Address}> | Assunto: '{sent.Subject}' | HTML: {sent.HtmlContent.Length} chars | Sent={response.Sent}");
        }

        private static async Task RunOrderSummaryWithAttachmentScenarioAsync(ISoftprimeMailer mailer, FakeMailerDispatcher dispatcher, bool isSmokeTest)
        {
            var model = new ResumoPedidoEmailModel
            {
                To = new MailAddress("compras@acme.example", "Setor de Compras"),
                NumeroPedido = "PED-2026-0094",
                NomeCliente = "ACME Industria Ltda",
                Itens = new List<ItemPedidoEmail>
                {
                    new() { Descricao = "Licenca Softprime Core", Quantidade = 2, PrecoUnitario = 1200.00m },
                    new() { Descricao = "Suporte Enterprise", Quantidade = 1, PrecoUnitario = 850.00m }
                }
            };

            byte[] comprovanteBytes = Encoding.UTF8.GetBytes("Comprovante ficticio do pedido PED-2026-0094 - Total R$ 3.250,00");
            using var attachmentStream = new MemoryStream(comprovanteBytes);
            using var attachment = new Attachment(attachmentStream, "comprovante-ped-2026-0094.txt", "text/plain");

            var response = await mailer.Send(
                templateKey: "ResumoPedido",
                model: model,
                subject: "Confirmacao do Pedido PED-2026-0094",
                plainTextContent: "Pedido PED-2026-0094 confirmado. Total: R$ 3.250,00.",
                attachments: new[] { attachment });

            if (!response.Sent || dispatcher.SentEmails.Count != 2)
                throw new InvalidOperationException("Falha ao enviar template ResumoPedido com anexo.");

            var sent = dispatcher.SentEmails[1];
            if (!sent.HtmlContent.Contains("PED-2026-0094", StringComparison.Ordinal) ||
                !sent.HtmlContent.Contains("Licenca Softprime Core", StringComparison.Ordinal) ||
                sent.Attachments.Count != 1 ||
                sent.Attachments[0].SizeBytes != comprovanteBytes.Length)
            {
                throw new InvalidOperationException("ResumoPedido ou anexo em memoria nao corresponde ao esperado.");
            }

            LogStep(isSmokeTest, "2. Template com Loop @foreach + Anexo em Memoria (ResumoPedido.cshtml)",
                $"Para: {sent.To.Address} | Anexo: {sent.Attachments[0].Name} ({sent.Attachments[0].SizeBytes} bytes) | Sent={response.Sent}");
        }

        private static async Task RunSimulatedDispatchFailureScenarioAsync(ISoftprimeMailer mailer, FakeMailerDispatcher dispatcher, bool isSmokeTest)
        {
            dispatcher.SimulateNextFailure("Provedor SMTP simulado indisponivel (modo offline).");

            var model = new BoasVindasEmailModel
            {
                To = new MailAddress("teste.falha@cliente.example", "Teste Falha"),
                NomeUsuario = "Usuario Teste",
                Empresa = "Empresa Demo",
                UrlAtivacao = "https://portal.softprime.example/ativar/demo"
            };

            var response = await mailer.Send("BoasVindas", model, "Teste de falha controlada");

            if (response.Sent || response.ErrorMessages == null || response.ErrorMessages.Count == 0)
                throw new InvalidOperationException("FakeMailerDispatcher deveria retornar Sent=false e ErrorMessages preenchido.");

            if (dispatcher.SentEmails.Count != 2)
                throw new InvalidOperationException("Envio que falhou nao deve entrar na lista de e-mails enviados.");

            LogStep(isSmokeTest, "3. Cenario Negativo: Falha Simulada no Dispatcher",
                $"Sent={response.Sent} | Erro tratado: {response.ErrorMessages[0]}");
        }

        private static async Task RunMissingTemplateScenarioAsync(ISoftprimeMailer mailer, bool isSmokeTest)
        {
            var model = new BoasVindasEmailModel
            {
                To = new MailAddress("alguem@cliente.example"),
                NomeUsuario = "Alguem",
                Empresa = "Empresa",
                UrlAtivacao = "https://portal.softprime.example"
            };

            try
            {
                await mailer.Send("TemplateInexistente", model, "Nao deve completar");
            }
            catch (Exception ex)
            {
                LogStep(isSmokeTest, "4. Cenario Negativo: Template Inexistente",
                    $"Excecao {ex.GetType().Name} capturada -> {ex.Message}");
                return;
            }

            throw new InvalidOperationException("Template inexistente deveria lancar excecao na compilacao/resolucao.");
        }

        private static void LogStep(bool isSmokeTest, string title, string detail)
        {
            if (isSmokeTest) return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"--- {title} ---");
            Console.ResetColor();
            Console.WriteLine($"  {detail}");
        }
    }

    #region Subclasse de SoftprimeMailer, FakeDispatcher e Models

    public sealed class SampleMailer : SoftprimeMailer
    {
        public SampleMailer(SoftprimeMailerSettings settings, ISoftprimeMailerDispatcher dispatcher)
            : base(settings, dispatcher, typeof(TemplateMarker))
        {
        }
    }

    public sealed class FakeMailerDispatcher : ISoftprimeMailerDispatcher
    {
        private readonly List<DispatchedEmail> _sentEmails = new();
        private string? _nextFailureReason;

        public IReadOnlyList<DispatchedEmail> SentEmails => _sentEmails;

        public void SimulateNextFailure(string errorMessage) => _nextFailureReason = errorMessage;

        public Task<MailerResponse> Send(
            MailAddress from,
            MailAddress to,
            string subject,
            string htmlContent,
            string? plainTextContent = null,
            Attachment[]? attachments = null)
        {
            if (!string.IsNullOrEmpty(_nextFailureReason))
            {
                string error = _nextFailureReason;
                _nextFailureReason = null;

                return Task.FromResult(new MailerResponse
                {
                    Sent = false,
                    ErrorMessages = new List<string> { error }
                });
            }

            var attachmentInfos = new List<AttachmentSnapshot>();
            if (attachments != null)
            {
                foreach (var item in attachments)
                {
                    byte[] bytes = item.ContentStream.ReadFully();
                    attachmentInfos.Add(new AttachmentSnapshot(item.Name ?? "sem-nome", bytes.Length));
                }
            }

            _sentEmails.Add(new DispatchedEmail(
                from,
                to,
                subject,
                htmlContent,
                plainTextContent,
                attachmentInfos,
                DateTimeOffset.UtcNow));

            return Task.FromResult(new MailerResponse
            {
                Sent = true,
                ErrorMessages = new List<string>()
            });
        }
    }

    public sealed record AttachmentSnapshot(string Name, int SizeBytes);

    public sealed record DispatchedEmail(
        MailAddress From,
        MailAddress To,
        string Subject,
        string HtmlContent,
        string? PlainTextContent,
        IReadOnlyList<AttachmentSnapshot> Attachments,
        DateTimeOffset DispatchedAtUtc);

    public sealed class BoasVindasEmailModel : MailerModelBase
    {
        public string NomeUsuario { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string UrlAtivacao { get; set; } = string.Empty;
    }

    public sealed class ResumoPedidoEmailModel : MailerModelBase
    {
        public string NumeroPedido { get; set; } = string.Empty;
        public string NomeCliente { get; set; } = string.Empty;
        public List<ItemPedidoEmail> Itens { get; set; } = new();
        public decimal Total => Itens.Sum(i => i.Subtotal);
    }

    public sealed class ItemPedidoEmail
    {
        public string Descricao { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Subtotal => Quantidade * PrecoUnitario;
    }

    #endregion
}

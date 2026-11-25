using System;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using FluentAssertions;
using RazorLight;
using Softprime.Framework.Mailer.Models;
using Softprime.Framework.Mailer.Tests.Templates;
using Xunit;

namespace Softprime.Framework.Mailer.Tests
{
    public class MailerTests
    {
        [Fact]
        public async Task Send_EmbeddedTemplate_RendersModelAndForwardsMessageAndAttachment()
        {
            var expected = new MailerResponse { Sent = true };
            var dispatcher = new RecordingDispatcher(expected);
            var mailer = new TestMailer(dispatcher);
            var model = CreateModel();
            using var stream = new MemoryStream(new byte[] { 2, 4, 6, 8 });
            using var document = new Attachment(stream, "receipt.txt", "text/plain");

            var response = await mailer.Send("Receipt", model, "Synthetic receipt",
                "Receipt for Ada & Lin", new[] { document });

            response.Should().BeSameAs(expected);
            dispatcher.Calls.Should().Be(1);
            dispatcher.From.Address.Should().Be("billing@example.test");
            dispatcher.From.DisplayName.Should().Be("Test Billing");
            dispatcher.To.Should().BeSameAs(model.To);
            dispatcher.Subject.Should().Be("Synthetic receipt");
            dispatcher.Html.Should().Contain("<h1>Receipt for Ada &amp; Lin</h1>")
                .And.Contain("<li>Notebook</li>").And.Contain("<li>Pen</li>");
            dispatcher.PlainText.Should().Be("Receipt for Ada & Lin");
            dispatcher.Attachments.Should().ContainSingle().Which.Should().BeSameAs(document);
            document.Name.Should().Be("receipt.txt");
            stream.ToArray().Should().Equal(2, 4, 6, 8);
        }

        [Fact]
        public async Task Send_FailedDispatch_ReturnsDispatcherResponse()
        {
            var expected = new MailerResponse
            {
                Sent = false,
                ErrorMessages = new[] { "Synthetic dispatcher failure" }
            };
            var dispatcher = new RecordingDispatcher(expected);
            var mailer = new TestMailer(dispatcher);

            var response = await mailer.Send("Receipt", CreateModel(), "Synthetic receipt");

            response.Should().BeSameAs(expected);
            response.ErrorMessages.Should().Equal("Synthetic dispatcher failure");
            dispatcher.Calls.Should().Be(1);
            dispatcher.PlainText.Should().BeNull();
            dispatcher.Attachments.Should().BeNull();
        }

        [Fact]
        public async Task Send_CompiledTemplateUsedTwice_RendersEachModelAndRecipient()
        {
            var dispatcher = new RecordingDispatcher(new MailerResponse { Sent = true });
            var mailer = new TestMailer(dispatcher);
            await mailer.Send("Receipt", CreateModel(), "First receipt");
            var firstHtml = dispatcher.Html;
            var second = new ReceiptModel
            {
                To = new MailAddress("second@example.test"),
                Customer = "Bea",
                Items = new[] { "Calendar" }
            };

            await mailer.Send("Receipt", second, "Second receipt");

            dispatcher.Calls.Should().Be(2);
            firstHtml.Should().Contain("Ada &amp; Lin");
            dispatcher.Html.Should().Contain("Receipt for Bea").And.Contain("<li>Calendar</li>")
                .And.NotContain("Ada").And.NotContain("Notebook");
            dispatcher.To.Should().BeSameAs(second.To);
            dispatcher.Subject.Should().Be("Second receipt");
        }

        [Fact]
        public async Task Send_EmptyItemsAndAttachments_RendersTemplateAndForwardsEmptyArray()
        {
            var dispatcher = new RecordingDispatcher(new MailerResponse { Sent = true });
            var mailer = new TestMailer(dispatcher);
            var model = CreateModel();
            model.Items = Array.Empty<string>();
            var attachments = Array.Empty<Attachment>();

            var response = await mailer.Send("Receipt", model, "Empty receipt", "", attachments);

            response.Sent.Should().BeTrue();
            dispatcher.Html.Should().Contain("Receipt for Ada &amp; Lin").And.NotContain("<li>");
            dispatcher.PlainText.Should().BeEmpty();
            dispatcher.Attachments.Should().BeSameAs(attachments);
        }

        [Fact]
        public async Task Send_DispatcherThrows_PropagatesTheSameException()
        {
            var failure = new InvalidOperationException("synthetic dispatch failure");
            var dispatcher = new RecordingDispatcher(new MailerResponse()) { Failure = failure };
            var mailer = new TestMailer(dispatcher);

            Func<Task> send = () => mailer.Send("Receipt", CreateModel(), "Synthetic receipt");

            (await send.Should().ThrowExactlyAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
            dispatcher.Calls.Should().Be(1);
        }

        [Fact]
        public async Task Send_MissingTemplate_ThrowsBeforeDispatch()
        {
            var dispatcher = new RecordingDispatcher(new MailerResponse());
            var mailer = new TestMailer(dispatcher);

            Func<Task> send = () => mailer.Send("MissingTemplate", CreateModel(), "Synthetic receipt");

            await send.Should().ThrowAsync<TemplateNotFoundException>();
            dispatcher.Calls.Should().Be(0);
        }

        private static ReceiptModel CreateModel()
        {
            return new ReceiptModel
            {
                To = new MailAddress("recipient@example.test", "Test Recipient"),
                Customer = "Ada & Lin",
                Items = new[] { "Notebook", "Pen" }
            };
        }

        private sealed class TestMailer : SoftprimeMailer
        {
            public TestMailer(ISoftprimeMailerDispatcher dispatcher)
                : base(new SoftprimeMailerSettings
                {
                    DefaultFromEmail = "billing@example.test",
                    DefaultFromName = "Test Billing"
                }, dispatcher, typeof(TemplateMarker))
            {
            }
        }

        private sealed class RecordingDispatcher : ISoftprimeMailerDispatcher
        {
            private readonly MailerResponse _response;

            public RecordingDispatcher(MailerResponse response)
            {
                _response = response;
            }

            public int Calls { get; private set; }
            public MailAddress From { get; private set; }
            public MailAddress To { get; private set; }
            public string Subject { get; private set; }
            public string Html { get; private set; }
            public string PlainText { get; private set; }
            public Attachment[] Attachments { get; private set; }
            public Exception Failure { get; set; }

            public Task<MailerResponse> Send(MailAddress from, MailAddress to, string subject,
                string htmlContent, string plainTextContent, Attachment[] attachments)
            {
                Calls++;
                From = from;
                To = to;
                Subject = subject;
                Html = htmlContent;
                PlainText = plainTextContent;
                Attachments = attachments;
                return Failure == null ? Task.FromResult(_response) : Task.FromException<MailerResponse>(Failure);
            }
        }
    }

    public sealed class ReceiptModel : MailerModelBase
    {
        public string Customer { get; set; }
        public string[] Items { get; set; }
    }
}

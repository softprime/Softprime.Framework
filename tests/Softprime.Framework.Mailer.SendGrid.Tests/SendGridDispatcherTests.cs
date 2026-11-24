using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;
using Xunit;
using MailAttachment = System.Net.Mail.Attachment;

namespace Softprime.Framework.Mailer.SendGrid.Tests
{
    public class SendGridDispatcherTests
    {
        [Theory]
        [InlineData(HttpStatusCode.Accepted, true)]
        [InlineData(HttpStatusCode.OK, false)]
        [InlineData(HttpStatusCode.BadRequest, false)]
        public async Task Send_ProviderResponse_MapsMessageAndChecksForAccepted(HttpStatusCode status, bool sent)
        {
            var from = new MailAddress("sender@example.test", "Test Sender");
            var to = new MailAddress("recipient@example.test", "Test Recipient");
            var settings = CreateSettings();
            var client = new Mock<ISendGridClient>(MockBehavior.Strict);
            SendGridMessage message = null;
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), CancellationToken.None))
                .Callback<SendGridMessage, CancellationToken>((value, _) => message = value)
                .ReturnsAsync(new Response(status, null, null));
            SendGridSettings receivedSettings = null;
            var dispatcher = new SendGridDispatcher(settings, value =>
            {
                receivedSettings = value;
                return client.Object;
            });
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            stream.Position = 2;
            using var attachment = new MailAttachment(stream, "receipt.txt", "text/plain");

            var response = await dispatcher.Send(from, to, "Synthetic mail", "<b>HTML</b>",
                "Plain text", new[] { attachment });

            receivedSettings.Should().BeSameAs(settings);
            message.Should().NotBeNull();
            message.From.Email.Should().Be(from.Address);
            message.From.Name.Should().Be(from.DisplayName);
            var personalization = message.Personalizations.Should().ContainSingle().Which;
            var recipient = personalization.Tos.Should().ContainSingle().Which;
            recipient.Email.Should().Be(to.Address);
            recipient.Name.Should().Be(to.DisplayName);
            personalization.Subject.Should().Be("Synthetic mail");
            message.Subject.Should().BeNull();
            message.Contents.Should().SatisfyRespectively(
                text =>
                {
                    text.Type.Should().Be("text/plain");
                    text.Value.Should().Be("Plain text");
                },
                html =>
                {
                    html.Type.Should().Be("text/html");
                    html.Value.Should().Be("<b>HTML</b>");
                });
            var file = message.Attachments.Should().ContainSingle().Which;
            file.Filename.Should().Be("receipt.txt");
            file.Content.Should().Be("AQID");
            file.Type.Should().BeNull();
            stream.Position.Should().Be(stream.Length);
            response.Sent.Should().Be(sent);
            response.ErrorMessages.Should().BeNull();
            client.Verify(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Send_ClientThrows_ReturnsProviderError()
        {
            var client = new Mock<ISendGridClient>(MockBehavior.Strict);
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), CancellationToken.None))
                .ThrowsAsync(new InvalidOperationException("Synthetic SendGrid send failure"));
            var dispatcher = new SendGridDispatcher(CreateSettings(), _ => client.Object);

            var response = await dispatcher.Send(new MailAddress("sender@example.test"),
                new MailAddress("recipient@example.test"), "Synthetic mail", "<b>HTML</b>");

            response.Sent.Should().BeFalse();
            response.ErrorMessages.Should().Equal("Synthetic SendGrid send failure");
        }

        [Fact]
        public async Task Send_FactoryThrows_PropagatesError()
        {
            var dispatcher = new SendGridDispatcher(CreateSettings(),
                _ => throw new InvalidOperationException("Synthetic SendGrid factory failure"));

            Func<Task> send = () => dispatcher.Send(new MailAddress("sender@example.test"),
                new MailAddress("recipient@example.test"), "Synthetic mail", "<b>HTML</b>");

            await send.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Synthetic SendGrid factory failure");
        }

        private static SendGridSettings CreateSettings()
        {
            return new SendGridSettings { ApiKey = "test-sendgrid-key" };
        }
    }
}

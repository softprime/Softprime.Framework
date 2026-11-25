using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace Softprime.Framework.Mailer.AWS.Tests
{
    public class AWSDispatcherTests
    {
        [Theory]
        [InlineData(HttpStatusCode.Accepted, true)]
        [InlineData(HttpStatusCode.OK, false)]
        [InlineData(HttpStatusCode.BadRequest, false)]
        public async Task Send_ProviderResponse_MapsRequestAndChecksForAccepted(HttpStatusCode status, bool sent)
        {
            var from = new MailAddress("sender@example.test", "Test Sender");
            var to = new MailAddress("recipient@example.test", "Test Recipient");
            var settings = CreateSettings();
            var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
            SendEmailRequest request = null;
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), CancellationToken.None))
                .Callback<SendEmailRequest, CancellationToken>((value, _) => request = value)
                .ReturnsAsync(new SendEmailResponse { HttpStatusCode = status, MessageId = "synthetic-message" });
            AWSSettings receivedSettings = null;
            var dispatcher = new AWSDispatcher(settings, value =>
            {
                receivedSettings = value;
                return client.Object;
            });
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            stream.Position = 1;
            using var attachment = new Attachment(stream, "ignored.txt", "text/plain");

            var response = await dispatcher.Send(from, to, "Synthetic mail", "<b>HTML</b>",
                "Plain text", new[] { attachment });

            receivedSettings.Should().BeSameAs(settings);
            request.Should().NotBeNull();
            request.FromEmailAddress.Should().Be(from.Address);
            request.ReplyToAddresses.Should().Equal(from.Address);
            request.Destination.ToAddresses.Should().Equal(to.Address);
            request.Content.Simple.Subject.Data.Should().Be("Synthetic mail");
            request.Content.Simple.Subject.Charset.Should().Be("UTF-8");
            request.Content.Simple.Body.Html.Data.Should().Be("<b>HTML</b>");
            request.Content.Simple.Body.Html.Charset.Should().Be("UTF-8");
            request.Content.Simple.Body.Text.Data.Should().Be("Plain text");
            request.Content.Simple.Body.Text.Charset.Should().Be("UTF-8");
            request.Content.Raw.Should().BeNull();
            stream.Position.Should().Be(1);
            response.Sent.Should().Be(sent);
            response.ErrorMessages.Should().BeNull();
            client.Verify(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Send_NoPlainText_MapsHtmlAndAnEmptyTextDataField()
        {
            var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
            SendEmailRequest request = null;
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), CancellationToken.None))
                .Callback<SendEmailRequest, CancellationToken>((value, _) => request = value)
                .ReturnsAsync(new SendEmailResponse { HttpStatusCode = HttpStatusCode.Accepted });
            var dispatcher = new AWSDispatcher(CreateSettings(), _ => client.Object);

            var response = await dispatcher.Send(new MailAddress("sender@example.test"),
                new MailAddress("recipient@example.test"), "Synthetic mail", "<b>HTML</b>");

            response.Sent.Should().BeTrue();
            request.Content.Simple.Body.Html.Data.Should().Be("<b>HTML</b>");
            request.Content.Simple.Body.Text.Data.Should().BeNull();
            request.Content.Simple.Body.Text.Charset.Should().Be("UTF-8");
        }

        [Fact]
        public async Task Send_AfterFactoryFailure_UsesFactoryAgainAndReturnsSuccessfulResponse()
        {
            var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), CancellationToken.None))
                .ReturnsAsync(new SendEmailResponse { HttpStatusCode = HttpStatusCode.Accepted });
            var calls = 0;
            var dispatcher = new AWSDispatcher(CreateSettings(), _ => ++calls == 1
                ? throw new InvalidOperationException("synthetic factory failure")
                : client.Object);
            var from = new MailAddress("sender@example.test");
            var to = new MailAddress("recipient@example.test");

            var first = await dispatcher.Send(from, to, "First message", "<p>first</p>");
            var second = await dispatcher.Send(from, to, "Second message", "<p>second</p>");

            first.Sent.Should().BeFalse();
            first.ErrorMessages.Should().Equal("synthetic factory failure");
            second.Sent.Should().BeTrue();
            second.ErrorMessages.Should().BeNull();
            calls.Should().Be(2);
            client.Verify(value => value.SendEmailAsync(It.Is<SendEmailRequest>(
                request => request.Content.Simple.Subject.Data == "Second message"), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Send_ClientThrows_ReturnsProviderError()
        {
            var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
            client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), CancellationToken.None))
                .ThrowsAsync(new InvalidOperationException("Synthetic AWS send failure"));
            var dispatcher = new AWSDispatcher(CreateSettings(), _ => client.Object);

            var response = await dispatcher.Send(new MailAddress("sender@example.test"),
                new MailAddress("recipient@example.test"), "Synthetic mail", "<b>HTML</b>");

            response.Sent.Should().BeFalse();
            response.ErrorMessages.Should().Equal("Synthetic AWS send failure");
        }

        [Fact]
        public async Task Send_FactoryThrows_ReturnsError()
        {
            var dispatcher = new AWSDispatcher(CreateSettings(),
                _ => throw new InvalidOperationException("Synthetic AWS factory failure"));

            var response = await dispatcher.Send(new MailAddress("sender@example.test"),
                new MailAddress("recipient@example.test"), "Synthetic mail", "<b>HTML</b>");

            response.Sent.Should().BeFalse();
            response.ErrorMessages.Should().Equal("Synthetic AWS factory failure");
        }

        private static AWSSettings CreateSettings()
        {
            return new AWSSettings { AccessKey = "test-access-key", SecretKey = "test-secret-key" };
        }
    }
}

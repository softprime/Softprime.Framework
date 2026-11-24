using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Softprime.Framework.Api.Dto.Default;
using Softprime.Framework.Api.Tests;
using Xunit;

namespace Softprime.Framework.Api.Client.Tests
{
    public class ApiClientTests
    {
        [Theory]
        [InlineData("items", "/base/items")]
        [InlineData("/items", "/items")]
        public async Task Get_WithBasePath_ResolvesUriAndDeserializesList(string service, string expectedTarget)
        {
            const string response = """[{"id":17,"name":"ação","note":null,"status":2}]""";
            await using var endpoint = await Endpoint.StartAsync(response);
            var client = new ApiClient<ClientItem, int>(service, endpoint.BaseUrl);
            using var transport = client.Client;

            var items = (await client.Get()).ToList();
            var request = await endpoint.Request;

            client.UriService.Should().Be(service);
            transport.BaseAddress.Should().Be(new Uri(endpoint.BaseUrl));
            transport.Timeout.Should().Be(TimeSpan.FromMinutes(1));
            request.Method.Should().Be("GET");
            request.Target.Should().Be(expectedTarget);
            request.Accept.Should().Be("application/json");
            request.ApiKey.Should().BeEmpty();
            request.TenantKey.Should().BeEmpty();
            var item = items.Should().ContainSingle().Which;
            item.Id.Should().Be(17);
            item.Name.Should().Be("ação");
            item.Note.Should().Be("default note");
            item.Status.Should().Be(ItemStatus.Active);
        }

        [Fact]
        public async Task Get_WithPackagedNewtonsoftVersion_DeserializesTheSameWireShape()
        {
            const string response = """[{"id":17,"name":"ação","note":null,"status":2}]""";
            await using var endpoint = await Endpoint.StartAsync(response);
            var context = new LegacyNewtonsoftLoadContext();
            var clientAssembly = context.LoadFromAssemblyPath(typeof(ApiClient<,>).Assembly.Location);
            var clientType = clientAssembly.GetType(typeof(ApiClient<,>).FullName, throwOnError: true)
                .MakeGenericType(typeof(ClientItem), typeof(int));
            var client = Activator.CreateInstance(clientType, "items", endpoint.BaseUrl);
            using var transport = (HttpClient)clientType.GetProperty(nameof(ApiClientBase.Client)).GetValue(client);

            var get = clientType.GetMethod(nameof(ApiClient<ClientItem, int>.Get), Type.EmptyTypes);
            var items = await (Task<IEnumerable<ClientItem>>)get.Invoke(client, null);

            var serializationAssembly = context.Assemblies.Single(assembly => assembly.GetName().Name == "Newtonsoft.Json");
            FileVersionInfo.GetVersionInfo(serializationAssembly.Location).ProductVersion.Should().StartWith("12.0.3");
            var item = items.Should().ContainSingle().Which;
            item.Id.Should().Be(17);
            item.Name.Should().Be("ação");
            item.Note.Should().Be("default note");
            item.Status.Should().Be(ItemStatus.Active);
        }

        [Theory]
        [InlineData(-3, 0, 0, 1)]
        [InlineData(2, 3, 2, 3)]
        public async Task Get_WithPageAndSize_NormalizesBoundsAndDeserializesPagination(
            int page, int size, int expectedPage, int expectedSize)
        {
            const string response = """
                {
                    "PageIndex":4,"PageSize":6,"TotalCount":30,"TotalPages":5,
                    "Results":[{"Id":21}],"HasPreviousPage":true,"HasNextPage":false
                }
                """;
            await using var endpoint = await Endpoint.StartAsync(response);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl, "api-fixture");
            using var transport = client.Client;

            var result = await client.Get(page, size);
            var request = await endpoint.Request;

            request.Method.Should().Be("GET");
            request.Target.Should().Be($"/base/items?page={expectedPage}&size={expectedSize}");
            request.ApiKey.Should().Be("api-fixture");
            request.TenantKey.Should().BeEmpty();
            result.PageIndex.Should().Be(4);
            result.PageSize.Should().Be(6);
            result.TotalCount.Should().Be(30);
            result.TotalPages.Should().Be(5);
            result.HasPreviousPage.Should().BeTrue();
            result.HasNextPage.Should().BeFalse();
            result.Results.Should().ContainSingle().Which.Id.Should().Be(21);
        }

        [Fact]
        public async Task Get_TypedGuidClient_UsesIdSegmentAndSendsBothHeaders()
        {
            const string id = "1bd432c6-cbdc-4526-9e08-c30c1b4583e8";
            await using var endpoint = await Endpoint.StartAsync($"{{\"Id\":\"{id}\",\"Name\":\"typed\"}}");
            var client = new ApiClientTyped<GuidItem, Guid>("items", endpoint.BaseUrl, "api-fixture", "tenant-fixture");
            using var transport = client.Client;

            var result = await client.Get(Guid.Parse(id));
            var request = await endpoint.Request;

            request.Method.Should().Be("GET");
            request.Target.Should().Be($"/base/items/{id}");
            request.Accept.Should().Be("application/json");
            request.ApiKey.Should().Be("api-fixture");
            request.TenantKey.Should().Be("tenant-fixture");
            result.Id.Should().Be(Guid.Parse(id));
            result.Name.Should().Be("typed");
        }

        [Theory]
        [InlineData(false, "POST", "/base/items", 201)]
        [InlineData(true, "PUT", "/base/items/42", 200)]
        public async Task PostOrPut_JsonBody_UsesTheExpectedVerbPathAndPayload(
            bool update, string method, string target, int statusCode)
        {
            await using var endpoint = await Endpoint.StartAsync("""{"Id":84,"Name":"response","Status":2}""", statusCode);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;
            var item = new ClientItem { Id = 42, Name = "ação", Note = null, Status = ItemStatus.Active };

            var result = update ? await client.Put(item) : await client.Post(item);
            var request = await endpoint.Request;

            request.Method.Should().Be(method);
            request.Target.Should().Be(target);
            request.ContentType.Should().Be("application/json");
            JToken.DeepEquals(JToken.Parse(request.Body),
                JObject.Parse("""{"Id":42,"Name":"ação","Note":null,"Status":2}"""))
                .Should().BeTrue("the request used the legacy DTO JSON format, but the body was {0}", request.Body);
            result.Id.Should().Be(84);
            result.Name.Should().Be("response");
            result.Status.Should().Be(ItemStatus.Active);
        }

        [Fact]
        public async Task Delete_NoContent_SendsIdAndCompletes()
        {
            await using var endpoint = await Endpoint.StartAsync("", StatusCodes.Status204NoContent);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;

            await client.Delete(42);
            var request = await endpoint.Request;

            request.Method.Should().Be("DELETE");
            request.Target.Should().Be("/base/items/42");
        }

        [Theory]
        [InlineData(false, StatusCodes.Status500InternalServerError, "synthetic failure")]
        [InlineData(true, StatusCodes.Status404NotFound, """{"error":"synthetic missing item"}""")]
        public async Task Request_NonSuccess_ReportsUnmodifiedResponseBody(
            bool delete, int statusCode, string response)
        {
            await using var endpoint = await Endpoint.StartAsync(response, statusCode);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;
            Func<Task> operation = delete ? () => client.Delete(42) : () => client.Get(42);

            var error = await Assert.ThrowsAsync<ApiException>(operation);

            error.Message.Should().Be(response);
        }

        [Fact]
        public async Task Get_ApiKeyOnlyConstructor_SendsApiKeyAndOmitsTenantKey()
        {
            await using var endpoint = await Endpoint.StartAsync("[]");
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl, "synthetic-key");
            using var transport = client.Client;

            var items = await client.Get();
            var request = await endpoint.Request;

            items.Should().BeEmpty();
            request.ApiKey.Should().Be("synthetic-key");
            request.TenantKey.Should().BeEmpty();
        }

        [Theory]
        [InlineData("", StatusCodes.Status204NoContent)]
        [InlineData("null", StatusCodes.Status200OK)]
        public async Task Get_EmptyOrJsonNullResponse_ReturnsNull(string body, int statusCode)
        {
            await using var endpoint = await Endpoint.StartAsync(body, statusCode);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;

            var result = await client.Get(15);

            result.Should().BeNull();
            (await endpoint.Request).Target.Should().Be("/base/items/15");
        }

        [Fact]
        public async Task Get_PagesOnUriWithExistingQuery_AppendsTheLegacyQueryString()
        {
            await using var endpoint = await Endpoint.StartAsync("""{"Results":[]}""");
            var client = new ApiClient<ClientItem, int>("items?scope=all", endpoint.BaseUrl);
            using var transport = client.Client;

            var page = await client.Get(1, 2);

            page.Results.Should().BeEmpty();
            (await endpoint.Request).Target.Should().Be("/base/items?scope=all?page=1&size=2");
        }

        [Fact]
        public async Task Get_InvalidJsonOnSuccess_ExposesJsonReaderException()
        {
            await using var endpoint = await Endpoint.StartAsync("malformed");
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;

            Func<Task> operation = () => client.Get(42);

            await operation.Should().ThrowExactlyAsync<JsonReaderException>();
        }

        [Fact]
        public async Task Get_WhenTransportTimeoutExpires_ThrowsCancellationWithTimeoutCause()
        {
            await using var endpoint = await Endpoint.StartAsync(WaitForRequestAbortAsync);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;
            transport.Timeout = TimeSpan.FromSeconds(2);

            Func<Task> operation = () => client.Get(42);

            var error = await Assert.ThrowsAsync<TaskCanceledException>(operation);
            error.InnerException.Should().BeOfType<TimeoutException>();
        }

        [Fact]
        public async Task Get_WhenPendingRequestsAreCanceled_ThrowsCancellation()
        {
            await using var endpoint = await Endpoint.StartAsync(WaitForRequestAbortAsync);
            var client = new ApiClient<ClientItem, int>("items", endpoint.BaseUrl);
            using var transport = client.Client;
            var pending = client.Get(42);
            await endpoint.Request.WaitAsync(TimeSpan.FromSeconds(10));

            transport.CancelPendingRequests();

            var error = await Assert.ThrowsAsync<TaskCanceledException>(() => pending);
            (error.InnerException is TimeoutException).Should().BeFalse();
        }

        private static async Task WaitForRequestAbortAsync(HttpContext context)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The test completes the pending request by canceling the client side.
            }
        }

        private enum ItemStatus
        {
            Unknown,
            Active = 2
        }

        private class ClientItem : DtoBase<int>
        {
            public string Name { get; set; }
            public string Note { get; set; } = "default note";
            public ItemStatus Status { get; set; }
        }

        private class GuidItem : DtoBase<Guid>
        {
            public string Name { get; set; }
        }

        private sealed record ObservedRequest(
            string Method, string Target, string Accept, string ApiKey, string TenantKey,
            string ContentType, string Body);

        private sealed class Endpoint : IAsyncDisposable
        {
            private readonly WebApplication app;

            private Endpoint(WebApplication app, Task<ObservedRequest> request)
            {
                this.app = app;
                Request = request;
            }

            internal string BaseUrl => new Uri(LoopbackHost.Address(app), "/base/").AbsoluteUri;

            internal Task<ObservedRequest> Request { get; }

            internal static Task<Endpoint> StartAsync(string body, int statusCode = StatusCodes.Status200OK) =>
                StartAsync(async context =>
                {
                    context.Response.StatusCode = statusCode;
                    context.Response.ContentType = "application/json";
                    if (body.Length > 0)
                        await context.Response.WriteAsync(body);
                });

            internal static async Task<Endpoint> StartAsync(Func<HttpContext, Task> respond)
            {
                var requestReceived = new TaskCompletionSource<ObservedRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
                var app = await LoopbackHost.StartAsync(webApp => webApp.Run(async context =>
                {
                    var request = context.Request;
                    using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync(context.RequestAborted);
                    requestReceived.TrySetResult(new ObservedRequest(
                        request.Method, context.Features.Get<IHttpRequestFeature>().RawTarget,
                        request.Headers.Accept.ToString(), request.Headers["ApiKey"].ToString(),
                        request.Headers["TenantKey"].ToString(), request.ContentType, body));
                    await respond(context);
                }));

                return new Endpoint(app, requestReceived.Task);
            }

            public ValueTask DisposeAsync() => app.DisposeAsync();
        }
    }
}

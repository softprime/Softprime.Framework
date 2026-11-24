#if NET10_0
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Softprime.Framework.Api.Dto.Default;
using Xunit;

namespace Softprime.Framework.Api.Tests
{
    public class ApiHostTests
    {
        [Theory]
        [InlineData(null, HttpStatusCode.Forbidden)]
        [InlineData("wrong-key", HttpStatusCode.Forbidden)]
        [InlineData("fixture-key", HttpStatusCode.OK)]
        public async Task Get_WithApiKeyFilter_EnforcesTheHttpStatusAndExecutesTheApiAction(
            string apiKey, HttpStatusCode expectedStatus)
        {
            await using var app = await LoopbackHost.StartAsync(
                webApp => webApp.MapControllers(),
                builder => builder.Services.AddControllers().AddApplicationPart(typeof(ItemsController).Assembly));
            using var client = new HttpClient { BaseAddress = LoopbackHost.Address(app) };
            using var request = new HttpRequestMessage(HttpMethod.Get, "/items/42");
            if (apiKey != null)
                request.Headers.Add("X-Test-Key", apiKey);

            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(expectedStatus);
            if (expectedStatus == HttpStatusCode.OK)
                (await response.Content.ReadFromJsonAsync<HostedItemDto>()).Id.Should().Be(42);
        }
    }

    [Route("items")]
    [TypeFilter(typeof(ApiKeyAuthorizeFilter), Arguments = new object[] { "X-Test-Key", "fixture-key" })]
    public class ItemsController : ControllerBase, IApi<HostedItemDto, int>
    {
        [HttpGet]
        public Task<IActionResult> Get() => Task.FromResult<IActionResult>(Ok(new HostedItemDto[0]));

        [HttpGet("paged")]
        public Task<IActionResult> Get(int page, int size) =>
            Task.FromResult<IActionResult>(Ok(new PagedResultDto<HostedItemDto> { PageIndex = page, PageSize = size }));

        [HttpGet("{id:int}")]
        public Task<IActionResult> Get(int id) =>
            Task.FromResult<IActionResult>(Ok(new HostedItemDto { Id = id }));

        [HttpPost]
        public Task<IActionResult> Post([FromBody] HostedItemDto obj) => Task.FromResult<IActionResult>(Ok(obj));

        [HttpPut("{id:int}")]
        public Task<IActionResult> Put(int id, [FromBody] HostedItemDto obj)
        {
            obj.Id = id;
            return Task.FromResult<IActionResult>(Ok(obj));
        }

        [HttpDelete("{id:int}")]
        public Task<IActionResult> Delete(int id) => Task.FromResult<IActionResult>(NoContent());
    }

    public class HostedItemDto : DtoBase<int>
    {
    }
}
#endif

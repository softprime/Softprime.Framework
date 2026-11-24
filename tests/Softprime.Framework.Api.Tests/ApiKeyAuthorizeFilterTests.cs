using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Softprime.Framework.Api.Tests
{
    public class ApiKeyAuthorizeFilterTests
    {
        [Theory]
        [InlineData("X-Test-Key", "fixture-key")]
        [InlineData("x-test-key", "fixture-key")]
        public void OnActionExecuting_MatchingKey_LeavesTheActionEligible(string header, string value)
        {
            var filter = new ApiKeyAuthorizeFilter("X-Test-Key", "fixture-key");
            var context = CreateContext();
            context.HttpContext.Request.Headers[header] = value;

            filter.OnActionExecuting(context);

            context.Result.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("wrong-key")]
        [InlineData("FIXTURE-KEY")]
        public void OnActionExecuting_MissingOrDifferentKey_SetsForbiddenResult(string value)
        {
            var filter = new ApiKeyAuthorizeFilter("X-Test-Key", "fixture-key");
            var context = CreateContext();
            if (value != null)
                context.HttpContext.Request.Headers["X-Test-Key"] = value;

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(403);
        }

        [Fact]
        public void OnActionExecuting_DuplicateValues_SetsForbiddenResult()
        {
            var filter = new ApiKeyAuthorizeFilter("X-Test-Key", "fixture-key");
            var context = CreateContext();
            context.HttpContext.Request.Headers["X-Test-Key"] = new StringValues(new[] { "fixture-key", "fixture-key" });

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(403);
        }

        private static ActionExecutingContext CreateContext()
        {
            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(
                actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());
        }
    }
}

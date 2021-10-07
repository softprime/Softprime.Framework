using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Softprime.Framework.Api
{
    public class ApiKeyAuthorizeFilter : ActionFilterAttribute
    {
        private readonly string _nameApiKey;
        private readonly string _apiKey;

        public ApiKeyAuthorizeFilter(string nameApiKey, string apiKey)
        {
            _nameApiKey = nameApiKey;
            _apiKey = apiKey;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Request.Headers[_nameApiKey] != _apiKey)
                context.Result = new StatusCodeResult(403);
        }

    }
}
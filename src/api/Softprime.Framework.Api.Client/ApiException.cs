using System;

namespace Softprime.Framework.Api.Client
{
    public class ApiException : Exception
    {
        public ApiException(string message)
        : base(message)
        {
        }
    }
}

using System;
using Xunit;

namespace Softprime.Framework.Api.Dto.Tests
{
    public sealed class LegacyDtoFixture : IDisposable
    {
#if NET48
        private readonly AppDomain domain;
#endif
        private readonly LegacyDtoRunner runner;

        public LegacyDtoFixture()
        {
#if NET48
            // VSTest redirects Newtonsoft in its own AppDomain.
            var setup = new AppDomainSetup
            {
                ApplicationBase = AppContext.BaseDirectory,
                DisallowBindingRedirects = true
            };
            domain = AppDomain.CreateDomain("Legacy DTO serializer", null, setup);
            var runnerType = typeof(LegacyDtoRunner);
            runner = (LegacyDtoRunner)domain.CreateInstanceFromAndUnwrap(runnerType.Assembly.Location, runnerType.FullName);
#else
            runner = new LegacyDtoRunner();
#endif
        }

        internal void Verify(Action assertion)
        {
            var failure = runner.Run(assertion);
            Assert.True(failure == null, failure);
        }

        internal void Verify(Action<string, string> assertion, string json, string note)
        {
            var failure = runner.Run(assertion, json, note);
            Assert.True(failure == null, failure);
        }

        public void Dispose()
        {
#if NET48
            AppDomain.Unload(domain);
#endif
        }
    }

    public sealed class LegacyDtoRunner : MarshalByRefObject
    {
        public string Run(Action assertion)
        {
            try
            {
                assertion();
                return null;
            }
            catch (Exception exception)
            {
                return exception.ToString();
            }
        }

        public string Run(Action<string, string> assertion, string json, string note) =>
            Run(() => assertion(json, note));
    }
}

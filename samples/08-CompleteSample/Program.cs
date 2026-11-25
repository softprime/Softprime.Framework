using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.DAL;
using Softprime.Framework.EFCore;
using Softprime.Framework.Mailer;
using Softprime.Framework.Mailer.Models;
using Softprime.Framework.Multitenancy;

namespace Softprime.Samples.CompleteSample;

internal static class Program
{
    internal const string ApiKey = "chave-ficticia-sample";
    internal const string TenantHeader = "TenantKey";
    internal const string Alpha = "alpha";
    internal const string Beta = "beta";

    public static async Task<int> Main(string[] args)
    {
        var smoke = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        var directory = Directory.CreateTempSubdirectory("softprime-complete-");

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(directory.FullName, "catalogo.db"),
                ForeignKeys = true,
                Pooling = false
            }.ToString();

            await using var app = BuildApp(connectionString, smoke);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
            }

            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            await Demo.RunAsync(address, app.Services, smoke);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        Console.WriteLine(smoke
            ? "[SMOKE_TEST_PASSED] Softprime.Samples.CompleteSample: cadastros, SQLite, e-mail simulado e isolamento validados."
            : "Demonstração concluída. O banco SQLite temporário foi descartado.");
        return 0;
    }

    private static WebApplication BuildApp(string connectionString, bool smoke)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(smoke ? LogLevel.Error : LogLevel.Warning);

        builder.Services.AddControllers();
        builder.Services.AddMultitenancy<SampleTenantResolver>();
        builder.Services.AddDbContext<SampleDbContext>(options => options.UseSqlite(connectionString));
        builder.Services.AddScoped<IUnitOfWork, EFUnitOfWork<SampleDbContext>>();
        builder.Services.AddSingleton<FakeMailerDispatcher>();
        builder.Services.AddSingleton<ISoftprimeMailer>(services => new SampleMailer(
            new SoftprimeMailerSettings
            {
                DefaultFromEmail = "cadastro@example.test",
                DefaultFromName = "Softprime Samples"
            },
            services.GetRequiredService<FakeMailerDispatcher>()));

        var app = builder.Build();
        app.UseMultitenancy();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") && context.GetTenantContext() is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new { erro = "tenant_invalido" });
                return;
            }

            await next();
        });
        app.MapControllers();
        return app;
    }
}

public sealed class SampleTenantResolver : ITenantResolver
{
    public Task<TenantContext> ResolveAsync(HttpContext context)
    {
        var values = context.Request.Headers[Program.TenantHeader];
        var key = values.Count == 1 ? values[0] : null;
        return Task.FromResult(key is Program.Alpha or Program.Beta
            ? new TenantContext(new SampleTenant { TenantKey = key })
            : null!);
    }

    private sealed class SampleTenant : IAppTenant
    {
        public string TenantKey { get; set; } = string.Empty;
        public DataBaseType DataBaseType { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
    }
}

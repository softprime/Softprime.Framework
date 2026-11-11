using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softprime.Framework.Multitenancy;

namespace Softprime.Samples.Multitenancy
{
    public static class TenantConstants
    {
        public const string HeaderName = "X-Tenant-Key";
        public const string QueryName = "tenant";
        public const string AlphaKey = "alpha";
        public const string BetaKey = "beta";
    }

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
            bool isServeMode = args != null && Array.Exists(args, a => a.Equals("--serve", StringComparison.OrdinalIgnoreCase));

            if (isServeMode)
            {
                var app = BuildWebApp(args!, port: 5087, quietLogging: false);
                Console.WriteLine("================================================================================");
                Console.WriteLine(" Softprime.Framework — Sample 07-Multitenancy (Modo Servidor Interativo)");
                Console.WriteLine("================================================================================");
                Console.WriteLine("API rodando em: http://127.0.0.1:5087");
                Console.WriteLine($"Tenants validos: '{TenantConstants.AlphaKey}' e '{TenantConstants.BetaKey}'");
                Console.WriteLine($"Envie via Header ({TenantConstants.HeaderName}: alpha), Query (?{TenantConstants.QueryName}=beta) ou Host (alpha.softprime.local).");
                Console.WriteLine("Pressione Ctrl+C para encerrar.");
                await app.RunAsync();
                return 0;
            }

            return await RunSelfHostedDemoAsync(args ?? Array.Empty<string>(), isSmokeTest);
        }

        private static WebApplication BuildWebApp(string[] args, int port, bool quietLogging)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (quietLogging)
            {
                builder.Logging.ClearProviders();
            }

            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            var tenants = new MultitenancyOptions<SampleTenant>
            {
                Tenants = new Collection<SampleTenant>
                {
                    new()
                    {
                        TenantKey = TenantConstants.AlphaKey,
                        DisplayName = "Alpha Industria S.A.",
                        DataBaseType = DataBaseType.Postgres,
                        ConnectionString = "Host=localhost;Database=softprime_alpha;Username=demo"
                    },
                    new()
                    {
                        TenantKey = TenantConstants.BetaKey,
                        DisplayName = "Beta Comercio Ltda",
                        DataBaseType = DataBaseType.MsSql,
                        ConnectionString = "Server=localhost;Database=softprime_beta;Trusted_Connection=True"
                    }
                }
            };

            builder.Services.AddSingleton(tenants);
            builder.Services.AddSingleton<TenantResolutionMetrics>();
            builder.Services.AddSingleton<InMemoryTenantOrderStore>();
            builder.Services.AddScoped<TenantScopedOrderService>();
            builder.Services.AddMultitenancy<SampleTenantResolver>();

            var app = builder.Build();

            // 1. Middleware do framework resolve e anexa TenantContext ao HttpContext.
            app.UseMultitenancy();

            // 2. Barreira de seguranca para /api/*: rejeita explicitamente tenant ausente (400) ou desconhecido (404).
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
                    context.GetTenantContext() == null)
                {
                    string? rawIdentifier = TenantIdentifierExtractor.Extract(context);
                    if (string.IsNullOrWhiteSpace(rawIdentifier))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Error = "Tenant nao informado.",
                            Detail = $"Informe '{TenantConstants.HeaderName}' no header, '?{TenantConstants.QueryName}=' na query ou subdominio valido."
                        });
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Error = "Tenant invalido ou nao cadastrado.",
                        RequestedTenant = rawIdentifier
                    });
                    return;
                }

                await next();
            });

            // 3. Pipeline isolado por tenant (UsePerTenant) injeta metadados do tenant resolvido na resposta.
            app.UseWhen(
                context => context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
                           context.GetTenantContext() != null,
                branch =>
                {
                    branch.UsePerTenant<SampleTenant>((tenantCtx, tenantApp) =>
                    {
                        tenantApp.Use(async (ctx, next) =>
                        {
                            ctx.Response.Headers["X-Resolved-Tenant"] = tenantCtx.Tenant.TenantKey;
                            ctx.Response.Headers["X-Tenant-Database"] = tenantCtx.Tenant.DataBaseType.ToString();
                            await next();
                        });
                    });
                });

            MapEndpoints(app);

            return app;
        }

        private static void MapEndpoints(WebApplication app)
        {
            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

            app.MapGet("/api/tenant-info", (HttpContext context, ITenant<IAppTenant> currentTenant, TenantResolutionMetrics metrics) =>
            {
                var tenant = context.GetTenant<SampleTenant>();
                var tenantContext = context.GetTenantContext();

                return Results.Ok(new TenantInfoResponse(
                    TenantKey: currentTenant.Value.TenantKey,
                    DisplayName: tenant.DisplayName,
                    DataBaseType: currentTenant.Value.DataBaseType.ToString(),
                    ContextId: tenantContext.Id,
                    ResolveCountForTenant: metrics.GetCount(tenant.TenantKey)));
            });

            app.MapGet("/api/pedidos", (TenantScopedOrderService service) =>
                Results.Ok(service.GetOrders()));

            app.MapGet("/api/pedidos/{id:int}", (int id, TenantScopedOrderService service) =>
            {
                var order = service.GetOrderById(id);
                return order is null
                    ? Results.NotFound(new { Message = $"Pedido {id} nao encontrado para o tenant atual." })
                    : Results.Ok(order);
            });

            app.MapPost("/api/pedidos", (CreateOrderRequest request, TenantScopedOrderService service) =>
            {
                if (string.IsNullOrWhiteSpace(request.Descricao) || request.Valor <= 0)
                {
                    return Results.BadRequest(new { Message = "Descricao e Valor > 0 sao obrigatorios." });
                }

                var created = service.AddOrder(request.Descricao, request.Valor);
                return Results.Created($"/api/pedidos/{created.Id}", created);
            });
        }

        private static async Task<int> RunSelfHostedDemoAsync(string[] args, bool isSmokeTest)
        {
            var app = BuildWebApp(args, port: 0, quietLogging: true);

            try
            {
                await app.StartAsync();

                var server = app.Services.GetRequiredService<IServer>();
                var addresses = server.Features.Get<IServerAddressesFeature>();
                string baseUrl = addresses!.Addresses.First();

                if (!isSmokeTest)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("================================================================================");
                    Console.WriteLine(" Softprime.Framework — Sample 07-Multitenancy (Resolucao, Cache e Isolamento)");
                    Console.WriteLine("================================================================================");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"[INFO] Kestrel multitenant ativo em porta efemera: {baseUrl}");
                    Console.WriteLine();
                }

                using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                await ValidatePublicHealthAsync(http, isSmokeTest);
                await ValidateNegativeTenantScenariosAsync(http, isSmokeTest);
                await ValidateResolutionMechanismsAndCacheAsync(http, jsonOpts, isSmokeTest);
                await ValidateStrictTenantIsolationAsync(http, jsonOpts, isSmokeTest);

                if (isSmokeTest)
                {
                    Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.Multitenancy: Resolucao (header/query/subdominio), cache, isolamento e rejeicao de tenant invalido validados.");
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCESSO] Demonstracao Multitenancy concluida com 100% de exito!");
                    Console.ResetColor();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERRO] Falha no Multitenancy Sample: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static async Task ValidatePublicHealthAsync(HttpClient http, bool isSmokeTest)
        {
            var resp = await http.GetAsync("/health");
            EnsureStatus(resp, HttpStatusCode.OK, "GET /health");
            LogStep(isSmokeTest, "1. Endpoint Publico (/health)", "GET /health -> 200 OK (sem necessidade de tenant)");
        }

        private static async Task ValidateNegativeTenantScenariosAsync(HttpClient http, bool isSmokeTest)
        {
            var missingResp = await http.GetAsync("/api/pedidos");
            EnsureStatus(missingResp, HttpStatusCode.BadRequest, "GET /api/pedidos sem tenant");
            LogStep(isSmokeTest, "2. Cenario Negativo: Tenant Ausente",
                "GET /api/pedidos -> 400 BadRequest (rejeitado pela barreira multitenant)");

            using var invalidReq = new HttpRequestMessage(HttpMethod.Get, "/api/pedidos");
            invalidReq.Headers.Add(TenantConstants.HeaderName, "empresa-inexistente");
            var invalidResp = await http.SendAsync(invalidReq);
            EnsureStatus(invalidResp, HttpStatusCode.NotFound, "GET /api/pedidos com tenant invalido");
            LogStep(isSmokeTest, "3. Cenario Negativo: Tenant Invalido",
                "GET /api/pedidos [X-Tenant-Key: empresa-inexistente] -> 404 NotFound (rejeitado)");
        }

        private static async Task ValidateResolutionMechanismsAndCacheAsync(HttpClient http, JsonSerializerOptions jsonOpts, bool isSmokeTest)
        {
            // Resolucao via Header X-Tenant-Key
            using var alphaHeaderReq = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-info");
            alphaHeaderReq.Headers.Add(TenantConstants.HeaderName, TenantConstants.AlphaKey);
            var alphaResp = await http.SendAsync(alphaHeaderReq);
            EnsureStatus(alphaResp, HttpStatusCode.OK, "Resolucao via Header alpha");
            EnsureHeader(alphaResp, "X-Resolved-Tenant", TenantConstants.AlphaKey);

            var alphaInfo = await alphaResp.Content.ReadFromJsonAsync<TenantInfoResponse>(jsonOpts);
            if (alphaInfo == null || alphaInfo.TenantKey != TenantConstants.AlphaKey || alphaInfo.DataBaseType != nameof(DataBaseType.Postgres))
                throw new InvalidOperationException("Tenant alpha nao foi resolvido corretamente via header.");
            LogStep(isSmokeTest, "4. Resolucao via Header (X-Tenant-Key: alpha)",
                $"Tenant='{alphaInfo.DisplayName}' | Banco={alphaInfo.DataBaseType} | ResolveCount={alphaInfo.ResolveCountForTenant}");

            // Resolucao via Query String (?tenant=beta)
            var betaResp = await http.GetAsync($"/api/tenant-info?{TenantConstants.QueryName}={TenantConstants.BetaKey}");
            EnsureStatus(betaResp, HttpStatusCode.OK, "Resolucao via Query beta");
            EnsureHeader(betaResp, "X-Resolved-Tenant", TenantConstants.BetaKey);

            var betaInfo = await betaResp.Content.ReadFromJsonAsync<TenantInfoResponse>(jsonOpts);
            if (betaInfo == null || betaInfo.TenantKey != TenantConstants.BetaKey || betaInfo.DataBaseType != nameof(DataBaseType.MsSql))
                throw new InvalidOperationException("Tenant beta nao foi resolvido corretamente via query string.");
            LogStep(isSmokeTest, "5. Resolucao via Query String (?tenant=beta)",
                $"Tenant='{betaInfo.DisplayName}' | Banco={betaInfo.DataBaseType} | ResolveCount={betaInfo.ResolveCountForTenant}");

            // Resolucao via Subdominio (Host: alpha.softprime.local) comprovando cache hit
            using var subdomainReq = new HttpRequestMessage(HttpMethod.Get, "/api/tenant-info");
            subdomainReq.Headers.Host = "alpha.softprime.local";
            var subResp = await http.SendAsync(subdomainReq);
            EnsureStatus(subResp, HttpStatusCode.OK, "Resolucao via Subdominio alpha");
            var subInfo = await subResp.Content.ReadFromJsonAsync<TenantInfoResponse>(jsonOpts);
            if (subInfo == null || subInfo.ContextId != alphaInfo.ContextId || subInfo.ResolveCountForTenant != 1)
                throw new InvalidOperationException("MemoryCacheTenantResolver deveria reutilizar o TenantContext em cache para alpha.");
            LogStep(isSmokeTest, "6. Resolucao via Subdominio + Cache Hit (Host: alpha.softprime.local)",
                $"ContextId reutilizado ({subInfo.ContextId}) | ResolveCount permaneceu {subInfo.ResolveCountForTenant} (IMemoryCache)");
        }

        private static async Task ValidateStrictTenantIsolationAsync(HttpClient http, JsonSerializerOptions jsonOpts, bool isSmokeTest)
        {
            // Cria pedido exclusivo no Tenant Alpha
            using var createAlphaReq = new HttpRequestMessage(HttpMethod.Post, "/api/pedidos")
            {
                Content = JsonContent.Create(new CreateOrderRequest("Licenca Dedicada Alpha", 3900.00m))
            };
            createAlphaReq.Headers.Add(TenantConstants.HeaderName, TenantConstants.AlphaKey);
            var createResp = await http.SendAsync(createAlphaReq);
            EnsureStatus(createResp, HttpStatusCode.Created, "POST /api/pedidos em alpha");
            var createdAlphaOrder = await createResp.Content.ReadFromJsonAsync<TenantOrder>(jsonOpts);
            if (createdAlphaOrder == null || createdAlphaOrder.TenantKey != TenantConstants.AlphaKey)
                throw new InvalidOperationException("Pedido criado deveria pertencer exclusivamente ao tenant alpha.");

            // Lista pedidos de Alpha e de Beta
            var alphaOrders = await GetOrdersForTenantAsync(http, TenantConstants.AlphaKey, jsonOpts);
            var betaOrders = await GetOrdersForTenantAsync(http, TenantConstants.BetaKey, jsonOpts);

            if (alphaOrders.Any(o => o.TenantKey != TenantConstants.AlphaKey) ||
                !alphaOrders.Any(o => o.Id == createdAlphaOrder.Id))
            {
                throw new InvalidOperationException("Tenant alpha nao enxergou o proprio pedido ou recebeu dados de outro tenant.");
            }

            if (betaOrders.Any(o => o.TenantKey != TenantConstants.BetaKey) ||
                betaOrders.Any(o => o.Id == createdAlphaOrder.Id))
            {
                throw new InvalidOperationException("Vazamento de isolamento detectado: Tenant beta enxergou pedido de alpha.");
            }

            // Tentativa de acesso cruzado por ID de Alpha usando Tenant Beta -> 404 NotFound
            using var crossTenantReq = new HttpRequestMessage(HttpMethod.Get, $"/api/pedidos/{createdAlphaOrder.Id}");
            crossTenantReq.Headers.Add(TenantConstants.HeaderName, TenantConstants.BetaKey);
            var crossResp = await http.SendAsync(crossTenantReq);
            EnsureStatus(crossResp, HttpStatusCode.NotFound, "Acesso cruzado ao pedido de Alpha usando Beta");

            LogStep(isSmokeTest, "7. Isolamento Estrito de Dados (Alpha vs Beta)",
                $"Alpha possui {alphaOrders.Count} pedidos (inclui Id={createdAlphaOrder.Id}); Beta possui {betaOrders.Count} pedidos; acesso cruzado bloqueado (404 NotFound).");
        }

        private static async Task<List<TenantOrder>> GetOrdersForTenantAsync(HttpClient http, string tenantKey, JsonSerializerOptions jsonOpts)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/pedidos");
            req.Headers.Add(TenantConstants.HeaderName, tenantKey);
            var resp = await http.SendAsync(req);
            EnsureStatus(resp, HttpStatusCode.OK, $"GET /api/pedidos para {tenantKey}");
            return await resp.Content.ReadFromJsonAsync<List<TenantOrder>>(jsonOpts) ?? new List<TenantOrder>();
        }

        private static void EnsureStatus(HttpResponseMessage response, HttpStatusCode expected, string operation)
        {
            if (response.StatusCode != expected)
                throw new InvalidOperationException($"{operation}: esperado HTTP {(int)expected}, obtido {(int)response.StatusCode}.");
        }

        private static void EnsureHeader(HttpResponseMessage response, string headerName, string expectedValue)
        {
            if (!response.Headers.TryGetValues(headerName, out var values) ||
                !string.Equals(values.FirstOrDefault(), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Header '{headerName}' esperado '{expectedValue}', obtido '{values?.FirstOrDefault()}'.");
            }
        }

        private static void LogStep(bool isSmokeTest, string title, string detail)
        {
            if (isSmokeTest) return;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"--- {title} ---");
            Console.ResetColor();
            Console.WriteLine($"  {detail}");
        }
    }

    #region Tenant Model, Resolver com Cache e Store Isolado

    public sealed class SampleTenant : IAppTenant, IEquatable<SampleTenant>
    {
        public string TenantKey { get; set; } = string.Empty;
        public DataBaseType DataBaseType { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public bool Equals(SampleTenant? other)
            => other is not null && string.Equals(TenantKey, other.TenantKey, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => Equals(obj as SampleTenant);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(TenantKey);
    }

    public static class TenantIdentifierExtractor
    {
        public static string? Extract(HttpContext context)
        {
            string? headerKey = context.Request.Headers[TenantConstants.HeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerKey))
                return headerKey.Trim().ToLowerInvariant();

            string? queryKey = context.Request.Query[TenantConstants.QueryName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(queryKey))
                return queryKey.Trim().ToLowerInvariant();

            string host = context.Request.Host.Host;
            if (!string.IsNullOrWhiteSpace(host) &&
                !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !IPAddress.TryParse(host, out _))
            {
                string[] parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 || (parts.Length == 2 && parts[1].Equals("localhost", StringComparison.OrdinalIgnoreCase)))
                    return parts[0].Trim().ToLowerInvariant();
            }

            return null;
        }
    }

    public sealed class SampleTenantResolver : MemoryCacheTenantResolver
    {
        private readonly MultitenancyOptions<SampleTenant> _options;
        private readonly TenantResolutionMetrics _metrics;

        public SampleTenantResolver(
            IMemoryCache cache,
            MultitenancyOptions<SampleTenant> options,
            TenantResolutionMetrics metrics)
            : base(cache)
        {
            _options = options;
            _metrics = metrics;
        }

        protected override string GetContextIdentifier(HttpContext context)
            => TenantIdentifierExtractor.Extract(context)!;

        protected override string GetTenantIdentifier(TenantContext context)
            => context.Tenant.TenantKey.ToLowerInvariant();

        protected override Task<TenantContext> ResolveAsync(HttpContext context)
        {
            string? identifier = TenantIdentifierExtractor.Extract(context);
            if (string.IsNullOrWhiteSpace(identifier))
                return Task.FromResult<TenantContext>(null!);

            var tenant = _options.Tenants.FirstOrDefault(
                t => string.Equals(t.TenantKey, identifier, StringComparison.OrdinalIgnoreCase));

            if (tenant == null)
                return Task.FromResult<TenantContext>(null!);

            _metrics.Increment(tenant.TenantKey);
            return Task.FromResult(new TenantContext(tenant));
        }
    }

    public sealed class TenantResolutionMetrics
    {
        private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

        public void Increment(string tenantKey) => _counts.AddOrUpdate(tenantKey, 1, (_, current) => current + 1);

        public int GetCount(string tenantKey) => _counts.TryGetValue(tenantKey, out int count) ? count : 0;
    }

    public sealed class InMemoryTenantOrderStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<TenantOrder>> _ordersByTenant = new(StringComparer.OrdinalIgnoreCase);
        private int _sequence;

        public InMemoryTenantOrderStore()
        {
            Add(TenantConstants.AlphaKey, "Plano Corporativo Alpha", 2400.00m);
            Add(TenantConstants.AlphaKey, "Modulo Fiscal Alpha", 950.00m);
            Add(TenantConstants.BetaKey, "Assinatura Starter Beta", 490.00m);
        }

        public IReadOnlyList<TenantOrder> GetByTenant(string tenantKey)
        {
            if (!_ordersByTenant.TryGetValue(tenantKey, out var bag))
                return Array.Empty<TenantOrder>();

            return bag.OrderBy(o => o.Id).ToList();
        }

        public TenantOrder? GetById(string tenantKey, int id)
            => GetByTenant(tenantKey).FirstOrDefault(o => o.Id == id);

        public TenantOrder Add(string tenantKey, string descricao, decimal valor)
        {
            var order = new TenantOrder(Interlocked.Increment(ref _sequence), tenantKey, descricao, valor);
            var bag = _ordersByTenant.GetOrAdd(tenantKey, _ => new ConcurrentBag<TenantOrder>());
            bag.Add(order);
            return order;
        }
    }

    public sealed class TenantScopedOrderService
    {
        private readonly ITenant<IAppTenant> _currentTenant;
        private readonly InMemoryTenantOrderStore _store;

        public TenantScopedOrderService(ITenant<IAppTenant> currentTenant, InMemoryTenantOrderStore store)
        {
            _currentTenant = currentTenant;
            _store = store;
        }

        private string RequireTenantKey()
            => _currentTenant.Value?.TenantKey
               ?? throw new InvalidOperationException("Nenhum tenant ativo no escopo da requisicao.");

        public IReadOnlyList<TenantOrder> GetOrders() => _store.GetByTenant(RequireTenantKey());

        public TenantOrder? GetOrderById(int id) => _store.GetById(RequireTenantKey(), id);

        public TenantOrder AddOrder(string descricao, decimal valor) => _store.Add(RequireTenantKey(), descricao, valor);
    }

    public sealed record TenantInfoResponse(
        string TenantKey,
        string DisplayName,
        string DataBaseType,
        string ContextId,
        int ResolveCountForTenant);

    public sealed record CreateOrderRequest(string Descricao, decimal Valor);

    public sealed record TenantOrder(int Id, string TenantKey, string Descricao, decimal Valor);

    #endregion
}

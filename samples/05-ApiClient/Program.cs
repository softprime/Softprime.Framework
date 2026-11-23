using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softprime.Framework.Api.Client;
using Softprime.Framework.Api.Dto.Default;

namespace Softprime.Samples.ApiClient
{
    public static class ClientConstants
    {
        public const string ValidApiKey = "softprime-client-key-2026";
        public const string ValidTenantKey = "tenant-alpha";
    }

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
            return await RunDemoAsync(args ?? Array.Empty<string>(), isSmokeTest);
        }

        private static WebApplication BuildMockServer(string[] args, RequestAuditTracker tracker)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            var produtoStore = new InMemoryProdutoStore();
            var clienteStore = new InMemoryClienteStore();

            var app = builder.Build();

            app.Use(async (context, next) =>
            {
                string path = context.Request.Path.Value ?? string.Empty;
                string? apiKey = context.Request.Headers["ApiKey"].FirstOrDefault();
                string? tenantKey = context.Request.Headers["TenantKey"].FirstOrDefault();

                tracker.Record(path, apiKey, tenantKey, context.Request.QueryString.Value);

                if (path.StartsWith("/api/produtos", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(apiKey, ClientConstants.ValidApiKey, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { Message = "Header ApiKey ausente ou invalido." });
                    return;
                }

                if (path.StartsWith("/api/clientes", StringComparison.OrdinalIgnoreCase) &&
                    (!string.Equals(apiKey, ClientConstants.ValidApiKey, StringComparison.Ordinal) ||
                     !string.Equals(tenantKey, ClientConstants.ValidTenantKey, StringComparison.Ordinal)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { Message = "Headers ApiKey e TenantKey sao obrigatorios." });
                    return;
                }

                await next();
            });

            MapProdutoRoutes(app, produtoStore);
            MapClienteRoutes(app, clienteStore);
            MapFailureRoutes(app);

            return app;
        }

        private static void MapProdutoRoutes(WebApplication app, InMemoryProdutoStore store)
        {
            app.MapGet("/api/produtos", (HttpContext context) =>
            {
                if (context.Request.Query.ContainsKey("page"))
                {
                    int.TryParse(context.Request.Query["page"], out int page);
                    if (!int.TryParse(context.Request.Query["size"], out int size)) size = 10;

                    var all = store.GetAll();
                    int totalCount = all.Count;
                    int totalPages = (int)Math.Ceiling(totalCount / (double)size);
                    var items = all.Skip(page * size).Take(size).ToList();

                    return Results.Ok(new PagedResultDto<ProdutoDto>
                    {
                        PageIndex = page,
                        PageSize = size,
                        TotalCount = totalCount,
                        TotalPages = totalPages,
                        Results = items,
                        HasPreviousPage = page > 0,
                        HasNextPage = (page + 1) < totalPages
                    });
                }

                return Results.Ok(store.GetAll());
            });

            app.MapGet("/api/produtos/{id:int}", (int id) =>
            {
                var item = store.GetById(id);
                return item is null
                    ? Results.NotFound(new { Message = $"Produto {id} nao encontrado." })
                    : Results.Ok(item);
            });

            app.MapPost("/api/produtos", (ProdutoDto dto) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Codigo) || string.IsNullOrWhiteSpace(dto.Nome) || dto.Preco <= 0)
                {
                    return Results.BadRequest(new { Message = "Codigo, Nome e Preco > 0 sao obrigatorios." });
                }

                var created = store.Add(dto);
                return Results.Created($"/api/produtos/{created.Id}", created);
            });

            app.MapPut("/api/produtos/{id:int}", (int id, ProdutoDto dto) =>
            {
                var updated = store.Update(id, dto);
                return updated is null
                    ? Results.NotFound(new { Message = $"Produto {id} nao encontrado." })
                    : Results.Ok(updated);
            });

            app.MapDelete("/api/produtos/{id:int}", (int id) =>
                store.Delete(id)
                    ? Results.NoContent()
                    : Results.NotFound(new { Message = $"Produto {id} nao encontrado." }));
        }

        private static void MapClienteRoutes(WebApplication app, InMemoryClienteStore store)
        {
            app.MapGet("/api/clientes", () => Results.Ok(store.GetAll()));

            app.MapGet("/api/clientes/{id:guid}", (Guid id) =>
            {
                var item = store.GetById(id);
                return item is null
                    ? Results.NotFound(new { Message = $"Cliente {id} nao encontrado." })
                    : Results.Ok(item);
            });

            app.MapPost("/api/clientes", (ClienteDto dto) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Nome))
                {
                    return Results.BadRequest(new { Message = "Nome do cliente e obrigatorio." });
                }

                var created = store.Add(dto);
                return Results.Created($"/api/clientes/{created.Id}", created);
            });
        }

        private static void MapFailureRoutes(WebApplication app)
        {
            app.MapGet("/api/falhas/erro-servidor", () =>
                Results.Json(new { Error = "Falha interna simulada no servidor." }, statusCode: StatusCodes.Status500InternalServerError));

            app.MapGet("/api/falhas/lento", async (HttpContext context) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), context.RequestAborted);
                return Results.Ok(new { Message = "Resposta concluida apos delay." });
            });
        }

        private static async Task<int> RunDemoAsync(string[] args, bool isSmokeTest)
        {
            var tracker = new RequestAuditTracker();
            var app = BuildMockServer(args, tracker);

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
                    Console.WriteLine(" Softprime.Framework — Sample 05-ApiClient (Cliente HTTP Tipado e Resiliencia)");
                    Console.WriteLine("================================================================================");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"[INFO] Servidor mock local Kestrel ativo em porta efemera: {baseUrl}");
                    Console.WriteLine();
                }

                await RunApiKeyNegativeScenariosAsync(baseUrl, isSmokeTest);
                await RunProdutoCrudScenarioAsync(baseUrl, isSmokeTest);
                await RunTypedGuidTenantScenarioAsync(baseUrl, isSmokeTest);
                await RunServerErrorAndTimeoutScenariosAsync(baseUrl, isSmokeTest);
                VerifyServerAudit(tracker);

                if (isSmokeTest)
                {
                    Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.ApiClient: CRUD tipado, headers ApiKey/TenantKey, ApiException e timeout validados.");
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCESSO] Demonstracao ApiClient concluida com 100% de exito!");
                    Console.ResetColor();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERRO] Falha no ApiClient Sample: {ex.Message}");
                return 1;
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static async Task RunApiKeyNegativeScenariosAsync(string baseUrl, bool isSmokeTest)
        {
            var clientSemChave = new ApiClient<ProdutoDto, int>("api/produtos", baseUrl);
            var exSemChave = await AssertThrowsAsync<ApiException>(
                () => clientSemChave.Get(),
                "Chamada sem ApiKey deveria lancar ApiException");
            LogStep(isSmokeTest, "1. Seguranca: ApiClient sem ApiKey",
                $"ApiException capturada -> {exSemChave.Message}");

            var clientChaveInvalida = new ApiClient<ProdutoDto, int>("api/produtos", baseUrl, "chave-incorreta");
            var exChaveInvalida = await AssertThrowsAsync<ApiException>(
                () => clientChaveInvalida.Get(),
                "Chamada com ApiKey invalida deveria lancar ApiException");
            LogStep(isSmokeTest, "2. Seguranca: ApiClient com ApiKey invalida",
                $"ApiException capturada -> {exChaveInvalida.Message}");
        }

        private static async Task RunProdutoCrudScenarioAsync(string baseUrl, bool isSmokeTest)
        {
            var client = new ApiClient<ProdutoDto, int>("api/produtos", baseUrl, ClientConstants.ValidApiKey);

            // Verificacao de envio do header ApiKey pelo ApiClientBase
            if (!client.Client.DefaultRequestHeaders.TryGetValues("ApiKey", out var apiKeys) ||
                apiKeys.FirstOrDefault() != ClientConstants.ValidApiKey)
            {
                throw new InvalidOperationException("ApiClientBase nao configurou o header ApiKey esperado.");
            }

            // GET lista completa
            var lista = (await client.Get()).ToList();
            if (lista.Count != 3)
                throw new InvalidOperationException($"Esperava 3 produtos iniciais, recebeu {lista.Count}.");
            LogStep(isSmokeTest, "3. Listagem Tipada (Get())",
                $"Recebidos {lista.Count} produtos: {string.Join(", ", lista.Select(p => p.Codigo))}");

            // GET paginado
            var pagina = await client.Get(page: 0, size: 2);
            if (pagina.TotalCount != 3 || pagina.TotalPages != 2 || !pagina.HasNextPage || pagina.Results.Count() != 2)
                throw new InvalidOperationException("Resultado paginado de ApiClient.Get(page, size) divergente.");
            LogStep(isSmokeTest, "4. Paginacao (Get(0, 2))",
                $"PageIndex={pagina.PageIndex}, TotalCount={pagina.TotalCount}, TotalPages={pagina.TotalPages}, HasNext={pagina.HasNextPage}");

            // Normalizacao automatica de parametros negativos/zero pelo ApiClient
            var paginaNormalizada = await client.Get(page: -5, size: 0);
            if (paginaNormalizada.PageIndex != 0 || paginaNormalizada.PageSize != 1)
                throw new InvalidOperationException("ApiClient deveria normalizar page < 0 para 0 e size < 1 para 1.");

            // POST invalido -> ApiException (400 BadRequest)
            var exBadPost = await AssertThrowsAsync<ApiException>(
                () => client.Post(new ProdutoDto { Codigo = "", Nome = "", Preco = 0m }),
                "POST invalido deveria lancar ApiException");
            LogStep(isSmokeTest, "5. Validacao de Payload (POST invalido)",
                $"ApiException 400 capturada -> {exBadPost.Message}");

            // POST valido
            var criado = await client.Post(new ProdutoDto { Codigo = "CLI-004", Nome = "Headset Bluetooth", Preco = 420.00m });
            if (criado == null || criado.Id <= 0)
                throw new InvalidOperationException("POST valido deveria retornar ProdutoDto com Id atribuido.");
            LogStep(isSmokeTest, "6. Criacao (Post(dto))",
                $"Criado Id={criado.Id} ({criado.Codigo} - {criado.Nome})");

            // GET por Id
            var obtido = await client.Get(criado.Id);
            if (obtido == null || obtido.Codigo != "CLI-004")
                throw new InvalidOperationException("Get(id) nao retornou o produto recem-criado.");
            LogStep(isSmokeTest, "7. Consulta por Id (Get(id))",
                $"Encontrado Id={obtido.Id}: {obtido.Nome} (R$ {obtido.Preco:N2})");

            // PUT atualizacao
            obtido.Nome = "Headset Bluetooth Pro";
            obtido.Preco = 499.90m;
            var atualizado = await client.Put(obtido);
            if (atualizado == null || atualizado.Preco != 499.90m)
                throw new InvalidOperationException("Put(dto) nao persistiu o novo preco.");
            LogStep(isSmokeTest, "8. Atualizacao (Put(dto))",
                $"Atualizado Id={atualizado.Id}: {atualizado.Nome} (R$ {atualizado.Preco:N2})");

            // DELETE + verificacao 404
            await client.Delete(criado.Id);
            var exNotFound = await AssertThrowsAsync<ApiException>(
                () => client.Get(criado.Id),
                "Get(id) apos Delete(id) deveria lancar ApiException 404");
            LogStep(isSmokeTest, "9. Exclusao e Verificacao Pos-Exclusao (Delete(id))",
                $"Excluido Id={criado.Id}; nova consulta lancou ApiException -> {exNotFound.Message}");
        }

        private static async Task RunTypedGuidTenantScenarioAsync(string baseUrl, bool isSmokeTest)
        {
            var typedClient = new ApiClientTyped<ClienteDto, Guid>(
                "api/clientes",
                baseUrl,
                ClientConstants.ValidApiKey,
                ClientConstants.ValidTenantKey);

            if (!typedClient.Client.DefaultRequestHeaders.TryGetValues("TenantKey", out var tenants) ||
                tenants.FirstOrDefault() != ClientConstants.ValidTenantKey)
            {
                throw new InvalidOperationException("ApiClientTyped nao configurou o header TenantKey esperado.");
            }

            // Sem TenantKey deve falhar na rota /api/clientes
            var clientSemTenant = new ApiClientTyped<ClienteDto, Guid>("api/clientes", baseUrl, ClientConstants.ValidApiKey);
            await AssertThrowsAsync<ApiException>(
                () => clientSemTenant.Get(),
                "Rota multi-tenant sem TenantKey deveria lancar ApiException");

            var novoCliente = await typedClient.Post(new ClienteDto
            {
                Nome = "Softprime Tecnologia Ltda",
                Email = "contato@softprime.example"
            });

            if (novoCliente == null || novoCliente.Id == Guid.Empty)
                throw new InvalidOperationException("ApiClientTyped.Post deveria retornar Guid preenchido.");

            var clienteObtido = await typedClient.Get(novoCliente.Id);
            if (clienteObtido == null || clienteObtido.Id != novoCliente.Id)
                throw new InvalidOperationException("ApiClientTyped.Get(Guid) nao retornou o registro esperado.");

            LogStep(isSmokeTest, "10. ApiClientTyped<ClienteDto, Guid> com ApiKey + TenantKey",
                $"Cliente criado e consultado via Guid ({clienteObtido.Id}) com header TenantKey='{ClientConstants.ValidTenantKey}'.");
        }

        private static async Task RunServerErrorAndTimeoutScenariosAsync(string baseUrl, bool isSmokeTest)
        {
            // Erro 500 -> ApiException
            var errorClient = new ApiClient<ProdutoDto, int>("api/falhas/erro-servidor", baseUrl);
            var ex500 = await AssertThrowsAsync<ApiException>(
                () => errorClient.Get(),
                "Endpoint HTTP 500 deveria lancar ApiException");
            LogStep(isSmokeTest, "11. Tratamento de Erro 500 (ApiException)",
                $"ApiException capturada com payload -> {ex500.Message}");

            // Timeout -> TaskCanceledException / OperationCanceledException
            var slowClient = new ApiClient<ProdutoDto, int>("api/falhas/lento", baseUrl);
            slowClient.Client.Timeout = TimeSpan.FromMilliseconds(150);

            var exTimeout = await AssertThrowsAsync<OperationCanceledException>(
                () => slowClient.Get(),
                "Endpoint lento com Client.Timeout=150ms deveria cancelar por timeout");
            LogStep(isSmokeTest, "12. Cancelamento por Timeout (Client.Timeout)",
                $"Excecao {exTimeout.GetType().Name} capturada apos expirar timeout de 150ms.");
        }

        private static void VerifyServerAudit(RequestAuditTracker tracker)
        {
            var entries = tracker.Snapshot();
            bool hasClampedQuery = entries.Any(e => e.Contains("/api/produtos?page=0&size=1", StringComparison.Ordinal));
            bool hasBothHeaders = entries.Any(e =>
                e.Contains($"ApiKey={ClientConstants.ValidApiKey}", StringComparison.Ordinal) &&
                e.Contains($"TenantKey={ClientConstants.ValidTenantKey}", StringComparison.Ordinal));

            if (!hasClampedQuery)
                throw new InvalidOperationException("Servidor nao recebeu a query normalizada ?page=0&size=1.");

            if (!hasBothHeaders)
                throw new InvalidOperationException("Servidor nao recebeu os headers ApiKey e TenantKey na mesma requisicao.");
        }

        private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string failureMessage)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException expected)
            {
                return expected;
            }

            throw new InvalidOperationException(failureMessage);
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

    #region DTOs e Stores em Memória

    public class ProdutoDto : DtoBase<int>
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public decimal Preco { get; set; }
    }

    public class ClienteDto : DtoBase<Guid>
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public sealed class InMemoryProdutoStore
    {
        private readonly ConcurrentDictionary<int, ProdutoDto> _items = new();
        private int _sequence;

        public InMemoryProdutoStore()
        {
            Add(new ProdutoDto { Codigo = "CLI-001", Nome = "Notebook Corporativo", Preco = 5400.00m });
            Add(new ProdutoDto { Codigo = "CLI-002", Nome = "Teclado Mecanico", Preco = 350.00m });
            Add(new ProdutoDto { Codigo = "CLI-003", Nome = "Monitor 27 4K", Preco = 2200.00m });
        }

        public IReadOnlyList<ProdutoDto> GetAll() => _items.Values.OrderBy(x => x.Id).ToList();

        public ProdutoDto? GetById(int id) => _items.TryGetValue(id, out var item) ? item : null;

        public ProdutoDto Add(ProdutoDto dto)
        {
            int newId = Interlocked.Increment(ref _sequence);
            var created = new ProdutoDto
            {
                Id = newId,
                Codigo = dto.Codigo,
                Nome = dto.Nome,
                Preco = dto.Preco
            };
            _items[newId] = created;
            return created;
        }

        public ProdutoDto? Update(int id, ProdutoDto dto)
        {
            if (!_items.ContainsKey(id)) return null;
            var updated = new ProdutoDto
            {
                Id = id,
                Codigo = dto.Codigo,
                Nome = dto.Nome,
                Preco = dto.Preco
            };
            _items[id] = updated;
            return updated;
        }

        public bool Delete(int id) => _items.TryRemove(id, out _);
    }

    public sealed class InMemoryClienteStore
    {
        private readonly ConcurrentDictionary<Guid, ClienteDto> _items = new();

        public IReadOnlyList<ClienteDto> GetAll() => _items.Values.OrderBy(x => x.Nome).ToList();

        public ClienteDto? GetById(Guid id) => _items.TryGetValue(id, out var item) ? item : null;

        public ClienteDto Add(ClienteDto dto)
        {
            var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
            var created = new ClienteDto
            {
                Id = id,
                Nome = dto.Nome,
                Email = dto.Email
            };
            _items[id] = created;
            return created;
        }
    }

    public sealed class RequestAuditTracker
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public void Record(string path, string? apiKey, string? tenantKey, string? query)
            => _entries.Enqueue($"{path}{query} [ApiKey={(apiKey ?? "<none>")}, TenantKey={(tenantKey ?? "<none>")}]");

        public IReadOnlyList<string> Snapshot() => _entries.ToArray();
    }

    #endregion
}

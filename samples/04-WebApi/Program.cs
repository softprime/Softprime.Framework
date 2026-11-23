using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Softprime.Framework.Api;
using Softprime.Framework.Api.Dto.Default;

namespace Softprime.Samples.WebApi
{
    public static class ApiConstants
    {
        public const string HeaderName = "X-Api-Key";
        public const string ValidApiKey = "softprime-sample-key-2026";
    }

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));
            bool isServeMode = args != null && Array.Exists(args, a => a.Equals("--serve", StringComparison.OrdinalIgnoreCase));

            if (isServeMode)
            {
                var app = BuildWebApp(args!, port: 5084, quietLogging: false);
                Console.WriteLine("================================================================================");
                Console.WriteLine(" Softprime.Framework — Sample 04-WebApi (Modo Servidor Interativo)");
                Console.WriteLine("================================================================================");
                Console.WriteLine("API rodando em: http://127.0.0.1:5084");
                Console.WriteLine($"Header obrigatorio para /api/produtos: {ApiConstants.HeaderName}: {ApiConstants.ValidApiKey}");
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

            builder.Services.AddSingleton<InMemoryProdutoStore>();
            builder.Services.AddControllers();

            var app = builder.Build();

            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));
            app.MapControllers();

            return app;
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
                    Console.WriteLine(" Softprime.Framework — Sample 04-WebApi (IApi, DTOs, Validacao e ApiKeyAuth)");
                    Console.WriteLine("================================================================================");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine($"[INFO] Kestrel iniciado localmente em porta efemera: {baseUrl}");
                    Console.WriteLine();
                }

                using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // 1. Health Check (publico)
                var healthResp = await http.GetAsync("/health");
                EnsureStatus(healthResp, HttpStatusCode.OK, "GET /health");
                LogStep(isSmokeTest, "1. Health Check Publico", $"GET /health -> {(int)healthResp.StatusCode} {healthResp.StatusCode}");

                // 2. Cenario Negativo: Chamada sem ApiKey -> 403 Forbidden (ApiKeyAuthorizeFilter)
                var unauthResp = await http.GetAsync("/api/produtos");
                EnsureStatus(unauthResp, HttpStatusCode.Forbidden, "GET /api/produtos sem ApiKey");
                LogStep(isSmokeTest, "2. Seguranca (Sem Header X-Api-Key)", $"GET /api/produtos -> {(int)unauthResp.StatusCode} {unauthResp.StatusCode} (Bloqueado pelo ApiKeyAuthorizeFilter)");

                // 3. Cenario Negativo: Chamada com ApiKey invalida -> 403 Forbidden
                using (var wrongKeyReq = new HttpRequestMessage(HttpMethod.Get, "/api/produtos"))
                {
                    wrongKeyReq.Headers.Add(ApiConstants.HeaderName, "chave-errada");
                    var wrongKeyResp = await http.SendAsync(wrongKeyReq);
                    EnsureStatus(wrongKeyResp, HttpStatusCode.Forbidden, "GET /api/produtos com ApiKey invalida");
                    LogStep(isSmokeTest, "3. Seguranca (Header X-Api-Key invalido)", $"GET /api/produtos -> {(int)wrongKeyResp.StatusCode} {wrongKeyResp.StatusCode}");
                }

                // Adiciona a ApiKey valida nos proximos requests
                http.DefaultRequestHeaders.Add(ApiConstants.HeaderName, ApiConstants.ValidApiKey);

                // 4. GET lista completa
                var listResp = await http.GetAsync("/api/produtos");
                EnsureStatus(listResp, HttpStatusCode.OK, "GET /api/produtos autenticado");
                var produtos = await listResp.Content.ReadFromJsonAsync<List<ProdutoDto>>(jsonOpts);
                if (produtos == null || produtos.Count != 5)
                    throw new InvalidOperationException($"Esperava 5 produtos iniciais, recebeu {produtos?.Count ?? 0}.");
                LogStep(isSmokeTest, "4. Listagem Completa (GET)", $"GET /api/produtos -> 200 OK ({produtos.Count} itens iniciais)");

                // 5. GET paginado (PagedResultDto<ProdutoDto>)
                var pagedResp = await http.GetAsync("/api/produtos/paged?page=0&size=2");
                EnsureStatus(pagedResp, HttpStatusCode.OK, "GET /api/produtos/paged");
                var paged = await pagedResp.Content.ReadFromJsonAsync<PagedResultDto<ProdutoDto>>(jsonOpts);
                if (paged == null || paged.TotalCount != 5 || paged.TotalPages != 3 || !paged.HasNextPage || paged.Results.Count() != 2)
                    throw new InvalidOperationException("Resposta paginada nao corresponde ao contrato IPagedResult esperado.");
                LogStep(isSmokeTest, "5. Paginacao (PagedResultDto)", $"GET /api/produtos/paged?page=0&size=2 -> 200 OK (Page {paged.PageIndex + 1}/{paged.TotalPages}, TotalCount={paged.TotalCount}, HasNext={paged.HasNextPage})");

                // 6. POST invalido -> 400 BadRequest (Data Annotations)
                var invalidDto = new ProdutoDto { Codigo = "", Nome = "A", Preco = 0m };
                var badPostResp = await http.PostAsJsonAsync("/api/produtos", invalidDto);
                EnsureStatus(badPostResp, HttpStatusCode.BadRequest, "POST /api/produtos invalido");
                LogStep(isSmokeTest, "6. Validacao de Entrada (POST invalido)", $"POST /api/produtos -> {(int)badPostResp.StatusCode} {badPostResp.StatusCode} (Erros de ModelState rejeitados)");

                // 7. POST valido -> 201 Created
                var newDto = new ProdutoDto { Codigo = "API-006", Nome = "Dock Station USB-C", Preco = 890.00m };
                var postResp = await http.PostAsJsonAsync("/api/produtos", newDto);
                EnsureStatus(postResp, HttpStatusCode.Created, "POST /api/produtos valido");
                var created = await postResp.Content.ReadFromJsonAsync<ProdutoDto>(jsonOpts);
                if (created == null || created.Id <= 0)
                    throw new InvalidOperationException("POST valido deveria retornar DTO com Id gerado.");
                LogStep(isSmokeTest, "7. Criacao (POST valido)", $"POST /api/produtos -> 201 Created (Id={created.Id}, Codigo='{created.Codigo}')");

                // 8. GET por Id -> 200 OK
                var getByIdResp = await http.GetAsync($"/api/produtos/{created.Id}");
                EnsureStatus(getByIdResp, HttpStatusCode.OK, "GET /api/produtos/{id}");
                LogStep(isSmokeTest, "8. Busca por ID (GET {id})", $"GET /api/produtos/{created.Id} -> 200 OK");

                // 9. PUT atualizacao -> 200 OK
                created.Nome = "Dock Station USB-C Pro";
                created.Preco = 950.00m;
                var putResp = await http.PutAsJsonAsync($"/api/produtos/{created.Id}", created);
                EnsureStatus(putResp, HttpStatusCode.OK, "PUT /api/produtos/{id}");
                var updated = await putResp.Content.ReadFromJsonAsync<ProdutoDto>(jsonOpts);
                if (updated == null || updated.Preco != 950.00m)
                    throw new InvalidOperationException("PUT nao atualizou o preco corretamente.");
                LogStep(isSmokeTest, "9. Atualizacao (PUT)", $"PUT /api/produtos/{created.Id} -> 200 OK (Novo Preco: R$ {updated.Preco:N2})");

                // 10. DELETE -> 204 NoContent
                var delResp = await http.DeleteAsync($"/api/produtos/{created.Id}");
                EnsureStatus(delResp, HttpStatusCode.NoContent, "DELETE /api/produtos/{id}");
                LogStep(isSmokeTest, "10. Exclusao (DELETE)", $"DELETE /api/produtos/{created.Id} -> 204 NoContent");

                // 11. GET apos DELETE -> 404 NotFound
                var notFoundResp = await http.GetAsync($"/api/produtos/{created.Id}");
                EnsureStatus(notFoundResp, HttpStatusCode.NotFound, "GET /api/produtos/{id} apos DELETE");
                LogStep(isSmokeTest, "11. Verificacao Pos-Exclusao", $"GET /api/produtos/{created.Id} -> 404 NotFound");

                if (isSmokeTest)
                {
                    Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.WebApi: Endpoints CRUD, validacao, paginacao e ApiKey 403 validados via HTTP.");
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCESSO] Demonstracao Web API concluida com 100% de exito!");
                    Console.ResetColor();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERRO] Falha no WebApi Sample: {ex.Message}");
                return 1;
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static void EnsureStatus(HttpResponseMessage response, HttpStatusCode expected, string operation)
        {
            if (response.StatusCode != expected)
            {
                throw new InvalidOperationException($"{operation}: esperado HTTP {(int)expected}, obtido {(int)response.StatusCode}.");
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

    #region DTO, Store em Memória e Controller implementando IApi<TDto, TId>

    public class ProdutoDto : DtoBase<int>
    {
        [Required(ErrorMessage = "Codigo e obrigatorio.")]
        [StringLength(20, ErrorMessage = "Codigo deve ter no maximo 20 caracteres.")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome e obrigatorio.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        [Range(0.01, 1000000.00, ErrorMessage = "Preco deve ser maior que zero.")]
        public decimal Preco { get; set; }
    }

    public class InMemoryProdutoStore
    {
        private readonly ConcurrentDictionary<int, ProdutoDto> _items = new();
        private int _sequence;

        public InMemoryProdutoStore()
        {
            Add(new ProdutoDto { Codigo = "API-001", Nome = "Notebook Corporativo", Preco = 5400.00m });
            Add(new ProdutoDto { Codigo = "API-002", Nome = "Mouse Ergonomico", Preco = 180.00m });
            Add(new ProdutoDto { Codigo = "API-003", Nome = "Teclado Mecanico", Preco = 350.00m });
            Add(new ProdutoDto { Codigo = "API-004", Nome = "Monitor 27 4K", Preco = 2200.00m });
            Add(new ProdutoDto { Codigo = "API-005", Nome = "Webcam Full HD", Preco = 290.00m });
        }

        public IReadOnlyList<ProdutoDto> GetAll() => _items.Values.OrderBy(x => x.Id).ToList();

        public ProdutoDto? GetById(int id) => _items.TryGetValue(id, out var item) ? item : null;

        public ProdutoDto Add(ProdutoDto dto)
        {
            int newId = Interlocked.Increment(ref _sequence);
            var copy = new ProdutoDto
            {
                Id = newId,
                Codigo = dto.Codigo,
                Nome = dto.Nome,
                Preco = dto.Preco
            };
            _items[newId] = copy;
            return copy;
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

    [ApiController]
    [Route("api/produtos")]
    [ApiKeyAuthorizeFilter(ApiConstants.HeaderName, ApiConstants.ValidApiKey)]
    public class ProdutosController : ControllerBase, IApi<ProdutoDto, int>
    {
        private readonly InMemoryProdutoStore _store;

        public ProdutosController(InMemoryProdutoStore store)
        {
            _store = store;
        }

        [HttpGet]
        public Task<IActionResult> Get()
        {
            return Task.FromResult<IActionResult>(Ok(_store.GetAll()));
        }

        [HttpGet("paged")]
        public Task<IActionResult> Get([FromQuery] int page = 0, [FromQuery] int size = 10)
        {
            int safePage = page < 0 ? 0 : page;
            int safeSize = size < 1 ? 1 : size;

            var all = _store.GetAll();
            int totalCount = all.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)safeSize);
            var results = all.Skip(safePage * safeSize).Take(safeSize).ToList();

            var dto = new PagedResultDto<ProdutoDto>
            {
                PageIndex = safePage,
                PageSize = safeSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Results = results,
                HasPreviousPage = safePage > 0,
                HasNextPage = (safePage + 1) < totalPages
            };

            return Task.FromResult<IActionResult>(Ok(dto));
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> Get(int id)
        {
            var item = _store.GetById(id);
            if (item == null) return Task.FromResult<IActionResult>(NotFound(new { Message = $"Produto {id} nao encontrado." }));
            return Task.FromResult<IActionResult>(Ok(item));
        }

        [HttpPost]
        public Task<IActionResult> Post([FromBody] ProdutoDto obj)
        {
            var created = _store.Add(obj);
            return Task.FromResult<IActionResult>(CreatedAtAction(nameof(Get), new { id = created.Id }, created));
        }

        [HttpPut("{id:int}")]
        public Task<IActionResult> Put(int id, [FromBody] ProdutoDto obj)
        {
            if (obj.Id != 0 && obj.Id != id)
                return Task.FromResult<IActionResult>(BadRequest(new { Message = "Id da rota diverge do Id do payload." }));

            var updated = _store.Update(id, obj);
            if (updated == null) return Task.FromResult<IActionResult>(NotFound(new { Message = $"Produto {id} nao encontrado." }));
            return Task.FromResult<IActionResult>(Ok(updated));
        }

        [HttpDelete("{id:int}")]
        public Task<IActionResult> Delete(int id)
        {
            if (!_store.Delete(id)) return Task.FromResult<IActionResult>(NotFound(new { Message = $"Produto {id} nao encontrado." }));
            return Task.FromResult<IActionResult>(NoContent());
        }
    }

    #endregion
}

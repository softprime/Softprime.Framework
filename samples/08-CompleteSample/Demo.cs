using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.Api.Client;

namespace Softprime.Samples.CompleteSample;

internal static class Demo
{
    public static async Task RunAsync(string address, IServiceProvider services, bool smoke)
    {
        var empresasAlpha = Client<EmpresaDto>("empresas", Program.Alpha);
        var empresasBeta = Client<EmpresaDto>("empresas", Program.Beta);
        var usuariosAlpha = Client<UsuarioDto>("usuarios", Program.Alpha);
        var produtosAlpha = Client<ProdutoDto>("produtos", Program.Alpha);
        var produtosBeta = Client<ProdutoDto>("produtos", Program.Beta);
        using var httpEmpresasAlpha = empresasAlpha.Client;
        using var httpEmpresasBeta = empresasBeta.Client;
        using var httpUsuariosAlpha = usuariosAlpha.Client;
        using var httpProdutosAlpha = produtosAlpha.Client;
        using var httpProdutosBeta = produtosBeta.Client;

        var alpha = await empresasAlpha.Post(new EmpresaDto { Nome = "Alpha Indústria" });
        var beta = await empresasBeta.Post(new EmpresaDto { Nome = "Beta Comércio" });
        var usuario = await usuariosAlpha.Post(new UsuarioDto
        {
            EmpresaId = alpha.Id, Nome = "Ana Silva", Email = "ana@example.test"
        });
        var produtoAlpha = await produtosAlpha.Post(new ProdutoDto
        {
            EmpresaId = alpha.Id, Nome = "Caderno Alpha", Preco = 20m
        });
        var produtoBeta = await produtosBeta.Post(new ProdutoDto
        {
            EmpresaId = beta.Id, Nome = "Caneta Beta", Preco = 5m
        });

        Require(alpha.Id > 0 && beta.Id > 0 && alpha.Id != beta.Id && usuario.Id > 0 &&
            produtoAlpha.Id > 0 && produtoBeta.Id > 0 && produtoAlpha.Id != produtoBeta.Id,
            "Os cadastros devem retornar os IDs gerados no SQLite.");
        Require((await empresasAlpha.Get(alpha.Id)).Nome == alpha.Nome &&
            (await empresasBeta.Get(beta.Id)).Nome == beta.Nome &&
            (await usuariosAlpha.Get(usuario.Id)).EmpresaId == alpha.Id,
            "Os cadastros devem ficar disponíveis no tenant correto.");

        produtoAlpha.Nome = "Caderno Alpha revisado";
        produtoAlpha.Preco = 25m;
        var atualizado = await produtosAlpha.Put(produtoAlpha);
        var lido = await produtosAlpha.Get(produtoAlpha.Id);
        Require(atualizado.Id == produtoAlpha.Id && lido.Nome == produtoAlpha.Nome && lido.Preco == produtoAlpha.Preco,
            "O tenant deve conseguir consultar e atualizar seu próprio produto.");

        var rascunho = await produtosBeta.Post(new ProdutoDto
        {
            EmpresaId = beta.Id, Nome = "Rascunho Beta", Preco = 1m
        });
        await produtosBeta.Delete(rascunho.Id);
        await ExpectStatusAsync(httpProdutosBeta.GetAsync($"api/produtos/{rascunho.Id}"),
            HttpStatusCode.NotFound, "Exclusão de produto do próprio tenant");

        var paginaBeta = await produtosBeta.Get(page: 0, size: 10);
        Require((await produtosAlpha.Get()).Select(item => item.Id).SequenceEqual([produtoAlpha.Id]) &&
            paginaBeta.TotalCount == 1 &&
            paginaBeta.Results.Select(item => item.Id).SequenceEqual([produtoBeta.Id]),
            "A listagem e a paginação devem retornar apenas os produtos do tenant atual.");

        var mensagens = services.GetRequiredService<FakeMailerDispatcher>();
        Log("Empresas, usuário e produtos cadastrados via HTTP e gravados no SQLite.");

        await ExpectStatusAsync(httpProdutosAlpha.PostAsJsonAsync("api/produtos", new ProdutoDto
        {
            EmpresaId = alpha.Id, Nome = "", Preco = 0m
        }), HttpStatusCode.BadRequest, "Validação de produto");
        await ExpectStatusAsync(httpUsuariosAlpha.PostAsJsonAsync("api/usuarios", new UsuarioDto
        {
            EmpresaId = alpha.Id, Nome = "Outro usuário", Email = "email-invalido"
        }), HttpStatusCode.BadRequest, "Validação de usuário");

        using var httpTenantInvalido = Client<ProdutoDto>("produtos", "desconhecido").Client;
        using (var resposta = await httpTenantInvalido.GetAsync("api/produtos"))
        {
            Require(resposta.StatusCode == HttpStatusCode.NotFound &&
                (await resposta.Content.ReadFromJsonAsync<Dictionary<string, string>>())?
                .GetValueOrDefault("erro") == "tenant_invalido", "Tenant desconhecido deve ser rejeitado.");
        }

        var clienteSemChave = new ApiClientTyped<ProdutoDto, long>("api/produtos", address, "incorreta", Program.Alpha);
        using var httpSemChave = clienteSemChave.Client;
        await ExpectStatusAsync(httpSemChave.GetAsync("api/produtos"), HttpStatusCode.Forbidden, "Chave incorreta");
        try
        {
            await clienteSemChave.Get();
            throw new InvalidOperationException("ApiClient deveria lançar ApiException para HTTP 403.");
        }
        catch (ApiException)
        {
            Log("Entradas inválidas, tenant desconhecido e erro HTTP tratado pelo ApiClient.");
        }

        await ExpectStatusAsync(httpEmpresasBeta.GetAsync($"api/empresas/{alpha.Id}"),
            HttpStatusCode.NotFound, "Leitura cruzada de empresa");
        await ExpectStatusAsync(httpProdutosBeta.GetAsync($"api/usuarios/{usuario.Id}"),
            HttpStatusCode.NotFound, "Leitura cruzada de usuário");
        await ExpectStatusAsync(httpProdutosBeta.GetAsync($"api/produtos/{produtoAlpha.Id}"),
            HttpStatusCode.NotFound, "Leitura cruzada de produto");
        await ExpectStatusAsync(httpProdutosBeta.PostAsJsonAsync("api/produtos", new ProdutoDto
        {
            EmpresaId = alpha.Id, Nome = "Produto no tenant errado", Preco = 1m
        }), HttpStatusCode.NotFound, "Vínculo cruzado de produto");
        await ExpectStatusAsync(httpProdutosBeta.PostAsJsonAsync("api/usuarios", new UsuarioDto
        {
            EmpresaId = alpha.Id, Nome = "Usuário no tenant errado", Email = "outro@example.test"
        }), HttpStatusCode.NotFound, "Vínculo cruzado de usuário");
        await ExpectStatusAsync(httpProdutosBeta.PutAsJsonAsync($"api/produtos/{produtoAlpha.Id}", new ProdutoDto
        {
            EmpresaId = beta.Id, Nome = "Edição no tenant errado", Preco = 1m
        }), HttpStatusCode.NotFound, "Atualização cruzada");
        await ExpectStatusAsync(httpProdutosBeta.DeleteAsync($"api/produtos/{produtoAlpha.Id}"),
            HttpStatusCode.NotFound, "Exclusão cruzada");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var empresas = await db.Set<Empresa>().OrderBy(item => item.Id).ToListAsync();
        var usuarios = await db.Set<Usuario>().AsQueryable().ToListAsync();
        var produtos = await db.Set<Produto>().OrderBy(item => item.Id).ToListAsync();
        Require(empresas.Select(item => (item.Id, item.TenantKey))
                .SequenceEqual([(alpha.Id, Program.Alpha), (beta.Id, Program.Beta)]) &&
            usuarios.Select(item => (item.Id, item.EmpresaId, item.TenantKey))
                .SequenceEqual([(usuario.Id, alpha.Id, Program.Alpha)]) &&
            produtos.Select(item => (item.Id, item.EmpresaId, item.TenantKey, item.Nome, item.Preco))
                .SequenceEqual([
                    (produtoAlpha.Id, alpha.Id, Program.Alpha, produtoAlpha.Nome, produtoAlpha.Preco),
                    (produtoBeta.Id, beta.Id, Program.Beta, produtoBeta.Nome, produtoBeta.Preco)
                ]), "Os dados originais devem estar intactos no SQLite após as operações rejeitadas.");

        var enviados = mensagens.Listar();
        Require(enviados.Length == 1 && enviados[0].Destinatario == usuario.Email &&
            enviados[0].Html.Contains(usuario.Nome, StringComparison.Ordinal) &&
            WebUtility.HtmlDecode(enviados[0].Html).Contains(alpha.Nome, StringComparison.Ordinal),
            "Somente o usuário válido deve gerar um e-mail de boas-vindas com o nome da empresa.");

        Log("Leitura e escrita cruzadas bloqueadas. SQLite e e-mail simulado conferidos.");

        ApiClientTyped<TDto, long> Client<TDto>(string resource, string tenant)
            where TDto : Softprime.Framework.Api.Dto.IDto<long>
            => new($"api/{resource}", address, Program.ApiKey, tenant);

        void Log(string message)
        {
            if (!smoke)
                Console.WriteLine(message);
        }
    }

    private static async Task ExpectStatusAsync(Task<HttpResponseMessage> request, HttpStatusCode status, string scenario)
    {
        using var response = await request;
        Require(response.StatusCode == status,
            $"{scenario}: esperado HTTP {(int)status}, recebido HTTP {(int)response.StatusCode}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

# Sample 04-WebApi — Endpoints REST, Contrato `IApi`, DTOs e `ApiKeyAuthorizeFilter`

> Exemplo didático executável demonstrando a exposição de endpoints REST padronizados usando os contratos de API e transporte do framework (`Softprime.Framework.Api` e `Softprime.Framework.Api.Dto`) sobre ASP.NET Core moderno com servidor Kestrel autossuficiente e sem dependências externas.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como construir e proteger Web APIs seguindo os contratos do `Softprime.Framework`:
- **Contrato Padronizado de Controller (`IApi<TDto, TId>`):** Implementação integral das operações CRUD (`Get()`, `Get(page, size)`, `Get(id)`, `Post(dto)`, `Put(id, dto)`, `Delete(id)`) retornando `Task<IActionResult>`.
- **DTOs e Paginação (`DtoBase<TId>` e `PagedResultDto<TDto>`):** Estruturação de modelos de transferência com anotações de validação (`[Required]`, `[StringLength]`, `[Range]`) e envelopamento de paginação (`TotalCount`, `TotalPages`, `HasNextPage`, `HasPreviousPage`).
- **Autenticação por API Key (`ApiKeyAuthorizeFilter`):** Proteção declarativa de controllers via atributo de filtro verificando o cabeçalho HTTP customizado (`X-Api-Key`) e bloqueando requisições não autorizadas com `403 Forbidden`.
- **Respostas HTTP e Tratamento de Erros Semânticos:** Retornos corretos para `200 OK`, `201 Created`, `204 NoContent`, `400 BadRequest` (validação automática de `ModelState`), `403 Forbidden` (API Key ausente ou inválida) e `404 NotFound`.
- **Autonomia Total com Kestrel Efêmero:** Ao ser executado sem argumentos ou com `--smoke-test`, o sample sobe uma instância real do Kestrel em porta aleatória (`127.0.0.1:0`), dispara requisições HTTP reais com `HttpClient`, valida todos os status codes/JSON e encerra o servidor automaticamente. Também oferece modo `--serve` para exploração interativa manual na porta `5084`.

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma porta fixa ocupada (no modo padrão/smoke test usa porta efêmera `0`), nenhum banco de dados externo e nenhuma credencial secreta são necessários.

---

## 3. Comandos para rodar

### Demonstração automatizada completa (sobe Kestrel efêmero, testa todos os cenários e encerra):

**No Windows (PowerShell):**
```powershell
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0
```

**No Linux / macOS (Bash):**
```bash
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0
```

### Modo servidor interativo (mantém a API escutando em `http://127.0.0.1:5084` para chamadas via `curl`/Postman):
```powershell
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0 -- --serve
```

---

## 4. Fluxo de execução passo a passo

1. **Bootstrap do Host Kestrel:** Inicialização do `WebApplication` com `AddControllers()`, registro de `InMemoryProdutoStore` (singleton thread-safe com 5 produtos iniciais) e mapeamento do endpoint público `/health` e de `ProdutosController`.
2. **Health Check Público:** Chamada `GET /health` sem cabeçalho de autenticação retornando `200 OK`.
3. **Cenários Negativos de Segurança (`ApiKeyAuthorizeFilter`):**
   - `GET /api/produtos` sem cabeçalho `X-Api-Key` → bloqueado com `403 Forbidden`.
   - `GET /api/produtos` com `X-Api-Key` incorreta → bloqueado com `403 Forbidden`.
4. **Leitura e Paginação Autenticadas:**
   - `GET /api/produtos` com `X-Api-Key` válida → retorna os 5 produtos iniciais (`200 OK`).
   - `GET /api/produtos/paged?page=0&size=2` → retorna `PagedResultDto<ProdutoDto>` com metadados (`TotalCount=5`, `TotalPages=3`, `HasNextPage=true`).
5. **Validação de Payload (`[ApiController]` + Data Annotations):**
   - `POST /api/produtos` com campos inválidos (`Codigo=""`, `Preco=0`) → rejeitado automaticamente com `400 BadRequest`.
6. **Ciclo Completo de Escrita e Exclusão:**
   - `POST /api/produtos` válido → `201 Created` retornando o DTO com `Id=6`.
   - `GET /api/produtos/6` → `200 OK`.
   - `PUT /api/produtos/6` → `200 OK` com dados atualizados.
   - `DELETE /api/produtos/6` → `204 NoContent`.
   - `GET /api/produtos/6` após remoção → `404 NotFound`.
7. **Encerramento Limpo:** Desligamento gracioso do Kestrel (`StopAsync()` + `DisposeAsync()`).

---

## 5. Saída esperada no console

Ao executar o comando padrão de demonstração, você verá a seguinte saída no terminal:

```text
================================================================================
 Softprime.Framework — Sample 04-WebApi (IApi, DTOs, Validacao e ApiKeyAuth)
================================================================================

[INFO] Kestrel iniciado localmente em porta efemera: http://127.0.0.1:51684

--- 1. Health Check Publico ---
  GET /health -> 200 OK
--- 2. Seguranca (Sem Header X-Api-Key) ---
  GET /api/produtos -> 403 Forbidden (Bloqueado pelo ApiKeyAuthorizeFilter)
--- 3. Seguranca (Header X-Api-Key invalido) ---
  GET /api/produtos -> 403 Forbidden
--- 4. Listagem Completa (GET) ---
  GET /api/produtos -> 200 OK (5 itens iniciais)
--- 5. Paginacao (PagedResultDto) ---
  GET /api/produtos/paged?page=0&size=2 -> 200 OK (Page 1/3, TotalCount=5, HasNext=True)
--- 6. Validacao de Entrada (POST invalido) ---
  POST /api/produtos -> 400 BadRequest (Erros de ModelState rejeitados)
--- 7. Criacao (POST valido) ---
  POST /api/produtos -> 201 Created (Id=6, Codigo='API-006')
--- 8. Busca por ID (GET {id}) ---
  GET /api/produtos/6 -> 200 OK
--- 9. Atualizacao (PUT) ---
  PUT /api/produtos/6 -> 200 OK (Novo Preco: R$ 950,00)
--- 10. Exclusao (DELETE) ---
  DELETE /api/produtos/6 -> 204 NoContent
--- 11. Verificacao Pos-Exclusao ---
  GET /api/produtos/6 -> 404 NotFound

[SUCESSO] Demonstracao Web API concluida com 100% de exito!
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou verificação automatizada local, o sample suporta o argumento `--smoke-test`. Todos os 11 cenários HTTP reais são executados em memória contra o Kestrel local, retornando código de saída `0` para sucesso ou `1` em caso de erro:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0 -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/04-WebApi/Softprime.Samples.WebApi.csproj --framework net10.0 -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.WebApi: Endpoints CRUD, validacao, paginacao e ApiKey 403 validados via HTTP.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado localmente. | Execute especificando o target .NET 8 com `--framework net8.0`. |
| `403 Forbidden` ao chamar `/api/produtos` no modo `--serve` | Faltou enviar o cabeçalho de autenticação exigido por `ApiKeyAuthorizeFilter`. | Envie o header `X-Api-Key: softprime-sample-key-2026` na requisição. |
| Porta `5084` ocupada no modo `--serve` | Outro processo local está escutando na porta `5084`. | Use o modo padrão (sem `--serve`) que aloca automaticamente uma porta efêmera livre (`0`), ou encerre o processo conflitante. |
| O projeto gerou `.nupkg` indesejado | O arquivo `samples/Directory.Build.props` foi removido ou ignorado. | Restaure o arquivo `samples/Directory.Build.props` com `<IsPackable>false</IsPackable>`. |

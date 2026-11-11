# Sample 07-Multitenancy — Resolução de Tenant, `MemoryCacheTenantResolver`, Isolamento e Cenários Negativos

> Exemplo didático executável demonstrando o pipeline multitenant do framework (`Softprime.Framework.Multitenancy`) com resolução por header, query string e subdomínio, cache em memória, isolamento de dados e rejeição explícita de tenant ausente ou inválido sobre Kestrel autossuficiente.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como configurar e validar aplicações ASP.NET Core multitenant usando os componentes de `Softprime.Framework.Multitenancy`:
- **Contrato de Tenant (`IAppTenant` e `MultitenancyOptions<TTenant>`):** Modelagem dos tenants configurados (`alpha` com `DataBaseType.Postgres` e `beta` com `DataBaseType.MsSql`).
- **Resolvedor com Cache (`MemoryCacheTenantResolver`):** Implementação de `SampleTenantResolver` extraindo a chave na precedência `Header (X-Tenant-Key)` → `Query (?tenant=)` → `Subdomínio (Host)` e mantendo coerência entre `GetContextIdentifier` e `GetTenantIdentifier` para hits reais no `IMemoryCache`.
- **Injeção de Dependência e Middlewares (`AddMultitenancy` / `UseMultitenancy` / `UsePerTenant`):** Consumo do tenant ativo via `ITenant<IAppTenant>` e extensão de pipeline por tenant injetando cabeçalhos `X-Resolved-Tenant` e `X-Tenant-Database`.
- **Isolamento de Dados:** Garantia de que pedidos criados no Tenant `alpha` jamais vazam para listagens ou consultas por ID do Tenant `beta`.
- **Cenários Negativos Obrigatórios:** Rejeição de requisições para `/api/*` sem tenant (`400 BadRequest`) ou com tenant desconhecido (`404 NotFound`), mantendo `/health` público.

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma porta fixa ocupada no modo padrão/smoke test (`127.0.0.1:0`), nenhum banco externo e nenhuma credencial secreta são necessários.

---

## 3. Comandos para rodar

### Demonstração automatizada completa (sobe Kestrel efêmero, valida todos os cenários e encerra):

**No Windows (PowerShell):**
```powershell
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0
```

**No Linux / macOS (Bash):**
```bash
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0
```

*(Para executar sobre .NET 8, substitua `--framework net10.0` por `--framework net8.0`.)*

### Modo servidor interativo (mantém a API escutando em `http://127.0.0.1:5087`):
```powershell
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0 -- --serve
```

---

## 4. Fluxo de execução passo a passo

1. **Bootstrap Multitenant:** Registro de `MultitenancyOptions<SampleTenant>`, `AddMultitenancy<SampleTenantResolver>()`, `UseMultitenancy()`, middleware de guarda para `/api/*` e `UsePerTenant<SampleTenant>()`.
2. **Health Check Público:** `GET /health` sem cabeçalho ou parâmetro de tenant retorna `200 OK`.
3. **Cenários Negativos de Tenant:**
   - `GET /api/pedidos` sem identificador de tenant → rejeitado com `400 BadRequest`.
   - `GET /api/pedidos` com `X-Tenant-Key: empresa-inexistente` → rejeitado com `404 NotFound`.
4. **Resolução Multi-Estratégia e Cache:**
   - `GET /api/tenant-info` com header `X-Tenant-Key: alpha` → resolve `Alpha Industria S.A.` (`Postgres`), incrementando `ResolveCount=1`.
   - `GET /api/tenant-info?tenant=beta` → resolve `Beta Comercio Ltda` (`MsSql`).
   - `GET /api/tenant-info` com `Host: alpha.softprime.local` → resolve `alpha` diretamente do `IMemoryCache` preservando o mesmo `ContextId` e mantendo `ResolveCount=1`.
5. **Isolamento Estrito Entre Tenants:**
   - `POST /api/pedidos` no tenant `alpha` cria novo pedido.
   - `GET /api/pedidos` em `alpha` lista apenas pedidos de `alpha`; `GET /api/pedidos` em `beta` lista apenas pedidos de `beta`.
   - `GET /api/pedidos/{idDeAlpha}` usando `X-Tenant-Key: beta` retorna `404 NotFound`.
6. **Encerramento Limpo:** Desligamento gracioso do Kestrel (`StopAsync()` + `DisposeAsync()`).

---

## 5. Saída esperada no console

Ao executar o comando padrão de demonstração, você verá uma saída semelhante a esta no terminal:

```text
================================================================================
 Softprime.Framework — Sample 07-Multitenancy (Resolucao, Cache e Isolamento)
================================================================================

[INFO] Kestrel multitenant ativo em porta efemera: http://127.0.0.1:50087

--- 1. Endpoint Publico (/health) ---
  GET /health -> 200 OK (sem necessidade de tenant)
--- 2. Cenario Negativo: Tenant Ausente ---
  GET /api/pedidos -> 400 BadRequest (rejeitado pela barreira multitenant)
--- 3. Cenario Negativo: Tenant Invalido ---
  GET /api/pedidos [X-Tenant-Key: empresa-inexistente] -> 404 NotFound (rejeitado)
--- 4. Resolucao via Header (X-Tenant-Key: alpha) ---
  Tenant='Alpha Industria S.A.' | Banco=Postgres | ResolveCount=1
--- 5. Resolucao via Query String (?tenant=beta) ---
  Tenant='Beta Comercio Ltda' | Banco=MsSql | ResolveCount=1
--- 6. Resolucao via Subdominio + Cache Hit (Host: alpha.softprime.local) ---
  ContextId reutilizado (e24ad131-7216-47b9-b6fd-366074054e9b) | ResolveCount permaneceu 1 (IMemoryCache)
--- 7. Isolamento Estrito de Dados (Alpha vs Beta) ---
  Alpha possui 3 pedidos (inclui Id=4); Beta possui 1 pedidos; acesso cruzado bloqueado (404 NotFound).

[SUCESSO] Demonstracao Multitenancy concluida com 100% de exito!
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou verificação automatizada local, o sample suporta `--smoke-test`, retornando código de saída `0` para sucesso ou `1` em caso de falha:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0 -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj --framework net10.0 -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.Multitenancy: Resolucao (header/query/subdominio), cache, isolamento e rejeicao de tenant invalido validados.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado localmente. | Execute especificando o target .NET 8 com `--framework net8.0`. |
| `400 BadRequest` ao chamar `/api/pedidos` no modo `--serve` | Nenhum tenant foi informado na requisição. | Envie o header `X-Tenant-Key: alpha`, a query `?tenant=beta` ou acesse via `http://alpha.localhost:5087/api/pedidos`. |
| Cache miss inesperado em `MemoryCacheTenantResolver` próprio | `GetContextIdentifier(HttpContext)` retornou chave diferente de `GetTenantIdentifier(TenantContext)`. | Normalize ambas as chaves exatamente com o mesmo formato (ex.: `ToLowerInvariant()`). |
| Porta `5087` ocupada no modo `--serve` | Outro processo local está escutando na porta `5087`. | Use o modo padrão (sem `--serve`) que aloca uma porta efêmera livre (`0`), ou encerre o processo conflitante. |

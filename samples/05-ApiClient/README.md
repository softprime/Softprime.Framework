# Sample 05-ApiClient — Cliente HTTP Tipado, Paginação, `ApiException` e Timeout

> Exemplo didático executável demonstrando o consumo de Web APIs REST com os clientes genéricos do framework (`Softprime.Framework.Api.Client` e `Softprime.Framework.Api.Dto`) sobre um servidor local Kestrel em porta efêmera, sem dependências externas.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como consumir endpoints REST usando as abstrações de cliente HTTP do `Softprime.Framework`:
- **Clientes Tipados (`ApiClient<T, TId>` e `ApiClientTyped<T, TId>`):** Configuração de `uriService`, `baseUrl` e envio automático dos headers `ApiKey` e `TenantKey` pela classe base [`ApiClientBase`](../../src/api/Softprime.Framework.Api.Client/ApiClientBase.cs).
- **Operações CRUD Assíncronas:** Execução completa de `Get()`, `Get(page, size)` (retornando `IPagedResult<T>`), `Get(id)`, `Post(obj)`, `Put(obj)` e `Delete(id)`.
- **Normalização de Paginação:** Comprovação de que `ApiClient<T, TId>.Get(page, size)` normaliza valores inválidos (`page < 0` para `0`, `size < 1` para `1`).
- **Tratamento de Erros HTTP (`ApiException`):** Captura de `ApiException` com o payload de erro retornado pelo servidor para respostas `400 BadRequest`, `403 Forbidden`, `404 NotFound` e `500 InternalServerError`.
- **Cancelamento e Timeout (`Client.Timeout`):** Ajuste do `HttpClient` exposto em `client.Client.Timeout` e captura de `TaskCanceledException` contra um endpoint propositalmente lento.
- **Execução 100% Autônoma:** Como `ApiClientBase` instancia seu próprio `HttpClient` interno, o sample sobe um servidor Minimal API local em `127.0.0.1:0` durante a execução e o encerra ao final.

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma porta fixa ocupada (usa porta efêmera `0`), nenhuma API externa ou credencial secreta são necessárias.

---

## 3. Comandos para rodar

### No Windows (PowerShell):
```powershell
dotnet run --project samples/05-ApiClient/Softprime.Samples.ApiClient.csproj --framework net10.0
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/05-ApiClient/Softprime.Samples.ApiClient.csproj --framework net10.0
```

*(Para executar sobre .NET 8, substitua `--framework net10.0` por `--framework net8.0`.)*

---

## 4. Fluxo de execução passo a passo

1. **Subida do Mock Local:** Inicialização de um servidor Minimal API/Kestrel em `127.0.0.1:0` com middleware de validação/auditoria de `ApiKey` e `TenantKey`.
2. **Cenários Negativos de Autenticação (`403 Forbidden`):**
   - Instanciação sem `apiKey` e com `apiKey` inválida, validando que `Get()` lança `ApiException`.
3. **Ciclo CRUD Completo com `ApiClient<ProdutoDto, int>`:**
   - `Get()` retorna a lista inicial de 3 produtos.
   - `Get(0, 2)` retorna `IPagedResult<ProdutoDto>` com `TotalCount=3`, `TotalPages=2` e `HasNextPage=true`.
   - `Get(-5, 0)` comprova a normalização de query para `?page=0&size=1`.
   - `Post` inválido (`400`), `Post` válido, `Get(id)`, `Put(obj)`, `Delete(id)` e nova consulta `Get(id)` lançando `ApiException` (`404`).
4. **Cliente Tipado `Guid` + Multi-Tenant (`ApiClientTyped<ClienteDto, Guid>`):**
   - Validação de bloqueio quando o header `TenantKey` não é enviado.
   - Envio simultâneo de `ApiKey` e `TenantKey`, criação via `Post` e consulta por `Guid`.
5. **Resiliência (Erro `500` e Timeout):**
   - Chamada ao endpoint `/api/falhas/erro-servidor` disparando `ApiException` com corpo JSON do erro.
   - Chamada ao endpoint `/api/falhas/lento` com `Client.Timeout = 150ms` capturando `TaskCanceledException`.
6. **Auditoria e Encerramento:** Verificação no tracker do servidor de que os headers e query strings esperados foram recebidos, seguida de `StopAsync()` e `DisposeAsync()`.

---

## 5. Saída esperada no console

Ao executar o comando de demonstração, você verá uma saída semelhante a esta no terminal:

```text
================================================================================
 Softprime.Framework — Sample 05-ApiClient (Cliente HTTP Tipado e Resiliencia)
================================================================================

[INFO] Servidor mock local Kestrel ativo em porta efemera: http://127.0.0.1:64716

--- 1. Seguranca: ApiClient sem ApiKey ---
  ApiException capturada -> {"message":"Header ApiKey ausente ou invalido."}
--- 2. Seguranca: ApiClient com ApiKey invalida ---
  ApiException capturada -> {"message":"Header ApiKey ausente ou invalido."}
--- 3. Listagem Tipada (Get()) ---
  Recebidos 3 produtos: CLI-001, CLI-002, CLI-003
--- 4. Paginacao (Get(0, 2)) ---
  PageIndex=0, TotalCount=3, TotalPages=2, HasNext=True
--- 5. Validacao de Payload (POST invalido) ---
  ApiException 400 capturada -> {"message":"Codigo, Nome e Preco > 0 sao obrigatorios."}
--- 6. Criacao (Post(dto)) ---
  Criado Id=4 (CLI-004 - Headset Bluetooth)
--- 7. Consulta por Id (Get(id)) ---
  Encontrado Id=4: Headset Bluetooth (R$ 420,00)
--- 8. Atualizacao (Put(dto)) ---
  Atualizado Id=4: Headset Bluetooth Pro (R$ 499,90)
--- 9. Exclusao e Verificacao Pos-Exclusao (Delete(id)) ---
  Excluido Id=4; nova consulta lancou ApiException -> {"message":"Produto 4 nao encontrado."}
--- 10. ApiClientTyped<ClienteDto, Guid> com ApiKey + TenantKey ---
  Cliente criado e consultado via Guid (17c825d7-352e-40ec-ab02-9663de337181) com header TenantKey='tenant-alpha'.
--- 11. Tratamento de Erro 500 (ApiException) ---
  ApiException capturada com payload -> {"error":"Falha interna simulada no servidor."}
--- 12. Cancelamento por Timeout (Client.Timeout) ---
  Excecao TaskCanceledException capturada apos expirar timeout de 150ms.

[SUCESSO] Demonstracao ApiClient concluida com 100% de exito!
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou verificação automatizada local, o sample suporta `--smoke-test`, retornando código de saída `0` para sucesso ou `1` em caso de falha:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/05-ApiClient/Softprime.Samples.ApiClient.csproj --framework net10.0 -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/05-ApiClient/Softprime.Samples.ApiClient.csproj --framework net10.0 -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.ApiClient: CRUD tipado, headers ApiKey/TenantKey, ApiException e timeout validados.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado localmente. | Execute especificando o target .NET 8 com `--framework net8.0`. |
| `Warning NU1903` sobre `Newtonsoft.Json 12.0.3` | Dependência transitiva atual de `Softprime.Framework.Api.Client` antes da modernização `#6`. | Aviso esperado enquanto `src/` permanece intacto na trilha `#19`; não impede build nem execução. |
| `ApiException` ao consumir API própria que exige outro nome de header | `ApiClientBase` envia os headers fixos `ApiKey` e `TenantKey`. | Alinhe o filtro/middleware da API ou ajuste `client.Client.DefaultRequestHeaders` antes das chamadas. |
| O projeto gerou `.nupkg` indesejado | O arquivo `samples/Directory.Build.props` foi removido ou ignorado. | Restaure `samples/Directory.Build.props` com `<IsPackable>false</IsPackable>`. |

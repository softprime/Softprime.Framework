# Plano de Modernização ASP.NET Core e Multitenancy — Softprime.Framework

> Documento de referência técnica e arquitetural · **Entregável da Issue [#4]** (Fase 2 — Modernizar seletivamente).  
> Baseado nos achados do [Inventário de Consumidores e Dependências (`docs/package-inventory.md`)](package-inventory.md) ([#10]), da [Estratégia de Baseline de APIs Públicas (`docs/api-baseline-strategy.md`)](api-baseline-strategy.md) ([#11]), da [Matriz de Compatibilidade e Estratégia de Targets (`docs/target-compatibility-matrix.md`)](target-compatibility-matrix.md) ([#3]), da [Política de Versionamento e Publicação NuGet (`docs/nuget-versioning-policy.md`)](nuget-versioning-policy.md) ([#12]), da [Estratégia de Testes (`docs/test-strategy.md`)](test-strategy.md) ([#7]) e do [Planejamento de CI (`docs/ci-pipeline-spec.md`)](ci-pipeline-spec.md) ([#8]).  
> Data: 2026-11-05 · Branch: `issue/4-atualizar-aspnet-core-mvc`

---

## 1. Contexto e Diagnóstico Atual (Fatos Verificados)

A camada web e de hosting do `Softprime.Framework` é composta atualmente por duas bibliotecas de produção:
1. `Softprime.Framework.Api`: Contratos e utilitários para exposição de Web APIs REST/CRUD.
2. `Softprime.Framework.Multitenancy`: Infraestrutura de identificação, resolução e isolamento de tenants no pipeline HTTP do ASP.NET Core.

### Diagnóstico Técnico

- **Fim de Suporte Crítico (EOL):** Ambas as bibliotecas possuem forte acoplamento com o ecossistema **ASP.NET Core 2.2.0**. Essa versão teve seu suporte oficial encerrado pela Microsoft em **23 de dezembro de 2019** (mais de 6 anos fora de suporte).
- **Incompatibilidade de Shared Framework:** A partir do .NET Core 3.0 e consolidado no .NET 8 LTS e .NET 10 LTS, a Microsoft descontinuou a distribuição de pacotes NuGet granulares de alto nível (como `Microsoft.AspNetCore` e `Microsoft.AspNetCore.Mvc`). Todo o ecossistema web foi unificado no **Shared Framework** (`Microsoft.AspNetCore.App`), embutido nativamente no SDK.
- **Conflitos de Dependências Transitivas:** Referenciar pacotes NuGet legados 2.2.0 em aplicações modernas (.NET 8/10) arrasta centenas de dependências antigas descontinuadas, gerando alertas de segurança (CVEs conhecidas), conflitos de redirecionamento de assembly (*binding redirects* / *type forwarding*) e incompatibilidade com o motor de compilação moderno.

---

### Diagnóstico Individual por Biblioteca

#### A. `Softprime.Framework.Api`

- **Targets atuais:** `netstandard2.0;netstandard2.1`
- **Dependências diretas declaradas no `.csproj`:**
  - `Microsoft.AspNetCore.Mvc` (versão `2.2.0`)
  - `Microsoft.AspNetCore.Mvc.Abstractions` (versão `2.2.0`)
  - Referência de projeto: `..\Softprime.Framework.Api.Dto\Softprime.Framework.Api.Dto.csproj`
- **Superfície pública catalogada:**
  - `IApi<TDto, in TId> where TDto : IDto<TId>`: Contrato genérico padrão de operações CRUD (`Get()`, `Get(page, size)`, `Get(id)`, `Post(obj)`, `Put(id, obj)`, `Delete(id)`), onde todos os métodos retornam `Task<IActionResult>`.
  - `ApiKeyAuthorizeFilter : ActionFilterAttribute`: Filtro de ação que intercepta a requisição, compara o header `context.HttpContext.Request.Headers[_nameApiKey]` com a chave esperada `_apiKey` e atribui `context.Result = new StatusCodeResult(403)` em caso de divergência.

#### B. `Softprime.Framework.Multitenancy`

- **Targets atuais:** `netstandard2.0;netstandard2.1;net48`
- **Dependências diretas declaradas no `.csproj`:**
  - `Microsoft.AspNetCore` (versão `2.2.0`) — *metapacote que arrasta todo o runtime ASP.NET Core 2.2*
  - `Microsoft.Extensions.Caching.Memory` (versão `5.0.0`)
  - `System.ComponentModel.Annotations` (versão `5.0.0`)
- **Superfície pública e componentes catalogados:**
  - Contratos e Modelos: `IAppTenant`, `ITenant<TTenant>`, `DataBaseType` (enum Postgres, MsSql, Oracle), `TenantContext` (implementa `IDisposable`), `TenantWrapper<TTenant>`, `TenantPipelineBuilderContext<TTenant>`, `MemoryCacheTenantResolverOptions`, `MultitenancyOptions<TTenant>`.
  - Mecanismo de Resolução: `ITenantResolver`, `MemoryCacheTenantResolver` (classe abstrata com expiração por deslizamento e cancelamento de tokens).
  - Middlewares de Pipeline (`Internal/`): `TenantResolutionMiddleware`, `TenantPipelineMiddleware<TTenant>`, `PrimaryHostnameRedirectMiddleware<TTenant>`, `TenantUnresolvedRedirectMiddleware<TTenant>`.
  - Métodos de Extensão:
    - `MultitenancyApplicationBuilderExtensions.UseMultitenancy(this IApplicationBuilder app)`
    - `UsePerTenantApplicationBuilderExtensions.UsePerTenant<TTenant>(...)`
    - `MultitenancyServiceCollectionExtensions.AddMultitenancy<TResolver>(this IServiceCollection services)`
    - `MultitenancyHttpContextExtensions`: `SetTenantContext`, `GetTenantContext`, `GetTenant<TTenant>`.

---

## 2. Decisão Arquitetural sobre Targets e Consumidores `net48`

### Invariantes Técnicos Fundamentais

1. **Incompatibilidade Insuperável:** O Shared Framework `Microsoft.AspNetCore.App` **não existe** nem pode ser referenciado a partir de `.NET Framework 4.8` (`net48`) ou `.NET Standard 2.0/2.1`. Essa é uma restrição de arquitetura imposta pela Microsoft desde o .NET Core 3.0.
2. **Impossibilidade de Atualização em `net48`:** Não existe caminho técnico viável para executar ASP.NET Core moderno (.NET 8/10) dentro do runtime clássico do .NET Framework (`net48`).

### Decisão Formal Aprovada

> [!IMPORTANT]
> **Decisão sobre o Suporte a `net48` em `Api` e `Multitenancy`:**
> 1. **Descontinuação em Versões Futuras:** A partir da versão **`3.0.0`**, os pacotes `Softprime.Framework.Api` e `Softprime.Framework.Multitenancy` **descontinuam o suporte a `net48` e `netstandard2.x`**, adotando exclusivamente o multi-targeting moderno **`net10.0;net8.0`**.
> 2. **Preservação da Linha Legada:** Consumidores corporativos existentes que operam em `.NET Framework 4.8` com ASP.NET Core 2.2 legado permanecem atendidos pela linha estável **`2.0.0`** congelada no feed corporativo (conforme a [Política de Versionamento NuGet (`docs/nuget-versioning-policy.md`)](nuget-versioning-policy.md)). Se correções críticas de segurança forem indispensáveis, serão publicadas estritamente em patch releases legados (`2.0.1+`).
> 3. **Salto de Versão SemVer:** A remoção do target `net48` e `netstandard2.0` configura quebra intencional de compatibilidade de plataforma, exigindo rigorosamente o incremento de major para **`3.0.0`** ([#12]).
> 4. **Isolamento de Contratos de Domínio Neutros:** Observou-se que `IAppTenant`, `ITenant<T>` e `DataBaseType` não possuem acoplamento com o ASP.NET Core (dependem apenas de anotações básicas). Caso surja demanda futura de aplicações de backend/workers em `net48` que necessitem dessas interfaces sem pipeline HTTP, será avaliada a extração desses contratos para um pacote de abstração neutro (`netstandard2.0`) sem dependência web. Para a versão 3.0.0, a estrutura de pacotes existente é preservada para evitar dispersão.

---

## 3. Plano de Modernização de `Softprime.Framework.Api`

### 3.1. Estrutura Proposta para o Arquivo `.csproj`

Na modernização efetiva (Fase 2), o projeto `Softprime.Framework.Api.csproj` deixará de referenciar pacotes obsoletos e passará a usar o Shared Framework nativo:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Multi-targeting aprovado na issue #3: .NET 10 LTS (primário) e .NET 8 LTS (transição) -->
    <TargetFrameworks>net10.0;net8.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    
    <!-- Metadados comuns herdados de Directory.Build.props (#9) -->
    <Description>Biblioteca de abstração de contratos e filtros para Web APIs ASP.NET Core modernas</Description>
  </PropertyGroup>

  <!-- Shared Framework nativo do ASP.NET Core (substitui Microsoft.AspNetCore.Mvc e Abstractions) -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <!-- Dependência interna de DTOs (mantém netstandard2.0 universal) -->
  <ItemGroup>
    <ProjectReference Include="..\Softprime.Framework.Api.Dto\Softprime.Framework.Api.Dto.csproj" />
  </ItemGroup>

</Project>
```

---

### 3.2. Avaliação de Impacto e Contrato: `IApi<TDto, in TId>`

O arquivo `IApi.cs` define o contrato CRUD fundamental consumido pelos controllers de API:

```csharp
public interface IApi<TDto, in TId> where TDto : IDto<TId>
{
    Task<IActionResult> Get();
    Task<IActionResult> Get(int page, int size);
    Task<IActionResult> Get(TId id);
    Task<IActionResult> Post(TDto obj);
    Task<IActionResult> Put(TId id, TDto obj);
    Task<IActionResult> Delete(TId id);
}
```

#### Análise de Compatibilidade

- **Preservação de `Task<IActionResult>`:**  
  No ASP.NET Core moderno (.NET 8/10), `IActionResult` continua sendo a abstração fundamental em `Microsoft.AspNetCore.Mvc`. Manter `Task<IActionResult>` garante **100% de compatibilidade binária e de compilação** para todas as classes controladoras existentes que implementam `IApi<TDto, TId>`.
- **Avaliação de `ActionResult<T>` / `IResult`:**  
  O padrão `ActionResult<T>` (introduzido no ASP.NET Core 2.1) melhora a inferência de tipos em geradores de documentação OpenAPI/Swagger (evitando decorações manuais com `[ProducesResponseType]`). No entanto, alterar as assinaturas da interface `IApi` para `Task<ActionResult<IEnumerable<TDto>>>` ou `Task<ActionResult<TDto>>` causaria uma **quebra severa de compilação** em todos os controllers que implementam a interface.
- **Diretriz Aprovada para 3.0.0:**  
  - Manter as assinaturas de `IApi<TDto, in TId>` inalteradas com `Task<IActionResult>`.
  - Os controllers que implementam `IApi` em aplicações modernas continuam livres para retornar `Ok(dto)`, `CreatedAtAction(...)`, `NotFound()`, etc.
  - Para enriquecimento de OpenAPI em controllers, as aplicações consumidoras devem continuar utilizando os atributos de anotação de resposta da API (`[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<TDto>))]`).
- **Avaliação sobre `CancellationToken`:**  
  Adicionar parâmetros opcionais `CancellationToken cancellationToken = default` aos métodos da interface `IApi` quebra a implementação de qualquer classe existente que implemente a interface em compilações onde os métodos foram definidos sem o parâmetro. Portanto, `CancellationToken` não será incluído na assinatura de `IApi` na 3.0.0.

---

### 3.3. Avaliação de Impacto e Segurança: `ApiKeyAuthorizeFilter`

O arquivo `ApiKeyAuthorizeFilter.cs` implementa um filtro de autenticação simples baseado em cabeçalho HTTP:

```csharp
public class ApiKeyAuthorizeFilter : ActionFilterAttribute
{
    private readonly string _nameApiKey;
    private readonly string _apiKey;

    public ApiKeyAuthorizeFilter(string nameApiKey, string apiKey)
    {
        _nameApiKey = nameApiKey;
        _apiKey = apiKey;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers[_nameApiKey] != _apiKey)
            context.Result = new StatusCodeResult(403);
    }
}
```

#### Análise Técnica e Melhorias Recomendadas para a Fase de Código

1. **Herança e Ciclo de Vida:**  
   Em ASP.NET Core 8/10, `ActionFilterAttribute` continua existindo no namespace `Microsoft.AspNetCore.Mvc.Filters` dentro do Shared Framework. A classe compila e executa normalmente.
2. **Segurança — Mitigação de Ataques de Temporização (*Timing Attacks*):**  
   A comparação `context.HttpContext.Request.Headers[_nameApiKey] != _apiKey` utiliza igualdade padrão de strings do .NET, que interrompe a verificação no primeiro caractere divergente. Em cenários de alta precisão, isso expõe uma vulnerabilidade de *timing attack*.  
   *Melhoria planejada:* Utilizar `CryptographicOperations.FixedTimeEquals` (disponível nativamente em `System.Security.Cryptography` no .NET moderno) para comparar os bytes da chave.
3. **Robustez na Leitura de Headers:**  
   Acessar `Headers[_nameApiKey]` diretamente pode alocar strings ou retornar `StringValues.Empty`.  
   *Melhoria planejada:* Utilizar `context.HttpContext.Request.Headers.TryGetValue(_nameApiKey, out var headerValues)`.
4. **Código de Status HTTP (403 vs 401):**  
   Segundo a RFC 9110, requisições sem credenciais ou com credenciais inválidas devem retornar `401 Unauthorized` (exigindo autenticação), enquanto `403 Forbidden` indica que o cliente foi autenticado mas não tem permissão. No entanto, alterar o retorno de `403` para `401` quebraria consumidores automatizados que já esperam `403`.  
   *Decisão:* **Preservar `403 Forbidden`** (`StatusCodeResult(StatusCodes.Status403Forbidden)`) para assegurar compatibilidade comportamental.
5. **Padrão de Injeção e Chaves em Atributos:**  
   Como `ApiKeyAuthorizeFilter` herda de `ActionFilterAttribute` e seu construtor exige parâmetros `(string nameApiKey, string apiKey)`, seu uso típico em controllers ocorre decorando a action com strings estáticas (`[ApiKeyAuthorizeFilter("X-API-KEY", "secret")]`), o que leva ao *hardcoding* de segredos no código compilado.  
   *Diretriz:* Preservar o filtro existente para retrocompatibilidade, mas documentar na documentação de uso que o mecanismo preferencial moderno de autorização em ASP.NET Core deve ser via `AuthenticationHandler` ou policies com leitura de segredos a partir de `IConfiguration`.

---

## 4. Plano de Modernização de `Softprime.Framework.Multitenancy`

### 4.1. Estrutura Proposta para o Arquivo `.csproj`

O arquivo `Softprime.Framework.Multitenancy.csproj` será simplificado, eliminando todas as referências obsoletas de pacotes NuGet em favor do Shared Framework:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Multi-targeting moderno aprovado na issue #3 -->
    <TargetFrameworks>net10.0;net8.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    
    <!-- Metadados comuns herdados de Directory.Build.props (#9) -->
    <Description>Módulo Multitenancy da Softprime para aplicações ASP.NET Core modernas (.NET 8/10)</Description>
  </PropertyGroup>

  <!-- Shared Framework nativo do ASP.NET Core -->
  <!-- Fornece: Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Builder, 
       Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.DependencyInjection,
       System.ComponentModel.DataAnnotations e pipeline de middleware -->
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
```

*Nota:* Os pacotes `Microsoft.AspNetCore 2.2.0`, `Microsoft.Extensions.Caching.Memory 5.0.0` e `System.ComponentModel.Annotations 5.0.0` são **100% removidos**, pois todos esses componentes fazem parte integral do Shared Framework no .NET 8 e .NET 10.

---

### 4.2. Avaliação de Pipeline e Middlewares

#### A. `TenantResolutionMiddleware`
- **Operação:**  
  ```csharp
  public async Task Invoke(HttpContext context, ITenantResolver tenantResolver)
  {
      var tenantContext = await tenantResolver.ResolveAsync(context);
      if (tenantContext != null)
          context.SetTenantContext(tenantContext);
      await _next.Invoke(context);
  }
  ```
- **Compatibilidade:** O padrão de injetar dependências scoped (como `ITenantResolver`) diretamente no método `Invoke` do middleware é totalmente suportado pelo pipeline do ASP.NET Core 8/10.
- **Posicionamento no Pipeline:** No ASP.NET Core moderno com *Endpoint Routing*, `app.UseMultitenancy()` **deve** ser registrado antes de `app.UseRouting()` ou entre `app.UseRouting()` e `app.UseEndpoints()`/`app.MapControllers()`, para garantir que o contexto do tenant já esteja disponível quando os controllers e autorizações forem executados.

#### B. `TenantPipelineMiddleware<TTenant>` e `UsePerTenant`
- **Operação:** O middleware cria uma ramificação isolada do pipeline chamando `_rootApp.New()` para cada tenant único e armazena os delegates compilados em um `ConcurrentDictionary`.
- **Compatibilidade e Cuidados com Endpoint Routing:**  
  No ASP.NET Core 2.2, o roteamento era acoplado ao MVC terminal (`UseMvc`). No ASP.NET Core 8/10, o roteamento é baseado em *Endpoint Routing*. Se uma aplicação utilizar `UsePerTenant` configurando middlewares internos específicos por tenant (ex.: autenticações ou middlewares customizados), a ramificação criada via `_rootApp.New()` funciona perfeitamente, desde que não tente registrar endpoints terminais conflitantes na raiz. O código atual preserva compatibilidade total.

#### C. Middlewares de Redirecionamento
- `PrimaryHostnameRedirectMiddleware<TTenant>` e `TenantUnresolvedRedirectMiddleware<TTenant>`:
  Utilizam `context.Request.GetEncodedUrl()`, `UriBuilder` e `context.Response.Redirect(...)`.
  Essas APIs pertencem a `Microsoft.AspNetCore.Http.Extensions` e operam identicamente no .NET 8 e 10.
  A manipulação de status codes (`StatusCodes.Status301MovedPermanently` e `StatusCodes.Status302Found`) permanece íntegra.

---

### 4.3. Avaliação de Cache: `MemoryCacheTenantResolver`

- **APIs de Cache:** Utiliza `IMemoryCache`, `MemoryCacheEntryOptions`, `CancellationChangeToken` e `RegisterPostEvictionCallback`.
- **Compatibilidade:** A abstração `IMemoryCache` não sofreu alterações que quebrem contratos. No .NET 8/10, o método `RegisterPostEvictionCallback` com assinatura `(object key, object value, EvictionReason reason, object state)` é integralmente mantido.
- **Observação Comportamental (Otimização Não-Breaking):**  
  Em `MemoryCacheTenantResolver.cs` (linhas 42-65), nota-se que a busca inicial utiliza `cacheKey = GetContextIdentifier(context)` enquanto o armazenamento utiliza `tenantIdentifier = GetTenantIdentifier(tenantContext)`. Se o identificador de contexto e o de tenant forem strings distintas em implementações derivadas, pode haver um *cache miss* sistemático. Esse comportamento deve ser cuidadosamente preservado para não quebrar resolvers existentes, mas pode ser enriquecido com testes de caracterização dedicados.

---

### 4.4. Avaliação de Injeção de Dependência (`MultitenancyServiceCollectionExtensions`)

O método de extensão atual:

```csharp
public static IServiceCollection AddMultitenancy<TResolver>(this IServiceCollection services)
    where TResolver : class, ITenantResolver
{
    services.AddScoped<ITenantResolver, TResolver>();
    services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddScoped(prov => prov.GetService<IHttpContextAccessor>()?.HttpContext?.GetTenantContext());
    services.AddScoped(prov => prov.GetService<TenantContext>()?.Tenant);
    services.AddScoped<ITenant<IAppTenant>>(prov => new TenantWrapper<IAppTenant>(prov.GetService<IAppTenant>()));

    var resolverType = typeof(TResolver);
    if (typeof(MemoryCacheTenantResolver).IsAssignableFrom(resolverType))
        services.AddMemoryCache();

    return services;
}
```

#### Análise e Recomendações

1. **Preservação Estrita:** A assinatura `AddMultitenancy<TResolver>()` será mantida exatamente como está, garantindo paridade com código de inicialização (`Program.cs` / `Startup.cs`) já existente.
2. **Injeção Tipada do Tenant Concreto:**  
   O registro atual injeta `ITenant<IAppTenant>`, mas se um consumidor tentar injetar `ITenant<MinhaEmpresaTenant>` via DI, o container lançará exceção de resolução de serviço não registrado.  
   *Evolução Proposta (Não-Breaking):* Na implementação da Fase 2, além de manter `AddMultitenancy<TResolver>()`, disponibilizar uma sobrecarga adicional tipada:
   ```csharp
   public static IServiceCollection AddMultitenancy<TTenant, TResolver>(this IServiceCollection services)
       where TTenant : class, IAppTenant
       where TResolver : class, ITenantResolver
   ```
   Essa sobrecarga registrará adicionalmente `services.AddScoped<ITenant<TTenant>>(...)` e `services.AddScoped<TTenant>(...)`, facilitando a vida dos desenvolvedores modernos sem quebrar o método já existente.

---

## 5. Catálogo de Breaking Changes

A modernização da camada web acarreta as seguintes mudanças de compatibilidade:

| Categoria | Componente / Elemento | Descrição da Mudança | Impacto nos Consumidores | Classificação SemVer |
|---|---|---|---|---|
| **Plataforma / TFM** | `Softprime.Framework.Api` | Remoção de `netstandard2.0` e `netstandard2.1`; adoção exclusiva de `net10.0;net8.0`. | Consumidores em `.NET Framework 4.8` ou legados não conseguem referenciar a versão 3.0.0. Devem permanecer na 2.0.0. | **MAJOR** (`3.0.0`) |
| **Plataforma / TFM** | `Softprime.Framework.Multitenancy` | Remoção de `net48`, `netstandard2.0` e `netstandard2.1`; adoção exclusiva de `net10.0;net8.0`. | Consumidores em `.NET Framework 4.8` não conseguem referenciar a versão 3.0.0. Devem permanecer na 2.0.0. | **MAJOR** (`3.0.0`) |
| **Dependências NuGet** | Ambos os pacotes | Remoção de `Microsoft.AspNetCore.* 2.2.0`; inclusão de `FrameworkReference Microsoft.AspNetCore.App`. | Elimina centenas de pacotes transitivos legados. Aplicações consumidoras não sofrem impacto se já rodam em .NET 8/10. | **MAJOR** (atrelado aos TFMs) |
| **Binária / Assinatura** | `IApi<TDto, TId>` | Nenhuma. Retorno `Task<IActionResult>` e assinaturas de métodos preservadas na íntegra. | Zero impacto para controllers que implementam `IApi`. Compatibilidade de código e binária 100% mantida. | **Nenhum** |
| **Binária / Assinatura** | `ApiKeyAuthorizeFilter` | Nenhuma. Construtor `(string, string)` e herança de `ActionFilterAttribute` preservados. | Zero impacto de compilação ou assinatura para atributos já aplicados. | **Nenhum** |
| **Comportamental** | `ApiKeyAuthorizeFilter` | Preservado retorno `403 Forbidden` (`StatusCodeResult(403)`). Melhoria interna de mitigação de timing attack via `CryptographicOperations.FixedTimeEquals`. | Sem alteração no comportamento observado pelos clientes HTTP externos (continua retornando 403). | **Nenhum** |
| **Binária / Assinatura** | `Multitenancy` Extensões | Preservadas todas as assinaturas existentes (`UseMultitenancy`, `UsePerTenant`, `AddMultitenancy`). | Zero impacto de compilação na inicialização da aplicação. | **Nenhum** |

---

## 6. Estratégia de Validação e Testes Automatizados (Fase 2)

Quando a implementação da Fase 2 for iniciada, a integridade da modernização será verificada através de uma suíte automatizada de testes dedicada (conforme diretrizes da issue [#7]):

### 6.1. Projetos de Teste Planejados

1. **`tests/Softprime.Framework.Api.Tests/Softprime.Framework.Api.Tests.csproj`:**
   - Targets: `net10.0;net8.0`
   - Dependências: `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `fluentassertions`.
   - Cenários a cobrir:
     - Validação de execução de controller dummy implementando `IApi<SampleDto, int>`.
     - Validação de `ApiKeyAuthorizeFilter`:
       - Header correto $\to$ requisição passa com sucesso (200 OK).
       - Header divergente $\to$ retorno 403 Forbidden imediato.
       - Header ausente $\to$ retorno 403 Forbidden imediato.
       - Comparação de strings com caracteres especiais e tamanhos divergentes.

2. **`tests/Softprime.Framework.Multitenancy.Tests/Softprime.Framework.Multitenancy.Tests.csproj`:**
   - Targets: `net10.0;net8.0`
   - Dependências: `Microsoft.AspNetCore.TestHost`, `xunit`, `fluentassertions`, `moq`.
   - Cenários a cobrir:
     - `TenantResolutionMiddleware`: resolução e atribuição do `TenantContext` no `HttpContext.Items`.
     - `MemoryCacheTenantResolver`:
       - Resolução com cache miss (invoca `ResolveAsync` e armazena).
       - Resolução subsequente com cache hit (não re-invoca `ResolveAsync`).
       - Expiração e chamada de descarte (`DisposeTenantContext`).
     - `UsePerTenant`: execução de branch de pipeline configurado por tenant.
     - Redirecionamentos: validação dos redirects 301/302 para host primário ou tenant não resolvido.

---

## 7. Critérios de Aceite Atendidos da Issue #4

- [x] **Plano de migração documentado com decisão explícita sobre `net48`:** Documentado formalmente na Seção 2 — descontinuação em `3.0.0` e congelamento da linha `2.0.0` para consumidores legados.
- [x] **Breaking changes potenciais em `IApi` e `ApiKeyAuthorizeFilter` catalogados:** Mapeados e detalhados nas Seções 3 e 5, com plano para preservação de assinaturas e retorno `Task<IActionResult>`.
- [x] **Nenhuma alteração de código nesta issue:** Escopo estritamente restrito a levantamento arquitetural, auditoria técnica e documentação normativa.
- [x] **Entregáveis formalizados:** Documento dedicado `docs/aspnetcore-modernization-plan.md` e atualização do plano mestre `docs/modernization-plan.md`.

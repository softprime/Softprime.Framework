# Sample 03-Data-EFCore — Repository, Unit of Work, Paginação e Transações (SQLite Local)

> Exemplo didático executável demonstrando o consumo do padrão Repository e Unit of Work do framework (`Softprime.Framework.DAL` + `Softprime.Framework.EFCore5`) sobre um banco **SQLite local efêmero** utilizando exclusivamente **APIs públicas**.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como estruturar a persistência de dados utilizando os contratos e implementações de acesso a dados do `Softprime.Framework`:
- **Unit of Work (`IUnitOfWork` / `EFUnitOfWork<TContext>`):** Orquestração transacional e gerenciamento centralizado de repositórios por contexto de dados.
- **Repositório Genérico (`IRepository<T>` / `EFRepository<T>`):** Operações síncronas e assíncronas (`Save`, `SaveAsync`, `Get`, `GetAsync`, `All`, `Update`, `Delete`).
- **Ciclo de Transiência (`IsTransient()`):** Demonstração da transição de uma entidade em memória (`Id == null`, transiente) para uma entidade persistida após `SaveChanges()` (`Id` gerado automaticamente pelo SQLite).
- **Paginação (`Find`):** Consulta paginada por filtro utilizando `Find(filter, out total, index, size)` combinada com contagem global via `All().Count(...)`.
- **Controle Transacional Explícito (Commit e Rollback):** Uso de `BeginTransactionAsync()`, `CommitAsync()` e `RollbackAsync()` garantindo atomicidade e reversão automática em cenários de falha.
- **Execução Autônoma e Descartável:** Utilização de banco SQLite criado dinamicamente no diretório temporário do sistema (`%TEMP%`) e descartado ao término da execução.

> **Nota de Compatibilidade Legada:** Enquanto a biblioteca moderna `Softprime.Framework.EFCore10` (Issue #5) ainda não é criada na Fase 2, este sample referencia `Softprime.Framework.EFCore5` (EF Core 5.0) e utiliza o target `net6.0` (com `Microsoft.EntityFrameworkCore.Sqlite 5.0.17`), visto que o EF Core 5 apresenta incompatibilidade de runtime em `.NET 7/8/10` na reflexão de métodos `System.Linq.Queryable`. Futuramente, será migrado para `net10.0;net8.0` sobre o `EFCore10`.

---

## 2. Pré-requisitos

- [.NET SDK 6.0](https://dotnet.microsoft.com/) instalado (ou runtime .NET 6.0 para execução).
- **Autonomia Total:** Nenhuma instância de SQL Server, Oracle, Docker ou credencial externa é necessária. O banco SQLite local é criado e removido automaticamente em `%TEMP%`.

---

## 3. Comandos para rodar

### No Windows (PowerShell):
```powershell
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj
```

---

## 4. Fluxo de execução passo a passo

1. **Provisionamento Efêmero:** Geração de caminho único em `%TEMP%` (`softprime_sample_03_<guid>.db`) e criação automática do schema SQLite via `Database.EnsureCreatedAsync()`.
2. **Create (`Save` / `SaveAsync`):** Inserção de 6 produtos via `IRepository<Produto>` e persistência atômica via `uow.SaveChanges()`, validando a mudança de `IsTransient() == true` para `false` com atribuição do `Id`.
3. **Read e Paginação:** Busca por ID (`Get(long)`), busca assíncrona por expressão (`GetAsync(predicate)`) e leitura paginada de registros ativos em fatias de 2 itens por página.
4. **Update:** Alteração de preço de um produto existente e confirmação via `uow.SaveChanges()`.
5. **Transações (Rollback e Commit):**
   - Simulação de falha de regra de negócio após alteração em banco, acionando `RollbackAsync()` e comprovando que o preço original foi preservado.
   - Alteração válida encerrada com `CommitAsync()`, comprovando persistência definitiva.
6. **Delete e Cleanup:** Exclusão de produtos inativos por predicado (`Delete(predicate)`) e remoção garantida do arquivo `.db` temporário no bloco `finally`.

---

## 5. Saída esperada no console

Ao executar o comando de demonstração, você verá a seguinte saída no terminal:

```text
================================================================================
 Softprime.Framework — Sample 03-Data-EFCore (Repository, UoW e SQLite Local)
================================================================================

[INFO] Banco SQLite efemero criado em: C:\Users\ViniciusAraujo\AppData\Local\Temp\softprime_sample_03_22a063aac6dc454093ac8d007a3cedac.db
[NOTA] Usando provisoriamente Softprime.Framework.EFCore5 ate entrega do EFCore10 (#5).

--- 1. Create: Insercao via IRepository<Produto> e IUnitOfWork ---
Antes de salvar: 'Notebook Corporativo 14"' -> IsTransient() = True
Apos SaveChanges: 'Notebook Corporativo 14"' -> Id = 1, IsTransient() = False
Total inserido com sucesso: 6 produtos.

--- 2. Read & Paginacao: Get, GetAsync, All e Find paginado ---
Get(id=1): SKU-001 - Notebook Corporativo 14" (R$ 5.499,90)
GetAsync(SKU-004): Monitor 27" 4K IPS (Categoria: Eletronicos)
Produtos ativos (total global = 5):
  Pagina 0 (size=2, retornados=2): SKU-001, SKU-002
  Pagina 1 (size=2, retornados=2): SKU-003, SKU-004

--- 3. Update: Atualizacao de Entidade Existente ---
Produto SKU-002 atualizado: R$ 189,00 -> R$ 199,90

--- 4. Transacoes: Rollback em caso de erro e Commit em sucesso ---
[Rollback] Excecao capturada (Falha simulada durante processamento do lote!) -> Transacao revertida.
Verificacao pos-rollback SKU-003: Preco mantido em R$ 349,50 (Esperado: R$ 349,50)
[Commit] Novo preco de SKU-003 confirmado em transacao: R$ 329,90

--- 5. Delete: Remocao por Predicado ---
Produtos inativos removidos. Restantes no banco: 5 produtos.

[SUCESSO] Demonstracao de Persistencia EF Core concluida com 100% de exito!
[CLEANUP] Banco SQLite temporario descartado: C:\Users\ViniciusAraujo\AppData\Local\Temp\softprime_sample_03_22a063aac6dc454093ac8d007a3cedac.db
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou verificação automatizada local, o sample suporta o argumento `--smoke-test`. Nesse modo, todas as operações de CRUD, paginação e transações (commit e rollback) são validadas sobre um banco SQLite descartável em `%TEMP%`, retornando código de saída `0` para sucesso ou `1` em caso de falha:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.DataEFCore: CRUD, paginacao e transacoes validados em SQLite.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The framework 'Microsoft.NETCore.App', version '6.0.0' was not found` | .NET 6.0 SDK/Runtime não está instalado localmente. | Instale o .NET 6.0 SDK (`winget install Microsoft.DotNet.SDK.6`) enquanto este sample utiliza o `EFCore5` legado. |
| `TypeInitializationException` em `QueryableMethods` | Execução forçada em `.NET 8/10` ou versão `5.0.2` do pacote SQLite. | Utilize o target `net6.0` com `Microsoft.EntityFrameworkCore.Sqlite 5.0.17`, já configurados no `.csproj`. |
| `ArgumentException` em `repo.Get(id)` | Passagem de chave `int` para entidade `Entity` (cujo `Id` é `long?`). | Sempre passe chaves do tipo `long` (ex.: `1L` ou `produto.Id.Value`) ao consultar entidades herdadas de `Entity`. |
| O projeto gerou `.nupkg` indesejado | O arquivo `samples/Directory.Build.props` foi removido ou ignorado. | Restaure o arquivo `samples/Directory.Build.props` com `<IsPackable>false</IsPackable>`. |

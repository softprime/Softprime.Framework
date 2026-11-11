# Sample 02-Domain — Entidades, Identidade DDD, Value Objects e Lifecycle Hooks

> Exemplo didático executável demonstrando o consumo e as capacidades da camada de domínio (`Softprime.Framework.Domain`) utilizando exclusivamente **APIs públicas**.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática os padrões fundamentais de Domain-Driven Design (DDD) fornecidos pelo `Softprime.Framework.Domain`:
- **Entidades com Identidade Persistida (`Entity`):** Compreender a comparação semântica por chave primária (`Id == Id`), na qual duas entidades com o mesmo ID são consideradas iguais mesmo que seus atributos mutáveis tenham sido alterados.
- **Entidades Transientes e Assinatura de Domínio (`GetTypeSpecificSignatureProperties`):** Entidades recém-instanciadas em memória que ainda não possuem ID de banco (`Id == null`, `IsTransient() == true`) são comparadas por sua assinatura de negócio única (como CPF, CNPJ ou SKU).
- **Entidades com Identificadores Customizados (`EntityWithTypedId<TId>`):** Criação de entidades com tipos de chave alternativos além do padrão `long?`, como `Guid`, `string` ou identificadores compostos.
- **Objetos de Valor (`ValueObject`):** Modelagem de conceitos imutáveis sem identidade própria (ex.: `Dinheiro`, `Endereco`), comparados pela combinação estrutural de todas as suas propriedades através dos operadores sobrecarregados `==` e `!=`.
- **Autovalidação Declarativa (`ValidatableObject`):** Validação integrada de invariantes de domínio via Data Annotations (`[Required]`, `[StringLength]`, `[EmailAddress]`) expostas pelos métodos `IsValid()` e `ValidationResults()`.
- **Lifecycle Hooks:** Interceptação de eventos de ciclo de vida (`BeforeSave()`, `AfterSave()`, `BeforeDelete()`, `AfterDelete()`).

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma credencial externa, banco de dados ou serviço em nuvem é necessário. Execução 100% local e em memória.

---

## 3. Comandos para rodar

### No Windows (PowerShell):
```powershell
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj
```

---

## 4. Fluxo de execução passo a passo

1. **Avaliação de Entidades Persistidas:** Comparação de instâncias com `SetId(42L)`. Demonstração de que `cliente1.Equals(cliente2)` retorna `True` por terem o mesmo ID, enquanto `cliente1.Equals(cliente3)` retorna `False`.
2. **Avaliação de Entidades Transientes:** Criação de clientes sem ID (`IsTransient() == true`). Demonstração de que duas entidades transientes com o mesmo documento retornam `True` por compartilharem a mesma assinatura de domínio (`GetTypeSpecificSignatureProperties`), enquanto entidades com documentos distintos retornam `False`.
3. **Avaliação de Transiente vs. Persistida:** Comprovação de que uma entidade transiente nunca é igual a uma entidade já persistida.
4. **Entidades com GUID:** Criação de `Produto : EntityWithTypedId<Guid>`, demonstrando flexibilidade de tipos de chave primária.
5. **Avaliação de Value Objects:** Criação de instâncias de `Dinheiro`. Demonstração dos operadores `==` e `!=` comprovando que instâncias distintas na memória com os mesmos valores de `Quantia` e `Moeda` são consideradas estruturalmente idênticas.
6. **Autovalidação (`ValidatableObject`):** Execução de `IsValid()` e inspeção das mensagens de erro retornadas por `ValidationResults()`.
7. **Lifecycle Hooks:** Disparo de `BeforeSave()` e demonstração do preenchimento automático da data de cadastro e do status de ativação da entidade.

---

## 5. Saída esperada no console

Ao executar o comando de demonstração, você verá a seguinte saída no terminal:

```text
================================================================================
 Softprime.Framework — Sample 02-Domain (Demonstracao Didatica DDD)
================================================================================

--- 1. Entidades Persistidas: Igualdade por ID (Entity) ---
Cliente 1 (Id: 42, Nome: 'Empresa Alfa')
Cliente 2 (Id: 42, Nome: 'Alfa Tecnologia Ltda')
Cliente 1 e transiente? False
Cliente 1.Equals(Cliente 2) [mesmo Id=42]: True (Esperado: True)
Cliente 1.Equals(Cliente 3) [Ids 42 vs 99]: False (Esperado: False)

--- 2. Entidades Transientes: Igualdade por Assinatura de Dominio ---
Cliente Novo 1 e transiente? True
Cliente Novo 2 e transiente? True
Novo 1.Equals(Novo 2) [mesmo Documento de assinatura]: True (Esperado: True)
Novo 1.Equals(Novo 3) [Documentos distintos]: False (Esperado: False)
Novo 1.Equals(Cliente Persistido 1) [Transiente vs Persistido]: False (Esperado: False)

--- 3. Entidades com Chave Tipada Customizada (EntityWithTypedId<Guid>) ---
Produto A (Id: 601345d0-5751-4b2e-ac79-7b9db0722fcb, Sku: SKU-NB-001)
Produto B (Id: 601345d0-5751-4b2e-ac79-7b9db0722fcb, Sku: SKU-NB-001)
Produto A e transiente? False
Produto A.Equals(Produto B) [mesmo Guid]: True (Esperado: True)

--- 4. Objetos de Valor (ValueObject: Igualdade Estrutural por Propriedades) ---
Preco 1: BRL 150,50
Preco 2: BRL 150,50
Preco 1 == Preco 2 (mesmos atributos): True (Esperado: True)
Preco 1.Equals(Preco 2): True (Esperado: True)
Preco 1 == Preco Dolar (moeda diferente): False (Esperado: False)
Preco 1 != Preco Outro (valor diferente): True (Esperado: True)

--- 5. Autovalidacao Integrada de Entidades (ValidatableObject) ---
Cliente Valido -> IsValid(): True
Cliente Invalido -> IsValid(): False
Erros de validacao detectados:
   * Nome e obrigatorio. (Membros: Nome)
   * Documento e obrigatorio. (Membros: Documento)
   * Formato de e-mail invalido. (Membros: Email)

--- 6. Ciclo de Vida da Entidade (Lifecycle Hooks) ---
Antes do BeforeSave() -> DataCadastro: '', Ativo: False
Apos o BeforeSave() -> DataCadastro: '2026-11-09 16:13:28', Ativo: True

[SUCESSO] Demonstracao de Domain Driven Design concluida com 100% de exito!
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou testes automatizados locais, o sample suporta o argumento `--smoke-test`. Nesse modo, asserções verificam todas as regras de igualdade, transiência e validação em memória, retornando código de saída `0` para sucesso ou `1` em caso de erro:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/02-Domain/Softprime.Samples.Domain.csproj -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.Domain: Todas as assercoes DDD passaram com sucesso.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado na máquina. | Instale o .NET 10 SDK ou execute especificando o target .NET 8 com `--framework net8.0`. |
| Entidades transientes com mesmo documento retornam `Equals == false` | Não sobrescreveu `GetTypeSpecificSignatureProperties()`. | Em classes herdadas de `Entity`, sobrescreva `GetTypeSpecificSignatureProperties()` retornando a `PropertyInfo` que identifica o negócio (ex.: `typeof(Cliente).GetProperty(nameof(Documento))!`). |
| `ValueObject` lança `InvalidOperationException` sobre `DomainSignature` | Uso de atributo em `ValueObject`. | Value Objects por convenção comparam todas as suas propriedades públicas. Não utilize assinaturas explícitas em Value Objects. |
| O projeto gerou `.nupkg` indesejado | O arquivo `samples/Directory.Build.props` foi removido ou ignorado. | Restaure o arquivo `samples/Directory.Build.props` com `<IsPackable>false</IsPackable>`. |

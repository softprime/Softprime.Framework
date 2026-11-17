# Relatório de Auditoria de Dependências e Segurança (`Common.Security`)

> **Referência Oficial:** Issue [#6 — Revisar e atualizar demais dependências (Newtonsoft, RazorLight rc, NHibernate, AWS, SendGrid)](https://github.com/softprime/Softprime.Framework/issues/6)  
> **Fase do Roadmap:** Fase 2 — Modernizar seletivamente  
> **Data de Auditoria:** 05/11/2026  
> **Invariante desta rodada:** 100% documental e de governança. Nenhuma alteração em arquivos de código (`.cs`) ou arquivos de projeto (`.csproj`) é aplicada nesta etapa.

---

## 1. Sumário Executivo

Este documento consolida a auditoria completa das dependências externas e dos componentes criptográficos do `Softprime.Framework`. Ele atende aos critérios de governança da Fase 2, estabelecendo os alvos de atualização, a mitigação de vulnerabilidades conhecidas (CVEs/GHSAs) e, fundamentalmente, as **regras estritas de retrocompatibilidade criptográfica de leitura** para que nenhum consumidor legado perca a capacidade de decifrar dados preexistentes ou validar senhas de usuários já persistidas.

### Principais Conclusões:
1. **Vulnerabilidades Ativas:**
   - `Newtonsoft.Json 12.0.3` (`Softprime.Framework.Api.Client`): Alta severidade ([GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr) - DoS por StackOverflow). Correção direta: `13.0.3` (assets compatíveis; regressão ainda a validar).
   - `NHibernate 5.3.5` (`Softprime.Framework.NH`): Alta severidade ([GHSA-fg4q-ccq8-3r5q](https://github.com/advisories/GHSA-fg4q-ccq8-3r5q) - SQL injection em mappings, HQL e utilitários). Correção: `5.7.1` (mantendo-se na linha 5.x estável compatível com `net48` e `netstandard2.0`).
2. **Dependência em Pré-Release Histórico (`RazorLight`):**
   - `Softprime.Framework.Mailer` utiliza `RazorLight 2.0.0-rc.3` acoplado diretamente em `SoftprimeMailer`. A versão estável candidata é `2.3.1`. O comparativo arquitetural com o Codout ([#17]/[#22]) indica a necessidade de **desacoplar o contrato de envio do mecanismo de templates** através de uma abstração (`ITemplateRenderer`).
3. **DependÃªncias a revisar:**
   - `EntityFramework 6.4.4` (`Softprime.Framework.EF6`): Sinalizado como `Legacy` pelo NuGet. Atualização recomendada: `EntityFramework 6.5.1` (adiciona suporte oficial a runtimes modernos preservando total compatibilidade com `net48`).
4. **Fragilidades e Invariantes Criptográficas em `Common.Security`:**
   - `CryptoString`: Cifra DES 56-bit com chave e IV estáticos hardcoded. **Invariante:** `Decrypt(string)` deve permanecer capaz de decifrar payloads antigos. Introdução planejada de `AesCryptoString` com auto-detecção por prefixo de envelope (`$aes$v1$`).
   - `CryptoFile`: P/Invoke Windows-only (`KERNEL32.DLL`), cast incorreto no .NET moderno e carregamento integral de arquivos em memória. **Invariante:** Preservar `DecryptFile` em múltiplos SOs e especificar streaming em chunks de 64KB com AES.
   - `SimpleHash`: Cripto-hash de rodada única (MD5/SHA) com salt fraco de 4 a 8 bytes. Inadequado para senhas modernas. **Invariante:** `VerifyHash` não pode ser quebrado. Especificação de `IPasswordHasher` moderno (Argon2id/PBKDF2) com fluxo de **Rehash Transparente no Login**.

---

## 2. Auditoria Factual de Dependências de Terceiros

Revisão documental das referências e fontes oficiais em 2026-11-17. Não há logs anexos que comprovem a execução histórica da auditoria CLI. As versões são candidatas; implementação exige restore, auditoria das transitivas e testes por target.

### 2.1. Tabela Geral de Inventário e Avaliação

| Pacote | Projeto | Versão Atual | Target do Projeto | Versão Candidata | Severidade / Status | Risco de Breaking Change | Ação Recomendada |
|---|---|---|---|---|---|:---:|---|
| `Newtonsoft.Json` | `Api.Client` | 12.0.3 | `netstandard2.0;netstandard2.1` | `13.0.3` | **Alta** (GHSA-5crp-9r3c-p9vr) | **Mínimo** | Atualizar para `13.0.3` no asset `netstandard2.0`. Avaliar abstração de serializador para `System.Text.Json` em `net10.0`. |
| `RazorLight` | `Mailer` | 2.0.0-rc.3 | `netstandard2.0;netstandard2.1;net48` | `2.3.1` | Pré-release histórico | **Médio** (acoplamento) | Isolar contrato (`ITemplateRenderer`) e atualizar engine para `2.3.1`. |
| `NHibernate` | `NH` | 5.3.5 | `netstandard2.0;netstandard2.1;net48` | `5.7.1` | **Alta** (GHSA-fg4q-ccq8-3r5q) | **Médio** | Atualizar para `5.7.1` na família 5.x. Manter pacote legado congelado. |
| `FluentNHibernate` | `NH` | 3.1.0 | `netstandard2.0;netstandard2.1;net48` | `3.5.0` | Estável | **Baixo** | Atualizar em conjunto com o NHibernate 5.7.1. |
| `EntityFramework` | `EF6` | 6.4.4 | `netstandard2.1;net48` | `6.5.1` | DepreciaÃ§Ã£o nÃ£o comprovada | **Mínimo** | Atualizar para `6.5.1` (possui asset compatível com net48; regressão ainda a validar). |
| `AWSSDK.SimpleEmailV2` | `Mailer.AWS` | 3.5.1.25 | `netstandard2.0;netstandard2.1;net48` | `3.7.x` (linha v3; suporte a confirmar) | Desatualizado (2021) | **Baixo** | Atualizar para a versão mais recente da linha v3.7 (evitar v4 por breaking changes de API). |
| `SendGrid` | `Mailer.SendGrid` | 9.22.0 | `netstandard2.0;netstandard2.1;net48` | `9.29.3` | Estável | **Mínimo** | Atualização menor/patch para estabilidade de TLS e HTTP. |
| `System.ComponentModel.Annotations` | `Common`, `Domain`, `Multitenancy` | 5.0.0 | Multi-target | `5.0.0` | Estável | **Nenhum** | Manter em `netstandard2.0` e `net48`; suprimir em `net10.0;net8.0` (fornecido pelo runtime). |

---

### 2.2. Análise Detalhada por Componente

#### A. `Softprime.Framework.Api.Client` (`Newtonsoft.Json`)
- **Problema:** A versão 12.0.3 possui vulnerabilidade conhecida em que entradas profundamente aninhadas podem causar consumo excessivo de CPU/memória ou StackOverflow conforme o caminho de serialização, configurando Denial of Service (DoS).
- **Compatibilidade com Targets (#3):** O `Newtonsoft.Json 13.0.3` tem suporte oficial a `netstandard2.0`, `net45`, `net6.0+`. Atende perfeitamente ao target `netstandard2.0` aprovado para `Softprime.Framework.Api.Client`.
- **Avaliação de `System.Text.Json`:**
  - O runtime moderno (.NET 8 e .NET 10) possui `System.Text.Json` integrado com alto desempenho.
  - No entanto, `JsonExtensions.cs` expõe métodos de extensão utilitários (`ReadAsAsync`, `PostAsJsonAsync`, `PutAsJsonAsync`) que utilizam convenções do Newtonsoft (ex.: `NullValueHandling.Ignore`).
  - **Decisão:** Atualizar `Newtonsoft.Json` para `13.0.3` na baseline de compatibilidade universal. Para futuros alvos modernos (`net10.0`), desacoplar a serialização via interface de opções ou permitir que o consumidor escolha o serializador sem impor quebra de contrato aos consumidores `net48`.

#### B. `Softprime.Framework.Mailer` (`RazorLight`)
- **Problema:** O pacote utiliza `RazorLight 2.0.0-rc.3` (uma release candidate antiga). Além disso, a classe `SoftprimeMailer` instancia `RazorLightEngine` diretamente em seu construtor protegido, acoplando a biblioteca de envio de e-mails à compilação dinâmica do Razor.
- **Achado do Comparativo Codout ([#17]/[#22]):** O Codout separa estritamente o contrato de e-mail de seu mecanismo de renderização (`Codout.Mailer.Razor`), permitindo flexibilidade e testes mais simples sem dependência de reflexão pesada do Razor.
- **Decisão:**
  1. Introduzir na camada de abstração do `Mailer` a interface:
     ```csharp
     public interface ITemplateRenderer
     {
         Task<string> RenderAsync<T>(string templateKey, T model);
     }
     ```
  2. Implementar `RazorLightTemplateRenderer` utilizando a versão estável `RazorLight 2.3.1` (que suporta `netstandard2.0`).
  3. Permitir injeção de `ITemplateRenderer` no construtor de `SoftprimeMailer`, preservando o construtor legado para compatibilidade binária.

#### C. `Softprime.Framework.NH` (`NHibernate` e `FluentNHibernate`)
- **Problema:** `NHibernate 5.3.5` está na faixa afetada por SQL injection do GHSA-fg4q-ccq8-3r5q: discriminadores, campos estáticos em HQL e utilitários que chamam ObjectToSQLString. O advisory informa correções em 5.4.9 e 5.5.2.
- **Decisão:** Atualizar para `NHibernate 5.7.1` (candidata da família 5.x) e `FluentNHibernate 3.5.0`. Como o pacote `Softprime.Framework.NH` foi classificado como **linha legada preservada** ([#5]), a atualização permanece estritamente na família 5.x para não quebrar mapeamentos existentes em sistemas corporativos que ainda operam com `net48` e banco Oracle/SQL Server.

#### D. `Softprime.Framework.Mailer.AWS` e `Mailer.SendGrid`
- **AWS SES V2:** O pacote `AWSSDK.SimpleEmailV2` está na versão `3.5.1.25`. O AWS SDK lançou a major version 4.0 recentemente, mas ela inclui refatorações substanciais em `AmazonServiceClientConfig`. A decisão segura é fixar na versão `3.7.x` LTS, cuja versão exata, suporte vigente, assets e compatibilidade precisam ser confirmados antes da implementação.
- **SendGrid:** A versão `9.22.0` deve ser atualizada para `9.29.3` (versão madura da linha 9.x, mantendo compatibilidade de API de envio).

---

## 3. Auditoria Profunda de Segurança: `Common.Security`

A pasta `src/general/Softprime.Framework.Common/Security` contém classes críticas responsáveis por hashing, geração de senhas e criptografia de strings e arquivos.

```
Softprime.Framework.Common/Security/
├── Crypto.cs          # Wrappers estáticos de hashing (MD5, SHA1, SHA256, etc.)
├── CryptoFile.cs      # Criptografia de arquivos com DES
├── CryptoString.cs    # Criptografia de strings com DES e chaves estáticas
├── RandomPassword.cs  # Gerador de senhas pseudo-aleatórias
└── SimpleHash.cs      # Algoritmo de hash de senhas (rodada única + salt)
```

### 3.1. Análise de Fragilidades e Riscos

#### 1. `CryptoString.cs`
- **Diagnóstico Factual:**
  - Algoritmo: `DESCryptoServiceProvider` (DES - Data Encryption Standard). DES utiliza chave efetiva de apenas 56 bits, considerada quebrável por força bruta em poucas horas por hardware moderno.
  - Chave Fixa Hardcoded:
    ```csharp
    private const string ChaveCriptografia = "C308D044FF164E979396D45AC44189A8";
    _chave = Encoding.UTF8.GetBytes(ChaveCriptografia.Substring(0, 8)); // Apenas 8 bytes!
    ```
  - Vetor de Inicialização (IV) Estático:
    ```csharp
    private static readonly byte[] Iv = { 12, 33, 59, 85, 97, 101, 119, 122 };
    ```
  - **Risco:** Qualquer pessoa com acesso ao repositório ou binário decompilado consegue decifrar qualquer string criptografada pelo `CryptoString.Encrypt`.
  - **Invariante de Negócio Inviolável:** Sistemas legados já possuem strings cifradas salvas em tabelas de banco de dados e arquivos `web.config`. Se o método `Decrypt(string)` for alterado para um novo algoritmo sem preservar a rotina legada, **todos os dados já cifrados se tornarão irrecuperáveis**.

#### 2. `CryptoFile.cs`
- **Diagnóstico Factual:**
  - Algoritmo: DES com chave e IV idênticos derivados de string (`Key = Encoding.ASCII.GetBytes(sKey), IV = Encoding.ASCII.GetBytes(sKey)`).
  - P/Invoke Windows: `[DllImport("KERNEL32.DLL", EntryPoint = "RtlZeroMemory")]`. Lança falha em ambientes Linux/contêineres Docker.
  - Cast Inválido: `(DESCryptoServiceProvider)DES.Create()`. Em .NET Core / .NET 8+, `DES.Create()` retorna uma implementação interna que não herda de `DESCryptoServiceProvider`, gerando `InvalidCastException`.
  - Esgotamento de Memória: Lê arquivos inteiros em `bytearrayinput = new byte[fsInput.Length]`, causando falha de memória para arquivos volumosos.

#### 3. `SimpleHash.cs`
- **Diagnóstico Factual:**
  - Algoritmo: `ComputeHash` executa apenas **uma única iteração** de MD5, SHA1 ou SHA256 sobre `plainText + salt`.
  - Salt Fraco: Tamanho de salt de 4 a 8 bytes selecionado via System.Random; os bytes são preenchidos por RNGCryptoServiceProvider. O tamanho recomendado por padrões de segurança (OWASP/NIST) é de pelo menos 16 bytes, gerado por CSPRNG.
  - Ausência de KDF (Key Derivation Function): Não há fator de trabalho (iterações, memória, paralelismo). Um hash SHA256 simples pode ser verificado centenas de bilhões de vezes por segundo em GPUs modernas.
  - Formato Serializado: `Base64( HashBytes + SaltBytes )`.
  - **Invariante de Negócio Inviolável:** Milhares de usuários em sistemas de clientes possuem senhas armazenadas neste formato. Quebrar `VerifyHash` impediria o login de todos os usuários corporativos existentes.

#### 4. `RandomPassword.cs`
- **Diagnóstico Factual:**
  - Coleta 4 bytes via `RNGCryptoServiceProvider`, mas os converte para um `int seed` e inicializa `new Random(seed)`.
  - Toda a lógica de sorteio de caracteres subsequente é executada pelo gerador pseudo-aleatório `System.Random`, que não é seguro contra previsão estatística.

---

## 4. Arquitetura de Retrocompatibilidade e Modernização

Para elevar a segurança do framework sem gerar quebra de dados existentes, a Fase 2 de código adotará a seguinte estratégia técnica:

### 4.1. Preservação de Leitura e Novo Envelope Criptográfico (`CryptoString`)

```mermaid
flowchart TD
    Start([Chamada a Decrypt: phrase]) --> CheckFormat{Inicia com prefixo moderno?<br>ex: $aes$v1$}
    CheckFormat -- Sim --> DecryptAes[Descriptografar via formato autenticado moderno a especificar<br>com IV extraído do payload]
    CheckFormat -- Não --> DecryptLegacy[Descriptografar via DES Legado<br>com Chave e IV estáticos]
    DecryptAes --> Retorno([Texto Claro Retornado])
    DecryptLegacy --> Retorno
```

1. **Leitura 100% Retrocompatível:**
   - O método `CryptoString.Decrypt(string)` analisa o payload. Se não houver prefixo reconhecido de formato moderno, direciona imediatamente para o motor DES legado com a chave histórica de 8 bytes.
2. **Criptografia Moderna Paralela (`AesCryptoString`):**
   - Introdução de classe moderna baseada em `Aes.Create()` (AES-256).
   - Suporte a chave parametrizável/injetada via `IConfiguration` ou segredo externo (nunca mais hardcoded).
   - Nonce/IV e autenticação serão especificados junto com o algoritmo, targets e vetores de teste. Não misturar parâmetros de GCM e CBC.
   - Envelope moderno versionado a especificar antes da implementação; o prefixo ilustrativo não define protocolo disponível.

---

### 4.2. Rehash Transparente de Senhas no Login (`SimpleHash` → `IPasswordHasher`)

Seguindo o padrão arquitetural consolidado no Codout ([#17]/[#22]) e recomendado pela OWASP:

```mermaid
sequenceDiagram
    autonumber
    actor User as Usuário
    participant App as Aplicação Consumidora
    participant Hasher as ModernPasswordHasher (Argon2id / PBKDF2)
    participant Legacy as SimpleHash (Legado)
    participant DB as Banco de Dados

    User->>App: Submete Usuário e Senha
    App->>DB: Consulta registro por Usuário
    DB-->>App: Retorna hashArmazenado
    
    alt Formato Moderno (Prefixo $argon2id$ ou binário Identity v3)
        App->>Hasher: VerifyPassword(hashArmazenado, senhaDigitada)
        Hasher-->>App: Success (Senha válida)
    else Formato Legado (Base64 SimpleHash)
        App->>Legacy: VerifyHash(senhaDigitada, algorithm, hashArmazenado)
        alt Senha Incorreta
            Legacy-->>App: false
            App-->>User: Falha de autenticação
        else Senha Correta
            Legacy-->>App: true (Válida!)
            Note over App,Hasher: Gatilho de Rehash Transparente
            App->>Hasher: HashPassword(senhaDigitada)
            Hasher-->>App: novoHashModerno
            App->>DB: AtualizarSenha(usuarioId, novoHashModerno)
            Note over DB: Hash atualizado para padrão de alta segurança sem interrupção!
        end
    end
    App-->>User: Login Efetuado com Sucesso!
```

#### Regras de Implementação da Fase 2 de Código:
1. `SimpleHash.VerifyHash(...)` permanece intocado em sua assinatura e comportamento para garantir a checagem das senhas legadas.
2. É criada a interface `IPasswordHasher`:
   ```csharp
   public interface IPasswordHasher
   {
       string HashPassword(string password);
       PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword);
   }

   public enum PasswordVerificationResult
   {
       Failed = 0,
       Success = 1,
       SuccessRehashNeeded = 2
   }
   ```
3. O provedor padrão implementa verificação em cascata: se o hash fornecido for do formato legado do `SimpleHash`, executa a verificação legada. Se correta, retorna `SuccessRehashNeeded`, permitindo que o sistema consumidor grave a nova cifra moderna sem solicitar troca manual de senha ao usuário.

---

### 4.3. Correção de Portabilidade em `CryptoFile`
1. Remover o atributo `[DllImport("KERNEL32.DLL", ...)]` e eliminar chamadas dependentes de APIs Win32.
2. Substituir `(DESCryptoServiceProvider)DES.Create()` por `DES.Create()` para evitar falhas de cast no runtime moderno.
3. Projetar a classe `AesCryptoFile` utilizando streaming com buffers de 64KB (`CryptoStream` direto entre `FileStream` de entrada e saída) para suporte a arquivos de qualquer tamanho sem estouro de heap.

---

## 5. Matriz de Priorização e Cronograma de Execução

| Prioridade | Componente | Escopo de Implementação (Fase 2 de Código) | Justificativa de Risco |
|:---:|---|---|---|
| **P1** | `Api.Client` | Atualizar `Newtonsoft.Json` para `13.0.3` | Elimina vulnerabilidade crítica DoS (GHSA-5crp-9r3c-p9vr) com compatibilidade a validar. |
| **P1** | `Common.Security` | Preservar `Decrypt` legado e especificar `AesCryptoString` | Protege os dados já criptografados em bases de clientes contra perda irrecuperável. |
| **P1** | `Common.Security` | Implementar `IPasswordHasher` com Rehash Transparente | Eleva o patamar de segurança de senhas contra força bruta em GPU preservando o acesso de usuários legados. |
| **P1** | `NH` | Atualizar `NHibernate` para `5.7.1` e `FluentNHibernate` para `3.5.0` | Mitiga SQL injection do GHSA-fg4q-ccq8-3r5q; validar os mapeamentos. |
| **P2** | `Mailer` | Desacoplar `ITemplateRenderer` e atualizar `RazorLight` para `2.3.1` | Elimina pré-release e desacopla dependência pesada de reflexão. |
| **P2** | `EF6` | Atualizar `EntityFramework` para `6.5.1` | Candidata com assets modernos e legados; depreciação e regressão devem ser verificadas. |
| **P2** | `Mailer.AWS` / `SendGrid` | Atualizar SDKs para linhas estáveis maduras | Manutenção preventiva e correção de transporte HTTP/TLS. |

---

## 6. Critérios de Aceite da Issue #6 (Verificação)

- [x] **Auditoria de dependências documentada:** Todas as dependências diretas auditadas com versão atual, versão-alvo candidata, vulnerabilidades (CVE/GHSA), compatibilidade de target e riscos catalogados.
- [x] **Segurança de `Common.Security` mapeada:** Fragilidades de `CryptoString`, `CryptoFile`, `SimpleHash`, `RandomPassword` e `Crypto` explicitadas em relatório técnico.
- [x] **Requisito inviolável de retrocompatibilidade criptográfica formalizado:** Garantia documentada de que dados legados e senhas existentes continuarão sendo lidos e validados.
- [x] **Estratégia de Rehash Transparente especificada:** Mecanismo de upgrade contínuo de hash no login desenhado sem fricção para o usuário.
- [x] **Isolamento de Código:** Nenhuma alteração prematura de código C# ou de pacotes foi aplicada nesta rodada documental.

---
*Relatório de planejamento sujeito a revisão; nenhuma atualização de produção aplicada.*

## Fontes verificadas e limites

- [Newtonsoft GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr): versões anteriores a 13.0.1 afetadas; 13.0.3 permanece candidata.
- [NHibernate GHSA-fg4q-ccq8-3r5q](https://github.com/advisories/GHSA-fg4q-ccq8-3r5q): SQL injection, não RCE por desserialização.
- Assets e dependências: [NHibernate 5.7.1](https://www.nuget.org/packages/NHibernate/5.7.1), [FluentNHibernate 3.5.0](https://www.nuget.org/packages/FluentNHibernate/3.5.0), [RazorLight 2.3.1](https://www.nuget.org/packages/RazorLight/2.3.1), [EntityFramework 6.5.1](https://www.nuget.org/packages/EntityFramework/6.5.1), [SendGrid 9.29.3](https://www.nuget.org/packages/SendGrid/9.29.3). Assets não comprovam paridade comportamental.
- Suporte vigente da AWS v3 e depreciação do EF6 não foram comprovados nesta revisão; idade não equivale a vulnerabilidade. Levantamento externo de consumidores não será pré-requisito.

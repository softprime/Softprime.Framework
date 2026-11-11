# Sample 01-Common — Utilitários Gerais, Validações e Criptografia

> Exemplo didático executável demonstrando o consumo e as capacidades da biblioteca `Softprime.Framework.Common` utilizando exclusivamente **APIs públicas**.

---

## 1. Objetivo do Sample

Este sample ensina de forma prática como utilizar os recursos essenciais do `Softprime.Framework.Common`:
- **Validação de Documentos Brasileiros:** Verificação matemática e de formato de CPF, CNPJ, CEP e E-mails (via métodos estáticos e métodos de extensão).
- **Validação Declarativa de Modelos:** Uso de Data Annotations customizadas (`[CpfCnpj]` e validações condicionais como `[RequiredIf]`).
- **Manipulação de Strings e Texto:** Geração de slugs amigáveis (`SlugHelper`), higienização de strings (`OnlyNumbers`), remoção de acentos (`RemoveAcentos`) e conversão de valores monetários por extenso (`NumberToText`).
- **Utilitários de Datas:** Extensões semânticas de calendário (`5.DaysAgo()`, `3.DaysFromNow()`, `IsWeekDay()`, `IsWeekEnd()`).
- **Segurança e Criptografia:** Geração de senhas aleatórias seguras (`RandomPassword`), derivação de hash com salt aleatório (`SimpleHash` SHA-256) e criptografia simétrica reversível de strings (`CryptoString`).

---

## 2. Pré-requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou [.NET SDK 8.0](https://dotnet.microsoft.com/) instalado.
- **Autonomia Total:** Nenhuma credencial externa, banco de dados ou serviço em nuvem é necessário. Execução 100% local e offline.

---

## 3. Comandos para rodar

### No Windows (PowerShell):
```powershell
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj
```

---

## 4. Fluxo de execução passo a passo

1. **Inicialização do Runtime:** Configuração do provedor de codificações (`CodePagesEncodingProvider`) para compatibilidade uniforme entre plataformas.
2. **Avaliação de Documentos:** Testes com CPFs e CNPJs válidos e inválidos comprovando a rejeição de sequências forjadas e aceitação de dígitos verificadores legítimos.
3. **Validação de Entidades (Data Annotations):**
   - Submissão de um modelo válido de Pessoa Física.
   - Submissão de um modelo de Pessoa Jurídica com violações propositais (CPF inválido e ausência de Inscrição Estadual obrigatória) exibindo as mensagens de validação coletadas pelo `Validator`.
4. **Transformações de Texto e Negócio:** Conversão de texto em slug para URLs, extração numérica de máscaras financeiras e conversão de valores decimais em texto por extenso (Real/Centavos).
5. **Cálculo de Datas:** Exercício de navegação temporal fluente com extensões inteiras e verificação de dias úteis/finais de semana.
6. **Operações Criptográficas:**
   - Criação de credencial aleatória com alta entropia.
   - Hashing com inclusão transparente de salt criptográfico e verificação positiva e negativa.
   - Criptografia e decriptografia em duas vias de mensagem confidencial.

---

## 5. Saída esperada no console

Ao executar o comando de demonstração, você verá a seguinte saída no terminal:

```text
================================================================================
 Softprime.Framework — Sample 01-Common (Demonstracao Didatica)
================================================================================

--- 1. Validacao de Documentos e Formatos (CPF, CNPJ, CEP, Email) ---
CPF '52998224725' e valido? True
CPF '11122233344' e valido? False
CNPJ '11444777000161' e valido? True
Extensao IsCpf(): True
Extensao IsCnpj(): True
Extensao IsCep('01310-100'): True
Extensao IsEmail('contato@softprime.com.br'): True

--- 2. Validacao de Modelos com Data Annotations ([CpfCnpj], [RequiredIf]) ---
-> Cenario: Modelo Valido (Pessoa Fisica) | Valido: True
-> Cenario: Modelo Invalido (Pessoa Juridica sem Inscricao Estadual) | Valido: False
   * [Erro] Documento informado nao e um CPF ou CNPJ valido. (Campos: Documento)
   * [Erro] Inscricao Estadual e obrigatoria para Pessoa Juridica. (Campos: InscricaoEstadual)

--- 3. Extensoes de Strings, Datas e Numeros ---
Texto original: "Softprime Framework: Modernizacao de Software & Inovacao!"
SlugHelper.GenerateSlug(): softprime-framework-modernizacao-de-software--inovacao
RemoveAcentos(): Softprime Framework: Modernizacao de Software & Inovacao!
OnlyNumbers("R$ 1.250,50 (cod: ABC-789)"): 125050789
Valor por Extenso (NumberToText): hum mil e quinhentos e trinta reais e setenta e cinco centavos
5.DaysAgo(): 2026-11-01
3.DaysFromNow(): 2026-11-09
IsWeekDay(DateTime.Now): True
IsWeekEnd(DateTime.Now): False

--- 4. Utilitarios de Seguranca (RandomPassword, SimpleHash, CryptoString) ---
Senha aleatoria gerada (RandomPassword): e?6NGd!2o3B-%x8J
Hash SHA-256 com Salt (SimpleHash): A0knEUtco41nAcb+mnnWKcEaEimrnP3OIa488NcF7u7cAtFNL3N2
Verificacao com senha correta: True
Verificacao com senha errada: False
Texto original: Dados Confidenciais da Empresa 2026
CryptoString.Encrypt: mXMcVocNEddhWPC7H0FiNA64VJR/Rz/bZW4/xLcvOHkSKMrWrKJJBQ==
CryptoString.Decrypt: Dados Confidenciais da Empresa 2026

[SUCESSO] Demonstracao concluida com 100% de exito!
```

---

## 6. Como executar os testes associados (Smoke Test)

Para execução em esteiras de integração contínua (CI) ou testes automatizados locais, o sample suporta o argumento `--smoke-test`. Nesse modo, asserções booleanas verificam o comportamento esperado e retornam código de saída `0` para sucesso ou `1` em caso de erro:

### No Windows (PowerShell):
```powershell
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj -- --smoke-test
```

### No Linux / macOS (Bash):
```bash
dotnet run --project samples/01-Common/Softprime.Samples.Common.csproj -- --smoke-test
```

**Saída esperada:**
```text
[SMOKE_TEST_PASSED] Softprime.Samples.Common: Todas as assercoes passaram com sucesso.
```

---

## 7. Troubleshooting e erros comuns

| Sintoma / Erro | Causa Provável | Solução Imediata |
|---|---|---|
| `The TargetFramework 'net10.0' is not recognized` | SDK do .NET 10 não está instalado na máquina. | Instale o .NET 10 SDK ou execute especificando o target .NET 8 com `--framework net8.0`. |
| `CS5001: Programa não contém um método 'Main'` | Tentativa de build em projeto corrompido ou sem ponto de entrada. | Certifique-se de que `Program.cs` existe e está presente na pasta `samples/01-Common/`. |
| `System.ArgumentException: 'iso-8859-8' is not a supported encoding` | Falta de registro do provedor de CodePages no runtime moderno. | O sample já executa `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` no `Main`. Caso reutilize o método em outra aplicação, adicione essa linha na inicialização. |
| O projeto gerou `.nupkg` indesejado | O arquivo `samples/Directory.Build.props` foi removido ou ignorado. | Restaure o arquivo `samples/Directory.Build.props` com `<IsPackable>false</IsPackable>`. |

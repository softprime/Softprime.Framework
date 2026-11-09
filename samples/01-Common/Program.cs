using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Softprime.Framework.Common;
using Softprime.Framework.Common.Annotations;
using Softprime.Framework.Common.Extensions;
using Softprime.Framework.Common.Helpers;
using Softprime.Framework.Common.Security;

namespace Softprime.Samples.Common
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Registra suporte a code pages legado (necessario para metodos como Strings.RemoveAcentos no .NET moderno)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));

            if (isSmokeTest)
            {
                return RunSmokeTest();
            }

            return RunInteractiveDemo();
        }

        private static int RunInteractiveDemo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" Softprime.Framework — Sample 01-Common (Demonstracao Didatica)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine();

            // 1. Validacoes de Documentos Brasileiros (CPF / CNPJ / CEP / Email)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 1. Validacao de Documentos e Formatos (CPF, CNPJ, CEP, Email) ---");
            Console.ResetColor();

            string cpfValido = "52998224725";
            string cpfInvalido = "11122233344";
            string cnpjValido = "11444777000161";
            string cepValido = "01310-100";
            string emailValido = "contato@softprime.com.br";

            Console.WriteLine($"CPF '{cpfValido}' e valido? {Validations.ValidaCPF(cpfValido)}");
            Console.WriteLine($"CPF '{cpfInvalido}' e valido? {Validations.ValidaCPF(cpfInvalido)}");
            Console.WriteLine($"CNPJ '{cnpjValido}' e valido? {Validations.ValidaCNPJ(cnpjValido)}");
            Console.WriteLine($"Extensao IsCpf(): {cpfValido.IsCpf()}");
            Console.WriteLine($"Extensao IsCnpj(): {cnpjValido.IsCnpj()}");
            Console.WriteLine($"Extensao IsCep('{cepValido}'): {cepValido.IsCep()}");
            Console.WriteLine($"Extensao IsEmail('{emailValido}'): {emailValido.IsEmail()}");
            Console.WriteLine();

            // 2. Data Annotations Customizadas
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 2. Validacao de Modelos com Data Annotations ([CpfCnpj], [RequiredIf]) ---");
            Console.ResetColor();

            var modelValido = new CadastroModel
            {
                Nome = "Ana Silva",
                Documento = cpfValido,
                PessoaJuridica = false
            };

            var modelInvalido = new CadastroModel
            {
                Nome = "Empresa X",
                Documento = "00000000000",
                PessoaJuridica = true,
                InscricaoEstadual = null // Obrigatorio quando PessoaJuridica e true
            };

            ValidarModelo(modelValido, "Modelo Valido (Pessoa Fisica)");
            ValidarModelo(modelInvalido, "Modelo Invalido (Pessoa Juridica sem Inscricao Estadual)");
            Console.WriteLine();

            // 3. Extensoes de String, Datas e Numeros
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 3. Extensoes de Strings, Datas e Numeros ---");
            Console.ResetColor();

            string texto = "Softprime Framework: Modernizacao de Software & Inovacao!";
            var slugHelper = new SlugHelper();
            string slug = slugHelper.GenerateSlug(texto);
            string semAcentos = texto.RemoveAcentos();
            string formatado = "R$ 1.250,50 (cod: ABC-789)";
            string apenasNumeros = formatado.OnlyNumbers();

            Console.WriteLine($"Texto original: \"{texto}\"");
            Console.WriteLine($"SlugHelper.GenerateSlug(): {slug}");
            Console.WriteLine($"RemoveAcentos(): {semAcentos}");
            Console.WriteLine($"OnlyNumbers(\"{formatado}\"): {apenasNumeros}");

            decimal valor = 1530.75m;
            var numberToText = new NumberToText(valor);
            Console.WriteLine($"Valor por Extenso (NumberToText): {numberToText}");

            DateTime cincoDiasAtras = 5.DaysAgo();
            DateTime tresDiasFrente = 3.DaysFromNow();
            Console.WriteLine($"5.DaysAgo(): {cincoDiasAtras:yyyy-MM-dd}");
            Console.WriteLine($"3.DaysFromNow(): {tresDiasFrente:yyyy-MM-dd}");
            Console.WriteLine($"IsWeekDay(DateTime.Now): {DateTime.Now.IsWeekDay()}");
            Console.WriteLine($"IsWeekEnd(DateTime.Now): {DateTime.Now.IsWeekEnd()}");
            Console.WriteLine();

            // 4. Utilitarios de Seguranca e Criptografia
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 4. Utilitarios de Seguranca (RandomPassword, SimpleHash, CryptoString) ---");
            Console.ResetColor();

            string senhaGerada = RandomPassword.Generate(12, 16);
            Console.WriteLine($"Senha aleatoria gerada (RandomPassword): {senhaGerada}");

            string hash = SimpleHash.ComputeHash(senhaGerada, HashAlgorithmType.Sha256, null);
            Console.WriteLine($"Hash SHA-256 com Salt (SimpleHash): {hash}");

            bool senhaConfere = SimpleHash.VerifyHash(senhaGerada, HashAlgorithmType.Sha256, hash);
            bool senhaErrada = SimpleHash.VerifyHash("SenhaIncorreta123!", HashAlgorithmType.Sha256, hash);
            Console.WriteLine($"Verificacao com senha correta: {senhaConfere}");
            Console.WriteLine($"Verificacao com senha errada: {senhaErrada}");

            string mensagemSecreta = "Dados Confidenciais da Empresa 2026";
            string criptografado = CryptoString.Encrypt(mensagemSecreta);
            string descriptografado = CryptoString.Decrypt(criptografado);
            Console.WriteLine($"Texto original: {mensagemSecreta}");
            Console.WriteLine($"CryptoString.Encrypt: {criptografado}");
            Console.WriteLine($"CryptoString.Decrypt: {descriptografado}");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCESSO] Demonstracao concluida com 100% de exito!");
            Console.ResetColor();
            return 0;
        }

        private static int RunSmokeTest()
        {
            try
            {
                // Assertions para validacao de CPF e CNPJ
                string cpfValido = "52998224725";
                string cpfInvalido = "11122233344";
                string cnpjValido = "11444777000161";

                if (!Validations.ValidaCPF(cpfValido)) throw new InvalidOperationException("CPF valido rejeitado.");
                if (Validations.ValidaCPF(cpfInvalido)) throw new InvalidOperationException("CPF invalido aceito.");
                if (!Validations.ValidaCNPJ(cnpjValido)) throw new InvalidOperationException("CNPJ valido rejeitado.");

                // Assertions para extensoes de string
                var slugHelper = new SlugHelper();
                string slug = slugHelper.GenerateSlug("Atencao: Acao e Modernizacao!");
                if (string.IsNullOrWhiteSpace(slug) || slug.Contains(' ') || slug.Contains(':'))
                    throw new InvalidOperationException($"GenerateSlug gerou resultado inesperado: {slug}");

                string numeros = "ABC-123.456/78".OnlyNumbers();
                if (numeros != "12345678")
                    throw new InvalidOperationException($"OnlyNumbers falhou: esperado '12345678', obtido '{numeros}'");

                // Assertions para seguranca
                string senha = RandomPassword.Generate(10, 14);
                if (string.IsNullOrEmpty(senha) || senha.Length < 10)
                    throw new InvalidOperationException("RandomPassword falhou no tamanho da senha.");

                string hashSha256 = SimpleHash.ComputeHash(senha, HashAlgorithmType.Sha256, null);
                if (!SimpleHash.VerifyHash(senha, HashAlgorithmType.Sha256, hashSha256))
                    throw new InvalidOperationException("SimpleHash falhou ao verificar hash legitimo.");
                if (SimpleHash.VerifyHash(senha + "extra", HashAlgorithmType.Sha256, hashSha256))
                    throw new InvalidOperationException("SimpleHash aceitou hash corrompido.");

                string segredo = "TesteSmokeTest2026";
                string cifrado = CryptoString.Encrypt(segredo);
                string decifrado = CryptoString.Decrypt(cifrado);
                if (decifrado != segredo)
                    throw new InvalidOperationException("CryptoString falhou no ciclo de criptografia/descriptografia.");

                // Assertions para Data Annotations
                var modelOk = new CadastroModel { Nome = "Teste", Documento = cpfValido, PessoaJuridica = false };
                var ctx = new ValidationContext(modelOk);
                var results = new List<ValidationResult>();
                if (!Validator.TryValidateObject(modelOk, ctx, results, true))
                    throw new InvalidOperationException("Validacao de modelo legitimo falhou.");

                Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.Common: Todas as assercoes passaram com sucesso.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SMOKE_TEST_FAILED] Falha: {ex.Message}");
                return 1;
            }
        }

        private static void ValidarModelo(CadastroModel model, string cenario)
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(model, context, results, true);

            Console.WriteLine($"-> Cenario: {cenario} | Valido: {isValid}");
            if (!isValid)
            {
                foreach (var err in results)
                {
                    Console.WriteLine($"   * [Erro] {err.ErrorMessage} (Campos: {string.Join(", ", err.MemberNames)})");
                }
            }
        }
    }

    public class CadastroModel
    {
        [Required(ErrorMessage = "Nome e obrigatorio.")]
        public string Nome { get; set; } = string.Empty;

        [CpfCnpj(ErrorMessage = "Documento informado nao e um CPF ou CNPJ valido.")]
        public string Documento { get; set; } = string.Empty;

        public bool PessoaJuridica { get; set; }

        [RequiredIf("PessoaJuridica", true, ErrorMessage = "Inscricao Estadual e obrigatoria para Pessoa Juridica.")]
        public string? InscricaoEstadual { get; set; }
    }
}

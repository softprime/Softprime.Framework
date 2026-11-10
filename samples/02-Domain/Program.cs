using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Softprime.Framework.Domain;
using Softprime.Framework.Domain.DAL;

namespace Softprime.Samples.Domain
{
    public static class Program
    {
        public static int Main(string[] args)
        {
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
            Console.WriteLine(" Softprime.Framework — Sample 02-Domain (Demonstracao Didatica DDD)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine();

            // 1. Entidades com Identidade Persistida (Entity)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 1. Entidades Persistidas: Igualdade por ID (Entity) ---");
            Console.ResetColor();

            var clientePersistido1 = new Cliente("Empresa Alfa", "11222333000199");
            clientePersistido1.SetId(42L);

            var clientePersistido2 = new Cliente("Alfa Tecnologia Ltda", "99888777000100");
            clientePersistido2.SetId(42L); // Mesmo ID, atributos mutáveis diferentes

            var clientePersistido3 = new Cliente("Empresa Beta", "11222333000199");
            clientePersistido3.SetId(99L); // ID diferente

            Console.WriteLine($"Cliente 1 (Id: {clientePersistido1.Id}, Nome: '{clientePersistido1.Nome}')");
            Console.WriteLine($"Cliente 2 (Id: {clientePersistido2.Id}, Nome: '{clientePersistido2.Nome}')");
            Console.WriteLine($"Cliente 1 e transiente? {clientePersistido1.IsTransient()}");
            Console.WriteLine($"Cliente 1.Equals(Cliente 2) [mesmo Id=42]: {clientePersistido1.Equals(clientePersistido2)} (Esperado: True)");
            Console.WriteLine($"Cliente 1.Equals(Cliente 3) [Ids 42 vs 99]: {clientePersistido1.Equals(clientePersistido3)} (Esperado: False)");
            Console.WriteLine();

            // 2. Entidades Transientes e Assinatura de Domínio (GetTypeSpecificSignatureProperties)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 2. Entidades Transientes: Igualdade por Assinatura de Dominio ---");
            Console.ResetColor();

            var clienteNovo1 = new Cliente("Carlos Silva", "12345678901"); // Id null
            var clienteNovo2 = new Cliente("Carlos S.", "12345678901");     // Id null, mesmo Documento na assinatura
            var clienteNovo3 = new Cliente("Mariana Souza", "98765432100"); // Id null, Documento diferente

            Console.WriteLine($"Cliente Novo 1 e transiente? {clienteNovo1.IsTransient()}");
            Console.WriteLine($"Cliente Novo 2 e transiente? {clienteNovo2.IsTransient()}");
            Console.WriteLine($"Novo 1.Equals(Novo 2) [mesmo Documento de assinatura]: {clienteNovo1.Equals(clienteNovo2)} (Esperado: True)");
            Console.WriteLine($"Novo 1.Equals(Novo 3) [Documentos distintos]: {clienteNovo1.Equals(clienteNovo3)} (Esperado: False)");
            Console.WriteLine($"Novo 1.Equals(Cliente Persistido 1) [Transiente vs Persistido]: {clienteNovo1.Equals(clientePersistido1)} (Esperado: False)");
            Console.WriteLine();

            // 3. Entidades com Identificador Tipado Customizado (EntityWithTypedId<Guid>)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 3. Entidades com Chave Tipada Customizada (EntityWithTypedId<Guid>) ---");
            Console.ResetColor();

            var guidFixo = Guid.NewGuid();
            var produtoA = new Produto(guidFixo, "Notebook Pro 16", "SKU-NB-001");
            var produtoB = new Produto(guidFixo, "Notebook Pro 16 Atualizado", "SKU-NB-001");

            Console.WriteLine($"Produto A (Id: {produtoA.Id}, Sku: {produtoA.Sku})");
            Console.WriteLine($"Produto B (Id: {produtoB.Id}, Sku: {produtoB.Sku})");
            Console.WriteLine($"Produto A e transiente? {produtoA.IsTransient()}");
            Console.WriteLine($"Produto A.Equals(Produto B) [mesmo Guid]: {produtoA.Equals(produtoB)} (Esperado: True)");
            Console.WriteLine();

            // 4. Objetos de Valor (Value Objects) e Igualdade Estrutural
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 4. Objetos de Valor (ValueObject: Igualdade Estrutural por Propriedades) ---");
            Console.ResetColor();

            var precoOriginal = new Dinheiro(150.50m, "BRL");
            var precoCopia = new Dinheiro(150.50m, "BRL");
            var precoDolar = new Dinheiro(150.50m, "USD");
            var precoOutroValor = new Dinheiro(200.00m, "BRL");

            Console.WriteLine($"Preco 1: {precoOriginal}");
            Console.WriteLine($"Preco 2: {precoCopia}");
            Console.WriteLine($"Preco 1 == Preco 2 (mesmos atributos): {precoOriginal == precoCopia} (Esperado: True)");
            Console.WriteLine($"Preco 1.Equals(Preco 2): {precoOriginal.Equals(precoCopia)} (Esperado: True)");
            Console.WriteLine($"Preco 1 == Preco Dolar (moeda diferente): {precoOriginal == precoDolar} (Esperado: False)");
            Console.WriteLine($"Preco 1 != Preco Outro (valor diferente): {precoOriginal != precoOutroValor} (Esperado: True)");
            Console.WriteLine();

            // 5. Autovalidação Integrada (ValidatableObject)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 5. Autovalidacao Integrada de Entidades (ValidatableObject) ---");
            Console.ResetColor();

            var clienteValido = new Cliente("Empresa Exemplo", "12345678901") { Email = "contato@empresa.com" };
            var clienteInvalido = new Cliente("", "") { Email = "email_invalido_sem_arroba" };

            Console.WriteLine($"Cliente Valido -> IsValid(): {clienteValido.IsValid()}");
            Console.WriteLine($"Cliente Invalido -> IsValid(): {clienteInvalido.IsValid()}");

            if (!clienteInvalido.IsValid())
            {
                Console.WriteLine("Erros de validacao detectados:");
                foreach (var err in clienteInvalido.ValidationResults())
                {
                    Console.WriteLine($"   * {err.ErrorMessage} (Membros: {string.Join(", ", err.MemberNames)})");
                }
            }
            Console.WriteLine();

            // 6. Lifecycle Hooks (BeforeSave, AfterSave)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- 6. Ciclo de Vida da Entidade (Lifecycle Hooks) ---");
            Console.ResetColor();

            var novoContrato = new Contrato("Prestacao de Servicos Cloud");
            Console.WriteLine($"Antes do BeforeSave() -> DataCadastro: '{novoContrato.DataCadastro}', Ativo: {novoContrato.Ativo}");
            novoContrato.BeforeSave();
            Console.WriteLine($"Apos o BeforeSave() -> DataCadastro: '{novoContrato.DataCadastro}', Ativo: {novoContrato.Ativo}");
            novoContrato.AfterSave();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCESSO] Demonstracao de Domain Driven Design concluida com 100% de exito!");
            Console.ResetColor();
            return 0;
        }

        private static int RunSmokeTest()
        {
            try
            {
                // Assertions para Entidades Persistidas
                var c1 = new Cliente("A", "111"); c1.SetId(10L);
                var c2 = new Cliente("B", "222"); c2.SetId(10L);
                var c3 = new Cliente("A", "111"); c3.SetId(20L);

                if (c1.IsTransient()) throw new InvalidOperationException("Entidade com Id persistido nao deve ser transiente.");
                if (!c1.Equals(c2)) throw new InvalidOperationException("Entidades com o mesmo Id devem ser iguais.");
                if (c1.Equals(c3)) throw new InvalidOperationException("Entidades com Ids distintos nao devem ser iguais.");

                // Assertions para Entidades Transientes e DomainSignature
                var t1 = new Cliente("X", "123456"); // Id null
                var t2 = new Cliente("Y", "123456"); // Id null, mesmo Documento na assinatura
                var t3 = new Cliente("X", "999999"); // Id null, Documento diferente

                if (!t1.IsTransient()) throw new InvalidOperationException("Entidade sem Id deve ser transiente.");
                if (!t1.Equals(t2)) throw new InvalidOperationException("Entidades transientes com mesma assinatura de dominio devem ser iguais.");
                if (t1.Equals(t3)) throw new InvalidOperationException("Entidades transientes com assinaturas distintas nao devem ser iguais.");
                if (t1.Equals(c1)) throw new InvalidOperationException("Entidade transiente nao deve ser igual a entidade persistida.");

                // Assertions para ValueObject
                var v1 = new Dinheiro(100m, "BRL");
                var v2 = new Dinheiro(100m, "BRL");
                var v3 = new Dinheiro(200m, "BRL");

                if (!(v1 == v2)) throw new InvalidOperationException("Operador == em ValueObject falhou.");
                if (v1 == v3) throw new InvalidOperationException("ValueObjects com valores distintos nao devem ser iguais.");
                if (!(v1 != v3)) throw new InvalidOperationException("Operador != em ValueObject falhou.");
                if (!v1.Equals(v2)) throw new InvalidOperationException("Equals em ValueObject falhou.");

                // Assertions para ValidatableObject
                var cv = new Cliente("Nome Ok", "12345678901") { Email = "teste@softprime.com" };
                if (!cv.IsValid()) throw new InvalidOperationException("Cliente valido falhou no IsValid().");

                var ci = new Cliente("", "");
                if (ci.IsValid() || ci.ValidationResults().Count == 0)
                    throw new InvalidOperationException("Cliente invalido nao deve passar no IsValid().");

                // Assertions para Lifecycle Hooks
                var hookObj = new Contrato("Teste");
                hookObj.BeforeSave();
                if (string.IsNullOrEmpty(hookObj.DataCadastro))
                    throw new InvalidOperationException("BeforeSave nao populou DataCadastro.");

                Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.Domain: Todas as assercoes DDD passaram com sucesso.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SMOKE_TEST_FAILED] Falha: {ex.Message}");
                return 1;
            }
        }
    }

    #region Modelos e Value Objects de Exemplo

    /// <summary>
    /// Exemplo de Entidade de Negócio com Id padrão (long?).
    /// </summary>
    public class Cliente : Entity
    {
        [Required(ErrorMessage = "Nome e obrigatorio.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Nome deve ter entre 2 e 100 caracteres.")]
        public string Nome { get; set; }

        /// <summary>
        /// Define a identidade de negócio da entidade quando ela ainda não possui Id de banco (transiente).
        /// </summary>
        [Required(ErrorMessage = "Documento e obrigatorio.")]
        public string Documento { get; set; }

        [EmailAddress(ErrorMessage = "Formato de e-mail invalido.")]
        public string? Email { get; set; }

        public Cliente(string nome, string documento)
        {
            Nome = nome;
            Documento = documento;
        }

        // Construtor sem parâmetros para frameworks de serialização/ORM
        protected Cliente()
        {
            Nome = string.Empty;
            Documento = string.Empty;
        }

        /// <summary>
        /// Sobrescreve a assinatura de domínio para identificar entidades transientes com base no Documento.
        /// </summary>
        protected override IEnumerable<PropertyInfo> GetTypeSpecificSignatureProperties()
        {
            yield return typeof(Cliente).GetProperty(nameof(Documento))!;
        }
    }

    /// <summary>
    /// Exemplo de Entidade com Chave Tipada Customizada (Guid).
    /// </summary>
    public class Produto : EntityWithTypedId<Guid>
    {
        [Required]
        public string Descricao { get; set; }

        [Required]
        public string Sku { get; set; }

        public Produto(Guid id, string descricao, string sku)
        {
            SetId(id);
            Descricao = descricao;
            Sku = sku;
        }

        protected Produto()
        {
            Descricao = string.Empty;
            Sku = string.Empty;
        }

        protected override IEnumerable<PropertyInfo> GetTypeSpecificSignatureProperties()
        {
            yield return typeof(Produto).GetProperty(nameof(Sku))!;
        }
    }

    /// <summary>
    /// Exemplo clássico de Value Object: a igualdade e determinada pela combinação
    /// de TODAS as suas propriedades (Quantia e Moeda), sem conceito de identidade/Id.
    /// </summary>
    public class Dinheiro : ValueObject
    {
        public decimal Quantia { get; }
        public string Moeda { get; }

        public Dinheiro(decimal quantia, string moeda)
        {
            Quantia = quantia;
            Moeda = moeda;
        }

        public override string ToString()
        {
            return $"{Moeda} {Quantia:N2}";
        }
    }

    /// <summary>
    /// Exemplo de Entidade demonstrando interceptação de Lifecycle Hooks.
    /// </summary>
    public class Contrato : Entity
    {
        [Required]
        public string Titulo { get; set; }

        public string DataCadastro { get; private set; } = string.Empty;
        public bool Ativo { get; private set; }

        public Contrato(string titulo)
        {
            Titulo = titulo;
        }

        protected override IEnumerable<PropertyInfo> GetTypeSpecificSignatureProperties()
        {
            yield return typeof(Contrato).GetProperty(nameof(Titulo))!;
        }

        public override void BeforeSave()
        {
            base.BeforeSave();
            DataCadastro = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            Ativo = true;
        }
    }

    #endregion
}

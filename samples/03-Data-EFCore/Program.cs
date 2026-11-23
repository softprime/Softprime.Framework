using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.DAL;
using Softprime.Framework.Domain.DAL;
using Softprime.Framework.EFCore5;

namespace Softprime.Samples.DataEFCore
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool isSmokeTest = args != null && Array.Exists(args, a => a.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase));

            if (isSmokeTest)
            {
                return await RunSmokeTestAsync();
            }

            return await RunInteractiveDemoAsync();
        }

        private static string CreateTempDbPath()
        {
            return Path.Combine(Path.GetTempPath(), $"softprime_sample_03_{Guid.NewGuid():N}.db");
        }

        private static DbContextOptions<SampleDbContext> CreateOptions(string dbPath)
        {
            return new DbContextOptionsBuilder<SampleDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
        }

        private static void CleanupTempDb(string dbPath)
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch
                {
                    // Ignora falha eventual de lock residual em limpeza temporaria
                }
            }
        }

        private static async Task<int> RunInteractiveDemoAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" Softprime.Framework — Sample 03-Data-EFCore (Repository, UoW e SQLite Local)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine();

            string dbPath = CreateTempDbPath();
            Console.WriteLine($"[INFO] Banco SQLite efemero criado em: {dbPath}");
            Console.WriteLine("[NOTA] Usando provisoriamente Softprime.Framework.EFCore5 ate entrega do EFCore10 (#5).");
            Console.WriteLine();

            try
            {
                var options = CreateOptions(dbPath);

                // Cria o schema no SQLite temporario
                using (var initContext = new SampleDbContext(options))
                {
                    await initContext.Database.EnsureCreatedAsync();
                }

                // 1. Create (Save / SaveAsync + UnitOfWork.SaveChanges)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- 1. Create: Insercao via IRepository<Produto> e IUnitOfWork ---");
                Console.ResetColor();

                long primeiroId;
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();

                    var p1 = new Produto("SKU-001", "Notebook Corporativo 14\"", "Eletronicos", 5499.90m, true);
                    var p2 = new Produto("SKU-002", "Mouse Sem Fio Ergonomico", "Perifericos", 189.00m, true);
                    var p3 = new Produto("SKU-003", "Teclado Mecanico ABNT2", "Perifericos", 349.50m, true);
                    var p4 = new Produto("SKU-004", "Monitor 27\" 4K IPS", "Eletronicos", 2199.00m, true);
                    var p5 = new Produto("SKU-005", "Webcam Full HD", "Perifericos", 279.00m, true);
                    var p6 = new Produto("SKU-006", "Cabo VGA Legado (Descontinuado)", "Acessorios", 25.00m, false);

                    Console.WriteLine($"Antes de salvar: '{p1.Nome}' -> IsTransient() = {p1.IsTransient()}");

                    await repo.SaveAsync(p1);
                    repo.Save(p2);
                    repo.Save(p3);
                    repo.Save(p4);
                    repo.Save(p5);
                    repo.Save(p6);

                    uow.SaveChanges();
                    primeiroId = p1.Id!.Value;

                    Console.WriteLine($"Apos SaveChanges: '{p1.Nome}' -> Id = {p1.Id}, IsTransient() = {p1.IsTransient()}");
                    Console.WriteLine($"Total inserido com sucesso: {repo.All().Count()} produtos.");
                }
                Console.WriteLine();

                // 2. Read & Paginacao
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- 2. Read & Paginacao: Get, GetAsync, All e Find paginado ---");
                Console.ResetColor();

                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();

                    // Busca por chave (long)
                    var porId = repo.Get(primeiroId);
                    Console.WriteLine($"Get(id={primeiroId}): {porId.Codigo} - {porId.Nome} (R$ {porId.Preco:N2})");

                    // Busca assincrona por predicado
                    var porCodigo = await repo.GetAsync(p => p.Codigo == "SKU-004");
                    Console.WriteLine($"GetAsync(SKU-004): {porCodigo.Nome} (Categoria: {porCodigo.Categoria})");

                    // Paginacao com Find(filter, out total, index, size)
                    int totalGlobalAtivos = repo.All().Count(p => p.Ativo);
                    var pagina0 = repo.Find(p => p.Ativo, out int countPagina0, index: 0, size: 2).ToList();
                    var pagina1 = repo.Find(p => p.Ativo, out int countPagina1, index: 1, size: 2).ToList();

                    Console.WriteLine($"Produtos ativos (total global = {totalGlobalAtivos}):");
                    Console.WriteLine($"  Pagina 0 (size=2, retornados={countPagina0}): {string.Join(", ", pagina0.Select(p => p.Codigo))}");
                    Console.WriteLine($"  Pagina 1 (size=2, retornados={countPagina1}): {string.Join(", ", pagina1.Select(p => p.Codigo))}");
                }
                Console.WriteLine();

                // 3. Update (Update + SaveChanges)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- 3. Update: Atualizacao de Entidade Existente ---");
                Console.ResetColor();

                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    var mouse = repo.Get(p => p.Codigo == "SKU-002");
                    decimal precoAnterior = mouse.Preco;
                    mouse.Preco = 199.90m;

                    repo.Update(mouse);
                    uow.SaveChanges();

                    Console.WriteLine($"Produto {mouse.Codigo} atualizado: R$ {precoAnterior:N2} -> R$ {mouse.Preco:N2}");
                }
                Console.WriteLine();

                // 4. Transacoes: Rollback vs Commit
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- 4. Transacoes: Rollback em caso de erro e Commit em sucesso ---");
                Console.ResetColor();

                // 4a. Cenario de Rollback
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                using (var tx = await uow.DbContext.Database.BeginTransactionAsync())
                {
                    var repo = uow.Repository<Produto>();
                    var teclado = repo.Get(p => p.Codigo == "SKU-003");
                    decimal precoOriginal = teclado.Preco;

                    try
                    {
                        teclado.Preco = 9999.99m;
                        repo.Update(teclado);
                        uow.SaveChanges();

                        // Simula falha de regra de negocio antes de finalizar a transacao
                        throw new InvalidOperationException("Falha simulada durante processamento do lote!");
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync();
                        Console.WriteLine($"[Rollback] Excecao capturada ({ex.Message}) -> Transacao revertida.");
                    }
                }

                // Confirma que o Rollback preservou o valor original no banco
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var teclado = uow.Repository<Produto>().Get(p => p.Codigo == "SKU-003");
                    Console.WriteLine($"Verificacao pos-rollback SKU-003: Preco mantido em R$ {teclado.Preco:N2} (Esperado: R$ 349,50)");
                }

                // 4b. Cenario de Commit
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                using (var tx = await uow.DbContext.Database.BeginTransactionAsync())
                {
                    var repo = uow.Repository<Produto>();
                    var teclado = repo.Get(p => p.Codigo == "SKU-003");
                    teclado.Preco = 329.90m;
                    repo.Update(teclado);
                    uow.SaveChanges();
                    await tx.CommitAsync();
                    Console.WriteLine($"[Commit] Novo preco de SKU-003 confirmado em transacao: R$ {teclado.Preco:N2}");
                }
                Console.WriteLine();

                // 5. Delete (Delete + SaveChanges)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--- 5. Delete: Remocao por Predicado ---");
                Console.ResetColor();

                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    repo.Delete(p => !p.Ativo);
                    uow.SaveChanges();

                    int restantes = repo.All().Count();
                    Console.WriteLine($"Produtos inativos removidos. Restantes no banco: {restantes} produtos.");
                }
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SUCESSO] Demonstracao de Persistencia EF Core concluida com 100% de exito!");
                Console.ResetColor();
                return 0;
            }
            finally
            {
                CleanupTempDb(dbPath);
                Console.WriteLine($"[CLEANUP] Banco SQLite temporario descartado: {dbPath}");
            }
        }

        private static async Task<int> RunSmokeTestAsync()
        {
            string dbPath = CreateTempDbPath();
            try
            {
                var options = CreateOptions(dbPath);

                using (var initContext = new SampleDbContext(options))
                {
                    await initContext.Database.EnsureCreatedAsync();
                }

                long savedId;
                // 1. Teste de Create + IsTransient
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    var p1 = new Produto("SMK-01", "Item 1", "CatA", 10m, true);
                    var p2 = new Produto("SMK-02", "Item 2", "CatA", 20m, true);
                    var p3 = new Produto("SMK-03", "Item 3", "CatA", 30m, true);
                    var p4 = new Produto("SMK-04", "Item 4", "CatB", 40m, false);

                    if (!p1.IsTransient()) throw new InvalidOperationException("Entidade recem-criada deve ser transiente.");

                    await repo.SaveAsync(p1);
                    repo.Save(p2);
                    repo.Save(p3);
                    repo.Save(p4);
                    uow.SaveChanges();

                    if (p1.IsTransient() || p1.Id == null)
                        throw new InvalidOperationException("Entidade apos SaveChanges deve ter Id gerado e nao ser transiente.");
                    savedId = p1.Id.Value;
                }

                // 2. Teste de Read + Paginacao
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    var found = await repo.GetAsync(savedId);
                    if (found == null || found.Codigo != "SMK-01")
                        throw new InvalidOperationException("GetAsync por Id nao retornou a entidade esperada.");

                    var page0 = repo.Find(p => p.Ativo, out int count0, index: 0, size: 2).ToList();
                    var page1 = repo.Find(p => p.Ativo, out int count1, index: 1, size: 2).ToList();

                    if (page0.Count != 2 || count0 != 2)
                        throw new InvalidOperationException($"Pagina 0 deveria ter 2 itens, retornou {page0.Count}.");
                    if (page1.Count != 1 || count1 != 1)
                        throw new InvalidOperationException($"Pagina 1 deveria ter 1 item restante, retornou {page1.Count}.");
                }

                // 3. Teste de Update + Transacao (Rollback e Commit)
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                using (var tx = await uow.DbContext.Database.BeginTransactionAsync())
                {
                    var repo = uow.Repository<Produto>();
                    var item = repo.Get(p => p.Codigo == "SMK-02");
                    item.Preco = 999m;
                    repo.Update(item);
                    uow.SaveChanges();
                    await tx.RollbackAsync();
                }

                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    var item = repo.Get(p => p.Codigo == "SMK-02");
                    if (item.Preco != 20m)
                        throw new InvalidOperationException($"Rollback falhou: preco esperado 20, encontrado {item.Preco}.");

                    using var tx = await uow.DbContext.Database.BeginTransactionAsync();
                    item.Preco = 25m;
                    repo.Update(item);
                    uow.SaveChanges();
                    await tx.CommitAsync();
                }

                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var item = uow.Repository<Produto>().Get(p => p.Codigo == "SMK-02");
                    if (item.Preco != 25m)
                        throw new InvalidOperationException($"Commit falhou: preco esperado 25, encontrado {item.Preco}.");
                }

                // 4. Teste de Delete
                using (var uow = new SampleUnitOfWork(new SampleDbContext(options)))
                {
                    var repo = uow.Repository<Produto>();
                    repo.Delete(p => !p.Ativo);
                    uow.SaveChanges();

                    if (repo.All().Count() != 3)
                        throw new InvalidOperationException("Delete por predicado falhou.");
                }

                Console.WriteLine("[SMOKE_TEST_PASSED] Softprime.Samples.DataEFCore: CRUD, paginacao e transacoes validados em SQLite.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SMOKE_TEST_FAILED] Falha: {ex.Message}");
                return 1;
            }
            finally
            {
                CleanupTempDb(dbPath);
            }
        }
    }

    #region Entidade, Contexto e UnitOfWork de Exemplo

    public class Produto : Entity
    {
        [Required]
        public string Codigo { get; set; }

        [Required]
        public string Nome { get; set; }

        [Required]
        public string Categoria { get; set; }

        public decimal Preco { get; set; }
        public bool Ativo { get; set; }

        public Produto(string codigo, string nome, string categoria, decimal preco, bool ativo)
        {
            Codigo = codigo;
            Nome = nome;
            Categoria = categoria;
            Preco = preco;
            Ativo = ativo;
        }

        protected Produto()
        {
            Codigo = string.Empty;
            Nome = string.Empty;
            Categoria = string.Empty;
        }

        protected override IEnumerable<PropertyInfo> GetTypeSpecificSignatureProperties()
        {
            yield return typeof(Produto).GetProperty(nameof(Codigo))!;
        }
    }

    public class SampleDbContext : DbContext
    {
        public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
        {
        }

        public DbSet<Produto> Produtos => Set<Produto>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Produto>(entity =>
            {
                entity.ToTable("Produtos");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Id).ValueGeneratedOnAdd();
                entity.HasIndex(p => p.Codigo).IsUnique();
                entity.Property(p => p.Codigo).IsRequired().HasMaxLength(50);
                entity.Property(p => p.Nome).IsRequired().HasMaxLength(200);
                entity.Property(p => p.Categoria).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Preco).HasPrecision(18, 2);
            });
        }
    }

    public class SampleUnitOfWork : EFUnitOfWork<SampleDbContext>
    {
        public SampleUnitOfWork(SampleDbContext context) : base(context)
        {
        }
    }

    #endregion
}

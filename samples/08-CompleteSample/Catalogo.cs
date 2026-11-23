using Microsoft.EntityFrameworkCore;
using Softprime.Framework.Domain.DAL;

namespace Softprime.Samples.CompleteSample;

public sealed class Empresa : Entity
{
    public string TenantKey { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
}

public sealed class Usuario : Entity
{
    public string TenantKey { get; set; } = string.Empty;
    public long EmpresaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class Produto : Entity
{
    public string TenantKey { get; set; } = string.Empty;
    public long EmpresaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
}

internal sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>().Property(empresa => empresa.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<Usuario>().Property(usuario => usuario.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<Produto>().Property(produto => produto.Id).ValueGeneratedOnAdd();

        // A chave composta protege o vínculo mesmo se uma consulta esquecer o filtro de tenant.
        modelBuilder.Entity<Usuario>().HasOne<Empresa>().WithMany()
            .HasForeignKey(usuario => new { usuario.TenantKey, usuario.EmpresaId })
            .HasPrincipalKey(empresa => new { empresa.TenantKey, empresa.Id });
        modelBuilder.Entity<Produto>().HasOne<Empresa>().WithMany()
            .HasForeignKey(produto => new { produto.TenantKey, produto.EmpresaId })
            .HasPrincipalKey(empresa => new { empresa.TenantKey, empresa.Id });
    }
}

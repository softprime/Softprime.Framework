using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Softprime.Framework.Api;
using Softprime.Framework.Api.Dto.Default;
using Softprime.Framework.DAL;
using Softprime.Framework.Mailer;
using Softprime.Framework.Multitenancy;

namespace Softprime.Samples.CompleteSample;

[ApiController]
[Route("api")]
[ApiKeyAuthorizeFilter("ApiKey", Program.ApiKey)]
public sealed class CatalogoController(IUnitOfWork unitOfWork, ITenant<IAppTenant> tenant, ISoftprimeMailer mailer)
    : ControllerBase, IApi<ProdutoDto, long>
{
    private string TenantKey => tenant.Value.TenantKey;

    // Toda busca por ID inclui o tenant resolvido. O DTO nunca define o tenant salvo.
    private IQueryable<Produto> Produtos => unitOfWork.Repository<Produto>()
        .Find(produto => produto.TenantKey == TenantKey);

    [HttpPost("empresas")]
    public IActionResult CriarEmpresa(EmpresaDto dto)
    {
        var empresa = unitOfWork.Repository<Empresa>().Save(new Empresa
        {
            TenantKey = TenantKey,
            Nome = dto.Nome
        });
        unitOfWork.SaveChanges();

        return CreatedAtAction(nameof(LerEmpresa), new { id = empresa.Id },
            new EmpresaDto { Id = empresa.Id!.Value, Nome = empresa.Nome });
    }

    [HttpGet("empresas/{id:long}")]
    public async Task<IActionResult> LerEmpresa(long id)
    {
        var empresa = await BuscarEmpresaAsync(id);
        return empresa is null
            ? NotFound()
            : Ok(new EmpresaDto { Id = empresa.Id!.Value, Nome = empresa.Nome });
    }

    [HttpPost("usuarios")]
    public async Task<IActionResult> CriarUsuario(UsuarioDto dto)
    {
        if (!MailAddress.TryCreate(dto.Email, out var destinatario))
        {
            ModelState.AddModelError(nameof(dto.Email), "Informe um endereço de e-mail válido.");
            return ValidationProblem(ModelState);
        }

        var empresa = await BuscarEmpresaAsync(dto.EmpresaId);
        if (empresa is null)
            return NotFound();

        var usuario = unitOfWork.Repository<Usuario>().Save(new Usuario
        {
            TenantKey = TenantKey,
            EmpresaId = dto.EmpresaId,
            Nome = dto.Nome,
            Email = dto.Email
        });
        unitOfWork.SaveChanges();

        await mailer.Send("Templates.BoasVindas", new BoasVindasModel
        {
            To = destinatario,
            Nome = usuario.Nome,
            Empresa = empresa.Nome
        }, "Bem-vindo ao cadastro");

        return CreatedAtAction(nameof(LerUsuario), new { id = usuario.Id }, ParaDto(usuario));
    }

    [HttpGet("usuarios/{id:long}")]
    public async Task<IActionResult> LerUsuario(long id)
    {
        var usuario = await unitOfWork.Repository<Usuario>()
            .Find(usuario => usuario.TenantKey == TenantKey && usuario.Id == id)
            .SingleOrDefaultAsync();
        return usuario is null ? NotFound() : Ok(ParaDto(usuario));
    }

    [HttpGet("produtos")]
    public Task<IActionResult> ListarProdutos(int? page, int? size)
        => page.HasValue || size.HasValue ? Get(page ?? 0, size ?? 10) : Get();

    [NonAction]
    public async Task<IActionResult> Get()
        => Ok(await Produtos.OrderBy(produto => produto.Id).Select(ProdutoDto.Projection).ToListAsync());

    [NonAction]
    public async Task<IActionResult> Get(int page, int size)
    {
        if (page < 0 || size is < 1 or > 100 || page > int.MaxValue / size)
            return BadRequest();

        var total = await Produtos.CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)size);
        return Ok(new PagedResultDto<ProdutoDto>
        {
            PageIndex = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = totalPages,
            HasPreviousPage = page > 0,
            HasNextPage = page < totalPages - 1,
            Results = await Produtos.OrderBy(produto => produto.Id).Skip(page * size).Take(size)
                .Select(ProdutoDto.Projection).ToListAsync()
        });
    }

    [HttpGet("produtos/{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var produto = await Produtos.Where(produto => produto.Id == id)
            .Select(ProdutoDto.Projection).SingleOrDefaultAsync();
        return produto is null ? NotFound() : Ok(produto);
    }

    [HttpPost("produtos")]
    public async Task<IActionResult> Post(ProdutoDto dto)
    {
        if (await BuscarEmpresaAsync(dto.EmpresaId) is null)
            return NotFound();

        var produto = unitOfWork.Repository<Produto>().Save(new Produto
        {
            TenantKey = TenantKey,
            EmpresaId = dto.EmpresaId,
            Nome = dto.Nome,
            Preco = dto.Preco
        });
        unitOfWork.SaveChanges();

        dto.Id = produto.Id!.Value;
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("produtos/{id:long}")]
    public async Task<IActionResult> Put(long id, ProdutoDto dto)
    {
        var produto = await Produtos.SingleOrDefaultAsync(produto => produto.Id == id);
        if (produto is null || await BuscarEmpresaAsync(dto.EmpresaId) is null)
            return NotFound();

        produto.EmpresaId = dto.EmpresaId;
        produto.Nome = dto.Nome;
        produto.Preco = dto.Preco;
        unitOfWork.SaveChanges();

        dto.Id = id;
        return Ok(dto);
    }

    [HttpDelete("produtos/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        var produto = await Produtos.SingleOrDefaultAsync(produto => produto.Id == id);
        if (produto is null)
            return NotFound();

        unitOfWork.Repository<Produto>().Delete(produto);
        unitOfWork.SaveChanges();
        return NoContent();
    }

    private async Task<Empresa?> BuscarEmpresaAsync(long id) => await unitOfWork.Repository<Empresa>()
        .Find(empresa => empresa.TenantKey == TenantKey && empresa.Id == id).SingleOrDefaultAsync();

    private static UsuarioDto ParaDto(Usuario usuario) => new()
    {
        Id = usuario.Id!.Value,
        EmpresaId = usuario.EmpresaId,
        Nome = usuario.Nome,
        Email = usuario.Email
    };
}

public sealed class EmpresaDto : DtoBase<long>
{
    [Required, StringLength(120)]
    public string Nome { get; set; } = string.Empty;
}

public sealed class UsuarioDto : DtoBase<long>
{
    [Range(1, long.MaxValue)]
    public long EmpresaId { get; set; }

    [Required, StringLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class ProdutoDto : DtoBase<long>
{
    [Range(1, long.MaxValue)]
    public long EmpresaId { get; set; }

    [Required, StringLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true)]
    public decimal Preco { get; set; }

    internal static readonly System.Linq.Expressions.Expression<Func<Produto, ProdutoDto>> Projection =
        produto => new ProdutoDto
        {
            Id = produto.Id!.Value,
            EmpresaId = produto.EmpresaId,
            Nome = produto.Nome,
            Preco = produto.Preco
        };
}

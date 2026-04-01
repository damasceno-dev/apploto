using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;

namespace server.Infrastructure;

internal class LotoDbContext : DbContext
{
    public LotoDbContext(DbContextOptions options) : base(options){}
    public DbSet<Fluxo> Fluxos { get; set; }
    public DbSet<FluxoConta> FluxoContas { get; set; }
    public DbSet<FluxoClassificação> FluxoClassificações { get; set; }
    public DbSet<FluxoDetalhamento> FluxoDetalhamentos { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
}
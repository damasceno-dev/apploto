using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Name).HasMaxLength(255);
        builder.Property(user => user.Email).HasMaxLength(255);
        builder.Property(user => user.Password).HasMaxLength(255);

        builder.HasIndex(user => user.Email).IsUnique();

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(refreshToken => refreshToken.User)
            .HasForeignKey(refreshToken => refreshToken.UserId);

        builder.HasMany(user => user.BranchUsers)
            .WithOne(branchUser => branchUser.User)
            .HasForeignKey(branchUser => branchUser.UserId);
    }
}

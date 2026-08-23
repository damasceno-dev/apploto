using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Domain.Entities;

namespace server.Infrastructure.Configurations;

internal sealed class IdempotencyRequestConfiguration : IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("IdempotencyRequests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Key).HasMaxLength(128).IsRequired();
        builder.Property(request => request.Endpoint).HasMaxLength(100).IsRequired();
        builder.Property(request => request.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(request => request.ResponseEnvelope).HasColumnType("text").IsRequired();
        builder.Property(request => request.ExpiresAt).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne(request => request.Branch)
            .WithMany()
            .HasForeignKey(request => request.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.User)
            .WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new { request.Endpoint, request.BranchId, request.UserId, request.Key })
            .IsUnique()
            .HasDatabaseName("IX_IdempotencyRequests_Endpoint_BranchId_UserId_Key");
        builder.HasIndex(request => request.ExpiresAt);
    }
}

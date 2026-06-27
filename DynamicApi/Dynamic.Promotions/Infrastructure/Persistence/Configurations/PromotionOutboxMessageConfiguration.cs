using Dynamic.Promotions.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionOutboxMessageConfiguration : IEntityTypeConfiguration<PromotionOutboxMessage>
{
    public void Configure(EntityTypeBuilder<PromotionOutboxMessage> builder)
    {
        builder.ToTable("promotion_outbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(64).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("longtext").IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.HasIndex(message => new { message.Status, message.AvailableAtUtc });
        builder.HasIndex(message => new { message.AggregateId, message.Type }).IsUnique();
    }
}

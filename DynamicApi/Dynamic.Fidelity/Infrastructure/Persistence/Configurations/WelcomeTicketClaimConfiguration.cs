using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class WelcomeTicketClaimConfiguration : IEntityTypeConfiguration<WelcomeTicketClaim>
{
    public void Configure(EntityTypeBuilder<WelcomeTicketClaim> builder)
    {
        builder.ToTable("fidelity_welcome_ticket_claims");
        builder.HasKey(claim => new { claim.UserId, claim.NegocioId });
        builder.Property(claim => claim.TicketId).IsRequired();
        builder.Property(claim => claim.CreatedAtUtc).IsRequired();
    }
}

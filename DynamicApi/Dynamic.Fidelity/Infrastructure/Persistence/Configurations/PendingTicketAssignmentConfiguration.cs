using Dynamic.Fidelity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dynamic.Fidelity.Infrastructure.Persistence.Configurations;

public class PendingTicketAssignmentConfiguration : IEntityTypeConfiguration<PendingTicketAssignment>
{
    public void Configure(EntityTypeBuilder<PendingTicketAssignment> builder)
    {
        builder.ToTable("fidelity_pending_ticket_assignments");
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.QrToken).HasMaxLength(128).IsRequired();

        builder.HasIndex(assignment => assignment.UserId);
        builder.HasIndex(assignment => assignment.QrCampaignId);
        builder.HasIndex(assignment => assignment.Activated);
        builder.HasIndex(assignment => new { assignment.UserId, assignment.QrCampaignId }).IsUnique();
    }
}

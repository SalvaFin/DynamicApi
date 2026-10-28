namespace Dynamic.Fidelity.Domain.Entities;

public class PointsAccrualReason
{
    public const string DefaultName = "Paso por caja";

    public Guid Id { get; set; }
    public Guid NegocioId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool UniquePerCustomer { get; set; }
    public bool Active { get; set; } = true;
    public bool IsDefault => Id == NegocioId;

    // A business created after the migration also has this reason immediately.
    public static PointsAccrualReason Default(Guid negocioId) => new()
    {
        Id = negocioId, NegocioId = negocioId, Name = DefaultName
    };
}

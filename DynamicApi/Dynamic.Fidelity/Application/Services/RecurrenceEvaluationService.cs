using System.Text.Json;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Dynamic.Fidelity.Domain.Entities;
using Dynamic.Fidelity.Domain.Enums;
using Dynamic.Fidelity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dynamic.Fidelity.Application.Services;

public sealed record AppliedRecurrenceRule(Guid Id, string Name, RecurrenceRuleFamily Family,
    RecurrenceBenefitMode BenefitMode, RecurrenceCompatibility Compatibility, int Threshold,
    int? WindowDays, int? BenefitVisits, int? BenefitDays, decimal Multiplier, int Priority,
    DateTime ActivatedAtUtc, int? MaxActivationsPerCustomer);

public sealed class RecurrenceEvaluationService(DynamicFidelityDbContext db)
{
    public Task<bool> HasActiveRulesAsync(Guid negocioId, CancellationToken cancellationToken)
        => db.RecurrenceRules.AnyAsync(x => x.NegocioId == negocioId && x.Active, cancellationToken);

    public Task<PointsTransaction?> FindReplayAsync(Guid negocioId, Guid key, CancellationToken cancellationToken)
        => db.PointsTransactions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.NegocioId == negocioId && x.ClientOperationId == key, cancellationToken);

    // MariaDB named locks serialize all writers for one business/customer, including an absent points row.
    public async Task<IAsyncDisposable> LockAsync(Guid negocioId, IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        string databaseName = db.Database.GetDbConnection().Database;
        List<string> names = userIds.Distinct().OrderBy(x => x)
            .Select(x => "fv:" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{databaseName}:{negocioId:N}:{x:N}")))[..32]).ToList();
        List<string> acquired = [];
        try
        {
            foreach (string name in names)
            {
                await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK(@lockName, 10)";
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@lockName"; parameter.Value = name;
                command.Parameters.Add(parameter);
                object? value = await command.ExecuteScalarAsync(cancellationToken);
                if (Convert.ToInt32(value) != 1) throw new TimeoutException("No se pudo bloquear la visita del cliente.");
                acquired.Add(name);
            }
            return new VisitLock(db, acquired);
        }
        catch
        {
            await ReleaseAsync(db, acquired);
            throw;
        }
    }

    private static async Task ReleaseAsync(DynamicFidelityDbContext db, IEnumerable<string> names)
    {
        try
        {
            foreach (string name in names.Reverse())
            {
                await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@lockName)";
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@lockName"; parameter.Value = name;
                command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed class VisitLock(DynamicFidelityDbContext db, List<string> names) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(ReleaseAsync(db, names));
    }

    public async Task<RecurrenceResolution> ResolveAsync(Guid negocioId, Guid userId, DateTime atUtc,
        Guid transactionId, CancellationToken cancellationToken, RecurrenceRule? draftRule = null)
    {
        List<RecurrenceRule> rules = await db.RecurrenceRules.AsNoTracking()
            .Where(x => x.NegocioId == negocioId && x.Active)
            .ToListAsync(cancellationToken);
        if (draftRule is not null)
        {
            if (draftRule.Id != Guid.Empty) rules.RemoveAll(x => x.Id == draftRule.Id);
            draftRule.Active = true;
            draftRule.NegocioId = negocioId;
            rules.Add(draftRule);
        }
        var history = await db.PointsTransactions.AsNoTracking()
            .Where(x => x.NegocioId == negocioId && x.UserId == userId && x.PointsAmount > 0 &&
                (x.TransactionType == PointsTransactionType.Earn ||
                 x.TransactionType == PointsTransactionType.BackofficeEarn))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.CreatedAtUtc, x.RecurrenceSnapshotJson })
            .ToListAsync(cancellationToken);
        IEnumerable<RecurrenceVisit> visits = history.Select(x => new RecurrenceVisit(x.Id, x.CreatedAtUtc,
            ReadActivations(x.RecurrenceSnapshotJson)));
        return RecurrenceRuleEngine.Resolve(rules, visits, atUtc, transactionId);
    }

    public static int CalculatePoints(decimal amountEuros, decimal baseRatio, RecurrenceResolution resolution)
        => checked((int)decimal.Ceiling(amountEuros * baseRatio * resolution.Multiplier));

    public static void Stamp(PointsTransaction transaction, decimal baseRatio, int basePoints,
        RecurrenceResolution resolution)
    {
        transaction.BaseRatioSnapshot = baseRatio;
        transaction.BasePointsSnapshot = basePoints;
        transaction.BenefitMultiplierSnapshot = resolution.Multiplier;
        transaction.VisitOrdinalSnapshot = resolution.VisitNumber;
        transaction.RecurrenceSnapshotJson = JsonSerializer.Serialize(resolution.Applied.Select(x => new AppliedRecurrenceRule(
            x.Rule.Id, x.Rule.Name, x.Rule.Family, x.Rule.BenefitMode, x.Rule.Compatibility,
            x.Rule.Threshold, x.Rule.WindowDays, x.Rule.BenefitVisits, x.Rule.BenefitDays,
            x.Rule.Multiplier, x.Rule.Priority, x.ActivatedAtUtc,
            x.Rule.MaxActivationsPerCustomer)));
    }

    private static IReadOnlyDictionary<Guid, DateTime>? ReadActivations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<AppliedRecurrenceRule>>(json)?
                .ToDictionary(x => x.Id, x => x.ActivatedAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

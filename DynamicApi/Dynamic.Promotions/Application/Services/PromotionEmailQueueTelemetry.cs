using Dynamic.Promotions.Application.Models;
using Dynamic.Promotions.Application.Options;
using System.Text.RegularExpressions;

namespace Dynamic.Promotions.Application.Services;

public sealed class PromotionEmailQueueTelemetry
{
    private const int MaxRecentErrors = 20;
    private const int StalledAfterSeconds = 120;
    private readonly object _sync = new();
    private readonly PromotionDispatchOptions _options;
    private readonly string _instanceId = $"{Environment.MachineName}:{Environment.ProcessId}";
    private readonly DateTime _processStartedAtUtc = DateTime.UtcNow;
    private readonly Queue<DateTime> _deliveredTimestamps = new();
    private readonly Queue<PromotionEmailRecentError> _recentErrors = new();
    private PromotionEmailQueueDatabaseSample? _sample;
    private PromotionEmailCurrentDelivery? _currentDelivery;
    private DateTime? _lastHeartbeatAtUtc;
    private DateTime? _lastProgressAtUtc;
    private DateTime? _lastDeliveredAtUtc;
    private DateTime? _lastErrorAtUtc;
    private bool _smtpEnabled;
    private long _attempted;
    private long _delivered;
    private long _retried;
    private long _failed;
    private long _skipped;
    private long _recoveredStaleLeases;
    private int _consecutiveErrors;

    public PromotionEmailQueueTelemetry(Microsoft.Extensions.Options.IOptions<PromotionDispatchOptions> options)
    {
        _options = options.Value;
    }

    public void Heartbeat()
    {
        lock (_sync) _lastHeartbeatAtUtc = DateTime.UtcNow;
    }

    public void SetSmtpEnabled(bool enabled)
    {
        lock (_sync) _smtpEnabled = enabled;
    }

    public void UpdateQueueSample(PromotionEmailQueueDatabaseSample sample)
    {
        lock (_sync) _sample = sample;
    }

    public void StartDelivery(PromotionEmailCurrentDelivery delivery)
    {
        lock (_sync)
        {
            _currentDelivery = delivery;
            _attempted++;
        }
    }

    public void CompleteDelivered(DateTime occurredAtUtc)
    {
        lock (_sync)
        {
            _delivered++;
            _lastDeliveredAtUtc = occurredAtUtc;
            _lastProgressAtUtc = occurredAtUtc;
            _consecutiveErrors = 0;
            _currentDelivery = null;
            _deliveredTimestamps.Enqueue(occurredAtUtc);
            TrimDeliveryWindow(occurredAtUtc);
        }
    }

    public void CompleteSkipped(DateTime occurredAtUtc)
    {
        lock (_sync)
        {
            _skipped++;
            _lastProgressAtUtc = occurredAtUtc;
            _consecutiveErrors = 0;
            _currentDelivery = null;
        }
    }

    public void CompleteError(
        PromotionEmailRecentError error,
        bool permanentlyFailed)
    {
        lock (_sync)
        {
            if (permanentlyFailed) _failed++; else _retried++;
            _lastErrorAtUtc = error.OccurredAtUtc;
            _lastProgressAtUtc = error.OccurredAtUtc;
            _consecutiveErrors++;
            _currentDelivery = null;
            EnqueueError(error);
        }
    }

    public void RecordWorkerError(Exception exception)
    {
        DateTime now = DateTime.UtcNow;
        lock (_sync)
        {
            _lastErrorAtUtc = now;
            _consecutiveErrors++;
            EnqueueError(new PromotionEmailRecentError(
                now, null, null, null, "worker", Sanitize(exception.Message), null, false));
        }
    }

    public void RecordRecoveredStaleLeases(int count)
    {
        if (count <= 0) return;
        lock (_sync)
        {
            _recoveredStaleLeases += count;
            _lastProgressAtUtc = DateTime.UtcNow;
        }
    }

    public PromotionEmailQueueSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            DateTime now = DateTime.UtcNow;
            TrimDeliveryWindow(now);
            PromotionEmailQueueDatabaseSample sample = _sample ?? new(
                now, 0, 0, 0, 0, 0, 0, 0, null, []);
            DateTime rateWindowStart = _processStartedAtUtc > now.AddMinutes(-5) ? _processStartedAtUtc : now.AddMinutes(-5);
            decimal elapsedMinutes = Math.Max(1m / 60m, (decimal)(now - rateWindowStart).TotalMinutes);
            decimal averagePerMinute = decimal.Round(_deliveredTimestamps.Count / elapsedMinutes, 2);
            int deliveredLastMinute = _deliveredTimestamps.Count(timestamp => timestamp >= now.AddMinutes(-1));
            bool heartbeatStale = !_lastHeartbeatAtUtc.HasValue ||
                                  _lastHeartbeatAtUtc < now.AddSeconds(-Math.Max(30, _options.PollingIntervalSeconds * 4));
            bool queueStalled = _smtpEnabled && sample.Ready > 0 &&
                (!_lastProgressAtUtc.HasValue
                    ? now - _processStartedAtUtc > TimeSpan.FromSeconds(StalledAfterSeconds)
                    : now - _lastProgressAtUtc.Value > TimeSpan.FromSeconds(StalledAfterSeconds));

            (string status, string reason) = ResolveStatus(heartbeatStale, queueStalled, sample);
            DateTime? estimatedDrainAtUtc = averagePerMinute > 0
                ? now.AddMinutes((double)(sample.Pending / averagePerMinute))
                : null;
            List<string> warnings = [];
            warnings.Add("La telemetría es local a esta instancia y se reinicia al reiniciar el proceso.");
            if (!_smtpEnabled) warnings.Add("SMTP está deshabilitado; las entregas permanecerán en cola.");
            if (sample.StaleProcessing > 0) warnings.Add("Hay entregas con lease de procesamiento antiguo pendientes de recuperación.");
            if (sample.Blocked > 0) warnings.Add("Hay entregas pendientes que no están listas ni programadas; requieren revisión.");
            if (queueStalled) warnings.Add("Hay correos listos, pero no se observa progreso dentro del umbral configurado.");

            return new PromotionEmailQueueSnapshot(
                now,
                status,
                reason,
                "process-instance",
                _instanceId,
                _processStartedAtUtc,
                _lastHeartbeatAtUtc,
                _sample?.SampledAtUtc,
                _lastProgressAtUtc,
                _smtpEnabled,
                new PromotionEmailQueueConfiguration(
                    _options.PollingIntervalSeconds,
                    _options.EmailBatchSize,
                    _options.EmailsPerMinute,
                    _options.MaxEmailAttempts,
                    _options.EmailTelemetryRefreshSeconds,
                    StalledAfterSeconds),
                new PromotionEmailQueueDepth(
                    sample.Pending,
                    sample.Ready,
                    sample.Scheduled,
                    sample.Blocked,
                    sample.Processing,
                    sample.Failed,
                    sample.StaleProcessing,
                    sample.OldestReadyAtUtc,
                    sample.OldestReadyAtUtc.HasValue ? Math.Max(0, (now - sample.OldestReadyAtUtc.Value).TotalSeconds) : null,
                    estimatedDrainAtUtc),
                new PromotionEmailQueueRuntime(
                    _attempted,
                    _delivered,
                    _retried,
                    _failed,
                    _skipped,
                    _recoveredStaleLeases,
                    _consecutiveErrors,
                    deliveredLastMinute,
                    averagePerMinute,
                    _lastDeliveredAtUtc,
                    _lastErrorAtUtc),
                _currentDelivery,
                sample.ActiveCampaigns,
                _recentErrors.Reverse().ToArray(),
                warnings);
        }
    }

    private (string Status, string Reason) ResolveStatus(
        bool heartbeatStale,
        bool queueStalled,
        PromotionEmailQueueDatabaseSample sample)
    {
        if (heartbeatStale) return ("Unavailable", "No se recibe heartbeat reciente del worker.");
        if (!_smtpEnabled) return ("Disabled", "El canal SMTP está deshabilitado.");
        if (queueStalled) return ("Stalled", "La cola tiene entregas listas pero no progresa.");
        if (_consecutiveErrors >= 3 || sample.StaleProcessing > 0 || sample.Blocked > 0) return ("Degraded", "Se detectan errores, leases antiguos o entregas bloqueadas.");
        if (sample.Pending == 0 && sample.Processing == 0) return ("Idle", "No hay entregas pendientes ni en procesamiento.");
        return ("Running", "El worker está procesando la cola con normalidad.");
    }

    private void TrimDeliveryWindow(DateTime now)
    {
        DateTime cutoff = now.AddMinutes(-5);
        while (_deliveredTimestamps.TryPeek(out DateTime timestamp) && timestamp < cutoff)
        {
            _deliveredTimestamps.Dequeue();
        }
    }

    private void EnqueueError(PromotionEmailRecentError error)
    {
        _recentErrors.Enqueue(error with { Message = Sanitize(error.Message) });
        while (_recentErrors.Count > MaxRecentErrors) _recentErrors.Dequeue();
    }

    private static string Sanitize(string value)
    {
        string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        singleLine = Regex.Replace(singleLine, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[email-redacted]", RegexOptions.IgnoreCase);
        return singleLine.Length <= 300 ? singleLine : singleLine[..300];
    }
}

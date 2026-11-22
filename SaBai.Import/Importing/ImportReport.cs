using System.Collections.Concurrent;
using System.Text;

namespace SaBai.Import.Importing;

public enum Outcome
{
    Created,
    Updated,
    Skipped,
    Failed,
}

/// <summary>Counts per entity (fetched / created / updated / skipped / failed) plus failures and notes.</summary>
public sealed class ImportReport
{
    private readonly ConcurrentDictionary<string, Counts> _counts = new();
    private readonly List<string> _order = [];
    private readonly ConcurrentQueue<(string Entity, string Key, string Reason)> _failures = new();
    private readonly ConcurrentQueue<string> _notes = new();

    public sealed class Counts
    {
        internal int _fetched, _created, _updated, _skipped, _failed;
        public int Fetched => _fetched;
        public int Created => _created;
        public int Updated => _updated;
        public int Skipped => _skipped;
        public int Failed => _failed;
    }

    public Counts this[string entity] => Get(entity);

    public IReadOnlyCollection<(string Entity, string Key, string Reason)> Failures => _failures;

    public IReadOnlyCollection<string> Notes => _notes;

    public int FailureCount => _counts.Values.Sum(x => x.Failed);

    public void Fetched(string entity, int count) => Interlocked.Add(ref Get(entity)._fetched, count);

    public void Record(string entity, Outcome outcome)
    {
        var c = Get(entity);
        switch (outcome)
        {
            case Outcome.Created:
                Interlocked.Increment(ref c._created);
                break;
            case Outcome.Updated:
                Interlocked.Increment(ref c._updated);
                break;
            case Outcome.Skipped:
                Interlocked.Increment(ref c._skipped);
                break;
            case Outcome.Failed:
                Interlocked.Increment(ref c._failed);
                break;
        }
    }

    public void Fail(string entity, string key, string reason)
    {
        Record(entity, Outcome.Failed);
        _failures.Enqueue((entity, key, reason));
    }

    public void Note(string note) => _notes.Enqueue(note);

    private Counts Get(string entity)
    {
        return _counts.GetOrAdd(
            entity,
            name =>
            {
                lock (_order)
                {
                    _order.Add(name);
                }

                return new Counts();
            }
        );
    }

    public string Render(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(title);
        var width = Math.Max(20, _order.Select(x => x.Length).DefaultIfEmpty(0).Max() + 2);
        sb.AppendLine(
            $"{"Entity".PadRight(width)}{"fetched",9}{"created",9}{"updated",9}{"skipped",9}{"failed",9}"
        );
        List<string> order;
        lock (_order)
        {
            order = [.. _order];
        }

        foreach (var name in order)
        {
            var c = _counts[name];
            sb.AppendLine(
                $"{name.PadRight(width)}{c.Fetched,9}{c.Created,9}{c.Updated,9}{c.Skipped,9}{c.Failed,9}"
            );
        }

        if (!_notes.IsEmpty)
        {
            sb.AppendLine();
            sb.AppendLine("Notes:");
            foreach (var note in _notes)
            {
                sb.AppendLine($"  - {note}");
            }
        }

        if (!_failures.IsEmpty)
        {
            sb.AppendLine();
            sb.AppendLine($"Failures ({_failures.Count}):");
            foreach (var (entity, key, reason) in _failures)
            {
                sb.AppendLine($"  - {entity} {key}: {reason}");
            }
        }

        return sb.ToString();
    }
}

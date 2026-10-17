using System.Collections.Immutable;

namespace NamescopeGuard.Graph;

/// <summary>有向 Link 图：边由调用方 Scope 指向其可访问 internal 的 Scope。</summary>
internal sealed class ScopeGraph(ImmutableDictionary<string, ImmutableHashSet<string>> links)
{
    public IEnumerable<string> Scopes => links.Keys;

    public bool Contains(string scope) => links.ContainsKey(scope);

    public ImmutableHashSet<string> GetLinkTargets(string scope) =>
        links.TryGetValue(scope, out var targets) ? targets : [];

    public bool CanReach(string from, string to)
    {
        if (!Contains(from) || !Contains(to))
            return false;
        if (from == to)
            return true;

        var visited = new HashSet<string> { from };
        var pending = new Queue<string>();
        pending.Enqueue(from);

        while (pending.Count > 0)
        {
            foreach (var target in GetLinkTargets(pending.Dequeue()))
            {
                if (target == to)
                    return true;
                if (visited.Add(target))
                    pending.Enqueue(target);
            }
        }

        return false;
    }
}

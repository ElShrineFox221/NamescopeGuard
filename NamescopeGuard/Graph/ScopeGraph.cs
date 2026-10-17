using System.Collections.Immutable;

namespace NamescopeGuard.Graph;

/// <summary>有向 Link 图：边由调用方 Scope 指向其可访问 internal 的 Scope。</summary>
internal sealed class ScopeGraph(ImmutableDictionary<string, ImmutableDictionary<string, LinkKinds>> links)
{
    public IEnumerable<string> Scopes => links.Keys;

    public bool Contains(string scope) => links.ContainsKey(scope);

    public bool CanReach(string from, string to) => CanReach(from, to, LinkKinds.All);

    public bool CanReach(string from, string to, LinkKinds allowedKinds)
    {
        if (!Contains(from) || !Contains(to))
            return false;
        if (from == to)
            return true;
        if (allowedKinds == LinkKinds.None)
            return false;

        var visited = new HashSet<string> { from };
        var pending = new Queue<string>();
        pending.Enqueue(from);

        while (pending.Count > 0)
        {
            foreach (var pair in GetTargets(pending.Dequeue()))
            {
                if ((pair.Value & allowedKinds) == LinkKinds.None)
                    continue;
                if (pair.Key == to)
                    return true;
                if (visited.Add(pair.Key))
                    pending.Enqueue(pair.Key);
            }
        }

        return false;
    }

    private ImmutableDictionary<string, LinkKinds> GetTargets(string scope) =>
        links.TryGetValue(scope, out var targets) ? targets : ImmutableDictionary<string, LinkKinds>.Empty;
}

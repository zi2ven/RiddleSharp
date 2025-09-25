using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

/// <summary>
/// 提供用于构建和拓扑排序包依赖关系的方法。
/// </summary>
public static class PackageTopo
{
    public static (List<QualifiedName> order,
        List<(QualifiedName From, QualifiedName To)> edges)
        BuildAndTopo(Unit[] units, bool includeExternal = false)
    {
        var cmp = QualifiedNameComparer.Instance;

        var nodes = new HashSet<QualifiedName>(units.Select(u => u.PackageName), cmp);

        var depsByPkg = units
            .GroupBy(u => u.PackageName, cmp)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(u => u.Depend).ToHashSet(cmp),
                cmp
            );

        if (includeExternal)
        {
            foreach (var d in depsByPkg.Values.SelectMany(x => x))
                nodes.Add(d);
        }

        var adj = nodes.ToDictionary(
            n => n,
            _ => new HashSet<QualifiedName>(cmp),
            cmp
        );
        var indeg = nodes.ToDictionary(n => n, _ => 0, cmp);

        var edges = new List<(QualifiedName From, QualifiedName To)>();

        foreach (var (pkg, deps) in depsByPkg)
        {
            foreach (var d in deps.Where(d => nodes.Contains(d) || includeExternal).Where(d => adj[d].Add(pkg)))
            {
                indeg[pkg]++;
                edges.Add((d, pkg));
            }
        }

        var q = new Queue<QualifiedName>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<QualifiedName>(nodes.Count);

        while (q.Count > 0)
        {
            var v = q.Dequeue();
            order.Add(v);
            foreach (var w in adj[v].Where(w => --indeg[w] == 0))
            {
                q.Enqueue(w);
            }
        }

        if (order.Count == nodes.Count) return (order, edges);
        {
            var cycle = FindOneCycle(adj, cmp);
            var msg = cycle.Count > 0
                ? "Cycle detected: " + string.Join(" -> ", cycle.Select(x => x.ToString()))
                : "Cycle detected in dependency graph.";
            throw new InvalidOperationException(msg);
        }
    }

    /// <summary>
    /// 对单元数组进行排序。
    /// </summary>
    /// <param name="units">要排序的单元数组。</param>
    /// <param name="includeExternal">是否包含外部依赖，默认为 false。</param>
    /// <returns>排序后的单元数组。</returns>
    public static Unit[] SortUnits(Unit[] units, bool includeExternal = false)
    {
        var (order, _) = BuildAndTopo(units, includeExternal);
        var index = order.Select((p, i) => 
            (p, i)).ToDictionary(x => x.p, x => x.i, QualifiedNameComparer.Instance);
        return units.OrderBy(u => index[u.PackageName]).ToArray();
    }

    private static List<QualifiedName> FindOneCycle(
        IReadOnlyDictionary<QualifiedName, HashSet<QualifiedName>> adj,
        IEqualityComparer<QualifiedName> cmp)
    {
        var color = adj.Keys.ToDictionary(k => k, _ => 0, cmp);
        var parent = new Dictionary<QualifiedName, QualifiedName?>(cmp);

        foreach (var v in adj.Keys)
        {
            if (color[v] != 0) continue;
            var cycle = Dfs(v);
            if (cycle is not null) return cycle;
        }

        return [];

        List<QualifiedName>? Dfs(QualifiedName u)
        {
            color[u] = 1;
            foreach (var w in adj[u])
            {
                switch (color[w])
                {
                    case 0:
                    {
                        parent[w] = u;
                        var c = Dfs(w);
                        if (c is not null) return c;
                        break;
                    }
                    case 1:
                    {
                        var cycle = new List<QualifiedName> { w };
                        var x = u;
                        cycle.Add(x);
                        while (!cmp.Equals(x, w))
                        {
                            x = parent[x]!;
                            cycle.Add(x);
                        }

                        cycle.Reverse();
                        return cycle;
                    }
                }
            }

            color[u] = 2;
            return null;
        }
    }


    private sealed class QualifiedNameComparer : IEqualityComparer<QualifiedName>
    {
        public static readonly QualifiedNameComparer Instance = new();

        public bool Equals(QualifiedName? x, QualifiedName? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Parts.Count != y.Parts.Count) return false;
            return !x.Parts.Where((t, i) => !string.Equals(t, y.Parts[i], StringComparison.Ordinal)).Any();
        }

        public int GetHashCode(QualifiedName obj)
        {
            var hc = new HashCode();
            foreach (var p in obj.Parts)
                hc.Add(p, StringComparer.Ordinal);
            return hc.ToHashCode();
        }
    }
}
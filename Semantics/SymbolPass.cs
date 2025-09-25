using System.Collections;
using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

internal sealed class SymbolTable
{
    private readonly Dictionary<string, Stack<Decl>> _decls = [];
    private readonly Stack<HashSet<string>> _locals = [];

    public bool TryGetDecl(string name, out Decl decl)
    {
        if (_decls.TryGetValue(name, out var stack) && stack.Count > 0)
        {
            decl = stack.Peek();
            return true;
        }
        decl = null!;
        return false;
    }

    public SymbolTable()
    {
        Push();
        // init Primitive Type
        foreach (var i in PrimitiveType.Decls.Values)
        {
            AddDecl(i);
        }
    }

    public void Push()
    {
        _locals.Push([]);
    }

    public bool IsGlobal => _locals.Count == 1;

    public void Pop()
    {
        foreach (var i in _locals.Pop())
        {
            _decls[i].Pop();
            if (_decls[i].Count == 0)
            {
                _decls.Remove(i);
            }
        }
    }

    public void AddDecl(Decl decl)
    {
        if (_locals.Peek().Contains(decl.Name))
        {
            throw new Exception($"\'{decl.Name}\' is already declared");
        }

        _locals.Peek().Add(decl.Name);

        if (!_decls.TryGetValue(decl.Name, out var value))
        {
            value = new Stack<Decl>();
            _decls.Add(decl.Name, value);
        }

        value.Push(decl);
    }

    public T GetDecl<T>(string name) where T : Decl
    {
        if (_decls.TryGetValue(name, out var value) && value.Count > 0)
        {
            return (value.Peek() as T)!;
        }

        throw new Exception($"\'{name}\' is not declared");
    }
}

internal class SymbolPass
{
    public Unit[] Run(Unit[] units)
    {
        var unitMap = MergeUnits(units);
        var sorted = PackageTopo.SortUnits(unitMap.Values.ToArray());
        var exportsByPkg = new Dictionary<QualifiedName, Dictionary<string, Decl>>();

        foreach (var unit in sorted)
        {
            PredeclareTopDecls(unit);

            var exports = unit.Decls.Value
                .Where(kv => kv.Key.Parts.Count > 0 && StartsWith(kv.Key, unit.PackageName))
                .ToDictionary(
                    kv => kv.Key.Parts[^1], // 短名
                    kv => kv.Value
                );

            exportsByPkg[unit.PackageName] = exports;
        }
        
        foreach (var unit in sorted)
        {
            _ = new SymbolVisitor(unit, exportsByPkg);
        }

        return sorted.ToArray();
    }


    private static void PredeclareTopDecls(Unit unit)
    {
        var table = new SymbolTable();
        foreach (var d in unit.Stmts.OfType<Decl>())
        {
            d.QualifiedName = unit.PackageName.Add(d.Name);
            table.AddDecl(d);
            unit.Decls.Value[d.QualifiedName] = d;
        }
    }

    private static bool StartsWith(QualifiedName full, QualifiedName prefix)
    {
        if (prefix.Parts.Count > full.Parts.Count) return false;
        return !prefix.Parts.Where((t, i) => !Equals(full.Parts[i], t)).Any();
    }

    private static Dictionary<QualifiedName, Unit> MergeUnits(Unit[] units) =>
        units
            .GroupBy(u => u.PackageName)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var merged = new Unit(g.SelectMany(u => u.Stmts).ToArray(), g.Key);
                    merged.Depend.UnionWith(g.SelectMany(u => u.Depend));

                    // 去掉对自身包的依赖
                    // merged.Depend.Remove(g.Key);

                    return merged;
                });

    private sealed class SymbolVisitor : AstVisitor<object?>
    {
        private readonly SymbolTable _table = new();

        private readonly Unit _unit;
        private readonly IReadOnlyDictionary<QualifiedName, Dictionary<string, Decl>> _exportsByPkg;

        public SymbolVisitor(Unit unit,
            IReadOnlyDictionary<QualifiedName, Dictionary<string, Decl>> exportsByPkg)
        {
            _unit = unit;
            _exportsByPkg = exportsByPkg;
            
            PreDecl(unit.Stmts.OfType<Decl>().ToArray());
            
            VisitUnit(unit);
        }

        private void PreDecl(Decl[] decls)
        {
            foreach (var d in decls)
            {
                d.QualifiedName = _table.IsGlobal
                    ? _unit.PackageName.Add(d.Name)
                    : QualifiedName.Parse(d.Name);

                _table.AddDecl(d);
                _unit.Decls.Value[d.QualifiedName] = d;
            }
        }

        private void VisitOrNull(AstNode? node)
        {
            if (node is not null) Visit(node);
        }

        public override object? VisitVarDecl(VarDecl node)
        {
            if (!_table.IsGlobal) _table.AddDecl(node);
            return base.VisitVarDecl(node);
        }

        public override object? VisitFuncDecl(FuncDecl node)
        {
            if (!_table.IsGlobal) _table.AddDecl(node);

            VisitOrNull(node.TypeLit);
            foreach (var i in node.Args) VisitOrNull(i);

            _table.Push();
            foreach (var i in node.Body) VisitOrNull(i);
            _table.Pop();

            return null;
        }

        public override object? VisitBlock(Block node)
        {
            _table.Push();
            foreach (var i in node.Body) VisitOrNull(i);
            _table.Pop();
            return null;
        }

        public override object? VisitSymbol(Symbol node)
        {
            var decl = node.Name.Parts.Count == 1 ? _table.GetDecl<Decl>(node.Name.Parts[0]) : ResolveQualified(node.Name);

            node.DeclReference = new WeakReference<Decl>(decl);
            return null;
        }

        private Decl ResolveQualified(QualifiedName qn)
        {
            var parts = qn.Parts;

            QualifiedName? best = null;
            foreach (var pkg in Candidates())
            {
                if (pkg.Parts.Count >= parts.Count) continue;
                var match = !pkg.Parts.Where((t, i) => !Equals(t, parts[i])).Any();

                if (match && (best is null || pkg.Parts.Count > best.Parts.Count))
                    best = pkg;
            }

            if (best is null)
                throw new Exception($"'{qn}' 未找到：没有匹配的已导入包前缀");

            if (!_exportsByPkg.TryGetValue(best, out var exports))
                throw new Exception($"包 '{best}' 不存在或没有导出");

            var remaining = parts.Count - best.Parts.Count;
            if (remaining == 1)
            {
                var member = parts[^1];
                return !exports.TryGetValue(member, out var d) ? throw new Exception($"'{best}::{member}' 未导出") : d;
            }

            // todo 符号内成员
            throw new NotImplementedException($"暂不支持多级成员访问：'{qn}'");

            IEnumerable<QualifiedName> Candidates()
            {
                yield return _unit.PackageName;
                foreach (var dep in _unit.Depend) yield return dep;
            }
        }
    }
}
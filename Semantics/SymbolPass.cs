using System.Runtime.CompilerServices;
using RiddleSharp.Frontend;

namespace RiddleSharp.Semantics;

sealed class NoValue
{
    public static NoValue Value = new NoValue();

    private NoValue()
    {
    }
}

sealed class SymbolTable
{
    public Dictionary<string, Stack<Decl>> Decls = [];
}

internal class SymbolPass
{
    private SymbolTable _table = new();

    public void Run(Unit[] units)
    {
    }

    private Unit MergeUnits(Unit unit1, Unit unit2)
    {
        var mergedStmts = unit1.Stmts.Concat(unit2.Stmts).ToArray();
        return new Unit(mergedStmts, unit1.PackageName);
    }

    private Dictionary<QualifiedName, Unit> MergeUnits(Unit[] units)
    {
        var packTable = new Dictionary<QualifiedName, List<Unit>>();
        foreach (var unit in units)
        {
            if (!packTable.TryGetValue(unit.PackageName, out List<Unit>? value))
            {
                value = new List<Unit>();
                packTable[unit.PackageName] = value;
            }

            value.Add(unit);
        }

        var result = new Dictionary<QualifiedName, Unit>();
        foreach (var u in packTable.Values.SelectMany(us => us))
        {
            if (!result.TryGetValue(u.PackageName, out var value))
            {
                value = u;
                result.Add(u.PackageName, value);
            }

            result[u.PackageName] = MergeUnits(value, u);
        }
        return result;
    }
}
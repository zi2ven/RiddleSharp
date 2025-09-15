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
        var cu = MergeUnit(units);
    }

    private static Unit MergeUnit(Unit[] units)
    {
        var bs = new Unit([]);
        bs = units.Aggregate(bs, (current, unit) =>
            new Unit(current.Stmts.Concat(unit.Stmts).ToArray()));
        return bs;
    }

    public void RecordTopLevel(Unit unit)
    {
        
    }
}
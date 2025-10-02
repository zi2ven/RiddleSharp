using LGenerator;
using RiddleSharp.Frontend;

namespace RiddleSharp.Background.Lg;

public static class LgPass
{
    public static Unit[] Run(Unit[] units)
    {
        var m = new IrModule();
        m.PutFunction(new IrFunction(IrType.GetIntType(), "main", 0, [], new IrControlFlowGraph()));
        m.Functions["main"].ControlFlowGraph.BasicBlocks.Add("entry", new IrControlFlowGraph.BasicBlock("entry"));
        
        Console.WriteLine(m);
        return units;
    }
}
namespace RiddleSharp.Hir;

public sealed class HirModule
{
    public List<HirGlobalObject> Globals { get; } = [];
    public List<HirFunction> Functions { get; } = [];
}



public abstract class HirNode;

public abstract class HirValue : HirNode
{
    public abstract HirType NativeType { get; }
    public int Id { get; set; }
}

public abstract class HirConstant : HirValue { }

public sealed class HirConstantInt(long value, HirType type) : HirConstant
{
    public long Value { get; set; } = value;
    public override HirType NativeType => type;
}

public sealed class HirConstantFloat(double value, HirFpType type) : HirConstant
{
    public double Value { get; set; } = value;
    public override HirType NativeType => type;
}

public sealed class HirConstantString(string value, HirType? type = null) : HirConstant
{
    public override HirType NativeType { get; } = type ?? HirPointerType.CharPtrTy.Value;
    public string Value { get; set; } = value;
}

public abstract class HirGlobalObject(HirType type, string name) : HirValue
{
    public string Name { get; set; } = name;
    public override HirType NativeType => type;
}

public sealed class HirGlobalVariable(HirType type, string name, HirValue init) : HirGlobalObject(type, name)
{
    public HirValue Init { get; set; } = init;
}

public sealed class HirFunction(HirFunctionType type, string name) : HirGlobalObject(type, name)
{
    public List<HirParameter> Parameters { get; } = [];
    public List<HirBasicBlock> Blocks { get; } = [];
    public HirBasicBlock Entry => Blocks[0];
    public override HirFunctionType NativeType => type;
}

public sealed class HirParameter(string name, HirType type) : HirValue
{
    public string Name { get; } = name;
    public override HirType NativeType { get; } = type;
}

public sealed class HirBasicBlock(string name)
{
    public string Name { get; } = name;
    public List<HirInstruction> Inst { get; } = [];
    public IHirTerminator? Terminator { get; set; }

    public List<HirBasicBlock> Predecessors { get; } = [];
    public List<HirBasicBlock> Successors { get; } = [];
}

public interface IHirTerminator { }

public abstract class HirInstruction : HirValue { }

public sealed class HirAlloca(HirType allocType) : HirInstruction
{
    public HirType AllocType { get; } = allocType;
    public override HirType NativeType => new HirPointerType(AllocType);
}

public sealed class HirLoad(HirValue address, HirType resultType) : HirInstruction
{
    public HirValue Address { get; } = address;
    public override HirType NativeType { get; } = resultType;
}

public sealed class HirStore(HirValue address, HirValue value) : HirInstruction
{
    public HirValue Address { get; } = address;
    public HirValue Value { get; } = value;
    public override HirType NativeType { get; } = new HirVoidType();
}

public enum HirBinOp { Add, Sub, Mul, Div, Mod, And, Or, Xor, Shl, Shr }
public sealed class HirBinary(HirBinOp op, HirValue left, HirValue right, HirType type) : HirInstruction
{
    public HirBinOp Op { get; } = op;
    public HirValue Left { get; } = left;
    public HirValue Right { get; } = right;
    public override HirType NativeType { get; } = type;
}

public enum HirCmpPred { Eq, Ne, Lt, Le, Gt, Ge }
public sealed class HirCompare(HirCmpPred pred, HirValue left, HirValue right) : HirInstruction
{
    public HirCmpPred Pred { get; } = pred;
    public HirValue Left { get; } = left;
    public HirValue Right { get; } = right;
    public override HirType NativeType { get; } = new HirIntType(1, false); // i1 bool
}

public enum HirCastKind { BitCast, ZExt, SExt, Trunc, FpToSi, SiToFp, PtrToInt, IntToPtr }
public sealed class HirCast(HirCastKind kind, HirValue value, HirType to) : HirInstruction
{
    public HirCastKind Kind { get; } = kind;
    public HirValue Value { get; } = value;
    public override HirType NativeType { get; } = to;
}

public sealed class HirGetElementPtr(HirValue address, IReadOnlyList<HirValue> indices, HirType resultType) : HirInstruction
{
    public HirValue Address { get; } = address;
    public IReadOnlyList<HirValue> Indices { get; } = indices;
    public override HirType NativeType { get; } = resultType; // pointer
}

public sealed class HirCall(HirValue callee, IReadOnlyList<HirValue> args, HirType retType) : HirInstruction
{
    public HirValue Callee { get; } = callee;
    public IReadOnlyList<HirValue> Args { get; } = args;
    public override HirType NativeType { get; } = retType;
}

public sealed class HirSelect(HirValue cond, HirValue t, HirValue f) : HirInstruction
{
    public HirValue Cond { get; } = cond;
    public HirValue TrueValue { get; } = t;
    public HirValue FalseValue { get; } = f;
    public override HirType NativeType => TrueValue.NativeType;
}

public sealed class HirPhi(HirType type) : HirInstruction
{
    public List<(HirBasicBlock Pred, HirValue Value)> Incoming { get; } = [];
    public override HirType NativeType { get; } = type;
}

public sealed class HirReturn(HirValue? value) : HirInstruction, IHirTerminator
{
    public HirValue? Value { get; } = value;
    public override HirType NativeType { get; } = new HirVoidType();
}

public sealed class HirBr(HirBasicBlock target) : HirInstruction, IHirTerminator
{
    public HirBasicBlock Target { get; } = target;
    public override HirType NativeType { get; } = new HirVoidType();
}

public sealed class HirCondBr(HirValue cond, HirBasicBlock then, HirBasicBlock @else) : HirInstruction, IHirTerminator
{
    public HirValue Cond { get; } = cond;
    public HirBasicBlock Then { get; } = then;
    public HirBasicBlock Else { get; } = @else;
    public override HirType NativeType { get; } = new HirVoidType();
}

public sealed class HirUnreachable : HirInstruction, IHirTerminator
{
    public override HirType NativeType { get; } = new HirVoidType();
}

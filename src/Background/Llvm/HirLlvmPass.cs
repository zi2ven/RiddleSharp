using System.Runtime.CompilerServices;
using RiddleSharp.Hir;
using Ubiquity.NET.Llvm;
using Ubiquity.NET.Llvm.Instructions;
using Ubiquity.NET.Llvm.Types;
using Ubiquity.NET.Llvm.Values;
using Module = Ubiquity.NET.Llvm.Module;

namespace RiddleSharp.Background.Llvm;

/// <summary>
/// Lower a <see cref="HirModule"/> to LLVM IR using Ubiquity.NET.Llvm.
/// </summary>
public static class HirLlvmPass
{
    /// <summary>
    /// Lower the provided HIR module to LLVM, run a small optimization pipeline, and return the LLVM module.
    /// </summary>
    public static Module Run(HirModule hir, string moduleName = "riddlesharp")
    {
        var context = new Context();
        var module = context.CreateBitcodeModule(moduleName);
        var lowerer = new Lowerer(context, module);
        lowerer.Run(hir);

        foreach (var fn in module.Functions)
        {
            if (fn.IsDeclaration) continue;
            var r = fn.TryRunPasses("mem2reg", "instcombine", "reassociate", "gvn", "simplifycfg", "tailcallelim");
            if (r.Failed)
                throw new Exception(r.ToString());
        }

        return module;
    }

    private sealed class Lowerer(Context ctx, Module mod)
    {
        private readonly InstructionBuilder _b = new(ctx);

        private readonly Dictionary<string, IStructType> _structCache = new(StringComparer.Ordinal);
        private readonly Dictionary<HirGlobalObject, GlobalVariable> _globals = new();
        private readonly Dictionary<HirFunction, Function> _funcs = new();
        private readonly ConditionalWeakTable<HirValue, Value> _values = new();
        private readonly Dictionary<HirBasicBlock, BasicBlock> _blocks = new();
        private readonly List<(PhiNode phi, HirPhi src)> _pendingPhis = [];

        public void Run(HirModule m)
        {
            foreach (var f in m.Functions)
                DeclareFunction(f);

            foreach (var g in m.Globals)
                LowerGlobal(g);

            foreach (var f in m.Functions.Where(f => f.Blocks.Count > 0))
                DefineFunctionBody(f);
        }

        private void LowerGlobal(HirGlobalObject g)
        {
            switch (g)
            {
                case HirGlobalVariable gv:
                {
                    var ty = LlType(gv.NativeType);
                    var init = LConst(gv.Init);
                    var llgv = mod.AddGlobal(ty, false, linkage: Linkage.External, init, name: gv.Name);
                    _globals[g] = llgv;
                    _values.Add(g, llgv);
                    break;
                }
                case HirFunction hf:
                    // already declared in DeclareFunction
                    _values.Add(g, _funcs[hf]);
                    break;
                default:
                    throw new NotSupportedException($"Global kind {g.GetType().Name} not supported");
            }
        }

        private void DeclareFunction(HirFunction f)
        {
            var llFty = (IFunctionType)LlType(f.NativeType);
            var llFn = mod.CreateFunction(f.Name, llFty);

            for (var i = 0; i < llFn.Parameters.Count && i < f.Parameters.Count; i++)
                llFn.Parameters[i].Name = f.Parameters[i].Name;

            _funcs[f] = llFn;
            _values.Add(f, llFn);
        }

        private void DefineFunctionBody(HirFunction f)
        {
            var llFn = _funcs[f];

            foreach (var bb in f.Blocks)
            {
                var llBb = llFn.AppendBasicBlock(string.IsNullOrEmpty(bb.Name) ? "" : bb.Name);
                _blocks[bb] = llBb;
            }

            for (var i = 0; i < f.Parameters.Count; i++)
            {
                var p = f.Parameters[i];
                var llp = llFn.Parameters[i];
                _values.Add(p, llp);
            }

            foreach (var bb in f.Blocks)
            {
                var llBb = _blocks[bb];
                _b.PositionAtEnd(llBb);

                foreach (var inst in bb.Inst)
                {
                    if (inst is HirPhi phi)
                    {
                        var llPhi = _b.PhiNode(LlType(phi.NativeType));
                        _values.Add(phi, llPhi);
                        _pendingPhis.Add((llPhi, phi));
                    }
                }

                // Then, emit the rest of the instructions (skipping Phi – already created).
                foreach (var inst in bb.Inst)
                {
                    if (inst == bb.Terminator) break;
                    switch (inst)
                    {
                        case HirPhi:
                            // already handled
                            break;
                        case HirAlloca a:
                            _values.Add(a, _b.Alloca(LlType(a.AllocType)));
                            break;
                        case HirLoad l:
                            var type = LlType(l.NativeType);
                            if (type is IFunctionType)
                            {
                                type = type.CreatePointerType();
                            }
                            _values.Add(l, _b.Load(type, L(l.Address)));
                            break;
                        case HirStore s:
                            _values.Add(s, _b.Store(L(s.Value), L(s.Address)));
                            break;
                        case HirBinary bin:
                            _values.Add(bin, EmitBinary(bin));
                            break;
                        case HirCompare cmp:
                            _values.Add(cmp, EmitCompare(cmp));
                            break;
                        case HirCast cs:
                            _values.Add(cs, EmitCast(cs));
                            break;
                        case HirGetElementPtr gep:
                            _values.Add(gep, EmitGep(gep));
                            break;
                        case HirCall call:
                            _values.Add(call, EmitCall(call));
                            break;
                        case HirSelect sel:
                            _values.Add(sel, _b.Select(L(sel.Cond), L(sel.TrueValue), L(sel.FalseValue)));
                            break;
                        default:
                            throw new NotSupportedException($"Instruction {inst.GetType().Name} not supported");
                    }
                }

                // Terminator
                if (bb.Terminator is null)
                    throw new InvalidOperationException(
                        $"BasicBlock '{bb.Name}' missing terminator in function '{f.Name}'.");

                switch (bb.Terminator)
                {
                    case HirReturn ret:
                        if (ret.Value is null) _b.Return();
                        else _b.Return(L(ret.Value));
                        break;
                    case HirBr br:
                        _b.Branch(_blocks[br.Target]);
                        break;
                    case HirCondBr cbr:
                        _b.Branch(L(cbr.Cond), _blocks[cbr.Then], _blocks[cbr.Else]);
                        break;
                    case HirUnreachable:
                        _b.Unreachable();
                        break;
                    default:
                        throw new NotSupportedException(bb.Terminator.GetType().Name);
                }
            }

            // Add incoming edges for all Phi nodes now that every predecessor was fully emitted.
            foreach (var (llPhi, src) in _pendingPhis)
            {
                foreach (var (pred, value) in src.Incoming)
                    llPhi.AddIncoming(L(value), _blocks[pred]);
            }

            _pendingPhis.Clear();
        }

        private Value EmitBinary(HirBinary bin)
        {
            var l = L(bin.Left);
            var r = L(bin.Right);

            switch (bin.Op)
            {
                // Integer/FP arithmetic: select based on the *result* type
                case HirBinOp.Add:
                    return IsFp(bin.NativeType) ? _b.FAdd(l, r) : _b.Add(l, r);
                case HirBinOp.Sub:
                    return IsFp(bin.NativeType) ? _b.FSub(l, r) : _b.Sub(l, r);
                case HirBinOp.Mul:
                    return IsFp(bin.NativeType) ? _b.FMul(l, r) : _b.Mul(l, r);
                case HirBinOp.Div:
                    if (IsFp(bin.NativeType)) return _b.FDiv(l, r);
                    return IsSigned(bin.NativeType) ? _b.SDiv(l, r) : _b.UDiv(l, r);
                case HirBinOp.Mod:
                    if (IsFp(bin.NativeType)) return _b.FRem(l, r);
                    return IsSigned(bin.NativeType) ? _b.SRem(l, r) : _b.URem(l, r);

                case HirBinOp.And: return _b.And(l, r);
                case HirBinOp.Or: return _b.Or(l, r);
                case HirBinOp.Xor: return _b.Xor(l, r);

                case HirBinOp.Shl: return _b.ShiftLeft(l, r);
                case HirBinOp.Shr:
                    return IsSigned(bin.Left.NativeType) ? _b.ArithmeticShiftRight(l, r) : _b.LogicalShiftRight(l, r);

                default:
                    throw new NotSupportedException(bin.Op.ToString());
            }
        }

        private Value EmitCompare(HirCompare cmp)
        {
            var l = L(cmp.Left);
            var r = L(cmp.Right);

            // Pointer comparisons are lowered as integer compares (eq/ne only)
            if (cmp.Left.NativeType is HirPointerType || cmp.Right.NativeType is HirPointerType)
            {
                var pred = cmp.Pred switch
                {
                    HirCmpPred.Eq => IntPredicate.Equal,
                    HirCmpPred.Ne => IntPredicate.NotEqual,
                    HirCmpPred.Lt => IntPredicate.SignedLessThan, // todo 设置不同 signed
                    HirCmpPred.Le => IntPredicate.SignedLessThanOrEqual,
                    HirCmpPred.Gt => IntPredicate.SignedGreaterThan,
                    HirCmpPred.Ge => IntPredicate.SignedGreaterThanOrEqual,
                    _ => throw new NotSupportedException("Relational compare on pointers is not supported")
                };
                return _b.Compare(pred, l, r);
            }

            if (IsFp(cmp.Left.NativeType) || IsFp(cmp.Right.NativeType))
            {
                var pred = cmp.Pred switch
                {
                    HirCmpPred.Eq => RealPredicate.OrderedAndEqual,
                    HirCmpPred.Ne => RealPredicate.OrderedAndNotEqual,
                    HirCmpPred.Lt => RealPredicate.OrderedAndLessThan,
                    HirCmpPred.Le => RealPredicate.OrderedAndLessThanOrEqual,
                    HirCmpPred.Gt => RealPredicate.OrderedAndGreaterThan,
                    HirCmpPred.Ge => RealPredicate.OrderedAndGreaterThanOrEqual,
                    _ => throw new ArgumentOutOfRangeException()
                };
                return _b.Compare(pred, l, r);
            }
            else
            {
                var signed = IsSigned(cmp.Left.NativeType) || IsSigned(cmp.Right.NativeType);
                var pred = cmp.Pred switch
                {
                    HirCmpPred.Eq => IntPredicate.Equal,
                    HirCmpPred.Ne => IntPredicate.NotEqual,
                    HirCmpPred.Lt => signed ? IntPredicate.SignedLessThan : IntPredicate.UnsignedLessThan,
                    HirCmpPred.Le => signed ? IntPredicate.SignedLessThanOrEqual : IntPredicate.UnsignedLessThanOrEqual,
                    HirCmpPred.Gt => signed ? IntPredicate.SignedGreaterThan : IntPredicate.UnsignedGreaterThan,
                    HirCmpPred.Ge => signed
                        ? IntPredicate.SignedGreaterThanOrEqual
                        : IntPredicate.UnsignedGreaterOrEqual,
                    _ => throw new ArgumentOutOfRangeException()
                };
                return _b.Compare(pred, l, r);
            }
        }

        private Value EmitCast(HirCast cs)
        {
            var v = L(cs.Value);
            var to = LlType(cs.NativeType);
            return cs.Kind switch
            {
                HirCastKind.BitCast => _b.BitCast(v, to),
                HirCastKind.ZExt => _b.ZeroExtend(v, to),
                HirCastKind.SExt => _b.SignExtend(v, to),
                HirCastKind.Trunc => _b.Trunc(v, to),
                HirCastKind.FpToSi => _b.FPToSICast(v, to),
                HirCastKind.SiToFp => _b.SIToFPCast(v, to),
                HirCastKind.PtrToInt => _b.PointerToInt(v, to),
                HirCastKind.IntToPtr => _b.IntToPointer(v, (IPointerType)to),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private Value EmitGep(HirGetElementPtr gep)
        {
            var addr = L(gep.Address);
            var idx = gep.Indices.Select(i => EnsureIndexType(L(i))).ToArray();
            return _b.GetElementPtr(addr, idx);
        }

        private Value EnsureIndexType(Value v)
        {
            // LLVM accepts either i32 or i64 depending on target; default to i32 here and cast if needed.
            return v.NativeType is { IsInteger: true, IntegerBitWidth: 32 } ? v : _b.Trunc(v, ctx.Int32Type);
        }

        private Value EmitCall(HirCall call)
        {
            var callee = L(call.Callee);
            var args = call.Args.Select(L).ToList();

            if (callee is Function fn)
                return _b.Call(fn, args);

            // Indirect call via function pointer
            return _b.Call((IFunctionType)LlType(call.Callee.NativeType), callee, args);
        }

        private Value L(HirValue v)
        {
            if (_values.TryGetValue(v, out var got)) return got;

            switch (v)
            {
                case HirConstantInt ci:
                {
                    var c = ctx.CreateConstant(unchecked((uint)ci.Value));
                    _values.Add(v, c);
                    return c;
                }
                case HirConstantFloat cf:
                {
                    var c = cf.NativeType switch
                    {
                        HirFloatType => ctx.CreateConstant((float)cf.Value),
                        _ => ctx.CreateConstant(cf.Value)
                    };
                    _values.Add(v, c);
                    return c;
                }
                case HirConstantString s:
                {
                    var ptr = MakeGlobalStringPtr(s.Value);
                    _values.Add(v, ptr);
                    return ptr;
                }
                case HirFunction f:
                    return _funcs[f];
                case HirGlobalObject g:
                    return _globals[g];
                default:
                    throw new InvalidOperationException($"Value '{v.GetType().Name}' not materialized yet");
            }
        }

        private Constant LConst(HirValue v)
        {
            // Restrict to constants for use in constant contexts (e.g., global initializers)
            return v switch
            {
                HirConstantInt ci => ctx.CreateConstant(unchecked((uint)ci.Value)),
                HirConstantFloat cf => cf.NativeType is HirFloatType
                    ? ctx.CreateConstant((float)cf.Value)
                    : ctx.CreateConstant(cf.Value),
                HirConstantString s => MakeGlobalStringPtrConst(s.Value),
                HirFunction f => _funcs[f],
                HirGlobalObject g => _globals[g],
                _ => throw new NotSupportedException($"Non-constant initializer: {v.GetType().Name}")
            };
        }

        private Constant MakeGlobalStringPtrConst(string text)
        {
            // Create a private constant global for the bytes and return a constant GEP to the first element (i8*)
            var data = ctx.CreateConstantString(text, @nullTerminate: true);
            var g = mod.AddGlobal(data.NativeType, true, linkage: Linkage.Private, data, name: "");
            g.UnnamedAddress = UnnamedAddressKind.Global;
            g.Alignment = 1;

            return g;
        }

        private Constant MakeGlobalStringPtr(string text)
        {
            return MakeGlobalStringPtrConst(text);
        }

        private ITypeRef LlType(HirType t)
        {
            switch (t)
            {
                case HirIntType it:
                    return ctx.GetIntType((uint)it.SizeInBits);
                case HirFloatType:
                    return ctx.FloatType;
                case HirDoubleType:
                    return ctx.DoubleType;
                case HirVoidType:
                    return ctx.VoidType;
                case HirPointerType pt:
                    return LlType(pt.Pointee).CreatePointerType();
                case HirStructType st:
                    return GetOrCreateStruct(st);
                case HirFunctionType ft:
                    var ret = LlType(ft.ReturnType);
                    var ps = ft.Params.Select(LlType).ToArray();
                    return ctx.GetFunctionType(ft.IsVarArg, ret, ps);
                default:
                    throw new NotSupportedException($"Type {t.GetType().Name} not supported");
            }
        }

        private IStructType GetOrCreateStruct(HirStructType st)
        {
            var key = st.Name;
            if (!_structCache.TryGetValue(key, out var s))
            {
                s = ctx.CreateStructType(key, false);
                _structCache[key] = s;

                // Fill body now (HIR structs are fully known here)
                var fieldTys = st.Fields.Select(LlType).ToArray();
                s.SetBody(false, fieldTys);
            }

            return s;
        }

        private static bool IsFp(HirType t) => t is HirFpType;
        private static bool IsSigned(HirType t) => t is HirIntType { Signed: true };
    }
}
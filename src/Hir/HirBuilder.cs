using System.Diagnostics;
using JetBrains.Annotations;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Boolean = RiddleSharp.Frontend.Boolean;

namespace RiddleSharp.Hir;

public enum EvalMode
{
    Address,
    Value
} // lvalue 要地址，rvalue 要值

public sealed class HirBuilder(
    HirModule mod,
    Func<MemberAccess, int>? memberIndexResolver = null,
    Func<ClassDecl, HirStructType>? classTypeLowerer = null)
{
    private HirFunction _curFunc = null!;
    private HirBasicBlock _curBlock = null!;
    private int _id;

    private readonly Stack<Dictionary<Decl, HirValue>> _scopes = new();

    public HirFunction BuildFunction(FuncDecl f, string? mangledName = null)
    {
        var ft = (HirFunctionType)LowerTy(f.Type ?? throw new InvalidOperationException("missing function type"));

        var fun = new HirFunction(ft, mangledName ?? f.Name);
        mod.Functions.Add(fun);

        for (var i = 0; i < ft.Params.Count; i++)
        {
            var p = new HirParameter(f.Args[i].Name, ft.Params[i]);
            fun.Parameters.Add(p);
        }

        var entry = NewBlock("entry");
        fun.Blocks.Add(entry);
        _curFunc = fun;
        _curBlock = entry;

        PushScope();

        // 形参写入栈位（保证参数也能取地址）
        for (var i = 0; i < f.Args.Count; i++)
        {
            var argDecl = f.Args[i];
            var paramVal = fun.Parameters[i];

            var alloca = EmitAlloca(paramVal.NativeType);
            EmitStore(alloca, paramVal);
            Bind(argDecl, alloca);
        }

        if (f.Body is not null)
        {
            foreach (var s in f.Body)
                BuildStmt(s);
        }
        else
        {
            // 外部声明/无体函数 插一个不可达 return
            EmitTerminator(new HirUnreachable());
        }

        PopScope();
        return fun;
    }


    private void BuildStmt(Stmt s)
    {
        switch (s)
        {
            case Block b:
                PushScope();
                foreach (var st in b.Body) BuildStmt(st);
                PopScope();
                break;

            case ExprStmt es:
                _ = BuildExpr(es.Expr, EvalMode.Value);
                break;

            case Return r:
                BuildReturn(r);
                break;

            case VarDecl v:
                BuildVarDecl(v);
                break;

            case If iff:
                BuildIf(iff);
                break;

            case While wh:
                BuildWhile(wh);
                break;

            default:
                throw new NotSupportedException($"Stmt {s.GetType().Name}");
        }
    }

    private HirValue BuildExpr(Expr e, EvalMode mode)
    {
        return e switch
        {
            Integer i => EmitConstInt(i),
            Boolean b => EmitConstBool(b),
            StringLit s => EmitConstString(s),
            Symbol s => mode == EvalMode.Address
                ? GetAddressOfSymbol(s)
                : EmitLoad(GetAddressOfSymbol(s), LowerTy(e.Type!)),
            BinaryOp bin => EmitBinary(bin),
            Call call => EmitCall(call),
            MemberAccess m => mode == EvalMode.Address
                ? EmitAddressOfMember(m)
                : EmitLoad(EmitAddressOfMember(m), LowerTy(m.Type!)),
            PointedExpr p => mode == EvalMode.Address
                ? BuildExpr(p.Value, EvalMode.Value) /* value is ptr */
                : EmitLoad(BuildExpr(p.Value, EvalMode.Value), LowerTy(p.Type!)),
            _ => throw new NotSupportedException(e.GetType().Name)
        };
    }

    private void BuildVarDecl(VarDecl v)
    {
        var ty = LowerTy(v.Type ?? throw new("no type for var"));
        var addr = EmitAlloca(ty);
        Bind(v, addr);

        if (v.Value is not null)
        {
            var rv = BuildExpr(v.Value, EvalMode.Value);
            EmitStore(addr, MaybeCast(rv, ty));
        }
    }

    private void BuildReturn(Return r)
    {
        var retTy = _curFunc.NativeType.ReturnType;
        var v = r.Expr is null ? null : MaybeCast(BuildExpr(r.Expr, EvalMode.Value), retTy);
        EmitTerminator(new HirReturn(v));
    }

    private void BuildIf(If iff)
    {
        var thenBlock = NewBlock("then");
        var elseBlock = NewBlock("else");
        var mergeBlock = NewBlock("merge");

        var condVal = BuildExpr(iff.Condition, EvalMode.Value);
        // cond 必须为 i1。必要时 cast
        condVal = MaybeCastToBool(condVal);

        EmitTerminator(new HirCondBr(condVal, thenBlock, elseBlock));

        // then
        AppendBlock(thenBlock);
        BuildStmt(iff.Then);
        if (_curBlock.Terminator is null)
            EmitTerminator(new HirBr(mergeBlock));

        // else
        AppendBlock(elseBlock);
        if (iff.Else is not null) BuildStmt(iff.Else);
        if (_curBlock.Terminator is null)
            EmitTerminator(new HirBr(mergeBlock));
        else return;

        // merge
        AppendBlock(mergeBlock);
    }

    private void BuildWhile(While wh)
    {
        var condBlock = NewBlock("while.cond");
        var bodyBlock = NewBlock("while.body");
        var exitBlock = NewBlock("while.exit");

        EmitTerminator(new HirBr(condBlock));

        // cond
        AppendBlock(condBlock);
        var condVal = BuildExpr(wh.Condition, EvalMode.Value);
        condVal = MaybeCastToBool(condVal);
        EmitTerminator(new HirCondBr(condVal, bodyBlock, exitBlock));

        // body
        AppendBlock(bodyBlock);
        BuildStmt(wh.Body);
        if (_curBlock.Terminator is null)
            EmitTerminator(new HirBr(condBlock));

        // exit
        AppendBlock(exitBlock);
    }


    private HirValue EmitBinary(BinaryOp bin)
    {
        var l = BuildExpr(bin.Left, EvalMode.Value);
        var r = BuildExpr(bin.Right, EvalMode.Value);

        switch (bin.Op)
        {
            case "==":
            case "!=":
            case "<":
            case "<=":
            case ">":
            case ">=":
            {
                var (ll, rr, _) = NumericBalance(l, r);
                var pred = bin.Op switch
                {
                    "==" => HirCmpPred.Eq,
                    "!=" => HirCmpPred.Ne,
                    "<" => HirCmpPred.Lt,
                    "<=" => HirCmpPred.Le,
                    ">" => HirCmpPred.Gt,
                    ">=" => HirCmpPred.Ge,
                    _ => throw new UnreachableException()
                };
                return Emit(new HirCompare(pred, ll, rr));
            }
        }

        if (bin.Op is "&&" or "||")
        {
            var lb = MaybeCastToBool(l);
            var rb = MaybeCastToBool(r);
            var kind = bin.Op == "&&" ? HirBinOp.And : HirBinOp.Or;
            return Emit(new HirBinary(kind, lb, rb, new HirIntType(1, false)));
        }

        var (ll2, rr2, ty) = NumericBalance(l, r);
        var op = bin.Op switch
        {
            "+" => HirBinOp.Add,
            "-" => HirBinOp.Sub,
            "*" => HirBinOp.Mul,
            "/" => HirBinOp.Div,
            "%" => HirBinOp.Mod,

            // 位运算
            "&" => HirBinOp.And,
            "|" => HirBinOp.Or,
            "^" => HirBinOp.Xor,
            "<<" => HirBinOp.Shl,
            ">>" => HirBinOp.Shr,

            _ => throw new NotSupportedException(bin.Op)
        };
        return Emit(new HirBinary(op, ll2, rr2, ty));
    }

    private (HirValue L, HirValue R, HirType T) NumericBalance(HirValue l, HirValue r)
    {
        var t = TypeUnifier.Unify(l.NativeType, r.NativeType);
        return (MaybeCast(l, t), MaybeCast(r, t), t);
    }

    private HirValue MaybeCast(HirValue v, HirType t) =>
        TypesEqual(v.NativeType, t) ? v : Emit(new HirCast(ChooseCast(v.NativeType, t), v, t));

    private HirValue MaybeCastToBool(HirValue v)
    {
        switch (v.NativeType)
        {
            // i1 直接用；整数/指针转 i1：!= 0；浮点转 i1：和 0.0 比较
            case HirIntType { SizeInBits: 1 }:
                return v;
            case HirIntType:
            {
                var zero = new HirConstantInt(0, v.NativeType);
                return Emit(new HirCompare(HirCmpPred.Ne, v, zero));
            }
            case HirPointerType:
            {
                var zero = new HirConstantInt(0, new HirIntType(64, false)); // TODO: ptr size
                var asInt = Emit(new HirCast(HirCastKind.PtrToInt, v, zero.NativeType));
                return Emit(new HirCompare(HirCmpPred.Ne, asInt, zero));
            }
            case HirFpType type:
            {
                var zero = new HirConstantFloat(0.0, type);
                return Emit(new HirCompare(HirCmpPred.Ne, v, zero));
            }
            default:
                throw new NotSupportedException($"Cannot cast {v.NativeType} to bool");
        }
    }

    private HirValue EmitCall(Call call)
    {
        var calleeVal = BuildExpr(call.Callee, EvalMode.Value);
        // 期望 callee 的类型是函数指针或直接函数
        var fty = calleeVal.NativeType switch
        {
            HirFunctionType ft => ft,
            HirPointerType { Pointee: HirFunctionType ft } => ft,
            _ => throw new InvalidOperationException("callee is not a function")
        };

        var args = new List<HirValue>(call.Args.Length);
        for (var i = 0; i < call.Args.Length; i++)
        {
            var a = BuildExpr(call.Args[i], EvalMode.Value);
            var needTy = fty.Params[Math.Min(i, fty.Params.Count - 1)];
            args.Add(MaybeCast(a, needTy));
        }

        return Emit(new HirCall(calleeVal, args, fty.ReturnType));
    }

    private HirValue GetAddressOfSymbol(Symbol s)
    {
        if (s.DeclReference is null || !s.DeclReference.TryGetTarget(out var decl))
            throw new InvalidOperationException($"Unresolved symbol: {s.Name}");

        foreach (var scope in _scopes)
            if (scope.TryGetValue(decl, out var addr))
                return addr;

        var gvar = mod.Globals.OfType<HirGlobalVariable>()
            .FirstOrDefault(g => g.Name == decl.Name || decl.QualifiedName?.ToString() == g.Name);
        if (gvar is not null) return gvar;

        var gfun = mod.Functions
            .FirstOrDefault(f => f.Name == decl.Name || decl.QualifiedName?.ToString() == f.Name);
        return gfun ?? throw new InvalidOperationException($"Symbol not bound: {s.Name}");
    }

    private HirValue EmitAddressOfMember(MemberAccess m)
    {
        var parentAddr = BuildExpr(m.Parent, EvalMode.Address);

        // 拿到父类型关联的 ClassDecl（优先从类型的 DeclRef，退化到符号引用）
        ClassDecl? cd = null;

        if (m.Parent.Type is Ty.ClassTy cty && cty.TryGetDecl(out var fromTy))
            cd = fromTy;

        if (cd is null && m.Parent is Symbol { DeclReference: not null } ps &&
            ps.DeclReference.TryGetTarget(out var owner) &&
            owner is ClassDecl clsFromSym)
            cd = clsFromSym;

        if (cd is null)
            throw new InvalidOperationException("Cannot resolve class declaration for member access.");

        // 计算成员索引, 优先使用外部注入的 resolver, 否则按声明顺序在 Members 中查
        var index = memberIndexResolver?.Invoke(m) ?? IndexOfMember(cd, m.Child);

        // GEP要求父类型为 ptr-to-struct
        var parentTy = LowerTy(m.Parent.Type!);
        if (parentTy is HirPointerType { Pointee: HirStructType st })
        {
            var i32 = new HirIntType(32, false);
            var zero = new HirConstantInt(0, i32);
            var idx = new HirConstantInt(index, i32);
            return Emit(new HirGetElementPtr(parentAddr, [zero, idx], new HirPointerType(st.Fields[index])));
        }

        throw new NotSupportedException("MemberAccess on non-struct pointer");
    }

    private static int IndexOfMember(ClassDecl cd, string name)
    {
        var keys = cd.Members.Keys.ToList();
        var idx = keys.IndexOf(name);
        return idx < 0
            ? throw new InvalidOperationException($"Member '{name}' not found in class '{cd.QualifiedName}'.")
            : idx;
    }


    private T Emit<T>(T inst) where T : HirInstruction
    {
        inst.Id = ++_id;
        _curBlock.Inst.Add(inst);
        return inst;
    }

    private void EmitTerminator(IHirTerminator term)
    {
        if (_curBlock.Terminator is not null)
            throw new InvalidOperationException("Terminator already set for current block");

        switch (term)
        {
            case HirBr br:
                _curBlock.Successors.Add(br.Target);
                br.Target.Predecessors.Add(_curBlock);
                Emit((HirInstruction)term);
                _curBlock.Terminator = term;
                break;
            case HirCondBr cbr:
                _curBlock.Successors.Add(cbr.Then);
                _curBlock.Successors.Add(cbr.Else);
                cbr.Then.Predecessors.Add(_curBlock);
                cbr.Else.Predecessors.Add(_curBlock);
                Emit((HirInstruction)term);
                _curBlock.Terminator = term;
                break;
            case HirReturn or HirUnreachable:
                Emit((HirInstruction)term);
                _curBlock.Terminator = term;
                break;
            default:
                throw new NotSupportedException(term.GetType().Name);
        }
    }

    private static HirBasicBlock NewBlock(string name) => new(name);

    private void AppendBlock(HirBasicBlock bb)
    {
        if (!_curFunc.Blocks.Contains(bb))
            _curFunc.Blocks.Add(bb);
        _curBlock = bb;
    }

    private void PushScope() => _scopes.Push(new Dictionary<Decl, HirValue>(ReferenceEqualityComparer<Decl>.Instance));
    private void PopScope() => _scopes.Pop();
    private void Bind(Decl d, HirValue v) => _scopes.Peek()[d] = v;


    private HirConstantInt EmitConstInt(Integer i)
    {
        // 依据 AST 的类型（若有）择优映射；默认 i32
        var t = i.Type is not null ? LowerTy(i.Type) : new HirIntType(32, true);
        return new HirConstantInt(i.Value, t);
    }

    private static HirConstantInt EmitConstBool(Boolean b)
    {
        return new HirConstantInt(b.Value ? 1 : 0, new HirIntType(1, false));
    }

    private HirConstantString EmitConstString(StringLit s)
    {
        var t = LowerTy(s.Type!);
        return new HirConstantString(s.Value, t);
    }

    private HirAlloca EmitAlloca(HirType ty) => Emit(new HirAlloca(ty));
    private HirLoad EmitLoad(HirValue addr, HirType resultType) => Emit(new HirLoad(addr, resultType));
    private HirStore EmitStore(HirValue addr, HirValue value) => Emit(new HirStore(addr, value));

    private static bool TypesEqual(HirType a, HirType b) => a.Equals(b);

    private static HirCastKind ChooseCast(HirType from, HirType to)
    {
        if (TypesEqual(from, to)) return HirCastKind.BitCast;

        switch (from)
        {
            case HirIntType fi when to is HirIntType ti:
            {
                if (fi.SizeInBits < ti.SizeInBits) return fi.Signed ? HirCastKind.SExt : HirCastKind.ZExt;
                return fi.SizeInBits > ti.SizeInBits ? HirCastKind.Trunc : HirCastKind.BitCast;
            }
            case HirFpType when to is HirFpType:
                // todo 扩展为 FPExt/FPTrunc 枚举
                return HirCastKind.BitCast;
            case HirIntType when to is HirFpType:
                return HirCastKind.SiToFp;
            case HirFpType when to is HirIntType:
                return HirCastKind.FpToSi;
            case HirPointerType when to is HirPointerType:
                return HirCastKind.BitCast;
            case HirPointerType when to is HirIntType:
                return HirCastKind.PtrToInt;
            case HirIntType when to is HirPointerType:
                return HirCastKind.IntToPtr;
            default:
                throw new NotSupportedException($"Cast {from} -> {to}");
        }
    }

    private HirType LowerTy(Ty ty)
    {
        return ty switch
        {
            Ty.IntTy it => new HirIntType((long)it.WidthInBits / 8, it.Signed),
            Ty.PointerType pt => new HirPointerType(LowerTy(pt.Pointee)),
            Ty.FloatTy => new HirFloatType(),
            Ty.DoubleTy => new HirDoubleType(),
            Ty.VoidTy => new HirVoidType(),

            Ty.ClassTy ct => LowerClassTy(ct),

            Ty.FuncTy fun => new HirFunctionType(
                LowerTy(fun.Ret),
                fun.Args.Select(LowerTy).ToList(),
                fun.IsVarArg),

            _ => throw new NotSupportedException($"LowerTy: {ty}")
        };

        HirType LowerClassTy(Ty.ClassTy ct)
        {
            if (ct.TryGetDecl(out var cls))
            {
                if (classTypeLowerer is not null)
                    return classTypeLowerer(cls!);

                var fieldTypes = cls!.Members.Values
                    .Select(m => m.Type ?? throw new InvalidOperationException(
                        $"Field '{m.Name}' of class '{cls.Name}' has no type"))
                    .Select(LowerTy)
                    .ToArray();

                return new HirStructType(cls.QualifiedName!.ToString(), fieldTypes);
            }

            throw new InvalidOperationException(
                $"Class type '{ct.Name}' missing DeclRef; ensure SymbolPass sets it before lowering.");
        }
    }


    [UsedImplicitly]
    private sealed class TypeUnifier
    {
        public static HirType Unify(HirType a, HirType b)
        {
            if (a.Equals(b)) return a;

            if (a is HirIntType ai && b is HirIntType bi)
            {
                var bits = Math.Max(ai.SizeInBits, bi.SizeInBits);
                var signed = ai.Signed || bi.Signed;
                return new HirIntType((long)bits, signed);
            }

            // fp vs fp 提升到更宽
            if (a is HirFpType af && b is HirFpType bf)
            {
                var bits = Math.Max(af.SizeInBits, bf.SizeInBits);
                return bits switch
                {
                    32 => new HirFloatType(),
                    64 => new HirDoubleType(),
                    _ => throw new Exception("Unknown float bits")
                };
            }

            // int vs fp 提升为 fp
            if (a is HirIntType && b is HirFpType) return b;
            if (a is HirFpType && b is HirIntType) return a;

            // 指针必须相同形状 ; 此处简化：不同也认为能 bitCast
            if (a is HirPointerType && b is HirPointerType) return a;

            throw new InvalidOperationException($"Cannot unify types {a} and {b}");
        }
    }

    private sealed class ReferenceEqualityComparer<TRef> : IEqualityComparer<TRef>
        where TRef : class
    {
        public static readonly ReferenceEqualityComparer<TRef> Instance = new();
        public bool Equals(TRef? x, TRef? y) => ReferenceEquals(x, y);
        public int GetHashCode(TRef obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
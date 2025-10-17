using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Boolean = RiddleSharp.Frontend.Boolean;

namespace RiddleSharp.Hir;

public static class HirGen
{
    public static HirModule Run(Unit[] units)
    {
        var mod = new HirModule();

        foreach (var u in units)
        foreach (var d in u.Stmts.OfType<FuncDecl>())
        {
            if (d.Type is not { } ft)
                throw new InvalidOperationException($"Function '{d.Name}' has no resolved type.");
            var hft = new HirFunctionType(
                LowerTy(ft.Ret),
                ft.Args.Select(LowerTy).ToList(),
                ft.IsVarArg);
            var fun = new HirFunction(hft, (d.QualifiedName ?? QualifiedName.Parse(d.Name)).ToString());
            mod.Functions.Add(fun);
        }

        foreach (var u in units)
        foreach (var g in u.Stmts.OfType<VarDecl>().Where(v => v.IsGlobal))
        {
            var ty = LowerTy(g.Type ?? throw new Exception($"Global '{g.Name}' has no type"));
            HirValue init = TryLowerConst(g.Value) ?? new HirConstantInt(0, ty as HirIntType ?? new HirIntType(32));
            var name = (g.QualifiedName ?? QualifiedName.Parse(g.Name)).ToString();
            mod.Globals.Add(new HirGlobalVariable(ty, name, init));
        }

        var builder = new HirBuilder(
            mod,
            memberIndexResolver: m =>
            {
                var clsTy = (Ty.ClassTy)m.Parent.Type!;
                if (!clsTy.TryGetDecl(out var cd))
                    throw new InvalidOperationException("ClassTy has no DeclRef");
                var keys = cd!.Members.Keys.ToList();
                var idx = keys.IndexOf(m.Child);
                return idx < 0 ? throw new InvalidOperationException($"'{m.Child}' not found") : idx;
            },
            classTypeLowerer: cd =>
            {
                var fields = cd.Members.Values
                    .Select(v => LowerTy(v.Type!))
                    .ToArray();
                return new HirStructType(cd.QualifiedName!.ToString(), fields);
            });

        foreach (var u in units)
        foreach (var f in u.Stmts.OfType<FuncDecl>())
        {
            var fullName = (f.QualifiedName ?? QualifiedName.Parse(f.Name)).ToString();
            var hf = mod.Functions.First(fn => fn.Name == fullName);

            if (f.Body is null) continue;

            builder.BuildFunction(f, fullName);
        }

        return mod;

        HirConstant? TryLowerConst(Expr? e) => e switch
        {
            null           => null,
            Integer i      => new HirConstantInt(i.Value, LowerTy(i.Type ?? Ty.IntTy.Int32)),
            StringLit s    => new HirConstantString(s.Value, LowerTy(s.Type ?? Ty.PointerType.CharPointer)),
            Boolean b      => new HirConstantInt(b.Value ? 1 : 0, new HirIntType(1, false)),
            _              => null
        };

        HirType LowerTy(Ty ty) => ty switch
        {
            Ty.IntTy it        => new HirIntType((long)it.WidthInBits/8, it.Signed),
            Ty.PointerType pt  => new HirPointerType(LowerTy(pt.Pointee)),
            Ty.FloatTy         => new HirFloatType(),
            Ty.DoubleTy        => new HirDoubleType(),
            Ty.VoidTy          => new HirVoidType(),
            Ty.ClassTy ct      => LowerClassTy(ct),
            Ty.FuncTy fun      => new HirFunctionType(
                                      LowerTy(fun.Ret),
                                      fun.Args.Select(LowerTy).ToList(),
                                      fun.IsVarArg),
            _ => throw new NotSupportedException($"LowerTy: {ty}")
        };

        HirType LowerClassTy(Ty.ClassTy ct)
        {
            if (ct.TryGetDecl(out var cd))
            {
                var fields = cd!.Members.Values.Select(v => LowerTy(v.Type!)).ToArray();
                return new HirStructType(cd.QualifiedName!.ToString(), fields);
            }
            return new HirStructType(ct.Name.ToString(), []);
        }
    }
}
using System.Text;

namespace RiddleSharp.Hir;

public sealed class HirPrinter
{
    private readonly StringBuilder _sb = new();
    private readonly Dictionary<HirValue, string> _names = new();

    public string Print(HirModule m)
    {
        _sb.Clear();
        _names.Clear();

        // === Globals ===
        foreach (var g in m.Globals)
        {
            switch (g)
            {
                case HirGlobalVariable gv:
                    _sb.Append('@').Append(gv.Name)
                      .Append(" : ").Append(T(gv.NativeType))
                      .Append(" = ").Append(V(gv.Init))
                      .AppendLine();
                    break;
                default:
                    _sb.Append('@').Append(g.Name)
                      .Append(" : ").Append(T(g.NativeType))
                      .AppendLine();
                    break;
            }
        }
        if (m.Globals.Count > 0) _sb.AppendLine();

        foreach (var fn in m.Functions)
        {
            PrintFunction(fn);
            _sb.AppendLine();
        }

        return _sb.ToString();
    }

    private void PrintFunction(HirFunction f)
    {
        var ft = f.NativeType;
        _sb.Append("func @").Append(f.Name).Append('(');

        for (int i = 0; i < f.Parameters.Count; i++)
        {
            var p = f.Parameters[i];
            if (i > 0) _sb.Append(", ");
            _sb.Append(T(p.NativeType)).Append(' ').Append(V(p));
        }
        if (ft.IsVarArg) _sb.Append(f.Parameters.Count > 0 ? ", ..." : "...");

        _sb.Append(") -> ").Append(T(ft.ReturnType)).AppendLine();
        _sb.AppendLine("{");

        foreach (var bb in f.Blocks)
        {
            _sb.Append('^').Append(bb.Name).Append(':').AppendLine();

            foreach (var inst in bb.Inst)
            {
                // left side (result)
                var hasResult = inst.NativeType is not HirVoidType;
                if (hasResult)
                {
                    _sb.Append("  ").Append(V(inst)).Append(" = ");
                }
                else
                {
                    _sb.Append("  ");
                }

                // opcode + operands
                PrintInstruction(inst, bb);

                _sb.AppendLine();
            }
            _sb.AppendLine();
        }

        _sb.Append('}');
    }

    private void PrintInstruction(HirInstruction i, HirBasicBlock ctx)
    {
        switch (i)
        {
            case HirAlloca a:
                _sb.Append("alloca ").Append(T(a.AllocType));
                break;

            case HirLoad ld:
                _sb.Append("load ").Append(T(ld.NativeType)).Append(' ').Append(V(ld.Address));
                break;

            case HirStore st:
                _sb.Append("store ")
                   .Append(T(st.Value.NativeType)).Append(' ').Append(V(st.Value))
                   .Append(", ").Append(T(st.Address.NativeType)).Append(' ').Append(V(st.Address));
                break;

            case HirBinary bi:
                _sb.Append(OpName(bi.Op)).Append(' ')
                   .Append(T(bi.NativeType)).Append(' ')
                   .Append(V(bi.Left)).Append(", ").Append(V(bi.Right));
                break;

            case HirCompare cmp:
                _sb.Append("cmp.").Append(cmp.Pred.ToString().ToLowerInvariant()).Append(' ')
                   .Append(V(cmp.Left)).Append(", ").Append(V(cmp.Right));
                break;

            case HirCast cs:
                _sb.Append("cast.").Append(CastName(cs.Kind)).Append(' ')
                   .Append(T(cs.Value.NativeType)).Append(' ').Append(V(cs.Value))
                   .Append(" to ").Append(T(cs.NativeType));
                break;

            case HirGetElementPtr gep:
                _sb.Append("gep ")
                   .Append(T(gep.Address.NativeType)).Append(' ').Append(V(gep.Address))
                   .Append(", [");
                for (int k = 0; k < gep.Indices.Count; k++)
                {
                    if (k > 0) _sb.Append(", ");
                    _sb.Append(V(gep.Indices[k]));
                }
                _sb.Append("]");
                break;

            case HirCall call:
                _sb.Append("call ").Append(T(call.NativeType)).Append(' ')
                   .Append(V(call.Callee)).Append('(');
                for (int a = 0; a < call.Args.Count; a++)
                {
                    if (a > 0) _sb.Append(", ");
                    var arg = call.Args[a];
                    _sb.Append(T(arg.NativeType)).Append(' ').Append(V(arg));
                }
                _sb.Append(')');
                break;

            case HirSelect sel:
                _sb.Append("select ")
                   .Append(V(sel.Cond)).Append(", ")
                   .Append(V(sel.TrueValue)).Append(", ")
                   .Append(V(sel.FalseValue));
                break;

            case HirPhi phi:
                _sb.Append("phi ").Append(T(phi.NativeType)).Append(' ');
                for (int j = 0; j < phi.Incoming.Count; j++)
                {
                    if (j > 0) _sb.Append(", ");
                    var (pred, val) = phi.Incoming[j];
                    _sb.Append('[').Append(V(val)).Append(", ^").Append(pred.Name).Append(']');
                }
                break;

            // --- terminators ---
            case HirReturn ret:
                if (ret.Value is null)
                {
                    _sb.Append("ret");
                }
                else
                {
                    _sb.Append("ret ").Append(T(ret.Value.NativeType)).Append(' ').Append(V(ret.Value));
                }
                break;

            case HirBr br:
                _sb.Append("br ^").Append(br.Target.Name);
                break;

            case HirCondBr cbr:
                _sb.Append("cbr ").Append(V(cbr.Cond))
                   .Append(", ^").Append(cbr.Then.Name)
                   .Append(", ^").Append(cbr.Else.Name);
                break;

            case HirUnreachable:
                _sb.Append("unreachable");
                break;

            default:
                _sb.Append("; <unknown instr ").Append(i.GetType().Name).Append('>');
                break;
        }
    }

    // ----- helpers -----
    private string V(HirValue v)
    {
        // constants and named values
        switch (v)
        {
            case HirConstantInt ci:
                return ci.Value.ToString();
            case HirConstantFloat cf:
                return cf.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case HirConstantString cs:
                return $"\"{Escape(cs.Value)}\"";

            case HirGlobalObject g:
                return "@" + g.Name;

            case HirParameter p:
                return "%" + p.Name;

            default:
                // instructions and unnamed values -> %v{id}
                if (!_names.TryGetValue(v, out var n))
                {
                    n = "%v" + v.Id;
                    _names[v] = n;
                }
                return n;
        }
    }

    private static string T(HirType t) => t switch
    {
        HirVoidType        => "void",
        HirIntType it      => (it.SizeInBits == 1) ? "i1" : (it.Signed ? $"i{it.SizeInBits}" : $"u{it.SizeInBits}"),
        HirFpType fp       => fp.SizeInBits switch { 32 => "f32", 64 => "f64", _ => $"f{fp.SizeInBits}" },
        HirPointerType pt  => T(pt.Pointee) + "*",
        HirStructType st   => "struct " + st.Name + " { " + string.Join(", ", st.Fields.Select(T)) + " }",
        HirFunctionType ft => "fn(" + string.Join(", ", ft.Params.Select(T)) + (ft.IsVarArg ? (ft.Params.Count > 0 ? ", ..." : "...") : "") + $") -> {T(ft.ReturnType)}",
        _                  => t.ToString() ?? "<type>"
    };

    private static string OpName(HirBinOp op) => op switch
    {
        HirBinOp.Add => "add",
        HirBinOp.Sub => "sub",
        HirBinOp.Mul => "mul",
        HirBinOp.Div => "div",
        HirBinOp.Mod => "mod",
        HirBinOp.And => "and",
        HirBinOp.Or  => "or",
        HirBinOp.Xor => "xor",
        HirBinOp.Shl => "shl",
        HirBinOp.Shr => "shr",
        _            => op.ToString().ToLowerInvariant()
    };

    private static string CastName(HirCastKind k) => k switch
    {
        HirCastKind.BitCast => "bitcast",
        HirCastKind.ZExt    => "zext",
        HirCastKind.SExt    => "sext",
        HirCastKind.Trunc   => "trunc",
        HirCastKind.FpToSi  => "fptosi",
        HirCastKind.SiToFp  => "sitofp",
        HirCastKind.PtrToInt=> "ptrtoint",
        HirCastKind.IntToPtr=> "inttoptr",
        _                   => k.ToString().ToLowerInvariant()
    };

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '\"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(ch) => $"\\x{(int)ch:x2}",
                _ => ch
            });
        }
        return sb.ToString();
    }
}

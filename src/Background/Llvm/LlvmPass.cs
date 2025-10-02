using System.Reflection;
using System.Runtime.CompilerServices;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Ubiquity.NET.Llvm;
using Ubiquity.NET.Llvm.Instructions;
using Ubiquity.NET.Llvm.Interop;
using Ubiquity.NET.Llvm.Interop.ABI.llvm_c;
using Ubiquity.NET.Llvm.Types;
using Ubiquity.NET.Llvm.Values;
using Module = Ubiquity.NET.Llvm.Module;

namespace RiddleSharp.Background.Llvm;

public static class LlvmPass
{
    public static void Run(Unit[] units)
    {
        ConditionalWeakTable<VarDecl, Value> vars = new();
        ConditionalWeakTable<FuncDecl, Value> functions = new();
        using var context = new Context();
        using var module = context.CreateBitcodeModule();
        foreach (var unit in units)
        {
            var v = new LlvmVisitor(context, module, vars, functions);
            v.Visit(unit);
        }

        Console.WriteLine(module.WriteToString());
    }

    private static object? GetHiddenHandle(object inst)
    {
        var t = inst.GetType();
        
        var p = t.GetProperty("Handle", BindingFlags.NonPublic | BindingFlags.Instance);
        if (p != null) return p.GetValue(inst);
        
        foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            if (f.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
                f.FieldType.Name.Contains("ValueRef", StringComparison.OrdinalIgnoreCase) ||
                f.FieldType == typeof(IntPtr))
                return f.GetValue(inst);

        return (from gp in t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
            where gp.Name.Contains("Handle", StringComparison.OrdinalIgnoreCase) ||
                  gp.PropertyType.Name.Contains("ValueRef", StringComparison.OrdinalIgnoreCase) ||
                  gp.PropertyType == typeof(IntPtr)
            select gp.GetValue(inst)).FirstOrDefault();
    }

    private class LlvmVisitor(
        Context context,
        Module module,
        ConditionalWeakTable<VarDecl, Value> vars,
        ConditionalWeakTable<FuncDecl, Value> functions)
        : AstVisitor<Value>
    {
        private readonly InstructionBuilder _builder = new(context);

        private readonly Dictionary<(Ty, Ty, string), Func<Value, Value, InstructionBuilder, Value>> _operators = new()
        {
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "+")] = (x, y, builder) => builder.Add(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "-")] = (x, y, builder) => builder.Sub(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "*")] = (x, y, builder) => builder.Mul(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "/")] = (x, y, builder) => builder.SDiv(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "%")] = (x, y, builder) => builder.SRem(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "=")] =
                (x, y, builder) =>
                {
                    var l = (x as Load)!;
                    Core.LLVMInstructionEraseFromParent((LLVMValueRef)GetHiddenHandle(l)!);
                    return builder.Store(y, l.Operands[0]!);
                },
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "==")] =
                (x, y, builder) => builder.Compare(IntPredicate.Equal, x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "!=")] =
                (x, y, builder) => builder.Compare(IntPredicate.NotEqual, x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "<")] =
                (x, y, builder) => builder.Compare(IntPredicate.SignedLessThan, x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "<=")] = (x, y, builder) =>
                builder.Compare(IntPredicate.SignedLessThanOrEqual, x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, ">")] =
                (x, y, builder) => builder.Compare(IntPredicate.SignedGreaterThan, x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, ">=")] = (x, y, builder) =>
                builder.Compare(IntPredicate.SignedGreaterThanOrEqual, x, y),
        };

        private Value GetVarAlloc(VarDecl var)
        {
            return vars.GetOrCreateValue(var);
        }

        private Value GetFunc(FuncDecl func)
        {
            return functions.GetOrCreateValue(func);
        }

        private T? VisitOrNull<T>(AstNode? node) where T : Value
        {
            return node is null ? null : (T?)Visit(node);
        }

        private T VisitOrThrow<T>(AstNode? node) where T : Value
        {
            if (node is null) throw new Exception("Node is null");
            return Visit(node) as T ?? throw new Exception($"Node is not of type {typeof(T)}");
        }

        private ITypeRef ParseType(Ty ty)
        {
            switch (ty)
            {
                case Ty.IntTy:
                    return context.Int32Type;
                case Ty.FuncTy funcTy:
                    var pty = funcTy.Args.Select(ParseType);
                    return context.GetFunctionType(ParseType(funcTy.Ret), pty);
                default:
                    throw new NotSupportedException($"Ty {ty} is not supported");
            }
        }

        public override Value VisitFuncDecl(FuncDecl node)
        {
            var name = node.QualifiedName!.ToString();
            // 做 main
            if (node.Name == "main")
            {
                name = "main";
            }

            var fty = (IFunctionType)ParseType(node.Type!);
            var func = module.CreateFunction(name, fty);
            functions.Add(node, func);
            var entry = func.AppendBasicBlock("entry");
            _builder.PositionAtEnd(entry);

            foreach (var a in node.Alloc)
            {
                var alloca = _builder.Alloca(ParseType(a.Type!));
                vars.Add(a, alloca);
            }

            foreach (var i in node.Body)
            {
                Visit(i);
            }

            return null!;
        }

        public override Value VisitVarDecl(VarDecl node)
        {
            if (node.IsGlobal)
            {
                var value = VisitOrThrow<Constant>(node.Value);
                module.AddGlobal(ParseType(node.Type!), false, Linkage.External, value, node.QualifiedName!.ToString());
                vars.Add(node, value);
            }
            else
            {
                var value = VisitOrNull<Value>(node.Value);
                if (value is not null)
                {
                    _builder.Store(value, GetVarAlloc(node));
                }
            }

            return null!;
        }

        public override Value VisitCall(Call node)
        {
            var callee = Visit(node.Callee);
            if (callee is not Function f)
            {
                throw new Exception("Call is not of type function");
            }

            var args = node.Args.Select(Visit).ToList();

            return _builder.Call(f, args);
        }

        public override Value VisitSymbol(Symbol node)
        {
            if (node.DeclReference is null || !node.DeclReference.TryGetTarget(out var d))
            {
                throw new Exception("Symbol is already declared");
            }

            switch (d)
            {
                case VarDecl v:
                    return v.IsGlobal ? GetVarAlloc(v) : _builder.Load(ParseType(v.Type!), GetVarAlloc(v));
                case FuncDecl f:
                    return GetFunc(f);
                default:
                    throw new NotImplementedException();
            }
        }

        public override Value VisitInteger(Integer node)
        {
            return context.CreateConstant(node.Value);
        }

        public override Value VisitBinaryOp(BinaryOp node)
        {
            var k = (node.Left.Type!, node.Right.Type!, node.Op);

            if (!_operators.TryGetValue(k, out var value))
            {
                throw new Exception($"Unknown operator \'{node.Left.Type} {node.Op} {node.Right.Type} \'");
            }

            return value(Visit(node.Left), Visit(node.Right), _builder);
        }
    }
}
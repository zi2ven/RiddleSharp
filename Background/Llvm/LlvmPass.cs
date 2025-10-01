using System.Runtime.CompilerServices;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Ubiquity.NET.Llvm;
using Ubiquity.NET.Llvm.Instructions;
using Ubiquity.NET.Llvm.Types;
using Ubiquity.NET.Llvm.Values;

namespace RiddleSharp.Background.Llvm;

public static class LlvmPass
{
    public static void Run(Unit[] units)
    {
        Context ctx = new();
        foreach (var unit in units)
        {
            var v = new LlvmVisitor(ctx);
            v.Visit(unit);
            Console.WriteLine(v.Module.WriteToString());
        }
    }

    private class LlvmVisitor(Context context) : AstVisitor<Value>
    {
        public readonly Module Module = context.CreateBitcodeModule();
        private readonly InstructionBuilder _builder = new(context);

        private readonly Dictionary<(Ty, Ty, string), Func<Value, Value, InstructionBuilder, Value>> _operators = new()
        {
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "+")] = (x, y, builder) => builder.Add(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "-")] = (x, y, builder) => builder.Sub(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "*")] = (x, y, builder) => builder.Mul(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "/")] = (x, y, builder) => builder.SDiv(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "%")] = (x, y, builder) => builder.SRem(x, y)
        };

        private readonly ConditionalWeakTable<VarDecl, Value> _vars = new();

        private Value GetVarAlloc(VarDecl var)
        {
            return _vars.GetOrCreateValue(var);
        }

        public T? VisitOrNull<T>(AstNode? node) where T : Value
        {
            return node is null ? null : (T?)Visit(node);
        }

        public T VisitOrThrow<T>(AstNode? node) where T : Value
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
            var fty = (IFunctionType)ParseType(node.Type!);
            var func = Module.CreateFunction(node.Name, fty);
            var entry = func.AppendBasicBlock("entry");
            _builder.PositionAtEnd(entry);

            foreach (var a in node.Alloc)
            {
                var alloca = _builder.Alloca(ParseType(a.Type!));
                _vars.Add(a, alloca);
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
                Module.AddGlobal(ParseType(node.Type!), false, Linkage.External, value, node.QualifiedName!.ToString());
                _vars.Add(node, value);
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
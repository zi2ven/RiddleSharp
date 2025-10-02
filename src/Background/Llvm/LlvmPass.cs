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
        ConditionalWeakTable<VarDecl, Value> vars = new();
        using var context = new Context();
        using var module = context.CreateBitcodeModule();
        foreach (var unit in units)
        {
            var v = new LlvmVisitor(context, module, vars);
            v.Visit(unit);
        }

        Console.WriteLine(module.WriteToString());
    }

    private class LlvmVisitor(Context context, Module module, ConditionalWeakTable<VarDecl, Value> vars)
        : AstVisitor<Value>
    {
        private readonly InstructionBuilder _builder = new(context);

        private readonly Dictionary<(Ty, Ty, string), Func<Value, Value, InstructionBuilder, Value>> _operators = new()
        {
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "+")] = (x, y, builder) => builder.Add(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "-")] = (x, y, builder) => builder.Sub(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "*")] = (x, y, builder) => builder.Mul(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "/")] = (x, y, builder) => builder.SDiv(x, y),
            [(Ty.IntTy.Instance, Ty.IntTy.Instance, "%")] = (x, y, builder) => builder.SRem(x, y)
        };

        private Value GetVarAlloc(VarDecl var)
        {
            return vars.GetOrCreateValue(var);
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
            var fty = (IFunctionType)ParseType(node.Type!);
            var func = module.CreateFunction(node.Name, fty);
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

        public override Value VisitSymbol(Symbol node)
        {
            if (node.DeclReference is null || !node.DeclReference.TryGetTarget(out var d))
            {
                throw new Exception("Symbol is already declared");
            }

            return d switch
            {
                VarDecl v => v.IsGlobal ? GetVarAlloc(v) : _builder.Load(ParseType(node.Type!), GetVarAlloc(v)),
                _ => throw new NotImplementedException()
            };
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
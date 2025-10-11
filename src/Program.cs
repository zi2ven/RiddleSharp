using RiddleSharp.Background.Llvm;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;

namespace RiddleSharp;

public static class Program
{
    private static string[] ReadFiles(string[] path)
    {
        return path.Select(File.ReadAllText).ToArray();
    }

    public static void Main(string[] args)
    {
        // var settings = ArgParser.Parse(args);

        // var files = ReadFiles(settings.Files);

        const string a = """
                         package main;
                         fun printf(fmt: char*, ...)->void;
                         class Foo{
                            var a: int;
                         }
                         fun main(){
                            var x: Foo;
                            x.a = 1;
                            var b = x.a;
                            printf("%d", b);
                            return;
                         }
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);


        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u1]);

        var tp = TypeInfer.Run(x);

        LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}
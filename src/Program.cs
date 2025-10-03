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
                         class Foo{
                            var a: int;
                            class ttt{
                                var b: Foo;
                            }
                            var c: ttt;
                         }
                         var a = Foo::ttt;
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);

        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u1]);

        // var tp = TypeInfer.Run(x);

        Console.WriteLine(x[0]);

        // LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}
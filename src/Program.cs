using RiddleSharp.Background.Lg;
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
                         var a = 1;
                         fun main(x: int) -> int {
                             return x + a;
                         }
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);

        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u1]);

        var tp = TypeInfer.Run(x);

        foreach (var i in tp)
        {
            Console.WriteLine(i);
        }

        LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}
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
                         import test;
                         var a = test::b;
                         fun main(x:int)->int{
                            var a = 1+1/1;
                            var b = a+1;
                         }
                         """;
        const string b = """
                         package test;
                         var b = 1;
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);
        var u2 = astLower.Parse(b);

        u1 = BinaryRotate.Run(u1);
        u2 = BinaryRotate.Run(u2);

        var x = SymbolPass.Run([u2, u1]);

        var tp = TypeInfer.Run(x);

        foreach (var i in tp)
        {
            Console.WriteLine(i);
        }

        LlvmPass.Run(tp);
    }
}
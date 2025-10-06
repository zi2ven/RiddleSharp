using System.Diagnostics;
using System.Runtime.InteropServices;
using RiddleSharp.Background.Llvm;
using RiddleSharp.Frontend;
using RiddleSharp.Semantics;
using Ubiquity.NET.Llvm;

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
                         fun fib(x:int)->int{
                            if(x<2){
                                return x;
                            }
                            else
                            return fib(x-1)+fib(x-2);
                         }
                         fun main(){
                            var a = fib(35);
                            return;
                         }
                         """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);

        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u1]);

        var tp = TypeInfer.Run(x);

        Console.WriteLine(x[0]);

        LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}

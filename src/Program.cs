using System.Diagnostics;
using System.Runtime.InteropServices;
using RiddleSharp.Background.Lg;
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
                         import test;
                         fun fib(x:int)->int{
                            if(x<2){
                                return x;
                            }
                            else
                            return fib(x-1)+fib(x-2);
                         }
                         fun main(){
                            var a = fib(35);
                            var b = test::get(1);
                            return;
                         }
                         """;
        const string cFile =  """
                              int get(int x);
                              """;

        var astLower = new CstLower();

        var u1 = astLower.Parse(a);
        var u2 = CppConverter.Run(cFile, "test");

        Console.WriteLine(u2);

        u1 = BinaryRotate.Run(u1);

        var x = SymbolPass.Run([u2, u1]);

        var tp = TypeInfer.Run(x);

        LlvmPass.Run(tp);
        // LgPass.Run(tp);
    }
}
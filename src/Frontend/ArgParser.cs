using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
// ReSharper disable ClassNeverInstantiated.Global

namespace RiddleSharp.Frontend;

public sealed class CompileSettings : CommandSettings
{
    [CommandArgument(0, "[files]")] public string[] Files { get; set; } = [];
    [CommandOption("-o|--output <File>")] public string Output { get; set; } = "";
}

internal sealed class CompileCommand : Command<CompileSettings>
{
    public override int Execute(CommandContext context, CompileSettings settings)
    {
        if (settings.Files.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]At least one input file is required[/]");
            return -1;
        }

        ArgParser.Settings = settings;
        return 0;
    }
}

public static class ArgParser
{

    public static CompileSettings? Settings = null;
    
    public static CompileSettings Parse(string[] args)
    {
        var app = new CommandApp<CompileCommand>();
        app.Run(args);
        if (Settings is null)
        {
            Environment.Exit(-1);
        }
        return Settings;
    }
}
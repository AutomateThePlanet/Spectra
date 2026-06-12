using System.Diagnostics;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Output;
using Spectra.CLI.Results;

namespace Spectra.CLI.Commands.Open;

public sealed class OpenHandler
{
    private readonly OutputFormat _outputFormat;

    public OpenHandler(OutputFormat outputFormat = OutputFormat.Human)
    {
        _outputFormat = outputFormat;
    }

    public Task<int> ExecuteAsync(string path)
    {
        if (!Path.IsPathRooted(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), path);

        bool opened = false;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            opened = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error opening '{path}': {ex.Message}");
        }

        var result = new OpenResult
        {
            Command = "open",
            Status = opened ? "completed" : "failed",
            Path = path,
            Opened = opened
        };

        if (_outputFormat == OutputFormat.Json)
            JsonResultWriter.Write(result);
        else if (opened)
            Console.WriteLine($"Opened: {path}");

        return Task.FromResult(opened ? ExitCodes.Success : ExitCodes.Error);
    }
}

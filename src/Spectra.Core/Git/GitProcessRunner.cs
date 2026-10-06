using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Spectra.Core.Git;

/// <summary>What one git process returned. <see cref="NotInstalled"/> is true when the executable could not be started.</summary>
public sealed record GitProcessResult(int ExitCode, byte[] Stdout, string Stderr, bool NotInstalled);

/// <summary>
/// Spec 086 (research R1): runs one git process. The seam <see cref="GitReader"/> is built on: the default runs
/// <c>git</c>, and tests pass a recording or faking runner.
/// </summary>
public interface IGitProcessRunner
{
    Task<GitProcessResult> RunAsync(
        string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, byte[]? stdin,
        CancellationToken ct);
}

/// <summary>
/// The <see cref="Process"/>-based runner: arguments through <see cref="ProcessStartInfo.ArgumentList"/> (no shell
/// quoting), the given environment over the inherited one, stdout captured as bytes, a 60 s timeout, and a missing
/// executable reported as <see cref="GitProcessResult.NotInstalled"/> instead of thrown.
/// </summary>
public sealed class ProcessGitRunner : IGitProcessRunner
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public async Task<GitProcessResult> RunAsync(
        string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, byte[]? stdin,
        CancellationToken ct)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            info.Environment[key] = value;
        }

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new Win32Exception("git did not start");
        }
        catch (Win32Exception)
        {
            return new GitProcessResult(-1, [], string.Empty, NotInstalled: true);
        }

        using (process)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(Timeout);
            var stdout = new MemoryStream();
            var readOut = process.StandardOutput.BaseStream.CopyToAsync(stdout, timeout.Token);
            var readErr = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                if (stdin is { Length: > 0 })
                {
                    await process.StandardInput.BaseStream.WriteAsync(stdin, timeout.Token);
                }

                process.StandardInput.Close();
                await Task.WhenAll(readOut, readErr);
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                TryKill(process);
                return new GitProcessResult(-1, [], $"git timed out after {Timeout.TotalSeconds:0} s", NotInstalled: false);
            }
            catch (IOException ex)
            {
                // The process exited before reading all of stdin (e.g. it failed early); report what it said.
                await process.WaitForExitAsync(ct);
                var err = readErr.IsCompletedSuccessfully ? readErr.Result : ex.Message;
                return new GitProcessResult(process.ExitCode, stdout.ToArray(), err, NotInstalled: false);
            }

            return new GitProcessResult(process.ExitCode, stdout.ToArray(), await readErr, NotInstalled: false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

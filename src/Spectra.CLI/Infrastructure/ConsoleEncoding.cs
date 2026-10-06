using System.Text;

namespace Spectra.CLI.Infrastructure;

/// <summary>
/// Spec 085 F6: UTF-8 output for redirected streams without touching the user's console. On Windows a redirected
/// stdout/stderr is otherwise written in the legacy code page, so non-ASCII text read by the skills (→, —, Ü) arrived
/// as '?' or 0x1A. Assigning <see cref="Console.OutputEncoding"/> would also switch the terminal window's code page for
/// the rest of the session, so only the redirected streams' writers are replaced; an interactive console is left as
/// the runtime provides it.
/// </summary>
public static class ConsoleEncoding
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void UseUtf8ForRedirectedStreams()
    {
        if (Console.IsOutputRedirected)
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Utf8NoBom) { AutoFlush = true });
        }

        if (Console.IsErrorRedirected)
        {
            Console.SetError(new StreamWriter(Console.OpenStandardError(), Utf8NoBom) { AutoFlush = true });
        }
    }
}

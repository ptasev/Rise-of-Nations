using System.Diagnostics;

using ConsoleAppFramework;

namespace RoNFileConverter;

internal class LogRunningTimeFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        try
        {
            await Next.InvokeAsync(context, cancellationToken);
            ConsoleApp.Log("Command succeeded. Elapsed: " + (Stopwatch.GetElapsedTime(startTime)));
        }
        catch
        {
            ConsoleApp.Log("Command failed. Elapsed: " + (Stopwatch.GetElapsedTime(startTime)));
            throw;
        }
    }
}

namespace TestAppAutomation.Helpers;

/// <summary>
/// Base class for all test fixtures providing shared setup/teardown.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected TestBase()
    {
        // Shared setup — browser init, API client, etc.
    }

    public void Dispose()
    {
        // Shared teardown
        GC.SuppressFinalize(this);
    }
}

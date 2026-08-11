using Microsoft.Extensions.Options;

namespace Gesellschaftsspieler.MCPServer.Tests;

/// <summary>Minimal IOptionsMonitor returning a fixed value, for enforcement unit tests.</summary>
public class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

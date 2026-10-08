using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using ReSerialSync.Services;

public sealed class FakeAiChatClientFactory : IAiChatClientFactory
{
    public FakeAiChatClient? Client { get; private set; }
    public IChatClient Create(Uri endpoint, string model, string apiKey) => Client = new FakeAiChatClient();
}
public sealed class FakeAiChatClient : IChatClient
{
    public string LastContext { get; private set; } = "";
    public bool SawToolResult { get; private set; }
    public int ToolCount { get; private set; }
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "fake")));
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = messages.ToArray();
        LastContext = string.Join("\n", history.Select(m => m.Text));
        var user = history.Last(m => m.Role == ChatRole.User);
        var afterUser = history.Skip(Array.IndexOf(history, user) + 1).ToArray();
        ToolCount = options?.Tools?.Count ?? 0;
        if (user.Text == "cancel-test") await Task.Delay(Timeout.Infinite, cancellationToken);
        if (user.Text == "failure-test") throw new HttpRequestException("Fake provider failure");
        if (user.Text == "connect-test" && !afterUser.Any(m => m.Role == ChatRole.Tool))
        {
            var name = options!.Tools!.OfType<AIFunction>().First(f => f.Name.Contains("Connect", StringComparison.OrdinalIgnoreCase) && !f.Name.Contains("Disconnect", StringComparison.OrdinalIgnoreCase)).Name;
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("connect-call", name, new Dictionary<string, object?> { ["portName"] = "COM2" })]) { FinishReason = ChatFinishReason.ToolCalls };
            yield break;
        }
        SawToolResult |= afterUser.Any(m => m.Role == ChatRole.Tool);
        yield return new ChatResponseUpdate(ChatRole.Assistant, "### Result\n\n");
        await Task.Delay(40, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, "**Ready** — `COM2`.\n\n| Port | State |\n| --- | --- |\n| COM2 | Ready |\n") { FinishReason = ChatFinishReason.Stop };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

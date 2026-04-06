namespace OpenClaw.MailBridge;

internal static class Program
{
    public static Task<int> Main(string[] args) => new BridgeApplication().RunAsync(args);
}

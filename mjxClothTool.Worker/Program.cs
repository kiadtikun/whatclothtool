namespace mjxClothTool.Worker;

internal static class Program
{
    private static async Task Main() =>
        await WorkerProtocol.RunAsync(Console.In, Console.Out, Console.Error, CancellationToken.None);
}

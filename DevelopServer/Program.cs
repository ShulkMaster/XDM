namespace DevelopServer;

class Program
{
    static async Task Main(string[] args)
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var templatesPath = Path.Combine(projectDir, "Templates");
        var wwwrootPath = Path.Combine(projectDir, "wwwroot");

        if (!Directory.Exists(templatesPath))
            throw new Exception("Templates directory not found");

        if (!Directory.Exists(wwwrootPath))
            throw new Exception("wwwroot directory not found");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var server = new Server(templatesPath, wwwrootPath);
        await server.RunAsync(cts.Token);
    }
}

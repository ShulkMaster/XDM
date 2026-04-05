using System.Net;
using System.Net.WebSockets;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace DevelopServer;

public class Server : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _templatesPath;
    private readonly string _wwwrootPath;
    private readonly FileSystemWatcher _watcher;
    private readonly List<WebSocket> _clients = [];
    private readonly Lock _clientsLock = new();

    public Server(string templatesPath, string wwwrootPath, int port = 5050)
    {
        _templatesPath = Path.GetFullPath(templatesPath);
        _wwwrootPath = Path.GetFullPath(wwwrootPath);

        _listener.Prefixes.Add($"http://localhost:{port}/");

        _watcher = new FileSystemWatcher(_templatesPath, "*.xml")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = false,
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += (_, e) => OnFileChanged(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(e.FullPath)!, e.Name!));
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _listener.Start();
        _watcher.EnableRaisingEvents = true;

        Console.WriteLine($"Server listening on {_listener.Prefixes.First()}");
        Console.WriteLine($"Serving from {_wwwrootPath}");
        Console.WriteLine($"Watching templates in: {_templatesPath}");

        while (!ct.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().WaitAsync(ct);

            if (context.Request.IsWebSocketRequest)
            {
                _ = HandleWebSocketAsync(context, ct);
            }
            else
            {
                HandleStaticFile(context);
            }
        }
    }

    private void HandleStaticFile(HttpListenerContext context)
    {
        var requestPath = context.Request.Url?.AbsolutePath ?? "/";
        if (requestPath == "/") requestPath = "/index.html";

        var filePath = Path.GetFullPath(Path.Combine(_wwwrootPath, requestPath.TrimStart('/')));

        if (!filePath.StartsWith(_wwwrootPath) || !File.Exists(filePath))
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        context.Response.ContentType = ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            _ => "application/octet-stream"
        };

        var bytes = File.ReadAllBytes(filePath);
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;

        lock (_clientsLock)
            _clients.Add(ws);

        Console.WriteLine("WebSocket client connected");

        try
        {
            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (Exception) { /* client disconnected */ }
        finally
        {
            lock (_clientsLock)
                _clients.Remove(ws);

            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            // Small delay to let file writes complete
            await Task.Delay(100);

            try
            {
                Console.WriteLine($"File changed: {e.FullPath}");
                var content = await File.ReadAllTextAsync(e.FullPath);
                var relativePath = Path.GetRelativePath(_templatesPath, e.FullPath);
                var pdfBytes = GeneratePdf(content);
                var segment = new ArraySegment<byte>(pdfBytes);

                List<WebSocket> snapshot;
                lock (_clientsLock)
                    snapshot = [.. _clients];

                foreach (var ws in snapshot)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            await ws.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
                        }
                        catch { /* ignore dead sockets */ }
                    }
                }

                Console.WriteLine($"Broadcast change: {relativePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {e.FullPath}: {ex.Message}");
            }
        });
    }

    private static byte[] GeneratePdf(string xmlContent)
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new SystemFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A4;

        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Courier", 12);
        var margin = XUnitPt.FromPoint(30);
        var rect = new XRect(margin, margin, page.Width.Point - margin * 2, page.Height.Point - margin * 2);
        var tf = new XTextFormatter(gfx);
        tf.DrawString(xmlContent, font, XBrushes.Black, rect);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _listener.Close();
    }
}

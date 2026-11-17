using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

// Usage: dotnet run --project tools/ECommerce.Realtime.Client -- <orders|admin> <jwt> [baseUrl]
if (args.Length < 2 || args[0] is not ("orders" or "admin"))
{
    Console.WriteLine("Usage: <orders|admin> <jwt> [baseUrl]");
    return 1;
}

var hub = args[0];
var token = args[1];
var baseUrl = args.Length > 2 ? args[2].TrimEnd('/') : "http://localhost:8080";

var connection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/{hub}", options =>
    {
        // The client library sends this as ?access_token=... on the WebSocket.
        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
    })
    .WithAutomaticReconnect()
    .Build();

foreach (var eventName in new[] { "OrderCreated", "OrderStatusChanged" })
{
    connection.On<JsonElement>(eventName, payload =>
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {eventName}: {payload}");
        Console.ResetColor();
    });
}

connection.Reconnecting += _ => { Console.WriteLine("Connection lost. Reconnecting..."); return Task.CompletedTask; };
connection.Reconnected += _ => { Console.WriteLine("Reconnected."); return Task.CompletedTask; };
connection.Closed += e => { Console.WriteLine($"Connection closed. {e?.Message}"); return Task.CompletedTask; };

try
{
    await connection.StartAsync();
}
catch (Exception ex)
{
    // A 401 or 403 from the server ends up here.
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Could not connect to /hubs/{hub}: {ex.Message}");
    Console.ResetColor();
    return 1;
}

Console.WriteLine($"Connected to /hubs/{hub}. Waiting for events. Press Ctrl+C to stop.");

var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
await done.Task;

await connection.StopAsync();
return 0;
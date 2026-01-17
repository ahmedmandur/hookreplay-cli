using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace HookReplay.Cli;

// JSON source generator for AOT/trimming compatibility
[JsonSerializable(typeof(CliConfig))]
[JsonSerializable(typeof(ReplayRequest))]
[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SseConnectedEvent))]
[JsonSerializable(typeof(TelemetryPayload))]
[JsonSerializable(typeof(NpmPackageInfo))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class CliJsonContext : JsonSerializerContext
{
}

internal class Program
{
    private const string DefaultServerUrl = "https://hookreplay.dev";
    private const string NpmPackageName = "hookreplay";

    private static readonly string CurrentVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".hookreplay",
        "config.json");

    private static CliConfig _config = new();
    private static HttpClient? _sseClient;
    private static bool _isConnected;
    private static readonly List<ReplayRequest> RequestHistory = [];
    private static CancellationTokenSource? _connectionCts;
    private static Task? _sseListenerTask;
    private static string? _latestVersion;

    private static async Task<int> Main(string[] args)
    {
        _config = LoadConfig();

        // Track first run (anonymous, non-blocking)
        _ = TrackFirstRunAsync();

        // Show welcome screen
        Console.Clear();
        ShowWelcome();

        // Check for updates (non-blocking, then prompt if available)
        await CheckForUpdatesAsync();

        // Main interactive loop
        await RunInteractiveMode();

        return 0;
    }

    private static void ShowWelcome()
    {
        // Professional gradient ASCII art logo
        var logo = new[]
        {
            "██╗  ██╗ ██████╗  ██████╗ ██╗  ██╗██████╗ ███████╗██████╗ ██╗      █████╗ ██╗   ██╗",
            "██║  ██║██╔═══██╗██╔═══██╗██║ ██╔╝██╔══██╗██╔════╝██╔══██╗██║     ██╔══██╗╚██╗ ██╔╝",
            "███████║██║   ██║██║   ██║█████╔╝ ██████╔╝█████╗  ██████╔╝██║     ███████║ ╚████╔╝ ",
            "██╔══██║██║   ██║██║   ██║██╔═██╗ ██╔══██╗██╔══╝  ██╔═══╝ ██║     ██╔══██║  ╚██╔╝  ",
            "██║  ██║╚██████╔╝╚██████╔╝██║  ██╗██║  ██║███████╗██║     ███████╗██║  ██║   ██║   ",
            "╚═╝  ╚═╝ ╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝╚═╝     ╚══════╝╚═╝  ╚═╝   ╚═╝   "
        };

        // Gradient colors from cyan to blue to purple
        var gradientColors = new[] { Color.Cyan1, Color.DeepSkyBlue1, Color.DodgerBlue1, Color.Blue, Color.Purple, Color.MediumPurple };

        AnsiConsole.WriteLine();
        for (var i = 0; i < logo.Length; i++)
        {
            var color = gradientColors[i % gradientColors.Length];
            AnsiConsole.MarkupLine($"[{color.ToMarkup()}]{logo[i].EscapeMarkup()}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[silver]v{CurrentVersion}[/]  [silver]Catch, inspect, and replay webhooks locally[/]");
        AnsiConsole.WriteLine();

        var statusTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        statusTable.AddColumn("Status");
        statusTable.AddColumn("Value");
        statusTable.AddRow("Connection", "[red]Disconnected[/]");
        statusTable.AddRow("API Key", string.IsNullOrEmpty(_config.ApiKey) ? "[yellow]Not configured[/]" : $"[green]{_config.ApiKey[..Math.Min(8, _config.ApiKey.Length)]}****[/]");
        statusTable.AddRow("Server", _config.ServerUrl ?? DefaultServerUrl);
        AnsiConsole.Write(statusTable);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[silver]Type[/] [white]help[/] [silver]for available commands, or[/] [white]quit[/] [silver]to exit.[/]");
        AnsiConsole.WriteLine();
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HookReplay-CLI");

            // Check npm registry for latest version
            var response = await httpClient.GetStringAsync($"https://registry.npmjs.org/{NpmPackageName}/latest");
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("version", out var versionElement))
            {
                _latestVersion = versionElement.GetString();

                if (!string.IsNullOrEmpty(_latestVersion) && IsNewerVersion(_latestVersion, CurrentVersion))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule($"[yellow]Update Available[/]").RuleStyle("yellow"));
                    AnsiConsole.MarkupLine($"[yellow]A new version of HookReplay CLI is available:[/] [green]{_latestVersion}[/] [silver](current: {CurrentVersion})[/]");
                    AnsiConsole.WriteLine();

                    if (AnsiConsole.Confirm("[yellow]Would you like to update now?[/]", defaultValue: false))
                    {
                        await RunUpdateAsync();
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[silver]You can update later with the[/] [white]update[/] [silver]command.[/]");
                    }
                    AnsiConsole.WriteLine();
                }
            }
        }
        catch
        {
            // Silently ignore update check failures
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.').Select(int.Parse).ToArray();
            var currentParts = current.Split('.').Select(int.Parse).ToArray();

            for (var i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (latestParts[i] > currentParts[i]) return true;
                if (latestParts[i] < currentParts[i]) return false;
            }

            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunUpdateAsync()
    {
        var updateCommand = DetectInstallationMethod();
        var isNpmInstall = updateCommand?.Contains("npm") == true;

        if (string.IsNullOrEmpty(updateCommand))
        {
            AnsiConsole.MarkupLine("[yellow]Could not detect installation method.[/]");
            AnsiConsole.MarkupLine("[silver]Please update manually:[/]");
            AnsiConsole.MarkupLine("  [white]npm install -g hookreplay[/]");
            AnsiConsole.MarkupLine("  [silver]or[/]");
            AnsiConsole.MarkupLine("  [white]dotnet tool update -g HookReplay.Cli[/]");
            return;
        }

        // For npm installs, we need to exit first because the binary is locked while running
        if (isNpmInstall)
        {
            AnsiConsole.MarkupLine("[cyan]To update, please run:[/]");
            AnsiConsole.MarkupLine($"  [white]npm install -g hookreplay[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[silver]The CLI must be closed before updating (npm cannot replace files in use).[/]");
            return;
        }

        // For dotnet tool, we can try to update in place
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Updating HookReplay CLI...", async ctx =>
            {
                try
                {
                    ctx.Status($"Running: {updateCommand}");

                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/sh",
                            Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT
                                ? $"/c {updateCommand}"
                                : $"-c \"{updateCommand}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        AnsiConsole.MarkupLine("[green]✓ Update successful![/]");
                        AnsiConsole.MarkupLine("[yellow]Please restart the CLI to use the new version.[/]");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        AnsiConsole.MarkupLine($"[red]Update failed:[/] {error}");
                        AnsiConsole.MarkupLine("[silver]Try updating manually with:[/]");
                        AnsiConsole.MarkupLine($"  [white]{updateCommand}[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Update failed:[/] {ex.Message}");
                }
            });
    }

    private static string? DetectInstallationMethod()
    {
        // Check if installed via npm (look for node_modules pattern in path)
        var executablePath = Environment.ProcessPath ?? "";

        if (executablePath.Contains("node_modules") || executablePath.Contains("npm"))
        {
            return "npm update -g hookreplay";
        }

        // Check if installed via dotnet tool
        if (executablePath.Contains(".dotnet") || executablePath.Contains("dotnet"))
        {
            return "dotnet tool update -g HookReplay.Cli";
        }

        // Default to npm as it's the recommended method
        return "npm update -g hookreplay";
    }

    private static void ShowVersion()
    {
        var versionPanel = new Panel(
            new Markup($"[bold cyan]HookReplay CLI[/]\n" +
                      $"[silver]Version:[/] [white]{CurrentVersion}[/]\n" +
                      $"[silver]Runtime:[/] [white].NET {Environment.Version}[/]\n" +
                      $"[silver]OS:[/] [white]{Environment.OSVersion.Platform} {Environment.OSVersion.Version}[/]\n" +
                      $"[silver]Architecture:[/] [white]{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}[/]"))
            .Header("[cyan]Version Info[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(versionPanel);

        if (!string.IsNullOrEmpty(_latestVersion) && IsNewerVersion(_latestVersion, CurrentVersion))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Update available:[/] [green]{_latestVersion}[/] [silver](run[/] [white]update[/] [silver]to install)[/]");
        }
    }

    private static async Task RunInteractiveMode()
    {
        while (true)
        {
            var prompt = _isConnected ? "[green]hookreplay[/]" : "[blue]hookreplay[/]";
            var statusIndicator = _isConnected ? "[green]●[/]" : "[red]●[/]";

            AnsiConsole.Markup($"{statusIndicator} {prompt}> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            try
            {
                switch (command)
                {
                    case "help":
                    case "?":
                        ShowHelp();
                        break;

                    case "connect":
                        await ConnectAsync(args);
                        break;

                    case "disconnect":
                        await DisconnectAsync();
                        break;

                    case "status":
                        ShowStatus();
                        break;

                    case "config":
                        HandleConfig(args);
                        break;

                    case "history":
                        ShowHistory();
                        break;

                    case "replay":
                        await ReplayFromHistory(args);
                        break;

                    case "clear":
                        Console.Clear();
                        ShowWelcome();
                        break;

                    case "version":
                    case "v":
                    case "--version":
                    case "-v":
                        ShowVersion();
                        break;

                    case "update":
                        await RunUpdateAsync();
                        break;

                    case "quit":
                    case "exit":
                    case "q":
                        await DisconnectAsync();
                        AnsiConsole.MarkupLine("[silver]Goodbye![/]");
                        return;

                    default:
                        AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}. Type [white]help[/] for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

            AnsiConsole.WriteLine();
        }
    }

    private static void ShowHelp()
    {
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn(new TableColumn("[cyan]Command[/]").Width(20));
        table.AddColumn(new TableColumn("[cyan]Description[/]"));

        table.AddRow("[white]connect[/]", "Connect to HookReplay server");
        table.AddRow("[white]disconnect[/]", "Disconnect from server");
        table.AddRow("[white]status[/]", "Show connection status");
        table.AddRow("[white]config[/]", "Manage configuration (api-key, server)");
        table.AddRow("[white]history[/]", "Show received request history");
        table.AddRow("[white]replay <number>[/]", "Replay a request from history");
        table.AddRow("[white]version[/]", "Show version and system info");
        table.AddRow("[white]update[/]", "Check for and install updates");
        table.AddRow("[white]clear[/]", "Clear the screen");
        table.AddRow("[white]help[/]", "Show this help message");
        table.AddRow("[white]quit[/]", "Exit the CLI");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[silver]Examples:[/]");
        AnsiConsole.MarkupLine("  [white]config api-key hr_abc123...[/]  [silver]- Set your API key[/]");
        AnsiConsole.MarkupLine("  [white]config server https://hookreplay.dev[/]  [silver]- Set server URL[/]");
        AnsiConsole.MarkupLine("  [white]connect[/]  [silver]- Connect to server[/]");
        AnsiConsole.MarkupLine("  [white]replay 1[/]  [silver]- Replay first request in history[/]");
    }

    private static async Task ConnectAsync(string[] args)
    {
        if (_isConnected)
        {
            AnsiConsole.MarkupLine("[yellow]Already connected. Use [white]disconnect[/] first.[/]");
            return;
        }

        // Check for API key in args or config
        var apiKey = args.Length > 0 ? args[0] : _config.ApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]No API key configured.[/]");
            AnsiConsole.MarkupLine("[silver]Use [white]config api-key <your-key>[/] to set it.[/]");

            // Offer to set it now
            if (AnsiConsole.Confirm("Would you like to enter your API key now?"))
            {
                apiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("[blue]API Key:[/]")
                        .Secret());

                _config.ApiKey = apiKey;
                SaveConfig(_config);
                AnsiConsole.MarkupLine("[green]API key saved![/]");
            }
            else
            {
                return;
            }
        }

        var serverUrl = _config.ServerUrl ?? DefaultServerUrl;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync($"Connecting to {serverUrl}...", async ctx =>
            {
                var sseUrl = $"{serverUrl.TrimEnd('/')}/api/cli/events";

                // Create HTTP handler that bypasses SSL for localhost
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (message.RequestUri?.Host == "localhost" || message.RequestUri?.Host == "127.0.0.1")
                        return true;
                    return errors == System.Net.Security.SslPolicyErrors.None;
                };

                _sseClient = new HttpClient(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan // SSE connections are long-lived
                };

                // Set Authorization header (more secure than query string)
                _sseClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                _connectionCts = new CancellationTokenSource();

                // Start listening to SSE events in background
                _sseListenerTask = ListenToSseAsync(sseUrl, _connectionCts.Token);

                // Wait a bit to see if connection succeeds
                await Task.Delay(1000);

                if (!_isConnected)
                {
                    // Connection might take a bit longer, wait more
                    await Task.Delay(2000);
                }
            });

        if (_isConnected)
        {
            AnsiConsole.MarkupLine("[green]Connected![/] Waiting for replay requests...");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Failed to connect. Check your API key and server URL.[/]");
        }
    }

    private static async Task ListenToSseAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var response = await _sseClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[red]Connection failed: {response.StatusCode} - {error}[/]");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? eventType = null;
            var dataBuilder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (line == null)
                {
                    // Stream ended
                    break;
                }

                if (line.StartsWith("event:"))
                {
                    eventType = line[6..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    dataBuilder.Append(line[5..].Trim());
                }
                else if (string.IsNullOrEmpty(line))
                {
                    // Empty line = end of event
                    if (!string.IsNullOrEmpty(eventType) && dataBuilder.Length > 0)
                    {
                        var data = dataBuilder.ToString();
                        await HandleSseEvent(eventType, data);
                    }
                    eventType = null;
                    dataBuilder.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]SSE connection error:[/] {ex.Message}");
        }
        finally
        {
            _isConnected = false;
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Disconnected from server.[/]");
            AnsiConsole.Markup(_isConnected ? "[green]●[/] [green]hookreplay[/]> " : "[red]●[/] [blue]hookreplay[/]> ");
        }
    }

    private static async Task HandleSseEvent(string eventType, string data)
    {
        switch (eventType)
        {
            case "connected":
                _isConnected = true;
                try
                {
                    var connectedEvent = JsonSerializer.Deserialize(data, CliJsonContext.Default.SseConnectedEvent);
                    AnsiConsole.MarkupLine($"[silver]Server confirmed connection: {connectedEvent?.Message}[/]");
                }
                catch
                {
                    AnsiConsole.MarkupLine($"[silver]Server confirmed connection[/]");
                }
                break;

            case "replay":
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[cyan]>>> Received ReplayRequest from server![/]");

                try
                {
                    using var jsonDoc = JsonDocument.Parse(data);
                    var jsonElement = jsonDoc.RootElement;

                    var request = new ReplayRequest
                    {
                        Method = jsonElement.GetProperty("method").GetString() ?? "GET",
                        Url = jsonElement.GetProperty("url").GetString() ?? "",
                        Body = jsonElement.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null,
                        QueryString = jsonElement.TryGetProperty("queryString", out var qsProp) ? qsProp.GetString() : null,
                        RequestId = jsonElement.TryGetProperty("requestId", out var idProp) ? idProp.GetGuid() : Guid.Empty
                    };

                    // Parse headers
                    if (jsonElement.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
                    {
                        request.Headers = new Dictionary<string, string>();
                        foreach (var header in headersProp.EnumerateObject())
                        {
                            request.Headers[header.Name] = header.Value.GetString() ?? "";
                        }
                    }

                    await HandleIncomingRequest(request);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error parsing request: {ex.Message}[/]");
                    AnsiConsole.MarkupLine($"[silver]Raw data: {data}[/]");
                }

                AnsiConsole.Markup(_isConnected ? "[green]●[/] [green]hookreplay[/]> " : "[red]●[/] [blue]hookreplay[/]> ");
                break;

            default:
                AnsiConsole.MarkupLine($"[silver]Unknown event: {eventType}[/]");
                break;
        }
    }

    private static async Task HandleIncomingRequest(ReplayRequest request)
    {
        // Add to history
        RequestHistory.Insert(0, request);
        if (RequestHistory.Count > 50) RequestHistory.RemoveAt(RequestHistory.Count - 1);

        if (string.IsNullOrEmpty(request.Url))
        {
            AnsiConsole.MarkupLine("[red]Error: No target URL provided by server[/]");
            return;
        }

        var fullUrl = request.Url + (request.QueryString ?? "");

        AnsiConsole.Write(new Rule($"[blue]Incoming Request #{RequestHistory.Count}[/]").RuleStyle("grey"));

        var infoTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Blue);
        infoTable.AddColumn("Property");
        infoTable.AddColumn("Value");
        infoTable.AddRow("[blue]Method[/]", request.Method);
        infoTable.AddRow("[blue]Target[/]", fullUrl);
        infoTable.AddRow("[blue]Headers[/]", $"{request.Headers?.Count ?? 0} headers");
        infoTable.AddRow("[blue]Body[/]", string.IsNullOrEmpty(request.Body) ? "[silver]empty[/]" : $"{request.Body.Length} chars");
        AnsiConsole.Write(infoTable);

        // Execute the request
        await ExecuteRequest(request, fullUrl);

        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    private static async Task ExecuteRequest(ReplayRequest request, string fullUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), fullUrl);

            // Add headers
            var excludeHeaders = new[] { "host", "content-length", "connection", "accept-encoding", "transfer-encoding" };
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    if (excludeHeaders.Contains(header.Key.ToLower())) continue;
                    if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    catch { }
                }
            }

            // Add body
            if (!string.IsNullOrEmpty(request.Body))
            {
                var contentType = request.Headers?.GetValueOrDefault("Content-Type") ?? "application/json";
                httpRequest.Content = new StringContent(request.Body, Encoding.UTF8);
                httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.SendAsync(httpRequest);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync();

            var statusColor = (int)response.StatusCode switch
            {
                >= 200 and < 300 => "green",
                >= 300 and < 400 => "yellow",
                >= 400 and < 500 => "orange3",
                _ => "red"
            };

            AnsiConsole.MarkupLine($"[{statusColor}]Response: {(int)response.StatusCode} {response.StatusCode}[/] [silver]({stopwatch.ElapsedMilliseconds}ms)[/]");

            if (!string.IsNullOrEmpty(responseBody))
            {
                var displayBody = responseBody;
                try
                {
                    var json = JsonDocument.Parse(responseBody);
                    displayBody = JsonSerializer.Serialize(json, CliJsonContext.Default.JsonDocument);
                }
                catch { }

                if (displayBody.Length > 500)
                {
                    displayBody = displayBody[..500] + "\n[silver]... (truncated)[/]";
                }

                AnsiConsole.Write(new Panel(displayBody)
                    .Header("[silver]Response Body[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Grey)
                    .Expand());
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Request failed:[/] {ex.Message}");
        }
    }

    private static async Task DisconnectAsync()
    {
        if (!_isConnected && _sseClient == null)
        {
            AnsiConsole.MarkupLine("[silver]Not connected.[/]");
            return;
        }

        await _connectionCts?.CancelAsync();

        if (_sseListenerTask != null)
        {
            try
            {
                await _sseListenerTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Ignore timeout
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _sseClient?.Dispose();
        _sseClient = null;
        _sseListenerTask = null;

        _isConnected = false;
        AnsiConsole.MarkupLine("[green]Disconnected.[/]");
    }

    private static void ShowStatus()
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Property");
        table.AddColumn("Value");

        table.AddRow("Connection", _isConnected ? "[green]Connected[/]" : "[red]Disconnected[/]");
        table.AddRow("API Key", string.IsNullOrEmpty(_config.ApiKey) ? "[yellow]Not set[/]" : $"[green]{_config.ApiKey[..Math.Min(8, _config.ApiKey.Length)]}****[/]");
        table.AddRow("Server", _config.ServerUrl ?? DefaultServerUrl);
        table.AddRow("Requests Received", RequestHistory.Count.ToString());

        AnsiConsole.Write(table);
    }

    private static void HandleConfig(string[] args)
    {
        if (args.Length == 0)
        {
            // Show current config
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("API Key", string.IsNullOrEmpty(_config.ApiKey) ? "[silver](not set)[/]" : $"{_config.ApiKey[..Math.Min(8, _config.ApiKey.Length)]}****");
            table.AddRow("Server URL", _config.ServerUrl ?? DefaultServerUrl);
            table.AddRow("Config File", ConfigFilePath);

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[silver]Usage: [white]config <setting> <value>[/][/]");
            AnsiConsole.MarkupLine("[silver]  config api-key <key>  - Set API key[/]");
            AnsiConsole.MarkupLine("[silver]  config server <url>   - Set server URL[/]");
            return;
        }

        var setting = args[0].ToLower();
        var value = args.Length > 1 ? string.Join(" ", args.Skip(1)) : null;

        switch (setting)
        {
            case "api-key":
            case "apikey":
            case "key":
                if (string.IsNullOrEmpty(value))
                {
                    value = AnsiConsole.Prompt(
                        new TextPrompt<string>("[blue]API Key:[/]")
                            .Secret());
                }
                _config.ApiKey = value;
                SaveConfig(_config);
                AnsiConsole.MarkupLine("[green]API key saved![/]");
                break;

            case "server":
            case "url":
                if (string.IsNullOrEmpty(value))
                {
                    AnsiConsole.MarkupLine("[red]Please provide a server URL.[/]");
                    return;
                }
                _config.ServerUrl = value;
                SaveConfig(_config);
                AnsiConsole.MarkupLine($"[green]Server URL set to:[/] {value}");
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown setting:[/] {setting}");
                AnsiConsole.MarkupLine("[silver]Available settings: api-key, server[/]");
                break;
        }
    }

    private static void ShowHistory()
    {
        if (RequestHistory.Count == 0)
        {
            AnsiConsole.MarkupLine("[silver]No requests received yet.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("#");
        table.AddColumn("Method");
        table.AddColumn("Target");
        table.AddColumn("Body");

        for (var i = 0; i < Math.Min(10, RequestHistory.Count); i++)
        {
            var req = RequestHistory[i];
            var target = req.Url;
            if (target.Length > 40) target = target[..40] + "...";

            table.AddRow(
                (i + 1).ToString(),
                req.Method,
                target,
                string.IsNullOrEmpty(req.Body) ? "[silver]empty[/]" : $"{req.Body.Length} chars"
            );
        }

        AnsiConsole.Write(table);

        if (RequestHistory.Count > 10)
        {
            AnsiConsole.MarkupLine($"[silver]... and {RequestHistory.Count - 10} more[/]");
        }

        AnsiConsole.MarkupLine("[silver]Use [white]replay <number>[/] to replay a request[/]");
    }

    private static async Task ReplayFromHistory(string[] args)
    {
        if (RequestHistory.Count == 0)
        {
            AnsiConsole.MarkupLine("[silver]No requests in history.[/]");
            return;
        }

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[silver]Usage: replay <number> [target-url][/]");
            ShowHistory();
            return;
        }

        if (!int.TryParse(args[0], out var index) || index < 1 || index > RequestHistory.Count)
        {
            AnsiConsole.MarkupLine($"[red]Invalid request number. Use 1-{RequestHistory.Count}[/]");
            return;
        }

        var request = RequestHistory[index - 1];
        var targetUrl = args.Length > 1 ? args[1] : request.Url;

        if (string.IsNullOrEmpty(targetUrl))
        {
            AnsiConsole.MarkupLine("[red]No target URL available. Provide one: replay <number> <url>[/]");
            return;
        }

        var fullUrl = targetUrl + (request.QueryString ?? "");

        AnsiConsole.MarkupLine($"[blue]Replaying request #{index} to {fullUrl}...[/]");

        await ExecuteRequest(request, fullUrl);
    }

    private static CliConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize(json, CliJsonContext.Default.CliConfig) ?? new CliConfig();
            }
        }
        catch { }
        return new CliConfig();
    }

    private static void SaveConfig(CliConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, CliJsonContext.Default.CliConfig);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to save config:[/] {ex.Message}");
        }
    }

    private static async Task TrackFirstRunAsync()
    {
        try
        {
            // Skip if already tracked
            if (_config.FirstRunTracked)
                return;

            // Generate install ID if not exists
            if (string.IsNullOrEmpty(_config.InstallId))
            {
                _config.InstallId = Guid.NewGuid().ToString("N")[..16];
                _config.FirstRunAt = DateTime.UtcNow;
            }

            // Build telemetry payload
            var payload = new TelemetryPayload
            {
                InstallId = _config.InstallId,
                Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                Os = Environment.OSVersion.Platform.ToString(),
                OsVersion = Environment.OSVersion.VersionString,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                DotNetVersion = Environment.Version.ToString(),
                Timestamp = DateTime.UtcNow,
                Event = "first_run"
            };

            var serverUrl = _config.ServerUrl ?? DefaultServerUrl;
            var telemetryUrl = $"{serverUrl.TrimEnd('/')}/api/cli/telemetry";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var json = JsonSerializer.Serialize(payload, CliJsonContext.Default.TelemetryPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(telemetryUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _config.FirstRunTracked = true;
                SaveConfig(_config);
            }
        }
        catch
        {
            // Silently ignore telemetry errors - don't disrupt user experience
        }
    }
}

internal class CliConfig
{
    public string? ApiKey { get; set; }
    public string? ServerUrl { get; set; }
    public bool FirstRunTracked { get; set; }
    public string? InstallId { get; set; }
    public DateTime? FirstRunAt { get; set; }
}

internal class TelemetryPayload
{
    public string? InstallId { get; set; }
    public string? Version { get; set; }
    public string? Os { get; set; }
    public string? OsVersion { get; set; }
    public string? Architecture { get; set; }
    public string? DotNetVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Event { get; set; }
}

internal class ReplayRequest
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public string? QueryString { get; set; }
    public Guid RequestId { get; set; }
}

internal class SseConnectedEvent
{
    public string? Message { get; set; }
    public string? ConnectionId { get; set; }
    public string? UserId { get; set; }
}

internal class NpmPackageInfo
{
    public string? Version { get; set; }
}

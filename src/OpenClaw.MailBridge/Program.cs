using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.MailBridge.Contracts;

namespace OpenClaw.MailBridge;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return MainAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> MainAsync(string[] args)
    {
        using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });

        var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Bootstrap");
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        var sqliteVersion = GetSqliteVersion();
        var outlookComType = Type.GetTypeFromProgID("Outlook.Application", throwOnError: false);

        bootstrapLogger.LogInformation("Main thread apartment state: {ApartmentState}", apartmentState);
        bootstrapLogger.LogInformation("SQLite ready: {SqliteVersion}", sqliteVersion);
        bootstrapLogger.LogInformation(
            outlookComType is null
                ? "Outlook COM ProgID not found. Install classic Outlook and the Outlook PIAs before COM integration work."
                : "Outlook COM ProgID resolved successfully: {TypeName}",
            outlookComType?.FullName);

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
            })
            .ConfigureServices((context, services) =>
            {
                var pipeName = context.Configuration["Bridge:PipeName"] ?? "openclaw-mail-bridge";

                services.AddSingleton(new BridgeRuntimeInfo(
                    PipeName: pipeName,
                    ApartmentState: apartmentState.ToString(),
                    OutlookComAvailable: outlookComType is not null,
                    OutlookComTypeName: outlookComType?.FullName,
                    SqliteVersion: sqliteVersion));
                services.AddHostedService<NamedPipeBridgeWorker>();
            })
            .Build();

        await host.RunAsync();
        return 0;
    }

    private static string GetSqliteVersion()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "select sqlite_version();";

        return command.ExecuteScalar()?.ToString() ?? "unknown";
    }
}

internal sealed class NamedPipeBridgeWorker(
    ILogger<NamedPipeBridgeWorker> logger,
    IConfiguration configuration,
    BridgeRuntimeInfo runtimeInfo) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = configuration["Bridge:PipeName"] ?? runtimeInfo.PipeName;

        logger.LogInformation(
            "Bridge server ready. PipeName={PipeName}; ApartmentState={ApartmentState}; OutlookComAvailable={OutlookComAvailable}; SQLite={SqliteVersion}",
            pipeName,
            runtimeInfo.ApartmentState,
            runtimeInfo.OutlookComAvailable,
            runtimeInfo.SqliteVersion);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                logger.LogInformation("Waiting for named pipe client connection on {PipeName}", pipeName);
                await server.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server) { AutoFlush = true };

                var requestJson = await reader.ReadLineAsync();
                var request = requestJson is null
                    ? null
                    : JsonSerializer.Deserialize<MailBridgeRequest>(requestJson, JsonOptions);

                var response = request is null
                    ? new MailBridgeResponse(
                        Success: false,
                        Message: "No request payload was received.",
                        Payload: null,
                        TimestampUtc: DateTimeOffset.UtcNow)
                    : new MailBridgeResponse(
                        Success: true,
                        Message: $"Processed operation '{request.Operation}'.",
                        Payload: JsonSerializer.Serialize(runtimeInfo, JsonOptions),
                        TimestampUtc: DateTimeOffset.UtcNow);

                var responseJson = JsonSerializer.Serialize(response, JsonOptions);
                await writer.WriteLineAsync(responseJson);

                logger.LogInformation("Processed named pipe request successfully.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Bridge shutdown requested.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Named pipe loop failed.");
            }
        }
    }
}

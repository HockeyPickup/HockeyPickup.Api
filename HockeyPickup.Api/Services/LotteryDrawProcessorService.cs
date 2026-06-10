using Azure.Messaging.ServiceBus;
using HockeyPickup.Api.Models.Domain;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HockeyPickup.Api.Services;

// Hosts a ServiceBusProcessor on the lottery-draws queue. Pure plumbing: every decision is delegated to
// ILotteryService.HandleDrawMessageAsync (fully unit-tested), so this class is excluded from coverage.
[ExcludeFromCodeCoverage]
public class LotteryDrawProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LotteryDrawProcessorService> _logger;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public LotteryDrawProcessorService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LotteryDrawProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
            ?? _configuration.GetConnectionString("ServiceBusConnectionString");
        var queueName = _configuration["ServiceBusLotteryQueueName"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(queueName))
        {
            _logger.LogInformation("Lottery draw processor not started: Service Bus connection or queue name not configured");
            return;
        }

        _client = new ServiceBusClient(connectionString);
        _processor = _client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = JsonSerializer.Deserialize<LotteryDrawMessage>(args.Message.Body.ToString());
        if (message == null)
        {
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var lotteryService = scope.ServiceProvider.GetRequiredService<ILotteryService>();
        var outcome = await lotteryService.HandleDrawMessageAsync(message, args.CancellationToken);

        if (outcome == LotteryDrawOutcome.Reschedule)
            await args.AbandonMessageAsync(args.Message);
        else
            await args.CompleteMessageAsync(args.Message);
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error in lottery draw processor");
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
            await _processor.StopProcessingAsync(cancellationToken);
        if (_processor != null)
            await _processor.DisposeAsync();
        if (_client != null)
            await _client.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}

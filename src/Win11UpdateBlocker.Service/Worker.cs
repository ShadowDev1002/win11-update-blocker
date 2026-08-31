using Win11UpdateBlocker.Core;

using Win11UpdateBlocker.Core.Ipc;

using Win11UpdateBlocker.Core.Logging;



namespace Win11UpdateBlocker.Service;



public class Worker : BackgroundService

{

    private static readonly TimeSpan EnforcementInterval = TimeSpan.FromMinutes(5);

    private readonly BlockerEngine _engine = new();

    private readonly ILogger<Worker> _logger;



    public Worker(ILogger<Worker> logger)

    {

        _logger = logger;

    }



    protected override async Task ExecuteAsync(CancellationToken stoppingToken)

    {

        FileLogger.Log($"{AppMetadata.DisplayName}: worker started.");

        _logger.LogInformation("{DisplayName} worker started.", AppMetadata.DisplayName);



        var pipeTask = BlockerPipeTransport.RunServerLoopAsync(

            request => BlockerPipeHandler.Handle(request, _engine),

            stoppingToken);



        try

        {

            while (!stoppingToken.IsCancellationRequested)

            {

                try

                {

                    _engine.EnforceCurrentMode();

                }

                catch (Exception ex)

                {

                    FileLogger.Log($"{AppMetadata.DisplayName}: enforcement failed — {ex.Message}");

                    _logger.LogError(ex, "Enforcement failed.");

                }



                await Task.Delay(EnforcementInterval, stoppingToken);

            }

        }

        finally

        {

            await pipeTask;

        }

    }



    public override async Task StopAsync(CancellationToken cancellationToken)

    {

        FileLogger.Log($"{AppMetadata.DisplayName}: worker stopping.");

        _logger.LogInformation("{DisplayName} worker stopping.", AppMetadata.DisplayName);

        await base.StopAsync(cancellationToken);

    }

}



using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Service;

Host.CreateDefaultBuilder(args)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseWindowsService(options => options.ServiceName = AppMetadata.ServiceInternalName)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();

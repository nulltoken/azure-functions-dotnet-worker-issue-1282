using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

IHost host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults((ctx, builder) =>
    {
        builder
            .AddApplicationInsights(opts =>
            {
                opts.EnableAdaptiveSampling = false;
            })
            .AddApplicationInsightsLogger();
    })

    // Cf. https://github.com/devops-circle/Azure-Functions-Logging-Tests/blob/master/Func.Isolated.Net7.With.AI/Program.cs
    .ConfigureServices((ctx, serviceProvider) =>
    {
        // You will need extra configuration because above will only log per default Warning (default AI configuration) and above because of following line:
        // https://github.com/microsoft/ApplicationInsights-dotnet/blob/main/NETCORE/src/Shared/Extensions/ApplicationInsightsExtensions.cs#L427
        // This is documented here:
        // https://github.com/microsoft/ApplicationInsights-dotnet/issues/2610#issuecomment-1316672650
        // So remove the default logger rule (warning and above). This will result that the default will be Information.
        serviceProvider.Configure<LoggerFilterOptions>(options =>
        {
            LoggerFilterRule? toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (toRemove is not null)
            {
                options.Rules.Remove(toRemove);
            }
        });

        // Setup DI here
        serviceProvider.AddHttpClient(Options.DefaultName);
    })
    .ConfigureAppConfiguration((hostContext, config) =>
    {
        // Add appsettings.json configuration so we can set logging in configuration.
        // Add in example a file called appsettings.json to the root and set the properties to:
        // Build Action: Content
        // Copy to Output Directory: Copy if newer
        //
        // Content:
        // {
        //   "Logging": {
        //     "LogLevel": {
        //       "Default": "Error" // Change this to ie Trace for more logging
        //     }
        //   }
        // }
        config.AddJsonFile("appsettings.json", optional: true);
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        // Make sure the configuration of the appsettings.json file is picked up.
        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
    })
    .Build();

host.Run();

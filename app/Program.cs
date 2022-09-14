﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using dotenv.net;

using sample;

DotEnv.Load();

using IHost host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();

        if (context.HostingEnvironment.IsProduction())
        {

        }
        else
        {

        }

        services
            // .AddSingleton<>(provider =>
            // {

            // })
            .AddHttpClient()
            // .AddSingleton<>()
            .AddHostedService<Collector>(provider =>
            {
                var endpointURL = "";
                if (args.Length > 0)
                {
                    endpointURL = args[0];
                }

                if (string.IsNullOrEmpty(endpointURL))
                {
                    var config = provider.GetRequiredService<IConfiguration>();
                    return Collector.FromConfig(config);
                }
                else
                {
                    var props = new Collector.Props
                    {
                        endpointURL = endpointURL,
                    };
                    return new Collector(props);
                }
            });
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();

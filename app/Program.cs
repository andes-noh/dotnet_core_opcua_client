using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using dotenv.net;

using sample;
using HttpClient;
using Client;
using System;

DotEnv.Load();

using IHost host = Host
    .CreateDefaultBuilder(args)
    .UseWindowsService()
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
            .AddSingleton<OpcUaClient>(provider =>
            {
                var endpoint = "";
                if (args.Length > 0)
                {
                    endpoint = args[0];
                }

                if (string.IsNullOrEmpty(endpoint))
                {
                    var config = provider.GetRequiredService<IConfiguration>();
                    return OpcUaClient.FromConfig(config);
                }
                else
                {
                    var props = new OpcUaClient.Props
                    {
                        endpoint = $"opc.tcp://{endpoint}:4840"
                    };
                    return new OpcUaClient(props);
                }
            })
            .AddSingleton<HttpHelper>(provider =>
            {
                var props = new HttpHelper.Props
                {
                    url = "https://localhost:8080/",
                };
                return new HttpHelper(props);
            })
            .AddHttpClient()
            // .AddSingleton<>()
            .AddHostedService<Collector>(provider =>
            {
                var cycleTime = "";
                if (args.Length > 0)
                {
                    cycleTime = args[1];
                }

                if (string.IsNullOrEmpty(cycleTime))
                {
                    var config = provider.GetRequiredService<IConfiguration>();
                    var http = provider.GetRequiredService<HttpHelper>();
                    var opcua = provider.GetRequiredService<OpcUaClient>();

                    return Collector.FromConfig(config, http, opcua);
                }
                else
                {
                    var props = new Collector.Props
                    {
                        cycleTime = Int32.Parse(cycleTime),
                    };
                    var http = provider.GetRequiredService<HttpHelper>();
                    var opcua = provider.GetRequiredService<OpcUaClient>();
                    return new Collector(props, http, opcua);
                }
            });
    })
    // .UseConsoleLifetime()
    .Build();

await host.RunAsync();

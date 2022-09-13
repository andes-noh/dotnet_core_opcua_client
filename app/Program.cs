using Microsoft.Extensions.Configuration;
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
                var text = "";
                if (args.Length > 0)
                {
                    text = args[0];
                }

                if (string.IsNullOrEmpty(text))
                {
                    var config = provider.GetRequiredService<IConfiguration>();
                    return Collector.FromConfig(config);
                }
                else
                {
                    var props = new Collector.Props
                    {
                        text = text,
                    };
                    return new Collector(props);
                }
            });
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();

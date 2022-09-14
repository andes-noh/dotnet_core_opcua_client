namespace sample;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Opc.Ua;
using Client;

public class Collector : BackgroundService
{
    OpcUaClient opcUaClient;
    public class Props
    {
        public string? endpointURL { get; set; }
    }
    private readonly Props _props;

    public static Collector FromConfig(IConfiguration config)
    {
        var props = new Props
        {
            endpointURL = "opc.tcp://" + config["ENDPOINT_URL"] + ":4840",
        };

        return new Collector(props);
    }

    public Collector(Props props)
    {
        _props = props;
    }

    public void DataCollector(CancellationToken cancellationToken, string endpoint)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!opcUaClient.getConnected)
                {
                    try
                    {
                        opcUaClient.UserIdentity = new UserIdentity("OpcUaClient", "12345678");
                        opcUaClient.ConnectServer(endpoint);
                        Thread.Sleep(10000);
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            opcUaClient.setConnected(false);
                            Thread.Sleep(5000);
                        }
                        catch (Exception e2)
                        {
                            try
                            {
                                opcUaClient.Disconnect();
                                Thread.Sleep(5000);
                            }
                            catch (Exception e3)
                            {
                                opcUaClient.setConnected(false);
                            }
                        }
                    }
                }
                else
                {
                    List<NodeId> nodeIds = new List<NodeId>();
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/State/acProg"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/ProgramInfo/progName"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/driveLoad"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/actSpeed"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/speedOvr"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/MachineAxis/feedRateOvr"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/State/acProg"));

                    List<DataValue> dataValues = opcUaClient.ReadValues(nodeIds.ToArray());

                    for (int i = 0; i < dataValues.Count; i++)
                    {
                        Console.WriteLine($"data{i + 1}: " + dataValues[i].ToString());
                    }

                    Thread.Sleep(5000);
                }

            }
            catch (Exception)
            {
                Console.WriteLine("Failed to read value");
            }
            // Console.WriteLine($"Sample Project: {text}");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        opcUaClient = new OpcUaClient();
        Console.WriteLine(_props.endpointURL);
        Task.Run(() => DataCollector(stoppingToken, _props.endpointURL));
        return Task.CompletedTask;
    }


    // BackgroundService는 생략가능
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        opcUaClient.Disconnect();
        return Task.CompletedTask;
    }
}

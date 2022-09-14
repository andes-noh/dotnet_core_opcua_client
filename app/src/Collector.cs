namespace sample;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Opc.Ua;
using Client;
using System.Security.Cryptography.X509Certificates;

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
                        // 인증 관련 id, password 설정
                        opcUaClient.UserIdentity = new UserIdentity("OpcUaClient", "12345678");

                        // Anonymous 연결 설정
                        // new UserIdentity( new AnonymousIdentityToken( ) );

                        // 인증서 연결 설정
                        // X509Certificate2 certificate = new X509Certificate2("", "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                        // new UserIdentity(certificate);

                        opcUaClient.ConnectServer(endpoint);
                        Thread.Sleep(5000);
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
                    // single collection
                    // NodeId nodeId = new NodeId("ns=2;s=/Channel/ProgramInfo/progName");
                    // DataValue dv = opcUaClient.ReadValue(nodeId);
                    // Console.WriteLine($"single data: " + dv.ToString());

                    // multi collection
                    List<NodeId> nodeIds = new List<NodeId>();
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/State/acProg"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/ProgramInfo/progName"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/driveLoad"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/actSpeed"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/Spindle/speedOvr"));
                    nodeIds.Add(new NodeId("ns=2;s=/Channel/MachineAxis/feedRateOvr"));

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
        // 생성자
        opcUaClient = new OpcUaClient();
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

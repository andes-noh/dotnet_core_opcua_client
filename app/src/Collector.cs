namespace sample;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Opc.Ua;
using Client;
using HttpClient;

public class Collector : BackgroundService
{

    public class Props
    {
        public int cycleTime { get; init; }
    }
    private readonly Props _props;

    private HttpHelper _httpHelper;
    private OpcUaClient _opcUaClient;

    public static Collector FromConfig(IConfiguration config, HttpHelper httpHelper, OpcUaClient opcUaClient)
    {
        var props = new Props
        {
            cycleTime = ushort.Parse(config["CYCLE_TIME"]),
        };
        return new Collector(props, httpHelper, opcUaClient);
    }

    public Collector(Props props, HttpHelper httpHelper, OpcUaClient opcUaClient)
    {
        _props = props;
        _httpHelper = httpHelper;
        _opcUaClient = opcUaClient;
    }

    public void DataCollector(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_opcUaClient.getConnected)
                {
                    try
                    {
                        // 인증 관련 id, password 설정
                        _opcUaClient.UserIdentity = new UserIdentity("OpcUaClient", "12345678");

                        // Anonymous 연결 설정
                        // new UserIdentity( new AnonymousIdentityToken( ) );

                        // 인증서 연결 설정
                        // X509Certificate2 certificate = new X509Certificate2("", "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
                        // new UserIdentity(certificate);

                        _opcUaClient.ConnectServer();
                        Thread.Sleep(5000);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _opcUaClient.setConnected(false);
                            Thread.Sleep(5000);
                        }
                        catch (Exception)
                        {
                            try
                            {
                                _opcUaClient.Disconnect();
                                Thread.Sleep(5000);
                            }
                            catch (Exception)
                            {
                                _opcUaClient.setConnected(false);
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

                    List<DataValue> dataValues = _opcUaClient.ReadValues(nodeIds.ToArray());

                    for (int i = 0; i < dataValues.Count; i++)
                    {
                        Console.WriteLine($"data{i + 1}: " + dataValues[i].ToString());
                    }

                    // 5000ms => 5s
                    Thread.Sleep(_props.cycleTime * 1000);
                }

            }
            catch (Exception)
            {
                _opcUaClient.Disconnect();
                _opcUaClient.setConnected(false);
                Console.WriteLine("Failed to read value or error connecting to server");
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 생성자
        // _opcUaClient = new OpcUaClient();
        Task.Run(() => DataCollector(stoppingToken));
        return Task.CompletedTask;
    }

    // BackgroundService는 생략가능
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _opcUaClient.Disconnect();
        _opcUaClient.setConnected(false);
        return Task.CompletedTask;
    }
}

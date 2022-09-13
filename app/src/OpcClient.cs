namespace Client;

using System;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

public class OpcUaClient
{
    public Session session;
    public String endpointURL;

    private ApplicationConfiguration config;
    private MonitoredItem monitoredItem;
    private Subscription subscription;

    private bool mConnected;

    public OpcUaClient()
    {
        config = CreateOpuaAppConfiguration();
    }

    public async Task ConnectServer(string endpointURL)
    {
        session = await Connect(endpointURL);
    }

    private async Task<Session> Connect(string endpointURL)
    {
        //
        Disconnect();
        config = CreateOpuaAppConfiguration();
        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(config);
        EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(endpointURL, true);

        ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
        session = await Session.Create(config, endpoint, false, false, "", 60000, UserIdentity, null);

        mConnected = true;

        return session;
    }

    public IUserIdentity UserIdentity { get; set; }

    public void Disconnect()
    {
        mConnected = false;
        session.Close(10000);
    }

    private ApplicationConfiguration CreateOpuaAppConfiguration()
    {
        var config = new ApplicationConfiguration()
        {
            ApplicationName = "OPC UA Client",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier(),
                AutoAcceptUntrustedCertificates = true
            },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        config.Validate(ApplicationType.Client);

        if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
        {
            config.CertificateValidator.CertificateValidation += (s, e) =>
            {
                e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted);
            };
        }

        return config;
    }

    private void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
        foreach (var value in item.DequeueValues())
        {
            Console.WriteLine(String.Format("[OPC UA Subscription Message]"));
        }
    }

    public void monitoredItem_Notification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
        if (notification == null)
        {
            return;
        }
    }

    public DataValue ReadValue(NodeId nodeId)
    {
        ReadValueId itemToRead = new ReadValueId();
        itemToRead.NodeId = nodeId;
        itemToRead.AttributeId = Attributes.Value;

        ReadValueIdCollection itemsToRead = new ReadValueIdCollection();
        itemsToRead.Add(itemToRead);

        DataValueCollection values = null;
        DiagnosticInfoCollection diagnosticInfos = null;

        ResponseHeader responseHeader = session.Read(null, 0, TimestampsToReturn.Both, itemsToRead, out values, out diagnosticInfos);

        ClientBase.ValidateResponse(values, itemsToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, itemsToRead);

        if (StatusCode.IsBad(values[0].StatusCode))
        {
            ServiceResult result = ClientBase.GetResult(values[0].StatusCode, 0, diagnosticInfos, responseHeader);
            throw new ServiceResultException(result);
        }

        return values[0];
    }
}




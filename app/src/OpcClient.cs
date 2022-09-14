namespace Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Opc.Ua;
using Opc.Ua.Client;

public class OpcUaClient
{
    public class Props
    {
        public string? endpoint { get; init; }
    }

    private readonly Props _props;
    public Session session;

    private ApplicationConfiguration config;

    // default false
    // private bool useSecurity;

    private bool mConnected = false;

    public void setConnected(bool value)
    {
        this.mConnected = value;
    }
    public bool getConnected => mConnected;

    // public bool UseSecurity
    // {
    //     get { return useSecurity; }
    //     set { useSecurity = value; }
    // }

    public static OpcUaClient FromConfig(IConfiguration config)
    {
        var props = new Props
        {
            endpoint = "opc.tcp://" + config["ENDPOINT_URL"] + ":4840"
        };
        return new OpcUaClient(props);
    }
    public OpcUaClient(Props props)
    {
        _props = props;
        config = CreateOpuaAppConfiguration();
        // var certificateValidator = new CertificateValidator();
        // certificateValidator.CertificateValidation += (sender, eventArgs) =>
        // {
        //     if (ServiceResult.IsGood(eventArgs.Error))
        //         eventArgs.Accept = true;
        //     else if (eventArgs.Error.StatusCode.Code == StatusCodes.BadCertificateUntrusted)
        //         eventArgs.Accept = true;
        //     else
        //         throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", eventArgs.Error.Code, eventArgs.Error.AdditionalInfo));
        // };

        // SecurityConfiguration securityConfigurationcv = new SecurityConfiguration
        // {
        //     AutoAcceptUntrustedCertificates = true,
        //     RejectSHA1SignedCertificates = false,
        //     MinimumCertificateKeySize = 1024,
        // };
        // certificateValidator.Update(securityConfigurationcv);

        // // Build the application configuration
        // var configuration = new ApplicationConfiguration
        // {
        //     ApplicationName = "OPC UA CLIENT",
        //     ApplicationType = ApplicationType.Client,
        //     CertificateValidator = certificateValidator,
        //     ApplicationUri = "urn:MyClient", //Kepp this syntax
        //     ProductUri = "OpcUaClient",

        //     ServerConfiguration = new ServerConfiguration
        //     {
        //         MaxSubscriptionCount = 100000,
        //         MaxMessageQueueSize = 1000000,
        //         MaxNotificationQueueSize = 1000000,
        //         MaxPublishRequestCount = 10000000,
        //     },

        //     SecurityConfiguration = new SecurityConfiguration
        //     {
        //         AutoAcceptUntrustedCertificates = true,
        //         RejectSHA1SignedCertificates = false,
        //         MinimumCertificateKeySize = 1024,
        //         SuppressNonceValidationErrors = true,

        //         ApplicationCertificate = new CertificateIdentifier
        //         {
        //             StoreType = CertificateStoreType.X509Store,
        //             StorePath = "CurrentUser\\My",
        //             SubjectName = "OPC UA CLIENT",
        //         },
        //         TrustedIssuerCertificates = new CertificateTrustList
        //         {
        //             StoreType = CertificateStoreType.X509Store,
        //             StorePath = "CurrentUser\\Root",
        //         },
        //         TrustedPeerCertificates = new CertificateTrustList
        //         {
        //             StoreType = CertificateStoreType.X509Store,
        //             StorePath = "CurrentUser\\Root",
        //         }
        //     },

        //     TransportQuotas = new TransportQuotas
        //     {
        //         OperationTimeout = 6000000,
        //         MaxStringLength = int.MaxValue,
        //         MaxByteStringLength = int.MaxValue,
        //         MaxArrayLength = 65535,
        //         MaxMessageSize = 419430400,
        //         MaxBufferSize = 65535,
        //         ChannelLifetime = -1,
        //         SecurityTokenLifetime = -1
        //     },
        //     ClientConfiguration = new ClientConfiguration
        //     {
        //         DefaultSessionTimeout = -1,
        //         MinSubscriptionLifetime = -1,
        //     },
        //     DisableHiResClock = true
        // };

        // configuration.Validate(ApplicationType.Client);
        // config = configuration;
    }

    public async Task ConnectServer()
    {
        session = await Connect(_props.endpoint);
    }

    private async Task<Session> Connect(string endpointURL)
    {
        //
        Disconnect();
        if (config == null)
        {
            throw new ArgumentNullException("configuration");
        }

        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(config);
        EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(endpointURL, false /*UseSecurity*/);

        ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
        session = await Session.Create(config, endpoint, false, false, "OPC UA CLIENT", 60000, UserIdentity, null);
        mConnected = true;

        return session;
    }

    public IUserIdentity UserIdentity { get; set; }

    public void Disconnect()
    {
        mConnected = false;
        if (session != null)
        {
            session.Close();
            session = null;
        }
        mConnected = false;
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

    public DataValue ReadValue(NodeId nodeId)
    {
        ReadValueId itemToRead = new ReadValueId();
        itemToRead.NodeId = nodeId;
        itemToRead.AttributeId = Attributes.Value;

        ReadValueIdCollection itemsToRead = new ReadValueIdCollection();
        itemsToRead.Add(itemToRead);

        DataValueCollection values = null;
        DiagnosticInfoCollection diagnosticInfos = null;

        ResponseHeader responseHeader = session.Read(null, 1000.0, TimestampsToReturn.Neither, itemsToRead, out values, out diagnosticInfos);

        ClientBase.ValidateResponse(values, itemsToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, itemsToRead);

        if (StatusCode.IsBad(values[0].StatusCode))
        {
            ServiceResult result = ClientBase.GetResult(values[0].StatusCode, 0, diagnosticInfos, responseHeader);
            throw new ServiceResultException(result);
        }

        return values[0];
    }

    public List<DataValue> ReadValues(NodeId[] nodeIds)
    {
        ReadValueIdCollection itemsToRead = new ReadValueIdCollection();

        DataValueCollection values = null;
        DiagnosticInfoCollection diagnosticInfos = null;

        for (int i = 0; i < nodeIds.Length; i++)
        {
            itemsToRead.Add(new ReadValueId()
            {
                NodeId = nodeIds[i],
                AttributeId = Attributes.Value
            });
        }

        session.Read(
            null,
            0,
            TimestampsToReturn.Neither,
            itemsToRead,
            out values,
            out diagnosticInfos);

        ClientBase.ValidateResponse(values, itemsToRead);
        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, itemsToRead);

        return values.ToList();
    }
}




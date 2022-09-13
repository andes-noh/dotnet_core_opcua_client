using Opc.Ua;
using Opc.Ua.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpcUaHelper
{

    public class mOpcUaClient
    {
        #region Constructors

        public mOpcUaClient()
        {
            dic_subscriptions = new Dictionary<string, Subscription>();

            var certificateValidator = new CertificateValidator();
            certificateValidator.CertificateValidation += (sender, eventArgs) =>
            {
                if (ServiceResult.IsGood(eventArgs.Error))
                    eventArgs.Accept = true;
                else if (eventArgs.Error.StatusCode.Code == StatusCodes.BadCertificateUntrusted)
                    eventArgs.Accept = true;
                else
                    throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", eventArgs.Error.Code, eventArgs.Error.AdditionalInfo));
            };

            SecurityConfiguration securityConfigurationcv = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024,
            };
            certificateValidator.Update(securityConfigurationcv);

            // Build the application configuration
            var configuration = new ApplicationConfiguration
            {
                ApplicationName = OpcUaName,
                ApplicationType = ApplicationType.Client,
                CertificateValidator = certificateValidator,
                ApplicationUri = "urn:MyClient", //Kepp this syntax
                ProductUri = "OpcUaClient",

                ServerConfiguration = new ServerConfiguration
                {
                    MaxSubscriptionCount = 100000,
                    MaxMessageQueueSize = 1000000,
                    MaxNotificationQueueSize = 1000000,
                    MaxPublishRequestCount = 10000000,
                },

                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    RejectSHA1SignedCertificates = false,
                    MinimumCertificateKeySize = 1024,
                    SuppressNonceValidationErrors = true,

                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\My",
                        SubjectName = OpcUaName,
                    },
                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\Root",
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\Root",
                    }
                },

                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 6000000,
                    MaxStringLength = int.MaxValue,
                    MaxByteStringLength = int.MaxValue,
                    MaxArrayLength = 65535,
                    MaxMessageSize = 419430400,
                    MaxBufferSize = 65535,
                    ChannelLifetime = -1,
                    SecurityTokenLifetime = -1
                },
                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = -1,
                    MinSubscriptionLifetime = -1,
                },
                DisableHiResClock = true
            };

            configuration.Validate(ApplicationType.Client);
            m_configuration = configuration;
        }

        #endregion Constructors

        #region Connect And Disconnect

        /// <summary>
        /// connect to server
        /// </summary>
        /// <param name="serverUrl">remote url</param>
        public async Task ConnectServer(string serverUrl)
        {
            m_session = await Connect(serverUrl);
        }

        /// <summary>
        /// Creates a new session.
        /// </summary>
        /// <returns>The new session object.</returns>
        private async Task<Session> Connect(string serverUrl)
        {
            // disconnect from existing session.
            Disconnect();

            if (m_configuration == null)
            {
                throw new ArgumentNullException("_configuration");
            }

            // select the best endpoint.
            EndpointDescription endpointDescription = CoreClientUtils.SelectEndpoint(serverUrl, UseSecurity);
            EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create(m_configuration);

            ConfiguredEndpoint endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);

            m_session = await Session.Create(
                m_configuration,
                endpoint,
                false,
                false,
                (string.IsNullOrEmpty(OpcUaName)) ? m_configuration.ApplicationName : OpcUaName,
                60000,
                UserIdentity,
                new string[] { });

            // set up keep alive callback.
            m_session.KeepAlive += new KeepAliveEventHandler(Session_KeepAlive);

            // update the client status
            m_IsConnected = true;

            // raise an event.
            DoConnectComplete(null);

            // return the new session.
            return m_session;
        }

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        public void Disconnect()
        {
            UpdateStatus(false, DateTime.UtcNow, "Disconnected");

            // stop any reconnect operation.
            if (m_reConnectHandler != null)
            {
                m_reConnectHandler.Dispose();
                m_reConnectHandler = null;
            }

            // disconnect any existing session.
            if (m_session != null)
            {
                m_session.Close(10000);
                m_session = null;
            }

            // update the client status
            m_IsConnected = false;

            // raise an event.
            DoConnectComplete(null);
        }

        #endregion Connect And Disconnect

        #region Event Handlers

        /// <summary>
        /// Report the client status
        /// </summary>
        /// <param name="error">Whether the status represents an error.</param>
        /// <param name="time">The time associated with the status.</param>
        /// <param name="status">The status message.</param>
        /// <param name="args">Arguments used to format the status message.</param>
        private void UpdateStatus(bool error, DateTime time, string status, params object[] args)
        {
            m_OpcStatusChange?.Invoke(this, new OpcUaStatusEventArgs()
            {
                Error = error,
                Time = time.ToLocalTime(),
                Text = String.Format(status, args),
            });
        }

        /// <summary>
        /// Handles a keep alive event from a session.
        /// </summary>
        private void Session_KeepAlive(Session session, KeepAliveEventArgs e)
        {
            try
            {
                // check for events from discarded sessions.
                if (!Object.ReferenceEquals(session, m_session))
                {
                    return;
                }

                // start reconnect sequence on communication error.
                if (ServiceResult.IsBad(e.Status))
                {
                    if (m_reconnectPeriod <= 0)
                    {
                        UpdateStatus(true, e.CurrentTime, "Communication Error ({0})", e.Status);
                        return;
                    }

                    UpdateStatus(true, e.CurrentTime, "Reconnecting in {0}s", m_reconnectPeriod);

                    if (m_reConnectHandler == null)
                    {
                        m_ReconnectStarting?.Invoke(this, e);

                        m_reConnectHandler = new SessionReconnectHandler();
                        m_reConnectHandler.BeginReconnect(m_session, m_reconnectPeriod * 1000, Server_ReconnectComplete);
                    }

                    return;
                }

                // update status.
                UpdateStatus(false, e.CurrentTime, "Connected [{0}]", session.Endpoint.EndpointUrl);

                // raise any additional notifications.
                m_KeepAliveComplete?.Invoke(this, e);
            }
            catch (Exception exception)
            {
                //
            }
        }

        /// <summary>
        /// Handles a reconnect event complete from the reconnect handler.
        /// </summary>
        private void Server_ReconnectComplete(object sender, EventArgs e)
        {
            try
            {
                // ignore callbacks from discarded objects.
                if (!Object.ReferenceEquals(sender, m_reConnectHandler))
                {
                    return;
                }

                m_session = m_reConnectHandler.Session;
                m_reConnectHandler.Dispose();
                m_reConnectHandler = null;

                // raise any additional notifications.
                m_ReconnectComplete?.Invoke(this, e);
            }
            catch (Exception exception)
            {
                //
            }
        }

        #endregion Event Handlers

        #region Public Members

        /// <summary>
        /// a name of application name show on server
        /// </summary>
        public string OpcUaName { get; set; } = "Opc Ua Helper";

        /// <summary>
        /// Whether to use security when connecting.
        /// </summary>
        public bool UseSecurity
        {
            get { return m_useSecurity; }
            set { m_useSecurity = value; }
        }

        /// <summary>
        /// The user identity to use when creating the session.
        /// </summary>
        public IUserIdentity UserIdentity { get; set; }

        /// <summary>
        /// The currently active session.
        /// </summary>
        public Session Session
        {
            get { return m_session; }
        }

        /// <summary>
        /// Indicate the connect status
        /// </summary>
        public bool Connected
        {
            get { return m_IsConnected; }
        }

        /// <summary>
        /// The number of seconds between reconnect attempts (0 means reconnect is disabled).
        /// </summary>
        public int ReconnectPeriod
        {
            get { return m_reconnectPeriod; }
            set { m_reconnectPeriod = value; }
        }

        /// <summary>
        /// Raised when a good keep alive from the server arrives.
        /// </summary>
        public event EventHandler KeepAliveComplete
        {
            add { m_KeepAliveComplete += value; }
            remove { m_KeepAliveComplete -= value; }
        }

        /// <summary>
        /// Raised when a reconnect operation starts.
        /// </summary>
        public event EventHandler ReconnectStarting
        {
            add { m_ReconnectStarting += value; }
            remove { m_ReconnectStarting -= value; }
        }

        /// <summary>
        /// Raised when a reconnect operation completes.
        /// </summary>
        public event EventHandler ReconnectComplete
        {
            add { m_ReconnectComplete += value; }
            remove { m_ReconnectComplete -= value; }
        }

        /// <summary>
        /// Raised after successfully connecting to or disconnecing from a server.
        /// </summary>
        public event EventHandler ConnectComplete
        {
            add { m_ConnectComplete += value; }
            remove { m_ConnectComplete -= value; }
        }

        /// <summary>
        /// Raised after the client status change
        /// </summary>
        public event EventHandler<OpcUaStatusEventArgs> OpcStatusChange
        {
            add { m_OpcStatusChange += value; }
            remove { m_OpcStatusChange -= value; }
        }

        public ApplicationConfiguration AppConfig => m_configuration;

        #endregion Public Members

        #region Node Write/Read Support

        /// <summary>
        /// Read a value node from server
        /// </summary>
        /// <param name="nodeId">node id</param>
        /// <returns>DataValue</returns>
        public DataValue ReadNode(NodeId nodeId)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId( )
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value
                }
            };

            // read the current value
            m_session.Read(
                null,
                1000.0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            return results[0];
        }

        public T ReadNode<T>(string tag)
        {
            DataValue dataValue = ReadNode(new NodeId(tag));
            return (T)dataValue.Value;
        }

        public Task<T> ReadNodeAsync<T>(string tag)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId()
                {
                    NodeId = new NodeId(tag),
                    AttributeId = Attributes.Value
                }
            };

            // Wrap the ReadAsync logic in a TaskCompletionSource, so we can use C# async/await syntax to call it:
            var taskCompletionSource = new TaskCompletionSource<T>();
            m_session.BeginRead(
                requestHeader: null,
                maxAge: 0,
                timestampsToReturn: TimestampsToReturn.Neither,
                nodesToRead: nodesToRead,
                callback: ar =>
                {
                    DataValueCollection results;
                    DiagnosticInfoCollection diag;
                    var response = m_session.EndRead(
                      result: ar,
                      results: out results,
                      diagnosticInfos: out diag);

                    try
                    {
                        CheckReturnValue(response.ServiceResult);
                        CheckReturnValue(results[0].StatusCode);
                        var val = results[0];
                        taskCompletionSource.TrySetResult((T)val.Value);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                asyncState: null);

            return taskCompletionSource.Task;
        }

        public List<DataValue> ReadNodes(NodeId[] nodeIds)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            for (int i = 0; i < nodeIds.Length; i++)
            {
                nodesToRead.Add(new ReadValueId()
                {
                    NodeId = nodeIds[i],
                    AttributeId = Attributes.Value
                });
            }

            m_session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            return results.ToList();
        }

        public Task<List<DataValue>> ReadNodesAsync(NodeId[] nodeIds)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            for (int i = 0; i < nodeIds.Length; i++)
            {
                nodesToRead.Add(new ReadValueId()
                {
                    NodeId = nodeIds[i],
                    AttributeId = Attributes.Value
                });
            }

            var taskCompletionSource = new TaskCompletionSource<List<DataValue>>();

            m_session.BeginRead(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                callback: ar =>
                {
                    DataValueCollection results;
                    DiagnosticInfoCollection diag;
                    var response = m_session.EndRead(
                      result: ar,
                      results: out results,
                      diagnosticInfos: out diag);

                    try
                    {
                        CheckReturnValue(response.ServiceResult);
                        taskCompletionSource.TrySetResult(results.ToList());
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                asyncState: null);

            return taskCompletionSource.Task;
        }


        public List<T> ReadNodes<T>(string[] tags)
        {
            List<T> result = new List<T>();
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            for (int i = 0; i < tags.Length; i++)
            {
                nodesToRead.Add(new ReadValueId()
                {
                    NodeId = new NodeId(tags[i]),
                    AttributeId = Attributes.Value
                });
            }

            m_session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            foreach (var item in results)
            {
                result.Add((T)item.Value);
            }
            return result;
        }


        public Task<List<T>> ReadNodesAsync<T>(string[] tags)
        {
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
            for (int i = 0; i < tags.Length; i++)
            {
                nodesToRead.Add(new ReadValueId()
                {
                    NodeId = new NodeId(tags[i]),
                    AttributeId = Attributes.Value
                });
            }

            var taskCompletionSource = new TaskCompletionSource<List<T>>();
            m_session.BeginRead(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                callback: ar =>
                {
                    DataValueCollection results;
                    DiagnosticInfoCollection diag;
                    var response = m_session.EndRead(
                      result: ar,
                      results: out results,
                      diagnosticInfos: out diag);

                    try
                    {
                        CheckReturnValue(response.ServiceResult);
                        List<T> result = new List<T>();
                        foreach (var item in results)
                        {
                            result.Add((T)item.Value);
                        }
                        taskCompletionSource.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                asyncState: null);

            return taskCompletionSource.Task;
        }


        public bool WriteNode<T>(string tag, T value)
        {
            WriteValue valueToWrite = new WriteValue()
            {
                NodeId = new NodeId(tag),
                AttributeId = Attributes.Value
            };
            valueToWrite.Value.Value = value;
            valueToWrite.Value.StatusCode = StatusCodes.Good;
            valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
            valueToWrite.Value.SourceTimestamp = DateTime.MinValue;

            WriteValueCollection valuesToWrite = new WriteValueCollection
            {
                valueToWrite
            };



            m_session.Write(
                null,
                valuesToWrite,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            ClientBase.ValidateResponse(results, valuesToWrite);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, valuesToWrite);

            if (StatusCode.IsBad(results[0]))
            {
                throw new ServiceResultException(results[0]);
            }

            return !StatusCode.IsBad(results[0]);
        }

        public Task<bool> WriteNodeAsync<T>(string tag, T value)
        {
            WriteValue valueToWrite = new WriteValue()
            {
                NodeId = new NodeId(tag),
                AttributeId = Attributes.Value,
            };
            valueToWrite.Value.Value = value;
            valueToWrite.Value.StatusCode = StatusCodes.Good;
            valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
            valueToWrite.Value.SourceTimestamp = DateTime.MinValue;
            WriteValueCollection valuesToWrite = new WriteValueCollection
            {
                valueToWrite
            };

            // Wrap the WriteAsync logic in a TaskCompletionSource, so we can use C# async/await syntax to call it:
            var taskCompletionSource = new TaskCompletionSource<bool>();
            m_session.BeginWrite(
                requestHeader: null,
                nodesToWrite: valuesToWrite,
                callback: ar =>
                {
                    var response = m_session.EndWrite(
                      result: ar,
                      results: out StatusCodeCollection results,
                      diagnosticInfos: out DiagnosticInfoCollection diag);

                    try
                    {
                        ClientBase.ValidateResponse(results, valuesToWrite);
                        ClientBase.ValidateDiagnosticInfos(diag, valuesToWrite);
                        taskCompletionSource.SetResult(StatusCode.IsGood(results[0]));
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                asyncState: null);
            return taskCompletionSource.Task;
        }

        public bool WriteNodes(string[] tags, object[] values)
        {
            WriteValueCollection valuesToWrite = new WriteValueCollection();

            for (int i = 0; i < tags.Length; i++)
            {
                if (i < values.Length)
                {
                    WriteValue valueToWrite = new WriteValue()
                    {
                        NodeId = new NodeId(tags[i]),
                        AttributeId = Attributes.Value
                    };
                    valueToWrite.Value.Value = values[i];
                    valueToWrite.Value.StatusCode = StatusCodes.Good;
                    valueToWrite.Value.ServerTimestamp = DateTime.MinValue;
                    valueToWrite.Value.SourceTimestamp = DateTime.MinValue;
                    valuesToWrite.Add(valueToWrite);
                }
            }

            m_session.Write(
                null,
                valuesToWrite,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            ClientBase.ValidateResponse(results, valuesToWrite);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, valuesToWrite);

            bool result = true;
            foreach (var r in results)
            {
                if (StatusCode.IsBad(r))
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        #endregion Node Write/Read Support

        #region Monitor Support

        public void AddSubscription(string key, string tag, Action<string, MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            AddSubscription(key, new string[] { tag }, callback);
        }


        public void AddSubscription(string key, string[] tags, Action<string, MonitoredItem, MonitoredItemNotificationEventArgs> callback)
        {
            Subscription m_subscription = new Subscription(m_session.DefaultSubscription);

            m_subscription.PublishingEnabled = true;
            m_subscription.PublishingInterval = 0;
            m_subscription.KeepAliveCount = uint.MaxValue;
            m_subscription.LifetimeCount = uint.MaxValue;
            m_subscription.MaxNotificationsPerPublish = uint.MaxValue;
            m_subscription.Priority = 100;
            m_subscription.DisplayName = key;

            for (int i = 0; i < tags.Length; i++)
            {
                var item = new MonitoredItem
                {
                    StartNodeId = new NodeId(tags[i]),
                    AttributeId = Attributes.Value,
                    DisplayName = tags[i],
                    SamplingInterval = 100,
                };
                item.Notification += (MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs args) =>
                {
                    callback?.Invoke(key, monitoredItem, args);
                };
                m_subscription.AddItem(item);
            }

            m_session.AddSubscription(m_subscription);
            m_subscription.Create();

            lock (dic_subscriptions)
            {
                if (dic_subscriptions.ContainsKey(key))
                {
                    // remove
                    dic_subscriptions[key].Delete(true);
                    m_session.RemoveSubscription(dic_subscriptions[key]);
                    dic_subscriptions[key].Dispose();
                    dic_subscriptions[key] = m_subscription;
                }
                else
                {
                    dic_subscriptions.Add(key, m_subscription);
                }
            }
        }

        public void RemoveSubscription(string key)
        {
            lock (dic_subscriptions)
            {
                if (dic_subscriptions.ContainsKey(key))
                {
                    // remove
                    dic_subscriptions[key].Delete(true);
                    m_session.RemoveSubscription(dic_subscriptions[key]);
                    dic_subscriptions[key].Dispose();
                    dic_subscriptions.Remove(key);
                }
            }
        }

        public void RemoveAllSubscription()
        {
            lock (dic_subscriptions)
            {
                foreach (var item in dic_subscriptions)
                {
                    item.Value.Delete(true);
                    m_session.RemoveSubscription(item.Value);
                    item.Value.Dispose();
                }
                dic_subscriptions.Clear();
            }
        }

        #endregion Monitor Support

        #region Private Methods

        /// <summary>
        /// Raises the connect complete event on the main GUI thread.
        /// </summary>
        private void DoConnectComplete(object state)
        {
            m_ConnectComplete?.Invoke(this, null);
        }

        private void CheckReturnValue(StatusCode status)
        {
            if (!StatusCode.IsGood(status))
                throw new Exception(string.Format("Invalid response from the server. (Response Status: {0})", status));
        }

        #endregion Private Methods

        #region Private Fields

        private ApplicationConfiguration m_configuration;
        private Session m_session;
        private bool m_IsConnected;                       //是否已经连接过
        private int m_reconnectPeriod = 10;               // 重连状态
        private bool m_useSecurity;

        private SessionReconnectHandler m_reConnectHandler;
        private EventHandler m_ReconnectComplete;
        private EventHandler m_ReconnectStarting;
        private EventHandler m_KeepAliveComplete;
        private EventHandler m_ConnectComplete;
        private EventHandler<OpcUaStatusEventArgs> m_OpcStatusChange;

        private Dictionary<string, Subscription> dic_subscriptions;        // 系统所有的节点信息

        #endregion Private Fields
    }
}

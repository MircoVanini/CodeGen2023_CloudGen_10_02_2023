using nanoFramework.Azure.Devices.Client;
using nanoFramework.Azure.Devices.Provisioning.Client;
using Microsoft.Extensions.Logging;
using nanoFramework.Logging;
using nanoFramework.Logging.Debug;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Proxima.Nano.Demo
{
    public class AzureClient
    {
        #region Fields

        private DebugLogger logger;
        private DeviceClient client;
        private static AzureClient instance;
        private SyncQueue queue;

        #endregion

        #region Properties

        public static AzureClient Instance
        {
            get
            {
                if (instance == null)
                    instance = new AzureClient();
                return instance;
            }
        }

        public bool IsConnect => client != null && client.IsConnected;

        #endregion

        #region Constructors

        public AzureClient()
        {
            logger = new DebugLogger(nameof(AzureClient));
            queue  = new SyncQueue(logger, OnDequeueItem);
        }

        #endregion

        #region Public Methods

        public void CreateDeviceClient()
        {
            try
            {
                logger.LogInformation("CreateDeviceClient is starting...");

                // See the previous sections in the SDK help, you either need to have the Azure certificate embedded
                // Either passing it in the constructor
                //
                X509Certificate azureCA = new(Proxima.Nano.Demo.Resources.GetBytes(Proxima.Nano.Demo.Resources.BinaryResources.BaltimoreRootCA_crt));
                
                var provisioning = ProvisioningDeviceClient.Create(Constants.DpsAddress, 
                                                                   Constants.IdScope, 
                                                                   Constants.RegistrationID,
                                                                   Constants.SasKey, 
                                                                   azureCA);

                var dvice = provisioning.Register(new CancellationTokenSource(60000).Token);

                if (dvice.Status != ProvisioningRegistrationStatusType.Assigned)
                {
                    logger.LogError($"Registration is not assigned: {dvice.Status}, error message: {dvice.ErrorMessage}");
                    return;
                }

                // You can then create the device
                //
                client = new DeviceClient(dvice.AssignedHub, 
                                          dvice.DeviceId, 
                                          Constants.SasKey, 
                                          nanoFramework.M2Mqtt.Messages.MqttQoSLevel.AtMostOnce, 
                                          azureCA);

                // add callbacks
                //
                client.StatusUpdated += OnStatusUpdated;
                client.AddMethodCallback(GetDoorStatus);

                // Open it and continue like for the previous sections
                //
                var res = client.Open();

                if (!res)
                {
                    logger.LogError($"can't open the device client");
                    return;
                }

                logger.LogInformation("CreateDeviceClient done");
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        public void DisposeDeviceClient()
        {
            if (client == null)
                return;

            queue.StopQueueThread();

            client.Close();
            client.Dispose();
            client = null;
        }

        public bool SendMessage(string message)
        {
            if (client == null || !client.IsConnected)
                return false;

            client.SendMessage(message);

            return true; // fire & forget !
        }

        public bool EnqueueMessage(string message)
        {
            if (client == null || !client.IsConnected)
                return false;

            if (!queue.IsRunning)
                queue.StartQueueThread();

            queue.Enqueue(message);

            return true;
        }

        #endregion

        #region Private Methods

        private void OnStatusUpdated(object sender, StatusUpdatedEventArgs e)
        {
            try
            {
                logger.LogInformation($"OnStatusUpdated");

                if (e.IoTHubStatus != null)
                {
                    logger.LogInformation($"IoTHub Status changed: {e.IoTHubStatus.Status}");
                    if (e.IoTHubStatus.Message != null)
                        logger.LogInformation($"IoTHub Status message: {e.IoTHubStatus.Message}");
                }

                // You may want to reconnect or use a similar retry mechanism
                //
                if (e.IoTHubStatus.Status == Status.Disconnected)
                {
                    logger.LogInformation("OnStatusUpdated -> IoTHub Stoppped !");
                }
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        private string GetDoorStatus(int rid, string payload)
        {
            var ret = string.Empty;

            try
            {
                logger.LogInformation($"GetDoorStatus -> rid: {rid} - payload: {payload}");

                var status = Application.Instance.IsDoorOpen ? "\"open\"" : "\"close\"";

                ret = $"{{\"doorStatus\":{status}}}";
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex.ToString());
            }

            return ret;
        }

        private bool OnDequeueItem(object message)
        {
            return SendMessage(message as string);
        }


        #endregion
    }
}

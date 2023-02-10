using Iot.Device.Hcsr04.Esp32;
using Iot.Device.Uln2003;
using Microsoft.Extensions.Logging;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Json;
using nanoFramework.Networking;
using nanoFramework.Logging.Debug;
using System;
using System.Diagnostics;
using System.Threading;
using UnitsNet;

namespace Proxima.Nano.Demo
{
    public class Application
    {
        #region Constants

        private const int SonarTriggerPin = Gpio.IO12;
        private const int SonarEchoPin    = Gpio.IO14;

        private const int MotorInp1Pin = Gpio.IO27;
        private const int MotorInp2Pin = Gpio.IO26;
        private const int MotorInp3Pin = Gpio.IO25;
        private const int MotorInp4Pin = Gpio.IO33;

        #endregion

        #region Fields

        private bool        isWiFiConnected;
        private Length      distance;
        private DebugLogger logger;
        private bool        isDoorOpen;

        private static Application instance;

        #endregion

        #region Properties

        public static Application Instance 
        {
            get
            {
                if (instance == null)
                    instance = new Application();
                return instance;
            }
        }

        public bool IsDoorOpen => isDoorOpen;

        #endregion

        #region Constructors

        public Application()
        {
            logger = new DebugLogger(nameof(Application));
        }

        #endregion

        #region Public Methods

        public void Run()
        {
            Hcsr04  sonar = null;
            Uln2003 motor = null;

            bool hasDistance = false;
            bool hasRotated  = false;
            int  debounce    = 0;

            try
            {
                logger.LogInformation(">>> Application.Run");

                ConnectToWiFi();

                if (isWiFiConnected)
                    AzureClient.Instance.CreateDeviceClient();

                sonar = new Hcsr04(SonarTriggerPin, SonarEchoPin);
                motor = new Uln2003(MotorInp1Pin, MotorInp2Pin, MotorInp3Pin, MotorInp4Pin);

                while (true)
                {
                    hasDistance = sonar.TryGetDistance(out distance);
                       
                    if (!hasDistance)
                    {
                        if (++debounce < Constants.DebounceCount)
                        { 
                            Thread.Sleep(50);
                            continue;
                        }
                    }

                    if (hasDistance)
                        logger.LogDebug($"Distance: {distance.Centimeters} cm");
                    else
                        logger.LogDebug("Distance: unknown cm");

                    debounce = 0;

                    if (hasDistance && distance.Centimeters < 5 && distance.Centimeters > 0)
                    {
                        if (hasRotated == false)
                        {
                            logger?.LogInformation("OPEN DOOR");
                            
                            SendDoorEvent(Constants.MainDoorName, DoorEventType.Open);

                            // Set the motor speed to 15 revolutions per minute.
                            //
                            motor.RPM = 15;

                            // Set the motor mode.
                            //
                            motor.Mode = StepperMode.HalfStep;

                            // The motor rotate 2048 steps clockwise (180 degrees for HalfStep mode).
                            //
                            motor.Step(2048);

                            hasRotated = true;
                            isDoorOpen = true;
                        }
                    }
                    else
                    {
                        if (hasRotated)
                        {
                            logger?.LogInformation("CLOSE DOOR");
                            SendDoorEvent(Constants.MainDoorName, DoorEventType.Close);

                            motor.RPM  = 15;
                            motor.Mode = StepperMode.HalfStep;
                            motor.Step(-2048);

                            hasRotated = false;
                            isDoorOpen = false;
                        }
                    }

                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                sonar?.Dispose();
                motor?.Dispose();

                logger.LogInformation("<<< Application.Run");
            }
        }

        #endregion

        #region Private Methods

        private void ConnectToWiFi()
        {
            CancellationTokenSource cs = new(30000);

            isWiFiConnected = WifiNetworkHelper.ConnectDhcp(Constants.WiFiSSD, 
                                                            Constants.WiFiPassword, 
                                                            requiresDateTime: true, 
                                                            token: cs.Token);

            logger.LogInformation($"WiFi connection: {isWiFiConnected}");

            // wait to internal syncronize time & so on...
            //
            if (isWiFiConnected)
                Thread.Sleep(100);
        }

        private void SendDoorEvent(string name, DoorEventType eventType)
        {
            if (!isWiFiConnected || !AzureClient.Instance.IsConnect)
            {
                logger.LogInformation("Skip event for connection broken reason");
                return;
            }

            var doorEvent = new DoorEvent()
            {
                Id        = Guid.NewGuid(),
                DataTicks = DateTime.UtcNow.Ticks,
                Sender    = Constants.ApplicationName,
                Name      = name,
                EventType = eventType
            };

            var message = JsonConvert.SerializeObject(doorEvent);

            AzureClient.Instance.EnqueueMessage(message);
        }

        #endregion
    }
}

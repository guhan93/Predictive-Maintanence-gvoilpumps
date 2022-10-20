using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Message = Microsoft.Azure.Devices.Client.Message;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

namespace Fabrikam.FieldDevice.Generator
{
    public class PumpDevice
    {
        private const string CLOUDTOGGLEPOWERCOMMAND = "ToggleMotorPower";
        public event EventHandler<PumpPowerStateChangedArgs> PumpPowerStateChanged;
               
        private readonly TimeSpan CycleTime = TimeSpan.FromMilliseconds(500);
        private readonly TwinCollection ReportedProperties = new TwinCollection();
        private DeviceClient _deviceClient = null;
        private int _messagesSent = 0;
        private readonly string _deviceId;
        private readonly string _deviceKey;
        private readonly string _idScope;
        private readonly string _dpsEndpoint;
        private readonly string _serialNumber;
        private readonly string _ipAddress;
        private readonly Location _location;
        private readonly IEnumerable<PumpTelemetryItem> _pumpTelemetryData;
        private string _pumpPowerState = Generator.PumpPowerState.OFF;
        private CancellationTokenSource _localCancellationSource = new CancellationTokenSource();
        
        
        
        public int MessagesSent => _messagesSent;

        public string DeviceId => _deviceId;

        public string PumpPowerState { get => _pumpPowerState; set => _pumpPowerState = value; }

       
        public PumpDevice(int deviceNumber, string deviceKey, string idScope, string dpsEndpoint, string serialNumber, string ipAddress,
            Location location, IEnumerable<PumpTelemetryItem> pumpTelemetryData)
        {
            _deviceId = $"DEVICE{deviceNumber:000}";
            _deviceKey = deviceKey;
            _idScope = idScope;
            _dpsEndpoint = dpsEndpoint;
            _serialNumber = serialNumber;
            _ipAddress = ipAddress;
            _location = location;
            _pumpTelemetryData = pumpTelemetryData;            
        }

        private void ResetDevice()
        {
            _localCancellationSource = new CancellationTokenSource();
            
        }

        public async Task RegisterAndConnectDeviceAsync() {

            // dps
            using var security = new SecurityProviderSymmetricKey(_deviceId, _deviceKey, null);
            using var transportHandler = new ProvisioningTransportHandlerMqtt();
            ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(_dpsEndpoint, _idScope, security, transportHandler);
            DeviceRegistrationResult result = await provClient.RegisterAsync();
            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, security.GetPrimaryKey());

            // initialize client
            _deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
            await _deviceClient.SetMethodHandlerAsync(CLOUDTOGGLEPOWERCOMMAND, TogglePowerCommandReceived, null);

            //set default state
            PumpPowerState = Generator.PumpPowerState.ON;

            await SendDevicePropertiesAndInitialState();
        }

        
        public async Task RunDeviceAsync()
        {
            await SendDataToHub(_pumpTelemetryData, _localCancellationSource.Token).ConfigureAwait(false);
        }

        public void CancelCurrentRun()
        {
            _localCancellationSource.Cancel();
        }


        /// <summary>
        /// Updates the device properties.
        /// </summary>
        private async Task SendDevicePropertiesAndInitialState()
        {
            try
            {
                Console.WriteLine($"Sending device properties to {DeviceId}:");
                ReportedProperties["SerialNumber"] = _serialNumber;
                ReportedProperties["IPAddress"] = _ipAddress;
                ReportedProperties["Location"] = _location;
                Console.WriteLine(JsonConvert.SerializeObject(ReportedProperties));

                await _deviceClient.UpdateReportedPropertiesAsync(ReportedProperties);
                await SendEvent(JsonConvert.SerializeObject(new { PowerState = _pumpPowerState }), _localCancellationSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error sending device properties to {DeviceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Takes a set of PumpTelemetryItem data for a device in a dataset and sends the
        /// data to the message with a configurable delay between each message.
        /// </summary>
        /// <param name="pumpTelemetry">The set of data to send as messages to the IoT Central.</param>
        /// <returns></returns>
        private async Task SendDataToHub(IEnumerable<PumpTelemetryItem> pumpTelemetry, CancellationToken cancellationToken)
        {
            foreach (var telemetryItem in pumpTelemetry)
            {
                if (!_localCancellationSource.IsCancellationRequested)
                {
                    await SendEvent(JsonConvert.SerializeObject(telemetryItem), cancellationToken).ConfigureAwait(false);
                }
        
                await Task.Delay(CycleTime, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uses the DeviceClient to send a message to the IoT Central.
        /// </summary>        
        /// <param name="message">JSON string representing serialized device data.</param>
        /// <returns>Task for async execution.</returns>
        private async Task SendEvent(string message, CancellationToken cancellationToken)
        {
            using (var eventMessage = new Message(Encoding.ASCII.GetBytes(message)))
            {
                await _deviceClient.SendEventAsync(eventMessage, cancellationToken).ConfigureAwait(false);

                // Keep track of messages sent and update progress periodically.
                var currCount = Interlocked.Increment(ref _messagesSent);
                if (currCount % 50 == 0)
                {
                    Console.WriteLine($"Device: {DeviceId} Message count: {currCount}");
                }
            }
        }

        private Task<MethodResponse> TogglePowerCommandReceived(MethodRequest methodRequest, object userContext)
        {
            var desiredState = false;
            if(methodRequest.DataAsJson != "null"){
                desiredState = Convert.ToBoolean(methodRequest.DataAsJson);
            }

            if(desiredState && _pumpPowerState == Generator.PumpPowerState.ON ||
                !desiredState && _pumpPowerState == Generator.PumpPowerState.OFF) {
                //no action necessary [on requesting on or off requesting off]
                 Console.WriteLine($"Device: {DeviceId} Commanded by the Cloud to Toggle Power, already in the desired state: {_pumpPowerState}");
                return Task.FromResult(new MethodResponse(new byte[0], 200));
            }

            if(_pumpPowerState == Generator.PumpPowerState.ON)
            {
                CancelCurrentRun();
                ResetDevice();
                _pumpPowerState = Generator.PumpPowerState.OFF;
            }
            else
            {
                _pumpPowerState = Generator.PumpPowerState.ON;
            }

            OnPumpPowerStateChanged(new PumpPowerStateChangedArgs() { DeviceId = DeviceId, PumpPowerState = _pumpPowerState });
            
            SendEvent(JsonConvert.SerializeObject(new { PowerState = _pumpPowerState }), _localCancellationSource.Token).ConfigureAwait(false);
            Console.WriteLine($"Device: {DeviceId} Commanded by the Cloud to Toggle Power to desired state {desiredState}, Power is now {_pumpPowerState}");
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        protected virtual void OnPumpPowerStateChanged(PumpPowerStateChangedArgs e)
        {
            PumpPowerStateChanged?.Invoke(this, e);
        }

    }
}

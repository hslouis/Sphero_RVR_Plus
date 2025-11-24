using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace Sphero_RVR_Plus_CS.Core
{
    /// <summary>
    /// BLE connection handler for Sphero RVR+ using Windows BLE GATT APIs
    /// </summary>
    public class RvrBleConnection
    {
        private static readonly Guid RVR_BLE_SERVICE_UUID = Guid.Parse("00010001-574f-4f20-5370-6865726f2121");
        private static readonly Guid RVR_BLE_CHARACTERISTIC_UUID = Guid.Parse("00010002-574f-4f20-5370-6865726f2121");
        private static readonly Guid RVR_BLE_CHARACTERISTIC_NOTIFY_UUID = Guid.Parse("00010003-574f-4f20-5370-6865726f2121");

        private readonly string _deviceName;
        private BluetoothLEDevice? _device;
        private GattDeviceService? _service;
        private GattCharacteristic? _characteristic;
        private GattCharacteristic? _notifyCharacteristic;
        private bool _connected;
        private readonly bool _preferIndications;

        public event Action<byte[]>? DataReceived;

        // Public properties to expose characteristics for diagnostic tests
        public GattCharacteristic? CmdCharacteristic => _characteristic;
        public GattCharacteristic? NotifyCharacteristic => _notifyCharacteristic;

        public RvrBleConnection(string deviceName, bool preferIndications = false)
        {
            _deviceName = deviceName;
            _preferIndications = preferIndications;
        }

		/// <summary>
		/// Attempt to connect to the RVR+ over BLE using an optimized fast path
		/// </summary>
		/// <returns></returns>
		public async Task<bool> ConnectAsync()
		{
			const int SCAN_TIMEOUT_SECONDS = 14;   // au lieu de 20
			const int GATT_RETRIES = 3;           // au lieu de 3
			const int SHORT_DELAY_MS = 300;       // au lieu de 300–500

			try
			{
				Trace.WriteLine($"🔍 Fast scan for BLE device: {_deviceName}");

				// 1) Enum rapide des devices déjà connus par Windows
				var selector = BluetoothLEDevice.GetDeviceSelector();
				var devices = await DeviceInformation.FindAllAsync(selector);

				var rvrInfo = devices?.FirstOrDefault(d =>
					!string.IsNullOrWhiteSpace(d.Name) &&
					(d.Name.Contains(_deviceName, StringComparison.OrdinalIgnoreCase) ||
					 d.Name.Contains("rvr", StringComparison.OrdinalIgnoreCase) ||
					 d.Name.Contains("sphero", StringComparison.OrdinalIgnoreCase)));

				if (rvrInfo == null)
				{
					// 2) Pub scan plus court
					Trace.WriteLine($"⚠️ Not found via enumeration. Trying advertisement scan ({SCAN_TIMEOUT_SECONDS}s)...");
					_device = await ScanWithWatcherAsync(_deviceName, TimeSpan.FromSeconds(SCAN_TIMEOUT_SECONDS)); 
				}
				else
				{
					Trace.WriteLine($"🔗 Connecting to {rvrInfo.Name} (enumeration)...");
					_device = await BluetoothLEDevice.FromIdAsync(rvrInfo.Id);
				}

				if (_device == null)
				{
					Trace.WriteLine("❌ Could not acquire BLE device instance");
					Trace.WriteLine("💡 Ensure RVR+ is on, in BLE pairing mode (LEDs flashing), and in range.");
					return false;
				}

				_device.ConnectionStatusChanged += (s, e) =>
				{
					if (_device != null)
						Trace.WriteLine($"🔌 Connection status: {_device.ConnectionStatus}");
				};

				// Petit délai, mais plus court
				await Task.Delay(100);

				// =======================
				//   SERVICES (FAST PATH)
				// =======================
				GattDeviceServicesResult? svcResult = null;

				// 1) Directement par UUID en mode Cached (souvent suffisant)
				var svcByUuidCached = await _device.GetGattServicesForUuidAsync(
					RVR_BLE_SERVICE_UUID, BluetoothCacheMode.Cached);
				Trace.WriteLine($"🔎 GetGattServicesForUuid (cached) => {svcByUuidCached.Status}, count={svcByUuidCached.Services?.Count ?? 0}");

				if (svcByUuidCached.Status == GattCommunicationStatus.Success &&
					svcByUuidCached.Services?.Count > 0)
				{
					_service = svcByUuidCached.Services[0];
				}
				else
				{
					// 2) Quelques essais en Uncached sur le UUID spécifique
					for (int attempt = 1; attempt <= GATT_RETRIES && _service == null; attempt++)
					{
						var svcByUuid = await _device.GetGattServicesForUuidAsync(
							RVR_BLE_SERVICE_UUID, BluetoothCacheMode.Uncached);
						Trace.WriteLine($"🔎 GetGattServicesForUuid (uncached) attempt {attempt} => {svcByUuid.Status}, count={svcByUuid.Services?.Count ?? 0}");

						if (svcByUuid.Status == GattCommunicationStatus.Success &&
							svcByUuid.Services?.Count > 0)
						{
							_service = svcByUuid.Services[0];
							break;
						}

						await Task.Delay(SHORT_DELAY_MS);
					}

					// 3) Fallback : une seule énumération complète
					if (_service == null)
					{
						svcResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
						Trace.WriteLine($"🔎 GetGattServices(all) => {svcResult.Status}, count={svcResult.Services?.Count ?? 0}");

						if (svcResult.Status == GattCommunicationStatus.Success &&
							svcResult.Services?.Count > 0)
						{
							_service = svcResult.Services.FirstOrDefault(s => s.Uuid == RVR_BLE_SERVICE_UUID)
									   ?? svcResult.Services[0];
						}
					}
				}

				if (_service == null)
				{
					Trace.WriteLine("❌ RVR+ BLE service not found");
					return false;
				}
				Trace.WriteLine("🔧 RVR+ service found");

				// ==========================
				//   CHARACTERISTICS (FAST)
				// ==========================
				_characteristic = null;
				_notifyCharacteristic = null;

				// 1) Control characteristic : UUID spécifique, cached d'abord
				GattCharacteristicsResult? charResult =
					await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_UUID, BluetoothCacheMode.Cached);
				Trace.WriteLine($"🔎 GetCharacteristics(ctrl cached) => {charResult.Status}, count={charResult.Characteristics?.Count ?? 0}");

				if (charResult.Status == GattCommunicationStatus.Success &&
					charResult.Characteristics?.Count > 0)
				{
					_characteristic = charResult.Characteristics[0];
				}
				else
				{
					// 2) Uncached avec 1-2 retries
					for (int attempt = 1; attempt <= GATT_RETRIES && _characteristic == null; attempt++)
					{
						charResult = await _service.GetCharacteristicsForUuidAsync(
							RVR_BLE_CHARACTERISTIC_UUID, BluetoothCacheMode.Uncached);
						Trace.WriteLine($"🔎 GetCharacteristics(ctrl uncached) attempt {attempt} => {charResult.Status}, count={charResult.Characteristics?.Count ?? 0}");

						if (charResult.Status == GattCommunicationStatus.Success &&
							charResult.Characteristics?.Count > 0)
						{
							_characteristic = charResult.Characteristics[0];
							break;
						}

						await Task.Delay(SHORT_DELAY_MS);
					}

					// 3) Fallback : une seule énumération complète
					if (_characteristic == null)
					{
						var allChars = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
						Trace.WriteLine($"🔎 GetCharacteristics(all) => {allChars.Status}, count={allChars.Characteristics?.Count ?? 0}");

						if (allChars.Status == GattCommunicationStatus.Success &&
							allChars.Characteristics?.Count > 0)
						{
							_characteristic = allChars.Characteristics.FirstOrDefault(c => c.Uuid == RVR_BLE_CHARACTERISTIC_UUID)
											  ?? allChars.Characteristics.FirstOrDefault();
						}
					}
				}

				if (_characteristic == null)
				{
					Trace.WriteLine("❌ RVR+ control characteristic not found");
					return false;
				}
				Trace.WriteLine("⚙️ Control characteristic found");

				// 4) Notify characteristic, même logique (un peu simplifiée)
				var charNotifyResult =
					await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_NOTIFY_UUID, BluetoothCacheMode.Cached);
				Trace.WriteLine($"🔎 GetCharacteristics(notify cached) => {charNotifyResult.Status}, count={charNotifyResult.Characteristics?.Count ?? 0}");

				if (charNotifyResult.Status == GattCommunicationStatus.Success &&
					charNotifyResult.Characteristics?.Count > 0)
				{
					_notifyCharacteristic = charNotifyResult.Characteristics[0];
				}
				else
				{
					for (int attempt = 1; attempt <= GATT_RETRIES && _notifyCharacteristic == null; attempt++)
					{
						var notifUncached = await _service.GetCharacteristicsForUuidAsync(
							RVR_BLE_CHARACTERISTIC_NOTIFY_UUID, BluetoothCacheMode.Uncached);
						Trace.WriteLine($"🔎 GetCharacteristics(notify uncached) attempt {attempt} => {notifUncached.Status}, count={notifUncached.Characteristics?.Count ?? 0}");

						if (notifUncached.Status == GattCommunicationStatus.Success &&
							notifUncached.Characteristics?.Count > 0)
						{
							_notifyCharacteristic = notifUncached.Characteristics[0];
							break;
						}

						await Task.Delay(SHORT_DELAY_MS);
					}
				}

				if (_notifyCharacteristic != null)
				{
					Trace.WriteLine("🔔 Notify characteristic discovered");
				}

				// ======================
				//   NOTIFICATIONS CCCD
				// ======================
				bool notifySet = false;

				// Notify char privilégiée
				if (_notifyCharacteristic != null &&
					(_notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
					 _notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)))
				{
					_notifyCharacteristic.ValueChanged += OnCharacteristicValueChanged;

					var cccd = (_preferIndications && _notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
						? GattClientCharacteristicConfigurationDescriptorValue.Indicate
						: GattClientCharacteristicConfigurationDescriptorValue.Notify;

					var cccdStatus = await _notifyCharacteristic
						.WriteClientCharacteristicConfigurationDescriptorAsync(cccd);
					Trace.WriteLine($"📡 Notifications (notify char) {(cccdStatus == GattCommunicationStatus.Success ? "enabled" : "failed")} ({cccdStatus})");
					notifySet = cccdStatus == GattCommunicationStatus.Success;
				}

				// En plus sur la char de commande si dispo
				if (_characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
					_characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
				{
					_characteristic.ValueChanged += OnCharacteristicValueChanged;

					var cccd = (_preferIndications && _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
						? GattClientCharacteristicConfigurationDescriptorValue.Indicate
						: GattClientCharacteristicConfigurationDescriptorValue.Notify;

					var cccdStatus = await _characteristic
						.WriteClientCharacteristicConfigurationDescriptorAsync(cccd);
					Trace.WriteLine($"📡 Notifications (cmd char) {(cccdStatus == GattCommunicationStatus.Success ? "enabled" : "failed")} ({cccdStatus})");
				}

				_connected = true;
				Trace.WriteLine("✅ Connected to RVR+ over BLE GATT (fast path)");
				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"❌ BLE connection error: {ex.Message}");
				return false;
			}
		}


		private async Task<BluetoothLEDevice?> ScanWithWatcherAsync(string name, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<ulong>();
            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            bool NameMatches(string? localName)
            {
                if (string.IsNullOrWhiteSpace(localName)) return false;
                return localName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                       localName.Contains("rvr", StringComparison.OrdinalIgnoreCase) ||
                       localName.Contains("sphero", StringComparison.OrdinalIgnoreCase);
            }

            watcher.Received += (s, e) =>
            {
                try
                {
                    var localName = e.Advertisement?.LocalName;
                    if (NameMatches(localName))
                    {
                        Trace.WriteLine($"📡 Found via adv: {localName} @ {e.BluetoothAddress:X}");
                        tcs.TrySetResult(e.BluetoothAddress);
                    }
                }
                catch { }
            };

            Trace.WriteLine("▶️ Starting BLE advertisement scan...");
            watcher.Start();
            var cts = new System.Threading.CancellationTokenSource(timeout);
            var reg = cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            try
            {
                ulong address = await tcs.Task;
                Trace.WriteLine("⏹️ Stopped scan, attempting connect by address...");
                return await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            }
            catch (TaskCanceledException)
            {
                Trace.WriteLine("⏹️ Scan timed out with no matching device.");
                return null;
            }
            finally
            {
                try { reg.Dispose(); } catch { }
                try { cts.Dispose(); } catch { }
                try { watcher.Stop(); } catch { }
            }
        }

        private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var buffer = args.CharacteristicValue;
            byte[] data;
            using (var reader = DataReader.FromBuffer(buffer))
            {
                data = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(data);
            }
            try
            {
                var src = sender?.Uuid.ToString() ?? "<unknown>";

            }
            catch
            {
                Trace.WriteLine($"🎉 BLE DATA RECEIVED! Length={data.Length} Data=📥 {BitConverter.ToString(data).Replace("-", "")}");
            }
            DataReceived?.Invoke(data);
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_characteristic != null)
                {
                    await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                    try { _characteristic.ValueChanged -= OnCharacteristicValueChanged; } catch { }
                }
                if (_notifyCharacteristic != null)
                {
                    await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                    try { _notifyCharacteristic.ValueChanged -= OnCharacteristicValueChanged; } catch { }
                }
            }
            catch { }

            _service?.Dispose();
            _device?.Dispose();
            _service = null;
            _device = null;
            _characteristic = null;
            _notifyCharacteristic = null;
            _connected = false;
            Trace.WriteLine("🔌 Disconnected from RVR+");
        }

        public async Task<bool> SendCommandAsync(byte[] data)
        {
            if (!_connected || _characteristic == null)
            {
                Trace.WriteLine("❌ Not connected to RVR+");
                return false;
            }

            try
            {
                Trace.WriteLine($"📤 WRITE: {BitConverter.ToString(data).Replace("-", "")}");
                // Prefer WriteWithoutResponse first
                using (var writerWo = new DataWriter())
                {
                    writerWo.WriteBytes(data);
                    var statusWo = await _characteristic.WriteValueAsync(writerWo.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
                    Trace.WriteLine($"✉️ WriteWithoutResponse => {statusWo}");
                    if (statusWo == GattCommunicationStatus.Success)
                    {
                        await Task.Delay(10); // throttle a bit more
                        return true;
                    }
                }
                // Fallback to WriteWithResponse for diagnostics
                using (var writerWr = new DataWriter())
                {
                    writerWr.WriteBytes(data);
                    var result = await _characteristic.WriteValueWithResultAsync(writerWr.DetachBuffer(), GattWriteOption.WriteWithResponse);
                    Trace.WriteLine($"↪️ Fallback WriteWithResponse => {result.Status} (PE={result.ProtocolError})");
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        await Task.Delay(25);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Send error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Diagnostic: attempt to send a command via the notify characteristic (if writable).
        /// Some firmwares route writes through the other characteristic.
        /// </summary>
        public async Task<bool> SendCommandViaNotifyAsync(byte[] data)
        {
            if (!_connected || _notifyCharacteristic == null)
            {
                Trace.WriteLine("❌ Notify characteristic not available for write");
                return false;
            }
            try
            {
                Trace.WriteLine($"📤 WRITE (notify char): {BitConverter.ToString(data).Replace("-", "")}");
                using (var writerWo = new DataWriter())
                {
                    writerWo.WriteBytes(data);
                    var statusWo = await _notifyCharacteristic.WriteValueAsync(writerWo.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
                    Trace.WriteLine($"✉️ WriteWithoutResponse (notify) => {statusWo}");
                    if (statusWo == GattCommunicationStatus.Success)
                    {
                        await Task.Delay(15);
                        return true;
                    }
                }
                using (var writerWr = new DataWriter())
                {
                    writerWr.WriteBytes(data);
                    var result = await _notifyCharacteristic.WriteValueWithResultAsync(writerWr.DetachBuffer(), GattWriteOption.WriteWithResponse);
                    Trace.WriteLine($"↪️ Fallback WriteWithResponse (notify) => {result.Status} (PE={result.ProtocolError})");
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        await Task.Delay(15);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Send (notify) error: {ex.Message}");
                return false;
            }
        }
    }
}

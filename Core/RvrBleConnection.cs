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

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Trace.WriteLine($"🔍 Scanning for BLE device: {_deviceName}");

                // Enumerate BLE devices
                var selector = BluetoothLEDevice.GetDeviceSelector();
                var devices = await DeviceInformation.FindAllAsync(selector);
                int deviceCount = devices?.Count ?? 0;
                Trace.WriteLine($"Found {deviceCount} BLE devices:");
                if (deviceCount > 0)
                {
                    foreach (var d in (IEnumerable<DeviceInformation>?)devices ?? Array.Empty<DeviceInformation>())
                    {
                        Trace.WriteLine($"  - {d?.Name} ({d?.Id})");
                    }
                }

                // Match by name (contains)
                var rvrInfo = devices?.FirstOrDefault(d =>
                    !string.IsNullOrWhiteSpace(d.Name) &&
                    (d.Name.Contains(_deviceName, StringComparison.OrdinalIgnoreCase) ||
                     d.Name.Contains("rvr", StringComparison.OrdinalIgnoreCase) ||
                     d.Name.Contains("sphero", StringComparison.OrdinalIgnoreCase)));

                if (rvrInfo == null)
                {
                    Trace.WriteLine($"⚠️ Not found via enumeration. Trying advertisement scan (20s)...");
                    _device = await ScanWithWatcherAsync(_deviceName, TimeSpan.FromSeconds(20));
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

                // Give a brief moment for GATT to become available
                await Task.Delay(500);

                // Get service
                // Warm-up: enumerate all services uncached, with a few retries
                GattDeviceServicesResult? allSvcResult = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    allSvcResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    var count = allSvcResult?.Services?.Count ?? 0;
                    Trace.WriteLine($"🔎 GetGattServices attempt {attempt} => {allSvcResult?.Status}, count={count}");
                    if (allSvcResult?.Status == GattCommunicationStatus.Success && count > 0) break;
                    await Task.Delay(600);
                }

                if (allSvcResult == null || allSvcResult.Status != GattCommunicationStatus.Success)
                {
                    Trace.WriteLine($"⚠️ Full service enumeration failed ({allSvcResult?.Status}). Trying UUID-specific query...");
                }

                // Pick service by UUID from the full list first
                _service = allSvcResult?.Services?.FirstOrDefault(s => s.Uuid == RVR_BLE_SERVICE_UUID);

                if (_service == null)
                {
                    // Fallback: direct UUID query with retries
                    GattDeviceServicesResult? svcByUuid = null;
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        svcByUuid = await _device.GetGattServicesForUuidAsync(RVR_BLE_SERVICE_UUID, BluetoothCacheMode.Uncached);
                        var count = svcByUuid?.Services?.Count ?? 0;
                        Trace.WriteLine($"🔎 GetGattServicesForUuid attempt {attempt} => {svcByUuid?.Status}, count={count}");
                        if (svcByUuid?.Status == GattCommunicationStatus.Success && count > 0)
                        {
                            _service = svcByUuid!.Services![0];
                            break;
                        }
                        await Task.Delay(600);
                    }
                }

                if (_service == null)
                {
                    Trace.WriteLine($"❌ RVR+ BLE service not found");
                    return false;
                }
                Trace.WriteLine("🔧 RVR+ service found");

                // Get characteristic (UUID-specific first, then fallback to enumerate all)
                GattCharacteristicsResult? charResult = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    charResult = await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_UUID, BluetoothCacheMode.Uncached);
                    var count = charResult?.Characteristics?.Count ?? 0;
                    Trace.WriteLine($"🔎 GetCharacteristics attempt {attempt} => {charResult?.Status}, count={count}");
                    if (charResult?.Status == GattCommunicationStatus.Success && count > 0)
                    {
                        _characteristic = charResult!.Characteristics![0];
                        break;
                    }
                    await Task.Delay(500);
                }
                // Try to obtain the notify characteristic explicitly as well
                GattCharacteristicsResult? charNotifyResult = null;
                for (int attempt = 1; attempt <= 3 && _notifyCharacteristic == null; attempt++)
                {
                    charNotifyResult = await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_NOTIFY_UUID, BluetoothCacheMode.Uncached);
                    var count = charNotifyResult?.Characteristics?.Count ?? 0;
                    Trace.WriteLine($"🔎 GetCharacteristics(notify) attempt {attempt} => {charNotifyResult?.Status}, count={count}");
                    if (charNotifyResult?.Status == GattCommunicationStatus.Success && count > 0)
                    {
                        _notifyCharacteristic = charNotifyResult!.Characteristics![0];
                        break;
                    }
                    await Task.Delay(500);
                }
                if (_characteristic == null)
                {
                    Trace.WriteLine("↪️ Fallback: enumerate all characteristics");
                    for (int attempt = 1; attempt <= 3 && _characteristic == null; attempt++)
                    {
                        var allChars = await _service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        var count = allChars?.Characteristics?.Count ?? 0;
                        Trace.WriteLine($"🔎 GetCharacteristics(all) attempt {attempt} => {allChars?.Status}, count={count}");
                        if (allChars?.Status == GattCommunicationStatus.Success && count > 0)
                        {
                            _characteristic = allChars!.Characteristics!.FirstOrDefault(c => c.Uuid == RVR_BLE_CHARACTERISTIC_UUID)
                                              ?? allChars!.Characteristics!.FirstOrDefault();
                            _notifyCharacteristic ??= allChars!.Characteristics!.FirstOrDefault(c => c.Uuid == RVR_BLE_CHARACTERISTIC_NOTIFY_UUID);
                            if (_characteristic != null) break;
                        }
                        await Task.Delay(500);
                    }
                }
                if (_characteristic == null)
                {
                    // Last ditch: try cached mode
                    var cached = await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_UUID, BluetoothCacheMode.Cached);
                    var count = cached?.Characteristics?.Count ?? 0;
                    Trace.WriteLine($"🔎 GetCharacteristics (cached) => {cached?.Status}, count={count}");
                    if (cached?.Status == GattCommunicationStatus.Success && count > 0)
                    {
                        _characteristic = cached!.Characteristics![0];
                    }
                }
                if (_characteristic == null)
                {
                    Trace.WriteLine($"❌ RVR+ control characteristic not found");
                    return false;
                }
                Trace.WriteLine("⚙️ Control characteristic found");
                try
                {
                    Trace.WriteLine($"   UUID: {_characteristic.Uuid}");
                    Trace.WriteLine($"   Props: {_characteristic.CharacteristicProperties}");
                }
                catch { }

                if (_notifyCharacteristic == null)
                {
                    // Try cached for notify characteristic as a last resort
                    var cachedNotify = await _service.GetCharacteristicsForUuidAsync(RVR_BLE_CHARACTERISTIC_NOTIFY_UUID, BluetoothCacheMode.Cached);
                    var ncount = cachedNotify?.Characteristics?.Count ?? 0;
                    Trace.WriteLine($"🔎 GetCharacteristics(notify cached) => {cachedNotify?.Status}, count={ncount}");
                    if (cachedNotify?.Status == GattCommunicationStatus.Success && ncount > 0)
                    {
                        _notifyCharacteristic = cachedNotify!.Characteristics![0];
                    }
                }
                if (_notifyCharacteristic != null)
                {
                    Trace.WriteLine("🔔 Notify characteristic discovered");
                    try
                    {
                        Trace.WriteLine($"   UUID: {_notifyCharacteristic.Uuid}");
                        Trace.WriteLine($"   Props: {_notifyCharacteristic.CharacteristicProperties}");
                    }
                    catch { }
                }

                // Prefer notifications on the dedicated notify characteristic.
                bool notifySet = false;
                if (_notifyCharacteristic != null &&
                    (_notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                     _notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate)))
                {
                    _notifyCharacteristic.ValueChanged += OnCharacteristicValueChanged;
                    var cccd = (_preferIndications && _notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                        ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                        : (_notifyCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
                            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                            : GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                    var cccdStatus = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccd);
                    Trace.WriteLine($"📡 Notifications (notify char) {(cccdStatus == GattCommunicationStatus.Success ? "enabled" : "failed")} ({cccdStatus})");
                    notifySet = cccdStatus == GattCommunicationStatus.Success;
                    try
                    {
                        var readCccd = await _notifyCharacteristic.ReadClientCharacteristicConfigurationDescriptorAsync();
                        Trace.WriteLine($"🔎 CCCD (notify char) now: {readCccd.Status} - {readCccd.ClientCharacteristicConfigurationDescriptor}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Could not read CCCD (notify char): {ex.Message}");
                    }
                    // Prime a read to nudge pipeline (some stacks deliver a first value here)
                    try
                    {
                        var read = await _notifyCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                        var len = (read?.Value?.Length ?? 0);
                        Trace.WriteLine($"🧪 Priming read (notify char): {read?.Status} len={len}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Priming read (notify char) failed: {ex.Message}");
                    }
                }

                // Also enable notifications on the command characteristic (responses observed here on some firmwares)
                if (_characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                    _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                {
                    _characteristic.ValueChanged += OnCharacteristicValueChanged;
                    var cccd = (_preferIndications && _characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                        ? GattClientCharacteristicConfigurationDescriptorValue.Indicate
                        : (_characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
                            ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                            : GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                    var cccdStatus = await _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccd);
                    Trace.WriteLine($"📡 Notifications (cmd char) {(cccdStatus == GattCommunicationStatus.Success ? "enabled" : "failed")} ({cccdStatus})");
                    try
                    {
                        var readCccd = await _characteristic.ReadClientCharacteristicConfigurationDescriptorAsync();
                        Trace.WriteLine($"🔎 CCCD (cmd char) now: {readCccd.Status} - {readCccd.ClientCharacteristicConfigurationDescriptor}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Could not read CCCD (cmd char): {ex.Message}");
                    }
                    // Prime a read
                    try
                    {
                        var read = await _characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                        var len = (read?.Value?.Length ?? 0);
                        Trace.WriteLine($"🧪 Priming read (cmd char): {read?.Status} len={len}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"⚠️ Priming read (cmd char) failed: {ex.Message}");
                    }
                }

                _connected = true;
                Trace.WriteLine("✅ Connected to RVR+ over BLE GATT");
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

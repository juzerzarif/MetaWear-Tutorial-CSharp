using System;
using System.Collections.Generic;
using System.Text;
using Windows.Devices.Bluetooth;

namespace MbientLab.BtleDeviceScanner {
    interface IScanConfig {
        int Duration { get; }
        Action<BluetoothLEDevice> SelectedDevice { get; }
        List<Guid> ServiceUuids { get; }
    }
}

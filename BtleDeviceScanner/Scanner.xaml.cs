using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace MbientLab.BtleDeviceScanner {
    public sealed class MacAddressHexString : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, string language) {
            string hexString = ((ulong)value).ToString("X");
            return hexString.Insert(2, ":").Insert(5, ":").Insert(8, ":").Insert(11, ":").Insert(14, ":");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }

    public sealed class ConnectionStateColor : IValueConverter {
        public SolidColorBrush ConnectedColor { get; set; }
        public SolidColorBrush DisconnectedColor { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language) {
            switch ((BluetoothConnectionStatus)value) {
                case BluetoothConnectionStatus.Connected:
                    return ConnectedColor;
                case BluetoothConnectionStatus.Disconnected:
                    return DisconnectedColor;
                default:
                    throw new MissingMemberException("Unrecognized connection status: " + value.ToString());
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Scanner : Page {
        private BluetoothLEAdvertisementWatcher btleWatcher;
        private HashSet<ulong> seenDevices = new HashSet<ulong>();
        private IScanConfig config;

        public Scanner() {
            InitializeComponent();

            btleWatcher = new BluetoothLEAdvertisementWatcher {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            btleWatcher.Received += async (w, btAdv) => {
                if (!seenDevices.Contains(btAdv.BluetoothAddress) && 
                        config.ServiceUuids.Aggregate(true, (acc, e) => acc & btAdv.Advertisement.ServiceUuids.Contains(e))) {
                    seenDevices.Add(btAdv.BluetoothAddress);
                    var device = await BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress);
                    if (device != null) {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => pairedDevices.Items.Add(device));
                    }
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            config = e.Parameter as IScanConfig;
            refreshDevices_Click(null, null);
        }

        /// <summary>
        /// Callback for the refresh button which populates the devices list
        /// </summary>
        private void refreshDevices_Click(object sender, RoutedEventArgs args) {
            var connected = pairedDevices.Items.Where(e => (e as BluetoothLEDevice).ConnectionStatus == BluetoothConnectionStatus.Connected);

            seenDevices.Clear();
            pairedDevices.Items.Clear();

            foreach (var it in connected) {
                seenDevices.Add((it as BluetoothLEDevice).BluetoothAddress);
                pairedDevices.Items.Add(it);
            }

            btleWatcher.Start();
            new Timer(e => btleWatcher.Stop(), null, config.Duration, Timeout.Infinite);
        }

        /// <summary>
        /// Callback for the devices list which navigates to the <see cref="DeviceSetup"/> page with the selected device
        /// </summary>
        private void pairedDevices_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            btleWatcher.Stop();
            var item = ((ListView)sender).SelectedItem as BluetoothLEDevice;

            if (item != null) {
                config.SelectedDevice(item);
            }
        }
    }
}

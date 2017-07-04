using MbientLab.BtleDeviceScanner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using MbientLab.MetaWear;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace StarterApp {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page, IScanConfig {
        public MainPage() {
            InitializeComponent();
        }

        public int Duration => 10000;

        public Action<BluetoothLEDevice> SelectedDevice => async (item) => {
            ContentDialog initPopup = new ContentDialog() {
                Title = "Initializing API",
                Content = "Please wait while the app initializes the API"
            };

            initPopup.ShowAsync();
            var board = MbientLab.MetaWear.Win10.Application.GetMetaWearBoard(item);
            await board.InitializeAsync();
            initPopup.Hide();

            Frame.Navigate(typeof(DeviceSetup), item);
        };

        public List<Guid> ServiceUuids => new List<Guid>(new Guid[] { Constants.METAWEAR_GATT_SERVICE });

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            if (Frame.BackStack.Count == 0) {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Frame.Navigate(typeof(Scanner), this));
            }
        }
    }
}

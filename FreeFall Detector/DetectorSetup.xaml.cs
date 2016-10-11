using MbientLab.MetaWear.Core;
using MbientLab.MetaWear.Processor;
using static MbientLab.MetaWear.Functions;
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
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FreeFall_Detector {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DetectorSetup : Page {
        private LogDownloadHandler logDlHandler;
        private Dictionary<String, Fn_IntPtr> dataprocDelegates;
        private Fn_IntPtr_Int initDelegate;
        private Fn_IntPtr[] logReadyDelegates;
        private BluetoothLEDevice selectedDevice;
        private BtleConnection btleConn;
        private IntPtr board;
        private Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic notifyChar;

        public DetectorSetup() {
            this.InitializeComponent();

            btleConn = new BtleConnection();
            btleConn.writeGattChar = new Fn_IntPtr_IntPtr_ByteArray(writeCharacteristic);
            btleConn.readGattChar = new Fn_IntPtr_IntPtr(readCharacteristic);

            initDelegate = new Fn_IntPtr_Int(initialized);

            dataprocDelegates = new Dictionary<string, Fn_IntPtr>();

            logReadyDelegates = new Fn_IntPtr[2];
        }

        private void initialized(IntPtr board, int status) {
            System.Diagnostics.Debug.WriteLine("Board Initialized!");

            mbl_mw_metawearboard_set_time_for_response(board, 500);
            mbl_mw_acc_set_odr(board, 50f);
            mbl_mw_acc_write_acceleration_config(board);
            
            var acc_signal = mbl_mw_acc_get_acceleration_data_signal(board);

            dataprocDelegates["ff"] = new Fn_IntPtr(ff => {
                dataprocDelegates["ff_handler"] = new Fn_IntPtr(dataPtr => {
                    System.Diagnostics.Debug.WriteLine("In FreeFall");
                });
                logReadyDelegates[0] = new Fn_IntPtr(logger => {
                    mbl_mw_logger_subscribe(logger, dataprocDelegates["ff_handler"]);
                    System.Diagnostics.Debug.WriteLine("Free Fall logger ready");
                    System.Diagnostics.Debug.WriteLine("Processor Setup Complete");
                });
                mbl_mw_datasignal_log(ff, logReadyDelegates[0]);
            });
            dataprocDelegates["no_ff"] = new Fn_IntPtr(noFF => {
                dataprocDelegates["no_ff_handler"] = new Fn_IntPtr(dataPtr => {
                    System.Diagnostics.Debug.WriteLine("Not in FreeFall");
                });
                logReadyDelegates[1] = new Fn_IntPtr(logger => {
                    mbl_mw_logger_subscribe(logger, dataprocDelegates["no_ff_handler"]);
                    System.Diagnostics.Debug.WriteLine("No Free Fall logger ready");
                });
                mbl_mw_datasignal_log(noFF, logReadyDelegates[1]);
            });
            dataprocDelegates["threshold"] = new Fn_IntPtr(ths => {
                mbl_mw_dataprocessor_comparator_create(ths, Comparator.Operation.EQ, 1, dataprocDelegates["no_ff"]);
                mbl_mw_dataprocessor_comparator_create(ths, Comparator.Operation.EQ, -1, dataprocDelegates["ff"]);
            });
            dataprocDelegates["avg"] = new Fn_IntPtr(avg => {
                mbl_mw_dataprocessor_threshold_create(avg, Threshold.Mode.BINARY, 0.5f, 0,
                    dataprocDelegates["threshold"]);
            });
            dataprocDelegates["rss"] = new Fn_IntPtr(rss => {
                mbl_mw_dataprocessor_average_create(rss, 4, dataprocDelegates["avg"]);
            });
            //whoops, wrong fn name
            mbl_mw_dataprocessor_rss_create(acc_signal, dataprocDelegates["rss"]);
        }

        private async void writeCharacteristic(IntPtr caller, IntPtr charPtr, IntPtr value, byte length) {
            byte[] managedArray = new byte[length];
            Marshal.Copy(value, managedArray, 0, length);

            var charGuid = Marshal.PtrToStructure<MbientLab.MetaWear.Core.GattCharacteristic>(charPtr).toGattCharGuid();
            var status = await selectedDevice.GetGattService(charGuid.serviceGuid).GetCharacteristics(charGuid.guid).FirstOrDefault()
                .WriteValueAsync(managedArray.AsBuffer(), GattWriteOption.WriteWithoutResponse);

            if (status != GattCommunicationStatus.Success) {
                System.Diagnostics.Debug.WriteLine("Error writing gatt characteristic");
            }
        }

        private async void readCharacteristic(IntPtr caller, IntPtr charPtr) {
            var charGuid = Marshal.PtrToStructure<MbientLab.MetaWear.Core.GattCharacteristic>(charPtr).toGattCharGuid();
            var result = await selectedDevice.GetGattService(charGuid.serviceGuid).GetCharacteristics(charGuid.guid).FirstOrDefault()
                .ReadValueAsync();

            if (result.Status == GattCommunicationStatus.Success) {
                mbl_mw_connection_char_read(board, charPtr, result.Value.ToArray(), (byte)result.Value.Length);
            } else {
                System.Diagnostics.Debug.WriteLine("Error reading gatt characteristic");
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            base.OnNavigatedTo(e);

            selectedDevice = e.Parameter as BluetoothLEDevice;
            notifyChar = selectedDevice.GetGattService(GattCharGuid.METAWEAR_NOTIFY_CHAR.serviceGuid).GetCharacteristics(GattCharGuid.METAWEAR_NOTIFY_CHAR.guid).FirstOrDefault();
            await notifyChar.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            notifyChar.ValueChanged += new TypedEventHandler<Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic, GattValueChangedEventArgs>(
                (Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic sender, GattValueChangedEventArgs obj) => {
                    byte[] response = obj.CharacteristicValue.ToArray();
                    mbl_mw_connection_notify_char_changed(board, response, (byte)response.Length);
                });

            board = mbl_mw_metawearboard_create(ref btleConn);
            mbl_mw_metawearboard_initialize(board, initDelegate);
        }

        private void start_Click(object sender, RoutedEventArgs e) {
            mbl_mw_logging_start(board, 0);
            mbl_mw_acc_enable_acceleration_sampling(board);
            mbl_mw_acc_start(board);
        }

        private void stop_Click(object sender, RoutedEventArgs e) {
            mbl_mw_acc_stop(board);
            mbl_mw_acc_disable_acceleration_sampling(board);
            mbl_mw_logging_stop(board);
        }

        private void download_Click(object sender, RoutedEventArgs e) {
            logDlHandler = new LogDownloadHandler();
            logDlHandler.receivedProgressUpdate = new Fn_Uint_Uint((uint nEntriesLeft, uint totalEntries) => {
                if (nEntriesLeft == 0) {
                    System.Diagnostics.Debug.WriteLine("Log Download Completed!");
                }
            });
            logDlHandler.receivedUnknownEntry = new Fn_Ubyte_Long_ByteArray((byte id, long epoch, IntPtr value, byte length) => {
                System.Diagnostics.Debug.WriteLine("Received unknown log data: id= " + id);
            });

            mbl_mw_logging_download(board, 0, ref logDlHandler);
        }

        private void back_Click(object sender, RoutedEventArgs e) {
            mbl_mw_metawearboard_tear_down(board);
            mbl_mw_metawearboard_free(board);

            this.Frame.Navigate(typeof(MainPage));
        }
    }
}

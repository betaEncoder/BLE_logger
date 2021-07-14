using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BLE_logger
{
    class Program
    {
        static BluetoothLEAdvertisementWatcher watcher;

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Option("-n|--name", Description = "target name")]
        public String Name { get; } = "";

        [Option("-s|--service", Description = "target service")]
        public Guid Service { get; } = Guid.Empty;

        [Option("-c|--characteristic", Description = "target characteristic to log")]
        public Guid Characteristic { get; } = Guid.Empty;

        [Option("-i|--interval", Description = "read interval")]
        public int Interval { get; } = 0;

        [Option("-l|--listen", Description = "listen advertise")]
        public bool Listen { get; } = false;

        [Option("-o|--output", Description = "filename")]
        public string Filename { get; } = "log.txt";

        [Option("-f|--format", Description = "output format;x|d")]
        public string Format { get; } = "x";

        private DeviceWatcher DeviceWatcher { get; set; }
        //private BluetoothLEDevice Device { get; set; }
        private DeviceInformation DevInfo;
        private GattDeviceServicesResult services;
        private GattCharacteristicsResult characteristics;
        private GattCharacteristic CharacteristictoLog;

        private void OnExecute()
        {
            string selector;
            if (Listen)
            {
                listen_advertise();
                return;
            }

            if (Service == Guid.Empty)
            {
                _ = CommandLineApplication.Execute<HelpOptionAttribute>();
                return;
            }else{
                selector = "(" + GattDeviceService.GetDeviceSelectorFromUuid(Service) + ")";
                DeviceWatcher = DeviceInformation.CreateWatcher(selector);
            }
            DeviceWatcher.Added += Watcher_DeviceAdded;
            DeviceWatcher.Stopped += connect;
            DeviceWatcher.Start();

            // Ctrl-C割り込みのイベントを受け取る
            Console.CancelKeyPress += Console_CancelKeyPress;

            while (true)
            {
                ;
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl+C pressed");
            Environment.Exit(0);
        }


        private async void Watcher_DeviceAdded(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            if (Name=="" || deviceInfo.Name == Name)
            {
                // デバイス情報を保存
                DevInfo = deviceInfo;

                // デバイス情報更新時のハンドラを解除しウォッチャーをストップ
                DeviceWatcher.Added -= Watcher_DeviceAdded;
                DeviceWatcher.Stop();
                
            }
        }

        private async void connect(DeviceWatcher sender, object args)
        {
            BluetoothLEDevice Device = await BluetoothLEDevice.FromIdAsync(DevInfo.Id);
            services = await Device.GetGattServicesForUuidAsync(Service);
            characteristics = await services.Services[0].GetCharacteristicsForUuidAsync(Characteristic);
            if(characteristics.Status!= GattCommunicationStatus.Success)
            {
                // access failed
                Console.WriteLine("Characteristic access failed.");
                Environment.Exit(-1);
            }
            CharacteristictoLog = characteristics.Characteristics[0];
            if (Interval == 0)
            {
                // enable notify
                var status = await CharacteristictoLog.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                CharacteristictoLog.ValueChanged += notify;
            }
            else
            {
                // set timer to read
            }
        }

        private void notify(GattCharacteristic characteristic, GattValueChangedEventArgs arg)
        {
            var reader = DataReader.FromBuffer(arg.CharacteristicValue);
            byte[] buffer = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(buffer);
            StreamWriter sw = new StreamWriter(Filename, true, System.Text.Encoding.UTF8);
            DateTime dt = DateTime.Now;
            string line;
            line = dt.ToString("MM/dd/yyyy hh:mm:ss.fff");
            if (Format == "x")
            {
                foreach (byte b in buffer)
                {
                    line += ", " + b.ToString("x");
                }
            }else if(Format == "i")
            {
                int tmp = 0;
                tmp |= (buffer[3]<<24) + (buffer[2]<<16) + (buffer[1]<<8) + buffer[0];
                line += ", "+tmp.ToString();
            }
            Console.WriteLine(line);
            sw.WriteLine(line);
            sw.Close();
        }

        private static void listen_advertise()
        {
            Console.WriteLine("Listening for advertise");
            watcher = new BluetoothLEAdvertisementWatcher();
            watcher.Received += Watcher_Received;
            watcher.ScanningMode = BluetoothLEScanningMode.Passive;
            watcher.Start();
            Thread.Sleep(10000);
            watcher.Stop();
        }

        private static void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var bleServiceUUIDs = args.Advertisement.ServiceUuids;

            Console.WriteLine("MAC:" + args.BluetoothAddress.ToString());
            Console.WriteLine("NAME:" + args.Advertisement.LocalName.ToString());
            Console.WriteLine("ServiceUuid");
            foreach (var uuidone in bleServiceUUIDs)
            {
                Console.WriteLine(uuidone.ToString());
            }
            Console.WriteLine("");
        }
    }
}

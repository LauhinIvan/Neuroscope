using HidLibrary;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Нейроскоп
{
    internal class Program : IDisposable
    {
        private const int DefaultVendorId = 0x049A;
        private const int DefaultProductId = 0x0005;
        private const int DefaultReportLength = 8;

        private readonly int _vendorId;
        private readonly int _productId;
        private readonly int _reportLength;

        private HidDevice _device;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<bool> _readCompleteTcs;

        public Program(int vendorId = DefaultVendorId, int productId = DefaultProductId, int reportLength = DefaultReportLength)
        {
            _vendorId = vendorId;
            _productId = productId;
            _reportLength = reportLength;
        }

        public static async Task Main(string[] args)
        {
            using var program = new Program();
            await program.RunAsync();
        }

        public async Task RunAsync()
        {
            try
            {
                _device = HidDevices.Enumerate(_vendorId, _productId).FirstOrDefault();
                if (_device == null)
                {
                    Console.WriteLine($"No device connected (VendorId=0x{_vendorId:X4}, ProductId=0x{_productId:X4})");
                    return;
                }

                Console.WriteLine("Device found.");

                _device.OpenDevice();

                if (!_device.IsOpen)
                {
                    Console.WriteLine("Failed to open device.");
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();

                Console.CancelKeyPress += (s, e) =>
                {
                    Console.WriteLine("Stopping...");
                    _cancellationTokenSource.Cancel();
                    e.Cancel = true;
                };

                await ReadReportsLoopAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }

        private async Task ReadReportsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _readCompleteTcs = new TaskCompletionSource<bool>();

                try
                {
                    _device.ReadReport(ReadReportCallback);

                    await _readCompleteTcs.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during reading report: {ex.Message}");
                }
            }
        }

        private void ReadReportCallback(HidReport report)
        {
            if (report == null || report.Data == null || report.Data.Length == 0)
            {
                Console.WriteLine("Received empty or invalid report.");
            }
            else
            {
                var dataToPrint = report.Data.Take(_reportLength);
                Console.WriteLine("Received: {0}", string.Join(", ", dataToPrint.Select(b => b.ToString("X2"))));
            }

            _readCompleteTcs?.SetResult(true);
        }

        public void Dispose()
        {
            if (_device != null)
            {
                if (_device.IsOpen)
                    _device.CloseDevice();
                _device.Dispose();
                _device = null;
            }

            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }
}
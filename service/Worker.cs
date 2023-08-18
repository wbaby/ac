using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Runtime.CompilerServices;
using service.Types;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8600
#pragma warning disable CS8603

namespace service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private NamedPipeServerStream _pipeServer;
        private byte[] _buffer;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _buffer = new byte[1024];
            _pipeServer = new NamedPipeServerStream("DonnaACPipe", PipeDirection.InOut, 1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Windows service starting, waiting for client to connect");

            _pipeServer.WaitForConnection();

            _logger.LogInformation("Client connected to the pipe server");

            while (!stoppingToken.IsCancellationRequested)
            { 
                try
                {
                    if (_pipeServer.Read(_buffer, 0, 1024) > 0)
                    {
                        _logger.LogInformation("Report received, decoding buffer");
                        await TranslatePipeBuffer();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Reading buffer from pipe failed with message: {0}", ex.Message);
                }
            }
        }

        private async Task TranslatePipeBuffer()
        {
            int reportCode = BitConverter.ToInt32(_buffer, 0);

            _logger.LogInformation("Report received with code: {0}", reportCode);

            switch (reportCode)
            {
                case 10:
                    var packet = BytesToStructure<MODULE_VERIFICATION_CHECKSUM_FAILURE>();

                    unsafe
                    {
                        _logger.LogInformation("Report code: {0}, Base address: {1}, Size: {2}, Name: ",
                            packet.ReportCode,
                            packet.ModuleBaseAddress,
                            packet.ModuleSize);
                    }

                    goto end;

                default:
                    _logger.LogError("Invalid report code received");
                    goto end;

            }
        end:
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        private T BytesToStructure<T>()
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(_buffer, 0, ptr, size);
                return (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8600
#pragma warning restore CS8603
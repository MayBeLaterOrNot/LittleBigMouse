﻿using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using H.Pipes;
using LittleBigMouse.Zoning;

namespace LittleBigMouse.DisplayLayout
{
    public class LittleBigMouseClientService : ILittleBigMouseClientService
    {
        public event EventHandler<LittleBigMouseServiceEventArgs> StateChanged;
        //private PipeClient<DaemonMessage> _client;
        NamedPipeClientStream _client;

        protected void OnStateChanged(LittleBigMouseState state)
        {
            StateChanged?.Invoke(this, new (state));
        }

        public LittleBigMouseClientService()
        {
        }

        public async void Start() => await SendAsync();

        public async Task StartAsync(ZonesLayout layout)
        {
            await SendMessageWithStartAsync(new DaemonMessage(LittleBigMouseCommand.Load, layout));
            await SendMessageWithStartAsync(new DaemonMessage(LittleBigMouseCommand.Run));
        }

        public async Task StopAsync() => await SendAsync();

        public async Task QuitAsync() => await SendAsync();

        public async Task LoadAtStartupAsync(bool state = true) => await SendAsync();

        public async Task CommandLineAsync(IList<string> args) => await SendAsync();

        public async Task RunningAsync() => await SendAsync();

        readonly SemaphoreSlim _startingSemaphore = new SemaphoreSlim(1, 1);


        async Task<bool> StartDaemonAsync()
        {
            //await StopDaemon();

            await _startingSemaphore.WaitAsync();
            try
            {
                if (_client != null)
                {
                    if (_client.IsConnected) return true;
                    _client = null;
                }
                /*
                var args = Debugger.IsAttached?"debug":"";
                var module = Process.GetCurrentProcess().MainModule;

                var filename = module?.FileName;
                if (filename == null) return false;

                filename = filename.Replace(".Control.exe", ".Daemon.exe").Replace(".vshost", "");

                _daemonProcess = new Process
                {
                    StartInfo = new ProcessStartInfo(filename)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                        Arguments = args,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                    }
                };
                if (_daemonProcess == null) return false;
                if (!_daemonProcess.Responding) return false;
                if (_daemonProcess.HasExited) return false;
                */

                //_client = new PipeClient<DaemonMessage>("lbm-daemon-beta");

                _client = new NamedPipeClientStream(".", "lbm-daemon-beta", PipeDirection.InOut);

                await _client.ConnectAsync();

                new Thread(() =>
                {
                    while (true)
                    {
                        _client.ReadByte();
                    }

                }).Start();

                //_client.MessageReceived += (sender, args) => OnStateChanged(args.Message.State);

                //_client.Disconnected += (sender, args) => {};

                return true;
            }
            finally
            {
                _startingSemaphore.Release();
            }
        }

        async Task StopDaemon()
        {
            await SendMessageWithStartAsync(new DaemonMessage(LittleBigMouseCommand.Stop,null));
        }

        void _daemonProcess_Exited(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }


        Task SendAsync([CallerMemberName]string name = null)
        {
            if(name==null) throw new ArgumentNullException(nameof(name));
            if (name.EndsWith("Async")) name = name[..^5];
            return Enum.TryParse<LittleBigMouseCommand>(name, out var command) ? SendMessageWithStartAsync(new DaemonMessage(command,null)) : Task.CompletedTask;
        }

        async Task SendMessageWithStartAsync(DaemonMessage message)
        {
            if (await StartDaemonAsync())
            {
                var retry = true;
                while (retry)
                {
                    try
                    {
                        await SendMessageAsync(message);
                        return;
                    }
                    catch (TimeoutException)
                    {
                        retry = await StartDaemonAsync();
                    }

                }
            }
        }

        async Task SendMessageAsync(DaemonMessage message)
        {
            //var serializer = new DataContractSerializer(
            //    typeof(DaemonMessage),
            //    new DataContractSerializerSettings() { 
            //        PreserveObjectReferences = true
            //        ,
            //        }
            //    );
/*            
            var serializer = new XmlSerializer(typeof(DaemonMessage));
            var serializer = new JsonSerializer();

            //var ms = new MemoryStream();

            //var xsn = new XmlSerializerNamespaces();
            //xsn.Add(string.Empty, string.Empty);

            var ms = new TextWriter();

            serializer.Serialize(ms, message);

            //serializer.WriteObject(ms, message);

            using var sr = new StreamReader(ms);

            ms.Position = 0;

            var xml = await sr.ReadToEndAsync();  */

            var xml = message.Serialize();

            byte[] messageBytes = Encoding.UTF8.GetBytes(xml);

            await _client.WriteAsync(messageBytes); //.StandardInput.WriteAsync(xml);
        }


    }

}
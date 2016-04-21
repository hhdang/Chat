using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using DirectiveServer.libs.Directives;
using DirectiveServer.libs.Enums;

namespace DirectiveServer.libs.Helper
{
    public sealed class DirectiveWorker
    {
        private DirectiveWorker() { }

        public static DirectiveWorker Instance { get; } = new DirectiveWorker();

        public delegate void SerialPortEventHandler(SerialPortEventArgs args);
        public event SerialPortEventHandler SerialPortEvent;

        void OnSerialPortEvent(SerialPortEventArgs args)
        {
            SerialPortEvent?.Invoke(args);
        }

        private ConcurrentQueue<BaseDirective> cq = new ConcurrentQueue<BaseDirective>();
        private ConcurrentQueue<byte[]> resultCQ = new ConcurrentQueue<byte[]>();
        private IProtocol protocolProvider = ProtocolFactory.Create(ProtocolVersion.V485_1);
        private SocketHelper spHelper = new SocketHelper();
        private List<byte> AllReceivers = new List<byte>();
        private List<byte> AllSenders = new List<byte>(); 


        private bool IsBusy { get; set; }
        private bool hasData = false;
        private static object _locker = new object();

        public void PrepareDirective(BaseDirective item)
        {
            Debug.WriteLine($"待处理指令数量->{cq.Count}");
            cq.Enqueue(item);
            
            lock (_locker)
            {
                if (!IsBusy)
                {
                    IsBusy = true;

                    Task.Run(async () =>
                    {
                        await DispatchDirective();
                    });
                }
            }

            if (!hasData)
            {
                hasData = true;

                Task.Run (async () => {
                    await ReceiveDirective();
                });

                Task.Run(async () => {
                    await ProcessDirective();
                });
            }
        }

        private async Task DispatchDirective()
        {
            BaseDirective item;

            while (cq.TryDequeue(out item))
            {

                try
                {
                    var directiveData = protocolProvider.GenerateDirectiveBuffer(item);
                    Debug.WriteLine($" 压入指令 ->{Common.BytesToString(directiveData.ToArray())}<- 压入指令end");
                    await spHelper.Open();
                    await spHelper.Send(directiveData);
                    AllSenders.AddRange(directiveData);

                    await Task.Delay(5);
                }
                catch (Exception ex)
                {
                    OnSerialPortEvent(new SerialPortEventArgs
                    {
                        SourceDirective = item,
                        IsSucceed = false,
                        Result = null,
                        Message = ex.Message,
                        Command = new byte[0] {}
                    });

                    continue;
                }
            }
            
            lock (_locker)
            {
                IsBusy = false;
            }
        }

        private async Task ReceiveDirective()
        {
            while (true)
            {
                var data = await spHelper.Receive();
                AllReceivers.AddRange(data.ToArray());
                spHelper.ClearBuffer();
                Debug.WriteLine($" 接受指令 ->{Common.BytesToString(data.ToArray())}<- 接受指令end");

                resultCQ.Enqueue(data.ToArray());
            }
        }

        private async Task ProcessDirective()
        {
            await Task.Run(async () =>
            {
                var dirtyData = new List<byte>();

                while (true)
                {
                   // Debug.WriteLine($"dirtyData length -> {dirtyData.Count} | {AllSenders}");
                    byte[] data;
                    if (resultCQ.TryDequeue(out data))
                    {
                        if (!parseResultAndNotify(data).Status)
                        {   
                            dirtyData.AddRange(data);

                            if (dirtyData.Count > 2)
                            {
                                var len = getDataLength(dirtyData[1]);
                                if (dirtyData.Count >= len)
                                {
                                    if (parseResultAndNotify(dirtyData.GetRange(0, len).ToArray()).Status)
                                    {
                                        dirtyData.RemoveRange(0, len);
                                    }
                                }
                            }
                        }
                    }
                    else
                        await Task.Delay(10);
                }
            });

        }

        private int getDataLength(byte t) {
            var directiveType = (DirectiveTypeEnum)t;
            switch (directiveType)
            {
                case DirectiveTypeEnum.Idle:
                    return 6;

                case DirectiveTypeEnum.TryStart:
                    return 9;

                case DirectiveTypeEnum.TryPause:
                    return 4;

                case DirectiveTypeEnum.Close:
                    return 4;

                case DirectiveTypeEnum.Running:
                    return 10;

                case DirectiveTypeEnum.Pausing:
                    return 9;

                default:
                    return 0;
            }
        }

        private DirectiveResult parseResultAndNotify(byte[] b) { 
            var recvData = protocolProvider.ResolveDirectiveResult(b);

            if (null == recvData)
            {
                Debug.WriteLine(".....recvData.....");
                return new DirectiveResult() { Status = false };
            }

            if (recvData.Status)
                OnSerialPortEvent(new SerialPortEventArgs
                {
                    IsSucceed = true,
                    Result = recvData,
                    Command = b
                });

            return recvData;
        }

        public void CleanTask()
        {
            spHelper.Cancel();
            spHelper.Close();
        }

        private string BytesToString(IEnumerable<byte> bytes)
        {
            var ret = "";

            bytes.ToList().ForEach(t =>
            {
                ret += (Convert.ToString(t, 16).PadLeft(2, '0') + ",");
            });

            return ret;
        }

    }

    public static class Extensions
    {
        public static void ForAll<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence)
            {
                action(item);
            }
        }
    }

    public class SerialPortEventArgs : EventArgs
    {
        public BaseDirective SourceDirective { get; set; }
        public bool IsSucceed { get; set; }
        public DirectiveResult Result { get; set; }
        public string Message { get; set; }
        public byte[] Command { get; set; }
    }


}

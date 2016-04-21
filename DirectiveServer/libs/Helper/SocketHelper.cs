using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DirectiveServer.libs.Helper
{
    public class SocketHelper :IDisposable
    {
        public string address = "192.168.1.124";
        public int port = 8080;
        public Socket client = null;
        private ConcurrentQueue<byte[]> resultCQ = new ConcurrentQueue<byte[]>();
        CancellationTokenSource readCancellationTokenSource;

        private bool isConnect => null != client && client.Connected;


        public void Dispose()
        {
            client?.Shutdown(SocketShutdown.Both);
            client?.Dispose();
        }

        public void ClearBuffer()
        {
            if (resultCQ != null) resultCQ = new ConcurrentQueue<byte[]>();
        }

        public void Cancel()
        {
            if (readCancellationTokenSource != null)
            {
                if (!readCancellationTokenSource.IsCancellationRequested)
                {
                    readCancellationTokenSource.Cancel();
                }
            }
        }

        public void Close()
        {
            client?.Shutdown(SocketShutdown.Both);
            client?.Dispose();
        }

        public async Task Open()
        {
            if (isConnect) return;

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var ip = IPAddress.Parse(address);
            var endPoint = new IPEndPoint(ip, port);

            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = endPoint;

            var receciveArg = new SocketAsyncEventArgs();

            args.Completed += (obj, e) =>
            {
                var sendbuffers = new byte[1024];
                receciveArg.SetBuffer(sendbuffers, 0, 1024);
                receciveArg.Completed += Rececive_Completed;
                client.ReceiveAsync(receciveArg);
            };

            readCancellationTokenSource = new CancellationTokenSource();

            client.ConnectAsync(args);

            await Task.Run(async () =>
            {
                while (!isConnect)
                {
                    await Task.Delay(5);
                }
            });
        }

        public async Task<byte[]> Receive()
        {
            byte[] data = null;
            do
            {
                await Task.Delay(5);
            }
            while (resultCQ.Count == 0);
            resultCQ.TryDequeue(out data);
            return data;
        }

        private void Rececive_Completed(object sender, SocketAsyncEventArgs e)
        {
            var _client = sender as Socket;
            if (e.SocketError == SocketError.Success)
            {
                if (e.BytesTransferred > 0)
                {
                    byte[] data = new byte[e.BytesTransferred];

                    for (var i = 0; i < e.BytesTransferred; i++)
                        data[i] = e.Buffer[i];
                    resultCQ.Enqueue(data);
                    Array.Clear(e.Buffer, 0, e.Buffer.Length);
                }
            }

            _client?.ReceiveAsync(e);
        }

        public async Task Send(byte[] data)
        {
            await Task.Delay(0);
            if (client == null || client.Connected == false)
            {
                Debug.WriteLine("未连接到服务器");
                return;
            }
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(data, 0, data.Length);
            args.Completed += (obj, e) =>
            {
            };

            client.SendAsync(args);
        }

    }
}

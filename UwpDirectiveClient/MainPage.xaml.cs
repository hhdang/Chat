using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace UwpDirectiveClient
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>

    public partial class MainPage : Page
    {
        public string address = "192.168.1.124";
        public int port = 8080;
        public Socket client = null;

        private bool isConnect => null != client && client.Connected;


        public MainPage()
        {
            InitializeComponent();
            txtAddr.Text = address;
            txtPort.Text = port.ToString();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnect)
            {
                Debug.WriteLine("客户端未连接到服务器");
                return;
            }

            Send(
                txtSend.Text.Split(new char[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(ex => Convert2Byte(ex.Trim()))
                    .ToArray());

        }

        public void Open()
        {
            if (isConnect) return;

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var ip = IPAddress.Parse(address);
            var endPoint = new IPEndPoint(ip, port);

            var args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = endPoint;
            args.Completed += (obj, e) =>
            {
                RenderMsg("连接服务器成功");
                var receciveArg = new SocketAsyncEventArgs();

                var sendbuffers = new byte[1024];
                receciveArg.SetBuffer(sendbuffers, 0, 1024);
                receciveArg.Completed += Rececive_Completed;

                client.ReceiveAsync(receciveArg);
            };

            client.ConnectAsync(args);
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            Open();
        }

        private static string BytesToString(byte[] bytes)
        {
            var temp = "";
            bytes.ToList().ForEach((t) =>
            {
                temp += "0X" + Convert.ToString(t, 16).PadLeft(2, '0') + " ";
            });

            return temp;
        }

        public async void RenderMsg(string msg, Brush color = null)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (null == color)
                    color = new SolidColorBrush(Colors.Blue);
                var tb = new TextBlock {Text = msg, Foreground = color};
                spMsg.Children.Add(tb);
            });
        }

        public void Send(byte[] data)
        {
            if (client == null || client.Connected == false)
            {
                Debug.WriteLine("未连接到服务器");
                return;
            }
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(data, 0, data.Length);
            args.Completed += Args_Completed;
            client.SendAsync(args);
        }

        private void Args_Completed(object sender, SocketAsyncEventArgs e)
        {
            Debug.WriteLine("send success");
        }

        private static byte Convert2Byte(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;
            str = str.Trim();
            if (str.IndexOf("0x", StringComparison.Ordinal) == 0 || str.IndexOf("0X", StringComparison.Ordinal) == 0)
            {
                return Convert.ToByte(str, 16);
            }

            return Convert.ToByte(str);

        }

        private static byte[] GenerateCheckCode(byte[] data)
        {
            return GenerateCheckCode(data, (byte) data.Length);
        }

        private static byte[] GenerateCheckCode(byte[] dataBuff, byte dataLen)
        {
            byte CRC16High = 0;
            byte CRC16Low = 0;

            int CRCResult = 0xFFFF;
            for (int i = 0; i < dataLen; i++)
            {
                CRCResult = CRCResult ^ dataBuff[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((CRCResult & 1) == 1)
                        CRCResult = (CRCResult >> 1) ^ 0xA001;
                    else
                        CRCResult >>= 1;
                }
            }
            CRC16High = Convert.ToByte(CRCResult & 0xff);
            CRC16Low = Convert.ToByte(CRCResult >> 8);
            //return ((CRCResult >> 8) + ((CRCResult & 0xff) << 8)); 

            return new byte[] {CRC16High, CRC16Low};
        }



        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.Connected)
            {
                client.Shutdown(SocketShutdown.Both);
                RenderMsg("断开连接");
            }
        }


        void Rececive_Completed(object sender, SocketAsyncEventArgs e)
        {
            var _client = sender as Socket;
            Debug.WriteLine(e.BytesTransferred);
            if (e.SocketError == SocketError.Success)
            {
                if (e.BytesTransferred > 0)
                {
                    byte[] data = new byte[e.BytesTransferred];

                    for (var i = 0; i < e.BytesTransferred; i++)
                        data[i] = e.Buffer[i];

                    RenderMsg(BytesToString(data));

                    Array.Clear(e.Buffer, 0, e.Buffer.Length);
                    _client?.ReceiveAsync(e);

                    
                }

            }
        }

    }
}

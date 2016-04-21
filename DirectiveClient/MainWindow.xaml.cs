using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DirectiveClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string address = "192.168.1.124";
        public int port = 8080;
        public string separator = "{|}";
        public bool isConnect = false;
        public Socket client = null;
        public string userName = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            txtAddr.Text = address;
            txtPort.Text = port.ToString();

        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!isConnect)
            {
                MessageBox.Show("客户端未连接到服务器");
                return;
            }

//            var bytes = txtSend.Text.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(ex => Convert2Byte(ex.Trim())).ToArray();
//            var x = GenerateCheckCode(bytes);

            SendToServer(txtSend.Text.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(ex => Convert2Byte(ex.Trim())).ToArray());

        }

        public Socket ConnectServer()
        {
            if (isConnect) return client;


            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress ip = IPAddress.Parse(address);
            IPEndPoint endPoint = new IPEndPoint(ip, port);

            client.Connect(endPoint);

            isConnect = true;
            Debug.Write("succccccc");

            return client;
        }

        public void AddMsg(string msg)
        {
            if (msg.IndexOf(separator, StringComparison.Ordinal) == -1)
            {
                Debug.WriteLine(msg + "-----------");
                return;
            }
            string[] info = msg.Split(separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string head = info[0];
            string content = info[1];
            switch (head)
            {
                case "CONNECT":
                    RenderMsg(content + "加入聊天室", Brushes.Gray);
                    break;
                case "PRIVATE":
                    RenderMsg(content, Brushes.Blue);
                    break;
                case "CHAT":
                    RenderMsg(content, Brushes.Black);
                    break;
                case "EXIT":
                    RenderMsg(content, Brushes.Gray);
                    break;
                case "LIST":
                    //AddUser(content);
                    break;
                case "QUIT":
                    break;
                default:
                    break;
            }

        }

        public void AddMsg(byte[] data)
        {
            if(data.Length <= 2) return;

            var msg = BytesToString(data);
            RenderMsg(msg, Brushes.Blue);
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            ConnectServer();

            RenderMsg("连接成功", Brushes.Blue);

            //持续接受服务端的数据
            new Thread(() => {

                var bytes = new List<byte>();

                while (isConnect)
                {
                    if (client == null || !client.Connected) break;
                    byte[] buffer = new byte[256];
                    int length = client.Receive(buffer);
                    for (var i = 0; i < length; i++)
                    {
                        bytes.Add(buffer[i]);
                    }


                    if (client.Available == 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddMsg(bytes.ToArray());
                        });

                        bytes.Clear();
                    }
                }
            }).Start();
        }

        private string BytesToString(byte[] bytes)
        {
            var temp = "";
            bytes.ToList().ForEach((t) =>
            {
                temp += "0X" + Convert.ToString(t, 16).PadLeft(2, '0') + " ";
            });

            return temp;
        }

        public void RenderMsg(string msg, Brush color = null )
        {
            TextBlock tb = new TextBlock { Text = msg, Foreground = color };
            spMsg.Children.Add(tb);
        }

        public void SendToServer(byte[] data)
        {
            if (client == null || client.Connected == false)
            {
                MessageBox.Show("未连接到服务器");
                return;
            }

            client.Send(data);
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
            return GenerateCheckCode(data, (byte)data.Length);
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

            return new byte[] { CRC16High, CRC16Low };
        }

        

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.Connected)
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
                isConnect = false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (client != null && client.Connected)
            {
                isConnect = false;
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

    }

    public enum MsgType
    {
        CONNECT = 1,
        PRIVATE,
        CHAT,
        EXIT,
        LIST,
        QUIT
    }
}

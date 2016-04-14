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

namespace AppClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string address = "192.168.1.124";
        public int port = 8008;
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
                MessageBox.Show("请登陆后在发言");
                return;
            }


            string msg = userName + "说:" + txtSend.Text;

            SendToServer(MsgType.CHAT, msg);

        }

        public Socket ConnectServer()
        {
            if (isConnect) return client;
            

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress ip = IPAddress.Parse(address);
            IPEndPoint endPoint = new IPEndPoint(ip, port);

            client.Connect(endPoint);

            isConnect = true;

            return client;
        }

        public void AddMsg(string msg)
        {
            if (msg.IndexOf(separator) == -1)
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

        public void RenderMsg(string msg, Brush color)
        {
            TextBlock tb = new TextBlock { Text = msg, Foreground = color };
            spMsg.Children.Add(tb);
        }

        public void SendToServer(MsgType type, string msg)
        {
            if (client == null || client.Connected == false)
            {
                MessageBox.Show("未连接到服务器");
                return;
            }

            string msgType = Enum.GetName(type.GetType(), type);
            string tmp = msgType + separator + msg;
            byte[] data = Encoding.Default.GetBytes(msgType + separator + msg);

            client.Send(data);
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("请输入用户名");
                return;
            }
            userName = txtName.Text;
            ConnectServer();
            SendToServer(MsgType.CONNECT, txtName.Text);

            //持续接受服务端的数据
            new Thread(() => {
                string str = "";

                while (isConnect)
                {
                    if (client == null || !client.Connected) break;
                    byte[] buffer = new byte[256];
                    int length = client.Receive(buffer);
                    str += Encoding.Default.GetString(buffer, 0, length);


                    if (client.Available == 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Debug.WriteLine(str);
                            AddMsg(str);
                        });

                        str = "";
                    }
                }
            }).Start();

            
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.Connected)
            {
                SendToServer(MsgType.EXIT, userName + "离开聊天室");
                isConnect = false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (client != null && client.Connected)
            {
                SendToServer(MsgType.EXIT, userName + "离开聊天室");
                isConnect = false;
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

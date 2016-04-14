using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Diagnostics;

namespace AppServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string separator = "{|}";
        public Socket server = null;
        public string address = "192.168.1.124";
        public int port = 8008;
        public bool isStart = false;
        byte[] data = new byte[256]; 
        public Hashtable clients = new Hashtable();

        public MainWindow()
        {
            InitializeComponent();
            txtPort.Text = port.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtPort.Text))
            {
                MessageBox.Show("端口不能为空");
                return;
            }
            StartUp();
            Dispatcher.Invoke(() => {
                RenderMsg("服务已开启...", Brushes.Gray);
            });

            //开启新线程 避免阻塞主线程
            new Thread(() =>
            {
                while (true)
                {
                    Socket socket = server.Accept();
                    Debug.WriteLine("客户端连接........");

                    string tmp = "";

                    //接受一个新的客户端 开启新线程
                    new Thread(() =>
                    {
                        //持续接受客户端信息
                        while (true)
                        {
                            if (!socket.Connected) break;
                            int length = socket.Receive(data);
                            tmp += Encoding.Default.GetString(data, 0, length);

                            //判断是否还有数据流需要接受
                            if (socket.Available == 0)
                            {
                                Debug.WriteLine(tmp);
                                Dispatcher.Invoke(() =>
                                {
                                    AddMsg(tmp, socket);
                                    Boardcast(tmp);
                                });
                                tmp = "";

                            }
                        }

                    }).Start();
                }
            }).Start();

        }

        public void Boardcast(string tmp)
        {
            foreach (DictionaryEntry item in clients)
            {
                Socket socket = (Socket)item.Key;
                socket.Send(Encoding.Default.GetBytes(tmp));
            }
        }


        public Socket StartUp()
        {
            if (isStart) return server;

            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress ip = IPAddress.Parse(address);
            IPEndPoint endPoint = new IPEndPoint(ip, port);
           
            server.Bind(endPoint);
            server.Listen(10);
            isStart = true;

            return server;
        }

        public void AddMsg(string msg, Socket socket)
        {
            string[] info = msg.Split(separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string head = info[0];
            string content = info[1];
            switch (head)
            {
                case "CONNECT":
                    clients.Add(socket, content);
                    RenderMsg(content + "加入聊天室", Brushes.Gray);
                    break;
                case "PRIVATE":
                    RenderMsg(content, Brushes.Blue);
                    break;
                case "CHAT":
                    RenderMsg(content, Brushes.Black);
                    break;
                case "EXIT":
                    clients.Remove(socket);
                    RenderMsg(content, Brushes.Gray);
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    break;
                case "LIST":
                    AddUser(content);
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

        public void AddUser(string name)
        {
            TextBlock tb = new TextBlock { Text = name };
            spUsers.Children.Add(tb);
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

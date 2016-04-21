using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using DirectiveServer.libs;
using DirectiveServer.libs.Directives;
using DirectiveServer.libs.Enums;
using DirectiveServer.libs.Helper;

namespace DirectiveServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public Socket server = null;
        private string address = "192.168.1.124";
        private int port = 8080;
        private bool isStart = false;
        private byte[] data = new byte[256];

        private bool isRunning = false;
        private bool isPausing = false;

        private Timer runningTimer = null;
        private Timer pausingTimer = null;

        public MainWindow()
        {
            InitializeComponent();
            txtPort.Text = port.ToString();
        }

        private void Send(Socket socket, byte[] bytes)
        {
            if(bytes.Length <= 2) return;
            socket.Send(processResolve(bytes));
        }

        private byte[] processResolve(byte[] bytes)
        {
            byte[] xdata = null;
            switch (bytes[1])
            {
                case 0x00:
                {
                    xdata = bytes;
                    isRunning = true;
                    runningTimer = new Timer(new TimerCallback((obj) =>
                    {
                        isRunning = false;
                        runningTimer.Dispose();
                    }), null, 1000*30, 0);
                }
                    break;

                case 0x01:
                {
                        xdata = bytes;
                        isPausing = true;
                    pausingTimer = new Timer(new TimerCallback((obj) =>
                    {
                        isPausing = false;
                        pausingTimer.Dispose();
                    }), null, 1000*30, 0);
                }
                    break;

                case 0x02:
                    xdata = bytes;
                    break;

                case 0x03:
                {
                    xdata = resolveIdleDirective(bytes);
                }
                    break;

                case 0x04:
                {
                    xdata = resolveRunningDirective(bytes);
                }
                    break;

                case 0x05:
                {
                    xdata = resolvePausingDirective(bytes);
                }
                    break;

                default:
                    break;
            }

            return xdata;
        }

        private byte[] GetDirectiveId(byte[] bytes)
        {
            var len = bytes.Length;
            var ret = new byte[2];
            ret[0] = bytes[len - 4];
            ret[1] = bytes[len - 3];

            return ret;
        }

        private byte[] resolveIdleDirective(byte[] bytes)
        {
            var ids = GetDirectiveId(bytes);
            var content = new byte[] { bytes[0], 0x03, 0x00, 0x00, ids[0], ids[1] };
            var checkCode = DirectiveHelper.GenerateCheckCode(content);

            return content.Concat(checkCode).ToArray();
        }

        private byte[] resolveRunningDirective(byte[] bytes)
        {
            var ids = GetDirectiveId(bytes);
            var rate = new byte[] {0x00, 0x00};
            if (isRunning)
            {
                rate = new byte[]{0x00, 0x23,};
            }
            var content = new byte[] { bytes[0], 0x04, 0x00, 0x16, rate[0], rate[1], 0x00, 0x01, ids[0], ids[1] };
            var checkCode = DirectiveHelper.GenerateCheckCode(content);
            return content.Concat(checkCode).ToArray();
        }

        private byte[] resolvePausingDirective(byte[] bytes)
        {
            var ids = GetDirectiveId(bytes);
            var rate = new byte[] { 0x00, 0x00 };
            if (isPausing)
            {
                rate = new byte[] { 0x00, 0x23, };
            }
            var content = new byte[] { bytes[0], 0x05, 0x00, 0x12, rate[0], rate[1], 0x01, ids[0], ids[1] };
            var checkCode = DirectiveHelper.GenerateCheckCode(content);
            return content.Concat(checkCode).ToArray();
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

        public Socket StartUp()
        {
            if (isStart) return server;

            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var ip = IPAddress.Parse(address);
            var endPoint = new IPEndPoint(ip, port);

            server.Bind(endPoint);
            server.Listen(10);
            isStart = true;

            btnOpen.IsEnabled = false;

            return server;
        }

        public void AddMsg(byte[] bytes)
        {
            if (bytes.Length <= 2) return;

            var msg = BytesToString(bytes);
            RenderMsg(msg, Brushes.Blue);
        }

        public void RenderMsg(string msg, Brush color)
        {
            var tb = new TextBlock { Text = msg, Foreground = color };
            spMsg.Items.Add(tb);
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
                    var bytes = new List<byte>();

                    //接受一个新的客户端 开启新线程
                    new Thread(() =>
                    {
                        //持续接受客户端信息
                        while (true)
                        {
                            try
                            {
                                if (!socket.Connected) break;
                                var length = socket.Receive(data);
                                for (var i = 0; i < length; i++)
                                {
                                    bytes.Add(data[i]);
                                }

                                //判断是否还有数据流需要接受
                                if (socket.Available == 0)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        AddMsg(bytes.ToArray());
                                        Send(socket, bytes.ToArray());
                                    });

                                    bytes.Clear();
                                }
                            }
                            catch (Exception)
                            {
                                //ignore
                            }

                        }

                    }).Start();
                }
            }).Start();

        }

        private void Test_OnClick(object sender, RoutedEventArgs e)
        {
            WriteLog("hello world", "info");
            WriteLog("hello world", "warning");
            WriteLog("hello world", "error");
            Action<string> x = (s) =>
            {

            };
        }

        private void WriteLog(string msg, string level)
        {
            using (var file = File.Open("E:/log", FileMode.Append, FileAccess.Write))
            {
                using (var sw = new StreamWriter(file))
                {
                    sw.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} {level}] {msg}");
                }
            }
        }

    }
}

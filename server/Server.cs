using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CCWin;

namespace sendFile
{
    public partial class Server : CCSkinMain
    {
        List<Socket> ClientSocketList = new List<Socket>();
        string picPath = "";
        public string message = "";
        public Server()
        {
            InitializeComponent();
        }
      //启动：服务器端开启侦听
      private void btnStart_Click(object sender, EventArgs e)
      {
          Socket socket = new Socket(
              AddressFamily.InterNetwork,
              SocketType.Stream,
              ProtocolType.Tcp
          );

          socket.Bind(
              new IPEndPoint(
                  IPAddress.Parse(txtIP.Text),
                  int.Parse(txtPort.Text)
              )
          );

          socket.Listen(10);

          ThreadPool.QueueUserWorkItem(
              new WaitCallback(AcceptClientConnect), socket);
        }

        //接受客户端的连接请求
        public void AcceptClientConnect(object socket)
        {
            var serverSocket = socket as Socket;
            this.AppendTextToTxtLog("服务器段开始接收客户端的连接。");
            while (true)
            {
                var proxSocket = serverSocket.Accept();
                this.AppendTextToTxtLog(string.Format("客户端：{0}连接上了", proxSocket.RemoteEndPoint.ToString()));
                ClientSocketList.Add(proxSocket);
                //不停地接收当前连接的客户断发送来的消息
                ThreadPool.QueueUserWorkItem(new WaitCallback(ReceiveData), proxSocket);
            }
        }

        //接受客户端的消息
        public void ReceiveData(object socket)
        {
            var proxSocket = socket as Socket;
            byte[] data = new byte[1024 * 1024];
            while (true)
            {
                int len = 0;
                try
                {
                    len = proxSocket.Receive(data, 0, data.Length, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    AppendTextToTxtLog(string.Format("客户端：{0} 非正常退出", proxSocket.RemoteEndPoint.ToString()));
                    ClientSocketList.Remove(proxSocket);
                    StopConnect(proxSocket);
                    return;
                }
                if (len <= 0)
                {
                    //客户端正常退出
                    AppendTextToTxtLog(string.Format("客户端：{0} 正常退出", proxSocket.RemoteEndPoint.ToString()));
                    ClientSocketList.Remove(proxSocket);
                    StopConnect(proxSocket);
                    return;//让方法结束，终结当前接收客户端数据的异步线程
                }

                int type = data[0];
                if (type == 1)
                {
                    //把接收到的数据放到文本框上去
                    SystemSounds.Beep.Play();
                    string str = Encoding.Default.GetString(data, 0, len);
                    AppendTextToTxtLog(string.Format("接收到客户端：{0}的消息是：{1}", proxSocket.RemoteEndPoint.ToString(), str));
                    message = str;
                }
                else if (type == 2)
                {
                    Flash();
                }
                else if (type == 3)
                {
                    saveFile(data, len);
                }
                else if (type == 4)
                {
                    showImage(data, len);
                }
            }
        }

        public void Flash()
        {
            Point curLocation = this.Location;
            Random r = new Random();
            for (int i = 0; i < 50; i++)
            {
                this.Location = new Point(
                    r.Next(curLocation.X - 10, curLocation.X + 10),
                    r.Next(curLocation.Y - 10, curLocation.Y + 10)
                );

                Thread.Sleep(30);
                this.Location = curLocation;
            }
        }
        public void saveFile(byte[] data, int length)
        {
            using (SaveFileDialog sFile = new SaveFileDialog())
            {
                sFile.Filter = "text file(*.txt)|*.txt|picture(*jpg)|*.jpg|word(*docx)|*docx|all file(*.*)|*.*";
                if (sFile.ShowDialog(this) != DialogResult.OK)
                    return;

                byte[] storedData = new byte[length - 1];
                Buffer.BlockCopy(data, 1, storedData, 0, length - 1);
                File.WriteAllBytes(sFile.FileName, storedData);

            }
        }

        private void StopConnect(Socket proxSocket)
        {
            try
            {
                if (proxSocket.Connected)
                {
                    proxSocket.Shutdown(SocketShutdown.Both);
                    proxSocket.Close(100);
                }
            }
            catch (Exception ex)
            {
                //不需处理断开连接时的异常
            }
        }

        private void showImage(byte[] data, int length)
        {
            byte[] storedData = new byte[length - 1];
            Buffer.BlockCopy(data, 1, storedData, 0, length - 1);
            string str = AppDomain.CurrentDomain.BaseDirectory;
            string rand = Guid.NewGuid().ToString("N");
            string str1 = str + rand;
            File.WriteAllBytes(str1, storedData);
            Bitmap myBitmap = new Bitmap(@str1);
            Graphics g = pictureBox1.CreateGraphics();
            g.DrawImage(myBitmap, 0, 0);
        }

        //往日志的文本框上追加数据
        public void AppendTextToTxtLog(string txt)
        {
            if (txtLog.InvokeRequired)
            {

                txtLog.BeginInvoke(new Action<string>(s => {
                    this.txtLog.Text = string.Format("{0}\r\n{1}", txtLog.Text, s);
                }), txt);
            }
            else
            {
                this.txtLog.Text = string.Format("{0}\r\n{1}", txtLog.Text, txt);
            }

        }
        //发送消息
        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            foreach (Socket socket in ClientSocketList)
            {
                if (socket.Connected)
                {
                    byte[] data1 = Encoding.Default.GetBytes(txtMsg.Text.ToString());
                    txtLog.AppendText(string.Format("\r\nMessage send:{0}", txtMsg.Text));
                    byte[] final = new byte[data1.Length+1];
                    final[0] = 1;
                    Buffer.BlockCopy(data1,0,final,1,data1.Length);
                    socket.Send(final, 0, final.Length, SocketFlags.None);
                }
            }
        }
         //向客户端发送抖动命令
        private void btnFlash_Click(object sender, EventArgs e)
        {
            foreach (Socket socket in ClientSocketList)
            {
                if (socket.Connected)
                {
                    socket.Send(new byte[] {2}, SocketFlags.None);
                }
            }
        }
        //选择一个小文件发送到客户端
        private void btnSendFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog file = new OpenFileDialog())
            {
                if (file.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                byte[] fileData = File.ReadAllBytes(file.FileName);
                byte[] final = new byte[fileData.Length+1];
                final[0] = 3;
                Buffer.BlockCopy(fileData, 0, final, 1, fileData.Length);
                foreach (Socket socket in ClientSocketList)
                {
                    if (socket.Connected)
                    {
                        socket.Send(final, SocketFlags.None);
                    }
                }
            }
        }

        private void sendPic_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog file = new OpenFileDialog())
            {
                file.Filter = "JPEG(*.jpg;*.jpeg)|*.jpg;*.jpeg)";
                if (file.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                byte[] fileData = File.ReadAllBytes(file.FileName);
                byte[] final = new byte[fileData.Length + 1];
                final[0] = 4;
                Buffer.BlockCopy(fileData, 0, final, 1, fileData.Length);
                foreach (Socket socket in ClientSocketList)
                {
                    if (socket.Connected)
                    {
                        socket.Send(final, SocketFlags.None);
                    }
                }
            }
        }

        private void Server_Load(object sender, EventArgs e)
        {

        }

        bool k = true; //一个标记，用于控制图标闪动
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (message.Length > 0) //如果网络中传输了数据
            {
                if (k) //k为true时
                {
                    notifyIcon1.Icon = server.Properties.Resources._1; //托盘图标为1
                    k = false; //设k为false
                }
                else //k为false时
                {
                    notifyIcon1.Icon = server.Properties.Resources._2; //图盘图标为2，透明的图标
                    k = true; //k为true
                }
            }
        }
    }
}

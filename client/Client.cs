using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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

namespace client
{
    public partial class client : CCSkinMain
    {
        public Socket ClientSocket;
        public string message="";
        public client()
        {
            //将得到的dll文件加载到主程序中去
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false; //从不是创建控件的线程访问它
        }

        //将要引入的DLL文件嵌入到资源中去
        System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");
            dllName = dllName.Replace(".", "_");
            if (dllName.EndsWith("_resources")) return null;
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            byte[] bytes = (byte[])rm.GetObject(dllName);
            return System.Reflection.Assembly.Load(bytes);
        }

        //客户端连接服务器端
        private void btnConn_Click(object sender, EventArgs e)
        {
            ClientSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

            try
            {
                ClientSocket.Connect(
                    IPAddress.Parse(txtIP.Text),
                    int.Parse(txtPort.Text)
                );
            }
            catch (Exception exception)
            {
                MessageBox.Show("连接失败,重新连接");
                return;
            }

            Thread thread = new Thread(
                new ParameterizedThreadStart(ReceiveData));
            thread.IsBackground = true;
            thread.Start(ClientSocket);
        }
        public void ReceiveData(object socket)
        {
            var proxySocket = socket as Socket;
            byte[] data = new byte[1024*1024];
            while (true)
            {
                int len = 0;
                try
                {
                    len = proxySocket.Receive(data, 0, data.Length, SocketFlags.None);
                }
                catch (Exception e)
                {
                    AppendTextToTxtLog(string.Format("服务端:{0} 非正常退出",proxySocket.RemoteEndPoint.ToString()));
                    StopConnect();
                    return;
                }

                if (len<=0)
                {
                    AppendTextToTxtLog(string.Format("服务端:{0}正常退出",proxySocket.RemoteEndPoint.ToString()));
                    StopConnect();
                    return;
                }

                int  type = data[0];
                if (type==1)
                {
                    SystemSounds.Beep.Play();
                    string str = Encoding.Default.GetString(data,1,len-1);
                    AppendTextToTxtLog(string.Format("收到服务器端:{0}的消息是:{1}",proxySocket.RemoteEndPoint.ToString(),str));
                    message = str;
                }
                else if (type == 2)
                {
                    Flash();
                } 
                else if (type==3)
                {
                    saveFile(data,len);
                }
                else if (type==4)
                {
                    showImage(data,len);
                }

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
        //往日志文本框上追加数据
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
                this.txtLog.Text = string.Format("{0}\r\n{1}", txt, txtLog.Text);
            }
        }
        private void StopConnect()
        {
            try
            {
                if (ClientSocket.Connected)
                {
                    ClientSocket.Shutdown(SocketShutdown.Both);
                    ClientSocket.Close(100);
                }
            }
            catch (Exception ex)
            {

            }
        }
		
        //发送消息到服务器端
        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            if (ClientSocket.Connected)
            {
                byte[] data1 = Encoding.Default.GetBytes(txtMsg.Text.ToString());
                txtLog.AppendText(string.Format("\r\nMessage send:{0}", txtMsg.Text));
                byte[] final = new byte[data1.Length + 1];
                final[0] = 1;
                Buffer.BlockCopy(data1, 0, final, 1, data1.Length);
                ClientSocket.Send(final, 0, final.Length, SocketFlags.None);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {

        }


        bool k = true; //一个标记，用于控制图标闪动
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (message.Length > 0) //如果网络中传输了数据
            {
                if (k) //k为true时
                {
                    notifyIcon1.Icon = Properties.Resources._1; //托盘图标为1
                    k = false; //设k为false
                }
                else //k为false时
                {
                    notifyIcon1.Icon = Properties.Resources._2; //图盘图标为2，透明的图标
                    k = true; //k为true
                }
            }
        }

        private void client_Load(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void txtIP_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnFlash_Click(object sender, EventArgs e)
        {
            if (ClientSocket.Connected)
            {
                ClientSocket.Send(new byte[] { 2 }, SocketFlags.None);
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
                byte[] final = new byte[fileData.Length + 1];
                final[0] = 3;
                Buffer.BlockCopy(fileData, 0, final, 1, fileData.Length);
                if (ClientSocket.Connected)
                {
                    ClientSocket.Send(final, SocketFlags.None);
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
                if (ClientSocket.Connected)
                {
                    ClientSocket.Send(final, SocketFlags.None);
                }
            }
        }
    }
}

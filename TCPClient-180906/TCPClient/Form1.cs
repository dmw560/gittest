using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace TCPClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        #region 参数
        private IPEndPoint ServerInfo;
        private Socket ClientSocket;
        //信息接收缓存
        private Byte[] MsgBuffer;
        //信息发送存储
        private Byte[] MsgSend;
        //收到消息队列
        Queue<string> receiveQueue = new Queue<string>();
        //定义委托，
        public delegate void ReadDelegate();
        public delegate void RecieveDelegate();
        //申明委托，为读取消息线程
        public ReadDelegate ReadThread;
        public RecieveDelegate RecieveThread;
        //发送计数
        public int token = 1;
        //是否注册
        public bool IsReg = false;
        //设置定时
        System.Timers.Timer timer;
        //实现长连接，定时发送数据
        System.Timers.Timer connectTimer;
        //消息队列
        MyQueue myQueue = new MyQueue();

        EventWaitHandle _waitHandle = new AutoResetEvent(false);

        //数据库连接字符串
        public string connectString = "server=127.0.0.1;database=NYGLPT;user=sa;pwd=password01!";
        //抄表人
        public string readUserName = "";
        public string guid = "";
        #endregion
        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                ServerInfo = new IPEndPoint(IPAddress.Parse(textBox1.Text.Trim()), Convert.ToInt32(textBox2.Text.Trim()));
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //客户端连接服务端指定IP端口，Sockket

                ClientSocket.Connect(ServerInfo);
                //将用户登录信息发送至服务器，由此可以让其他客户端获知
                //ClientSocket.Send(Encoding.ASCII.GetBytes("用户：yangjian  进入系统！\n"));
                //开始从连接的Socket异步读取数据。接收来自服务器，其他客户端转发来的信息
                //AsyncCallback引用在异步操作完成时调用的回调方法
                ClientSocket.BeginReceive(MsgBuffer, 0, MsgBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), null);

                this.textBox3.Text += "登录服务器成功！\r\n";
                this.textBox5.Text += "连接服务器成功！\r\n";
                WriteLog.WriteError("连接服务器成功！");
                this.button3.Enabled = true;
                this.button1.Enabled = false;
                this.button2.Enabled = true;
            }
            catch (Exception ex)
            {
                this.textBox5.Text += ex.Message + "\r";
                WriteLog.WriteError(ex.Message);
                return;
            }
            //注册
            RegisterCS();
        }
        /// <summary>
        /// 判断Socket是否处于连接状态
        /// </summary>
        /// <returns></returns>
        public bool IsConnected() {
            bool blockingState = ClientSocket.Blocking;
            try
            {
                if (ClientSocket != null && ClientSocket.Connected)
                {
                    byte[] tmp = new byte[1];

                    ClientSocket.Blocking = false;
                    ClientSocket.Send(tmp, 0, 0);
                    return true;
                }
                else {
                    return false;
                }
            }
            catch (SocketException e)
            {
                // 10035 == WSAEWOULDBLOCK    
                if (e.NativeErrorCode.Equals(10035))
                    return true;
                else
                {
                    return false;
                }
            }
            finally
            {
                if (ClientSocket.Connected) {
                    ClientSocket.Blocking = blockingState;
                }
            }
        }
        /// <summary>
        /// 读取返回数据
        /// </summary>
        /// <param name="AR"></param>
        private void ReceiveCallBack(IAsyncResult AR)
        {
            try
            {
                //结束挂起的异步读取，返回接收到的字节数。 AR，它存储此异步操作的状态信息以及所有用户定义数据
                int REnd = ClientSocket.EndReceive(AR);
                if (REnd > 0)
                {
                    lock (this.textBox5)
                    {
                        string temp = Encoding.ASCII.GetString(MsgBuffer, 0, REnd);
                        receiveQueue.Enqueue(temp);
                    }
                    ClientSocket.BeginReceive(MsgBuffer, 0, MsgBuffer.Length, 0, new AsyncCallback(ReceiveCallBack), null);
                }
                else {
                    //ClientSocket.Close();
                }
            }
            catch(Exception ex)
            {
                if (!IsConnected()) {
                    Connect();
                }
                WriteLog.WriteError(ex.Message);
            }

        }
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Load(object sender, EventArgs e)
        {
            connectString = GetConnectionStringsConfig("connectString");
            //定义一个IPV4，TCP模式的Socket
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            MsgBuffer = new Byte[65535];
            MsgSend = new Byte[65535];
            //允许子线程刷新数据
            CheckForIllegalCrossThreadCalls = false;
            //this.UserName.Text = Environment.MachineName;

            
            //异步接收消息队列中的消息
            RecieveThread = new RecieveDelegate(RecieveAsync);
            Thread t2 = new Thread(new ThreadStart(RecieveThread));
            t2.Start();

            //异步读取返回的消息
            ReadThread = new ReadDelegate(ReadAsync);
            Thread t = new Thread(new ThreadStart(ReadThread));
            t.Start();

            //button1_Click(sender, e);
            //timerStart_Click(sender, e);
        }
        /// <summary>
        /// 定时发送数据，保持长连接
        /// </summary>
        private void Timer_LongConnect() {
            connectTimer = new System.Timers.Timer(1000 * 60);
            connectTimer.Start();
            connectTimer.Elapsed += new System.Timers.ElapsedEventHandler((obj,e)=>
            {
                SendAsync("1", ClientSocket);
            });
        }

        /// <summary>
        /// 发送(客户端抄表时发送的数据，发送到消息队列即可)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            string msg = this.textBox4.Text;
            msg += "|" + Guid.NewGuid().ToString();
            myQueue.SendMessage(msg);
            this.textBox6.Text += msg + Environment.NewLine;

            //byte[] b = { 0x00, 0x00, 0x00, 0x00 };
            //float f= BitConverter.ToSingle(b, 0);
            //MessageBox.Show(f.ToString());
        }
        
        private void button2_Click(object sender, EventArgs e)
        {
            ClientSocket.Close();
            this.button1.Enabled = true;
            this.button2.Enabled = false;

        }
        /// <summary>
        /// 字符串转16进制数组
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        private static byte[] strToToHexByte(string hexString)
        {
            try
            {
                byte[] hex = Encoding.ASCII.GetBytes(hexString);
                string xyz = string.Empty;
                for (int j = 0; j < hex.Length; j++)
                {
                    xyz += hex[j].ToString("x2");
                }
                xyz += "00";
                xyz = xyz.Replace(" ", "");
                if ((xyz.Length % 2) != 0)
                    xyz += " ";
                byte[] returnBytes = new byte[xyz.Length / 2];

                for (int i = 0; i < returnBytes.Length; i++)

                    returnBytes[i] = Convert.ToByte(xyz.Substring(i * 2, 2).Trim(), 16);
                return returnBytes;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return null;
            }
            
        }
        /// <summary>
        /// 字节数组转16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string byteToHexStr(byte[] bytes)
        {
            try
            {
                string returnStr = "";
                if (bytes != null)
                {
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        returnStr += bytes[i].ToString("X2");
                    }
                }
                return returnStr;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }

        public static List<SocketAsyncEventArgs> s_lst = new List<SocketAsyncEventArgs>();
        public int SendAsync(string x, Socket sock)
        {
            SocketAsyncEventArgs e = null;
            MsgSend = strToToHexByte(x.Trim());
            if (MsgSend.Length <= 0)
                return 0;
            lock (s_lst)
            {
                if (s_lst.Count > 0)
                {
                    e = s_lst[s_lst.Count - 1];
                    s_lst.RemoveAt(s_lst.Count - 1);
                }
            }
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += (object sender, SocketAsyncEventArgs _e) =>
                {
                    lock (s_lst)
                        s_lst.Add(e);
                };
            }

            try
            {
                e.SetBuffer(MsgSend, 0, MsgSend.Length);
            }
            catch(Exception ex)
            {
                lock (s_lst)
                    s_lst.Add(e);

                return 0;
            }


            try
            {
                if (sock != null)
                {
                    if (sock.SendAsync(e))
                    {
                        return 1;
                    }
                    else
                    {
                        sock.Close();
                        sock = null;
                        return 0;
                    }
                }
                else
                {
                    return 0;
                }

            }
            catch
            {
                sock.Close();
                sock = null;
                return 0;
            }



            //lock (s_lst)
            //    s_lst.Add(e);
            //return 1;

        }

        /// <summary>
        /// 异步读取消息
        /// </summary>
        public void ReadAsync() {

            while (true) {
                if (receiveQueue.Count > 0)
                {
                    string str = receiveQueue.Dequeue();
                    //取得括号内的数据
                    try
                    {
                        string subString = str.Substring(str.IndexOf('{'), str.LastIndexOf('}') - str.IndexOf('{')+1);
                        string[] arr = subString.Split('}');

                        if (arr.Length > 0)
                        {
                            for (int i = 0; i < arr.Length - 1; i++)
                            {
                                JsonToObject(arr[i].Substring(arr[i].IndexOf('{'),arr[i].Length-arr[i].IndexOf('{'))+"}");
                            }
                        }
                    }
                    catch (Exception ex) {
                        WriteLog.WriteError("返回的数据有误,"+ex.Message);
                    }
                }
            }
            
        }
        /// <summary>
        /// 异步在消息队列中接收消息
        /// </summary>
        public void RecieveAsync() {
            while (true) {
                try
                {
                    object o = myQueue.ReceiveMessage();
                    if (o.ToString() == "读取错误")
                    {
                        //WriteLog.WriteError("消息队列读取错误");
                    }
                    else
                    {
                        string[] arr = o.ToString().Split('|');
                        string devEUI = arr[0].Trim();
                        string devAddr = arr[1].Trim();
                        readUserName = arr[2].Trim();
                        string type = arr[3].Trim();
                        guid = arr[4].Trim();

                        if (!IsConnected())
                        {
                            this.textBox5.Text += "正在重连..." + Environment.NewLine;
                            WriteLog.WriteError("正在重连...");
                            Connect();
                            _waitHandle.WaitOne();
                            Thread.Sleep(1000);
                        }

                        if (!IsReg)
                        {
                            this.textBox5.Text += "正在重新注册..." + Environment.NewLine;
                            WriteLog.WriteError("正在重新注册...");
                            RegisterCS();
                            Thread.Sleep(1000);
                        }
                        //开始接收
                        if (IsReg)
                        {
                            string str = "";
                            int x = 0;
                            //这里暂时只接收devNo,其他值默认
                            switch (type)
                            {
                                case "水表":
                                    //抄水表
                                    str = SpliceMsgWater("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "");
                                    x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "抄表请求发送成功！\r\n";
                                    WriteLog.WriteError("抄表请求发送成功！");
                                    InsertToTemp("0", devEUI, devAddr, readUserName, new Guid(guid), "抄表请求发送成功");
                                    break;
                                case "电":
                                    //抄电表
                                    str = SpliceMsgEle("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "00000100");
                                    x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "抄表请求发送成功！\r\n";
                                    WriteLog.WriteError("抄表请求发送成功！");
                                    InsertToTemp("0", devEUI, devAddr, readUserName, new Guid(guid), "抄表请求发送成功");
                                    break;
                                case "气":
                                    //抄气表
                                    break;
                                case "排污":
                                    break;
                                case "流量计":
                                    //抄流量计
                                    str = SpliceMsgFlow("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "00");//
                                    x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "抄表请求发送成功！\r\n";
                                    WriteLog.WriteError("抄表请求发送成功！");
                                    InsertToTemp("0", devEUI, devAddr, readUserName, new Guid(guid), "抄表请求发送成功");
                                    break;
                                case "液位计":
                                    //抄液位计
                                    str = SpliceMsgLevel("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "00");
                                    x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "抄表请求发送成功！\r\n";
                                    WriteLog.WriteError("抄表请求发送成功！");
                                    InsertToTemp("0", devEUI, devAddr, readUserName, new Guid(guid), "抄表请求发送成功");
                                    break;
                                case "燃气":
                                    str = SpliceMsgGas("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "");
                                    //x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "抄表请求发送成功！\r\n";
                                    //InsertToTemp("0", devEUI, devAddr, readUserName, new Guid(guid), "抄表请求发送成功");
                                    break;
                                case "对时":
                                    str = SpliceMsgServerTime("SENDTO", "0042475858FFFFFF", devEUI, devAddr, "");
                                    x = SendAsync(str, ClientSocket);
                                    this.textBox5.Text += "服务器请求对时！\r\n";
                                    break;
                                default:
                                    break;
                            }
                        }

                        else
                        {
                            this.textBox5.Text += "未注册！" + Environment.NewLine;
                            WriteLog.WriteError("未注册！");
                        }
                    }
                    Thread.Sleep(500);
                }
                catch (Exception ex) {
                    WriteLog.WriteError(ex.Message);
                }
            }
        }
        /// <summary>
        /// 将json字符串序列化对象
        /// </summary>
        /// <param name="str"></param>
        private void JsonToObject(string jsonString) {
            JavaScriptSerializer json = new JavaScriptSerializer();
            try
            {
                LoraMessage rm = (LoraMessage)json.Deserialize(jsonString, typeof(LoraMessage));
                //取到命令类型
                string cmd = rm.CMD;
                if (!string.IsNullOrEmpty(cmd))
                {
                    ///返回代码
                    /// 1：如果是SENDTO,代表准备发送；其他，成功
                    /// 2：SENDTO，已向网关发送
                    /// 0：通用失败
                    /// -1：参数不正确
                    /// -2：没有找到操作节点
                    /// -3：app payload数据无法解析出来
                    /// -4：队列已满，处理此条信息失败
                    
                    switch (cmd) { 
                        case "SENDTO":
                            int code = rm.CODE;
                            if (code == 1)
                            {
                                WriteLog.WriteError("准备发送，队列：" + rm.Qlen + "\r\n");
                                InsertToTemp("0", rm.DevEUI, "", readUserName, new Guid(guid), "准备发送，队列：" + rm.Qlen);
                            }
                            else if (code == 2)
                            {
                                WriteLog.WriteError("已发送给网关\r\n");
                                InsertToTemp("0", rm.DevEUI, "", readUserName, new Guid(guid), "已发送给网关");
                            }
                            else
                            {
                                WriteLog.WriteError("异常，" + rm.MSG+"\r\n");
                                InsertToTemp("0", rm.DevEUI, "", readUserName, new Guid(guid), "异常，" + rm.MSG);
                            }
                            break;
                        case "UPLOAD":
                            ParsePayload(rm.payload,rm.DevEUI);
                            break;
                        case "CSREG":
                            if (rm.CODE == 1)
                            {
                                IsReg = true;
                                _waitHandle.Set();
                                WriteLog.WriteError("注册成功");
                                this.textBox5.Text += "注册成功！" + Environment.NewLine;
                                InsertToTemp("0", "", "", readUserName, Guid.NewGuid(), "注册成功");
                            }
                            else {
                                IsReg = false;
                                _waitHandle.Set();
                                WriteLog.WriteError("注册失败");
                                this.textBox5.Text += "注册失败！" + Environment.NewLine;
                                InsertToTemp("0", "", "", readUserName, Guid.NewGuid(), "注册失败");
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
            }
        }
        /// <summary>
        /// 解析payload
        /// </summary>
        /// <param name="payload"></param>
        public void ParsePayload(string payload,string devEui) {
            try
            {
                //调用payload解析,base64字符串长度为4的倍数，不足补"="，否则报无效的长度
                int mod = payload.Length % 4;
                if (mod != 0)
                {
                    for (int i = mod; i < 4; i++)
                    {
                        payload += "=";
                    }
                }
                byte[] b = Convert.FromBase64String(payload);
                //获取设备ID
                if (devEui.Length < 16)
                {
                    for (int l = devEui.Length; l < 16; l++)
                    {
                        devEui = "0" + devEui;
                    }
                }
                //byte[] b = System.Text.Encoding.UTF8.GetBytes(payload);
                switch (b[0])
                {
                    case 0xfe:
                        //前导码 电表
                        ParseEle(b, devEui);
                        break;
                    case 0xa8:
                        //帧起始符  流量计/液位计/水表
                        ParseFlowMeter(b, devEui);
                        break;
                    case 0x68:
                        //燃气表
                        ParseGas(b, devEui);
                        break;
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                InsertToTemp("", "", "", "", new Guid(guid), "抄表失败:" + ex.Message);
            }
        }
        /// <summary>
        /// 解析电表
        /// </summary>
        /// <param name="b"></param>
        public void ParseEle(byte[] b, string devEui)
        {
            try
            {
                ///前4位为前导字节fe
                string devAddr = "";
                for (int i = 10; i > 4; i--)
                {
                    devAddr += b[i].ToString("X2");
                }
                byte controlByte = b[12];
                byte lengthByte = b[13];
                //无后续帧正常应答
                if (controlByte == 0x91)
                {
                    //数据长度
                    int length = Convert.ToInt32(lengthByte) - 4;
                    string data = "";
                    int count = 0;
                    //由低到高，反向遍历xxxxxxx.xx
                    for (int i = 17 + length; i > 17; i--)
                    {
                        count++;
                        string temp = (Convert.ToInt32(b[i]) - Convert.ToInt32(0x33)).ToString("X2");
                        data += temp;
                        //第三位后加小数点
                        if (count % 3 == 0)
                        {
                            data += ".";
                        }
                        //分割，如果返回了多个数据
                        if (count % 4 == 0)
                        {

                        }
                    }
                    //这里调用存入数据库即可
                    //InsertData(data,devEui ,devAddr, readUserName);
                    InsertToTemp(data, devEui, devAddr, readUserName, new Guid(guid), "抄表成功");
                    InsertData(data, devEui, devAddr, readUserName, DateTime.Now);
                    this.textBox5.Text += "电表：" + devAddr + "，正向有功总电能：" + Convert.ToDouble(data) + "kwh\r\n";
                    WriteLog.WriteError("电表：" + devAddr + "，正向有功总电能：" + Convert.ToDouble(data) + "kwh！");
                }
                else if (controlByte == 0xB1)
                { //有后续帧正常应答

                }
                else if (controlByte == 0xD1)
                { //异常应答

                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
            }
            
        }
        /// <summary>
        /// 解析流量计/液位计/水表
        /// </summary>
        /// <param name="b"></param>
        public void ParseFlowMeter(byte[] b, string devEui)
        {
            try
            {
                //A8-84-0B-02-00-01-03-04-0D-BD-3D-89-B9-8D-17-16
                //帧起始符A8
                //控制码 04 抄表 84抄表上报 02 参数设置 83 读取参数 80 确认/否认
                switch (b[1])
                {
                    case 0x84:
                        //抄表数据
                        string devAddr = b[5].ToString();//地址
                        //数据标识02 正常 03超时
                        switch (b[3])
                        {
                            case 0x02:
                                //协议号 0a流量计 0b液位计  01水表（这个协议号是根据消息拼接时设置的，所以要一一对应）
                                switch (b[4])
                                {
                                    case 0x0a:
                                        //功能码
                                        switch (b[6])
                                        {
                                            //正常
                                            case 0x03:
                                                int dataLength = Convert.ToInt32(b[7]);
                                                if (dataLength != 4)
                                                {
                                                    break;
                                                }
                                                byte[] data = new byte[dataLength];
                                                for (int i = 0; i < data.Length; i++)
                                                {
                                                    data[i] = b[8 + i];
                                                }
                                                //3412 16->10
                                                byte[] temp = new byte[4];
                                                //temp[0] = data[2];
                                                //temp[1] = data[3];
                                                //temp[2] = data[0];
                                                //temp[3] = data[1];
                                                int r = Convert.ToInt32(byteToHexStr(temp), 16);
                                                //瞬时流量 2143
                                                temp[0] = data[1];
                                                temp[1] = data[0];
                                                temp[2] = data[3];
                                                temp[3] = data[2];
                                                float f = BitConverter.ToSingle(temp, 0);
                                                //这里调用存入数据库即可
                                                //InsertData(data,devEui ,devAddr, readUserName);
                                                InsertToTemp(f.ToString(), devEui, devAddr, readUserName, new Guid(guid), "抄表成功");
                                                InsertData(f.ToString(), devEui, devAddr, readUserName, DateTime.Now);
                                                this.textBox5.Text += "流量计：" + devAddr + "，值：" + f + "m3/h\r\n";
                                                WriteLog.WriteError("流量计：" + devAddr + "，值：" + f + "m3/h");
                                                break;
                                            case 0x83:
                                                //错误
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                    case 0x0b:
                                        //功能码
                                        switch (b[6])
                                        {
                                            //正常
                                            case 0x03:
                                                int dataLength = Convert.ToInt32(b[7]);
                                                byte[] data = new byte[dataLength];
                                                for (int i = 0; i < data.Length; i++)
                                                {
                                                    data[i] = b[8 + i];
                                                }
                                                //2143
                                                for (int j = 0; j < data.Length - 1; j += 2)
                                                {
                                                    byte temp = data[j];
                                                    data[j] = data[j + 1];
                                                    data[j + 1] = temp;
                                                }
                                                float r = BitConverter.ToSingle(data, 0);
                                                //这里调用存入数据库即可
                                                //InsertData(data,devEui ,devAddr, readUserName);

                                                InsertToTemp(r.ToString(), devEui, devAddr, readUserName, new Guid(guid), "抄表成功");
                                                InsertData(r.ToString(), devEui, devAddr, readUserName, DateTime.Now);
                                                this.textBox5.Text += "液位计：" + devAddr + "，值：" + r + "m\r\n";
                                                WriteLog.WriteError("液位计：" + devAddr + "，值：" + r + "m\r\n");
                                                break;
                                            case 0x83:
                                                //错误
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                    case 0x01:
                                        //水表
                                        if (b[2] == 0x16)
                                        {
                                            byte[] wdata = { b[19], b[20], b[21], b[22] };
                                            string zs = "";
                                            string xs = (wdata[0] - 0x33).ToString("X2");
                                            for (int i = 3; i > 0; i--)
                                            {
                                                zs += (wdata[i] - 0x33).ToString("X2");
                                            }
                                            double d = Convert.ToDouble(zs + "." + xs);

                                            //devAddr
                                            StringBuilder sb = new StringBuilder();
                                            for (int i = 13; i > 7; i--)
                                            {
                                                sb.Append(b[i].ToString("X2"));
                                            }
                                            InsertToTemp(d.ToString(), devEui, sb.ToString(), readUserName, new Guid(guid), "抄表成功");
                                            InsertData(d.ToString(), devEui, sb.ToString(), readUserName, DateTime.Now);
                                            this.textBox5.Text += "水表：" + sb.ToString() + "，值：" + d + "m³\r\n";
                                            WriteLog.WriteError("水表：" + sb.ToString() + "，值：" + d + "m³\r\n");
                                        }
                                        else if (b[2] == 0x17)
                                        {
                                            byte[] wdata = { b[20], b[21], b[22], b[23] };
                                            string zs = "";
                                            string xs = (wdata[0] - 0x33).ToString("X2");
                                            for (int i = 3; i > 0; i--)
                                            {
                                                zs += (wdata[i] - 0x33).ToString("X2");
                                            }
                                            double d = Convert.ToDouble(zs + "." + xs);

                                            //devAddr
                                            StringBuilder sb = new StringBuilder();
                                            for (int i = 14; i > 8; i--)
                                            {
                                                sb.Append(b[i].ToString("X2"));
                                            }
                                            InsertToTemp(d.ToString(), devEui, sb.ToString(), readUserName, new Guid(guid), "抄表成功");
                                            InsertData(d.ToString(), devEui, sb.ToString(), readUserName, DateTime.Now);
                                            this.textBox5.Text += "水表：" + sb.ToString() + "，值：" + d + "m³\r\n";
                                            WriteLog.WriteError("水表：" + sb.ToString() + "，值：" + d + "m³\r\n");
                                        }
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case 0x03:
                                break;
                            default:
                                break;
                        }
                        break;
                    case 0x94:
                        //光照温湿度
                        //传感器类型标识
                        byte[] addr = { b[4], b[3] };
                        string strAddr = byteToHexStr(addr);
                        switch (strAddr)
                        {
                            case "000C":
                                //光照温湿度
                                //时间
                                DateTime time = new DateTime();
                                if (b[6] == 0xee || b[7] == 0xee || b[8] == 0xee || b[9] == 0xee || b[10] == 0xee || b[11] == 0xee)
                                {
                                    time = DateTime.Now;
                                }
                                else
                                {
                                    time = new DateTime(
                                    DBHelper.ConvertBCDToInt(b[11]) + 2000,
                                    DBHelper.ConvertBCDToInt(b[10]),
                                    DBHelper.ConvertBCDToInt(b[9]),
                                    DBHelper.ConvertBCDToInt(b[8]),
                                    DBHelper.ConvertBCDToInt(b[7]),
                                    DBHelper.ConvertBCDToInt(b[6]));
                                }
                                //光照
                                UInt16 light = Convert.ToUInt16(byteToHexStr(new byte[] { b[13], b[12] }), 16);//流明
                                //温度
                                double temp = (Convert.ToUInt16(byteToHexStr(new byte[] { b[15], b[14] }), 16) - 5000) * 0.01;//°C
                                //湿度
                                double hum = Convert.ToUInt16(byteToHexStr(new byte[] { b[17], b[16] }), 16) * 0.01;//%RH
                                this.textBox5.Text += "光照温湿度：光照-" + light + "，温度-" + temp + "，湿度-" + hum + Environment.NewLine;
                                WriteLog.WriteError("光照温湿度：光照-" + light + "，温度-" + temp + "，湿度-" + hum);
                                string str = string.Format("select * from DEV_LIGHTTEMPHUM where DEVEUI='{0}'", devEui);
                                int devId = DBHelper.GetFirst(str, null, CommandType.Text);
                                //插入新数据
                                string sql = string.Format("insert into DEV_LIGHTTEMPHUM_RECORD(DEVID,LIGHT,TEMP,HUM,READDATE) values({0},{1},{2},{3},'{4}')", devId, light, temp, hum, time);
                                int r = DBHelper.ExcuteSQL(sql, null, CommandType.Text);
                                //更新设备信息
                                string sql1 = string.Format("update DEV_LIGHTTEMPHUM set LIGHT={0},TEMP={1},HUM={2} where DEVEUI='{3}'", light, temp, hum, devEui);
                                int r1 = DBHelper.ExcuteSQL(sql1, null, CommandType.Text);
                                break;
                        }
                        break;
                    case 0x83:
                        //服务器对时
                        string msg = SpliceMsgServerTime("SENDTO", "0042475858FFFFFF", "0008915180900002", "", "");
                        SendAsync(msg, ClientSocket);
                        break;
                    case 0x80:
                        //确认/否认
                        break;
                    case 0x30:
                        //string msg = SpliceMsg5("SENDTO", "0042475858FFFFFF", devEui, "", "");
                        // SendAsync(msg, ClientSocket);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
            }
            
        }
        /// <summary>
        /// 解析燃气表
        /// </summary>
        /// <param name="b"></param>
        /// <param name="devEui"></param>
        public void ParseGas(byte[] b, string devEui) {
            try
            {
                //燃气表
                if (b[1] >= 0x30 && b[1] <= 0x39)
                {
                    string devAddr = "";
                    for (int i = 8; i > 1; i--)
                    {
                        devAddr += b[i].ToString("X2");
                    }
                    //控制码
                    switch (b[9])
                    {
                        //正常应答
                        case 0x81:
                            //data b[14]-b[17] b[18]单位 2C代表m³  3412
                            byte[] data = { b[14], b[15], b[16], b[17] };
                            //byte[] data = { 0x00, 0x25, 0x04, 0x00 };
                            string zs = "";
                            string xs = data[0].ToString("X2");
                            for (int i = 3; i > 0; i--)
                            {
                                zs += data[i].ToString("X2");
                            }
                            string val = zs + "." + xs;
                            double r = Convert.ToDouble(val);
                            InsertToTemp(r.ToString(), devEui, devAddr, readUserName, new Guid(guid), "抄表成功");
                            InsertData(r.ToString(), devEui, devAddr, readUserName, DateTime.Now);
                            this.textBox5.Text += "气：" + devAddr + "，值：" + r + "m³\r\n";
                            WriteLog.WriteError("气：" + devAddr + "，值：" + r + "m³");
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
            }
            
        }
        /// <summary>
        /// CRC校验
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] CRC16(byte[] data)
        {
            try
            {
                int len = data.Length;
                if (len > 0)
                {
                    ushort crc = 0xFFFF;

                    for (int i = 0; i < len; i++)
                    {
                        crc = (ushort)(crc ^ (data[i]));
                        for (int j = 0; j < 8; j++)
                        {
                            crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
                        }
                    }
                    byte hi = (byte)((crc & 0xFF00) >> 8);  //高位置
                    byte lo = (byte)(crc & 0x00FF);         //低位置

                    return new byte[] { lo, hi };
                }
                else {
                    return new byte[] { 0, 0 };
                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return new byte[] { 0, 0 };
            }
            
        }
        /// <summary>
        /// 拼接电表消息
        /// </summary>
        /// <returns></returns>
        public string SpliceMsgEle(string cmd,string appEui,string devEui,string devAddr,string dataCode) {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                ///(当前)正向有功总电能
                ///地址：010096642708
                ///数据标识 DI0-DI3  00 00 01 00
                ///\x68 \x08 \x27 \x64 \x96 \x00 \x01 \x68 
                ///\x11 \x04 \x33 \x33 \x34 \x33 \xdc \x16 
                /// {"CMD":"SENDTO","Token":2,"AppEUI":"0042475858000001","DevEUI":"0002902174200003","payload":"aAgnZJYAAWgRBDMzNDPcFg==","Port":231}
                byte[] b = new byte[16];
                //帧起始符
                b[0] = 0x68;
                //地址域(注意传递的顺序)
                try
                {
                    for (int i = 1; i < 7; i++)
                    {
                        b[i] = (Byte)Convert.ToInt32(devAddr.Substring(12 - i * 2, 2), 16);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog.WriteError(ex.Message);
                }

                //帧起始符
                b[7] = 0x68;
                //控制码(这里根据具体情况设置，0x11表示请求读电能表数据)
                b[8] = 0x11;
                //数据域长度（4+m）
                b[9] = 0x04;
                //数据域(00 00 01 00 +33H)
                for (int i = 10; i < 14; i++)
                {
                    int temp = Convert.ToInt32(dataCode.Substring((i - 10) * 2, 2), 16);
                    b[i] = (Byte)(temp + Convert.ToInt32("0x33", 16));
                }
                //b[10] = 0x33;
                //b[11] = 0x33;
                //b[12] = 0x34;
                //b[13] = 0x33;
                //校验码(前面所有值的和模256)
                byte bTemp = 0;
                for (int i = 0; i < 14; i++)
                {
                    bTemp += b[i];
                }
                b[14] = (Byte)(bTemp % 256);
                //结束符
                b[15] = 0x16;
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception e) {
                WriteLog.WriteError(e.Message);
                return "";
            }
        }
        /// <summary>
        /// 拼接消息（流量计）LoRaWAN通信转换器通信协议+MODBUS协议
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgFlow(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                //A8-04-16-02-00-00-00-32-C5-00-00-00-00-00-00-08-01-03-00-00-00-02-C4-0B-00-98-16
                byte[] b = new byte[27];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x04;
                //数据域长度
                b[2] = 0x16;
                //数据标识
                b[3] = 0x02;
                //测量点号
                b[4] = 0x00;
                b[5] = 0x00;
                //该命令执行的协议号  设备类型
                b[6] = 0x0a;
                //抄表超时时间
                b[7] = 0x32;//(50ms)
                //通道配置
                b[8] = 0xC5;//9600 odd 485
                //查询命令的前导符个数,0表示无需前导
                b[9] = 0x00;
                //查询命令的前导符
                //b[9] = 0x00;
                //唤醒命令发送间隔
                b[10] = 0x00;
                //唤醒命令发送次数
                b[11] = 0x00;
                b[12] = 0x00;
                //唤醒命令长度，0表示无需唤醒
                b[13] = 0x00;
                //查询命令长度
                b[14] = 0x00;
                //X 唤醒命令长度
                //b[15] = 0x00;
                //Y 查询命令长度
                b[15] = 0x08;
                b[24] = 0x00;
                b[26] = 0x16;
                //获取数据域
                byte addr = Convert.ToByte(devAddr);
                //寄存器地址
                byte bAddr = Convert.ToByte(dataCode);
                byte[] data = { addr, 0x03, 0x00, bAddr, 0x00, 0x02 };
                //返回数据
                //byte[] data2 = { 0x01, 0x03,0x04, 0x00, 0x00, 0x00, 0x00 };
                byte[] crc = CRC16(data);
                byte[] r = new byte[data.Length + crc.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    r[i] = data[i];
                }
                for (int j = 0; j < crc.Length; j++)
                {
                    r[r.Length - crc.Length + j] = crc[j];
                }
                //替换
                for (int k = 0; k < r.Length; k++)
                {
                    b[k + 16] = r[k];
                }
                byte bTemp = 0;
                for (int i = 0; i < 25; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[25] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 拼接消息（水表）
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgWater(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                //A8-04-1F-02-00-00-01-32-6E-00-00-00-00-00-00-12-FE-FE-FE-FE-68-15-06-02-18-00-33-68-01-02-43-C3-41-16-10-16
                byte[] b = new byte[36];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x04;
                //数据域长度
                b[2] = 0x1F;
                //数据标识
                b[3] = 0x02;
                //测量点号
                b[4] = 0x00;
                b[5] = 0x00;
                //该命令执行的协议号  设备类型
                b[6] = 0x01;
                //抄表超时时间
                b[7] = 0x32;//(50ms)
                //通道配置
                b[8] = 0x6E;
                //查询命令的前导符个数,0表示无需前导
                b[9] = 0x00;
                //查询命令的前导符
                //b[9] = 0x00;
                //唤醒命令发送间隔
                b[10] = 0x00;
                b[11] = 0x00;
                b[12] = 0x00;
                b[13] = 0x00;
                b[14] = 0x00;
                //命令长度
                b[15] = 0x12;
                b[16] = 0xFE;
                b[17] = 0xFE;
                b[18] = 0xFE;
                b[19] = 0xFE;
                //获取数据域
                byte[] bdata = new byte[14];
                bdata[0] = 0x68;
                byte[] baddr = new byte[6];
                for (int i = 0; i < 6; i++)
                {
                    baddr[i] = (Byte)Convert.ToInt32(devAddr.Substring(10 - i * 2, 2), 16);
                }
                for (int j = 0; j < baddr.Length; j++)
                {
                    bdata[1 + j] = baddr[j];
                }
                bdata[7] = 0x68;
                //控制码
                bdata[8] = 0x01;
                //数据长度
                bdata[9] = 0x02;
                //数据标识
                bdata[10] = 0x43;
                bdata[11] = 0xC3;
                //校验码
                byte bdataTemp = 0x00;
                for (int k = 0; k < 12; k++)
                {
                    bdataTemp += bdata[k];
                }
                bdata[12] = (Byte)(bdataTemp % 255);
                bdata[13] = 0x16;
                //添加数据域
                for (int m = 0; m < bdata.Length; m++)
                {
                    b[20 + m] = bdata[m];
                }

                byte bTemp = 0;
                for (int i = 0; i < 34; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[34] = (Byte)(bTemp % 256);
                b[35] = 0x16;
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 拼接消息 液位计LoRaWAN通信转换器通信协议+MODBUS协议
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgLevel(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                //A8-04-16-02-00-00-00-32-C5-00-00-00-00-00-00-08-01-03-00-00-00-02-C4-0B-00-98-16
                byte[] b = new byte[27];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x04;
                //数据域长度
                b[2] = 0x16;
                //数据标识
                b[3] = 0x02;
                //测量点号
                b[4] = 0x00;
                b[5] = 0x00;
                //该命令执行的协议号  设备类型
                b[6] = 0x0b;
                //抄表超时时间
                b[7] = 0x32;//(50ms)
                //通道配置
                b[8] = 0xC5;//9600 odd 485
                //查询命令的前导符个数,0表示无需前导
                b[9] = 0x00;
                //查询命令的前导符
                //b[9] = 0x00;
                //唤醒命令发送间隔
                b[10] = 0x00;
                //唤醒命令发送次数
                b[11] = 0x00;
                b[12] = 0x00;
                //唤醒命令长度，0表示无需唤醒
                b[13] = 0x00;
                //查询命令长度
                b[14] = 0x00;
                //X 唤醒命令长度
                //b[15] = 0x00;
                //Y 查询命令长度
                b[15] = 0x08;
                b[24] = 0x00;
                b[26] = 0x16;
                //获取数据域
                byte addr = Convert.ToByte(devAddr);
                //寄存器地址
                byte bAddr = Convert.ToByte(dataCode);
                byte[] data = { addr, 0x03, 0x00, bAddr, 0x00, 0x02 };
                //返回数据
                //byte[] data2 = { 0x01, 0x03,0x04, 0x00, 0x00, 0x00, 0x00 };
                byte[] crc = CRC16(data);
                byte[] r = new byte[data.Length + crc.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    r[i] = data[i];
                }
                for (int j = 0; j < crc.Length; j++)
                {
                    r[r.Length - crc.Length + j] = crc[j];
                }
                //替换
                for (int k = 0; k < r.Length; k++)
                {
                    b[k + 16] = r[k];
                }
                byte bTemp = 0;
                for (int i = 0; i < 25; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[25] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 服务器对时
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgServerTime(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                byte[] b = new byte[12];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x02;
                //数据域长度
                b[2] = 0x07;
                //数据标识
                b[3] = 0x0A;
                DateTime time = DateTime.Now;
                //秒
                b[4] = (Byte)time.Second;
                //分
                b[5] = (Byte)time.Minute;
                //时
                b[6] = (Byte)time.Hour;
                //日
                b[7] = (Byte)time.Day;
                //月
                b[8] = (Byte)time.Month;
                //年
                b[9] = Convert.ToByte(time.Year.ToString().Substring(2, 2));

                b[11] = 0x16;
                byte bTemp = 0;
                for (int i = 0; i < 11; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[10] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 确认
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string ConfirmMsg(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                byte[] b = new byte[9];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x00;
                //数据域长度
                b[2] = 0x04;
                //数据标识
                b[3] = 0x01;

                b[4] = 0x02;
                b[5] = 0x0a;
                //确认
                b[6] = 0x01;

                b[8] = 0x16;
                byte bTemp = 0;
                for (int i = 0; i < 8; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[7] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex)
            {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 光照温湿度
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgLTH(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                ///帧起始符：a8
                ///控制码：04
                ///数据域长度：09
                ///数据域：
                ///LoRaWAN传感器类型标识：00 0c
                ///数据单元标识：01
                ///数据内容： 18 03 15 10 41 30
                ///校验码：xx
                ///帧结束符：16
                byte[] b = new byte[14];
                //帧起始符
                b[0] = 0xA8;
                //控制码
                b[1] = 0x04;
                //数据域长度
                b[2] = 0x09;
                //LoRaWAN传感器类型标识
                b[3] = 0x00;
                b[4] = 0x0c;
                //数据单元标识
                b[5] = 0x01;
                DateTime time = DateTime.Now;
                //秒
                b[6] = (Byte)time.Second;
                //分
                b[7] = (Byte)time.Minute;
                //时
                b[8] = (Byte)time.Hour;
                //日
                b[9] = (Byte)time.Day;
                //月
                b[10] = (Byte)time.Month;
                //年
                b[11] = Convert.ToByte(time.Year.ToString().Substring(2, 2));

                b[13] = 0x16;
                byte bTemp = 0;
                for (int i = 0; i < 12; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[12] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 燃气表
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="appEui"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="dataCode"></param>
        /// <returns></returns>
        public string SpliceMsgGas(string cmd, string appEui, string devEui, string devAddr, string dataCode)
        {
            try
            {
                LoraMessage lm = new LoraMessage();
                ///示例
                ///帧起始符：a8
                ///仪表类型：30  燃气表
                ///地址：45-23-31-80-01-00-00  000180312345
                ///数据域：
                ///LoRaWAN传感器类型标识：00 0c
                ///数据单元标识：01
                ///数据内容： 18 03 15 10 41 30
                ///校验码：xx
                ///帧结束符：16
                byte[] b = new byte[16];
                //帧起始符
                b[0] = 0x68;
                //仪表类型
                b[1] = 0x30;
                //地址域(注意传递的顺序)
                try
                {
                    for (int i = 1; i < 8; i++)
                    {
                        b[i + 1] = (Byte)Convert.ToInt32(devAddr.Substring(14 - i * 2, 2), 16);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog.WriteError(ex.Message);
                }
                //控制码
                b[9] = 0x01;
                //数据域长度
                b[10] = 0x03;
                //数据标识
                b[11] = 0x90;
                b[12] = 0x1F;
                //序列号
                b[13] = 0x00;

                b[15] = 0x16;
                byte bTemp = 0;
                for (int i = 0; i < 16; i++)
                {
                    bTemp += b[i];
                }
                //校验码
                b[14] = (Byte)(bTemp % 256);
                //base64编码
                string payload = Convert.ToBase64String(b);

                var ob = new { CMD = cmd, Token = token, AppEUI = appEui, DevEUI = devEui, payload = payload, Port = 231 };
                //每次发送后+1
                token++;
                if (token > 1000)
                {
                    token = 1;
                }

                JavaScriptSerializer json = new JavaScriptSerializer();
                string msg = json.Serialize(ob);
                return msg;
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
                return "";
            }
        }
        /// <summary>
        /// 插入数据到数据库
        /// </summary>
        /// <param name="data">抄表数据</param>
        /// <param name="devAddr">表号</param>
        /// <param name="userName">抄表人</param>
        public void InsertData(string data,string devEui,string devAddr,string userName,DateTime time) {
            using (SqlConnection conn = new SqlConnection(connectString)) {
                try
                {
                    conn.Open();
                    #region 需要先根据devAddr(devNum)查询到当前设备的相关信息，再插入到数据表中
                    string infoStr = string.Format("select "
                                    + "di.DEVID as 设备ID,"
                                    + "di.DEVNO as EUI,"
                                    + "di.RATE as 倍率,"
                                    + "di.THRESHOLD as 阈值,"
                                    + "dt.TYPENAME as 类型名称,"
                                    + "dc.CUSTNAME as 用户名称,"
                                    + "dc.DEPAETMENT as 所属部门,"
                                    + "dc.NYTGZY as 能源通告专用,"
                                    + "dc.YSZY as 预算专用,"
                                    + "dc.TARGET as 目标 "
                                    + "from dev_info di,DEV_TYPE dt,DEV_CUSTOM dc "
                                    + "where di.TYPEID=dt.TYPEID "
                                    + "and di.CUSTID=dc.CUSTID and devNum='{0}'", devAddr);
                    SqlCommand infoComm = new SqlCommand(infoStr, conn);
                    infoComm.CommandType = CommandType.Text;
                    SqlDataAdapter infoAdapter = new SqlDataAdapter(infoComm);
                    DataTable infodt = new DataTable();
                    infoAdapter.Fill(infodt);
                    #endregion
                    #region 获取上一次抄表数据
                    string lastStr = string.Format("select top 1 dr.ENDDATA as endData "
                                    + "from DEV_RECORD dr,DEV_INFO di "
                                    + "where dr.DEVID=di.DEVID "
                                    + " and di.devNum='{0}' "
                                    + "order by dr.READDATE desc", devAddr);
                    SqlCommand lastComm = new SqlCommand(lastStr, conn);
                    lastComm.CommandType = CommandType.Text;
                    SqlDataAdapter lastAdapter = new SqlDataAdapter(lastComm);
                    DataTable lastdt = new DataTable();
                    lastAdapter.Fill(lastdt);
                    double lastData = 0;
                    if (lastdt.Rows.Count > 0)
                    {
                        lastData = Convert.ToDouble(lastdt.Rows[0][0].ToString());
                    }
                    #endregion
                    #region 插入抄表数据
                    string insertStr = string.Format("insert into DEV_RECORD(DEVID,INTERVAL,SATARTDATA,"
                                + "ENDDATA,READDATE,READUSER,USEAMOUNTS,LOSSAMOUNTS,"
                                + "TOTALAMOUNTS,CUSTNAME,DEPARTMENT,NYTGZY,YSZY,TARGET) "
                                + "values({0},{1},{2},{3},'{4}','{5}',{6},{7},{8},'{9}','{10}','{11}','{12}','{13}') ",
                                Convert.ToInt32(infodt.Rows[0]["设备ID"].ToString()),
                                DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString(),
                                lastData,
                                Convert.ToDouble(data),
                                time,
                                userName,
                                (Convert.ToDouble(data) - lastData) * Convert.ToInt32(infodt.Rows[0]["倍率"].ToString()),
                                0,
                                (Convert.ToDouble(data) - lastData) * Convert.ToInt32(infodt.Rows[0]["倍率"].ToString()),
                                infodt.Rows[0]["用户名称"].ToString(),
                                infodt.Rows[0]["所属部门"].ToString(),
                                infodt.Rows[0]["能源通告专用"].ToString(),
                                infodt.Rows[0]["预算专用"].ToString(),
                                infodt.Rows[0]["目标"].ToString());
                    SqlCommand comm = new SqlCommand(insertStr, conn);
                    comm.CommandType = CommandType.Text;
                    int i = comm.ExecuteNonQuery();
                    #endregion
                    #region 更新设备信息
                    string sql = string.Format("update dev_info set RECORDDATE='{0}',RECORDUSER='{1}',CONSUMPTION={2} where DEVNUM='{3}'",
                                    DateTime.Now, userName, Convert.ToDouble(data), devAddr);
                    SqlCommand c = new SqlCommand(sql, conn);
                    c.CommandType = CommandType.Text;
                    int r = c.ExecuteNonQuery();
                    #endregion
                    #region  插入报警表
                    if (infodt.Rows[0]["类型名称"].ToString() == "液位计") {
                        double val = Convert.ToDouble(data);
                        double temp = Convert.ToDouble(infodt.Rows[0]["阈值"]);
                        if (val >= temp) {
                            string alarmSql = string.Format("insert into DEV_RECORD_ALARM(DEVID,DEVEUI,TYPENAME,ALARMDATA,ALARMTIME)"
                                    + "values({0},'{1}','{2}',{3},'{4}')", infodt.Rows[0]["设备ID"].ToString(), infodt.Rows[0]["EUI"], infodt.Rows[0]["类型名称"], data, DateTime.Now);
                            SqlCommand alarmComm = new SqlCommand(alarmSql, conn);
                            alarmComm.CommandType = CommandType.Text;
                            int alarm = alarmComm.ExecuteNonQuery();
                        }
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    WriteLog.WriteError("抄表失败");
                    WriteLog.WriteError(ex.Message);
                }
            }
        }
        /// <summary>
        /// 存储返回临时数据，判断是否抄表成功
        /// </summary>
        /// <param name="data"></param>
        /// <param name="devEui"></param>
        /// <param name="devAddr"></param>
        /// <param name="userName"></param>
        private void InsertToTemp(string data, string devEui, string devAddr, string userName,Guid guid,string msg)
        {
            using (SqlConnection conn = new SqlConnection(connectString)) {
                try
                {
                    conn.Open();

                    //插入到抄表临时表，方便反馈信息给用户
                    string sqlTemp = string.Format("insert into DEV_RECORD_TEMP(DevEui,DevAddr,ReadData,ReadUser,ReadTime,DataMark,Msg) "
                                    + " values('{0}','{1}',{2},'{3}','{4}','{5}','{6}')", devEui, devAddr, Convert.ToDouble(data), userName, DateTime.Now, guid, msg);
                    SqlCommand c = new SqlCommand(sqlTemp, conn);
                    c = new SqlCommand(sqlTemp, conn);
                    c.CommandType = CommandType.Text;
                    int result = c.ExecuteNonQuery();
                }
                catch (Exception ex) {
                    WriteLog.WriteError(ex.Message);
                }
            }
        }
        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="msg"></param>
        public void RegisterCS() {
            ///注册
            var cs = new { CMD = "CSREG", Token = 1, AppEUI = "0042475858FFFFFF", AppNonce = 1234, Challenge = "ABCDEF1234567890ABCDEF1234567890" };
            JavaScriptSerializer json = new JavaScriptSerializer();
            string msg = json.Serialize(cs);
            SendAsync(msg, ClientSocket);
            token++;
            ///等待注册完成
            _waitHandle.WaitOne();
        }

        public void Connect() {
            ServerInfo = new IPEndPoint(IPAddress.Parse(textBox1.Text.Trim()), Convert.ToInt32(textBox2.Text.Trim()));

            try
            {
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //客户端连接服务端指定IP端口，Sockket

                ClientSocket.Connect(ServerInfo);
                //将用户登录信息发送至服务器，由此可以让其他客户端获知
                //ClientSocket.Send(Encoding.ASCII.GetBytes("用户：yangjian  进入系统！\n"));
                //开始从连接的Socket异步读取数据。接收来自服务器，其他客户端转发来的信息
                //AsyncCallback引用在异步操作完成时调用的回调方法
                ClientSocket.BeginReceive(MsgBuffer, 0, MsgBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallBack), null);

                this.textBox3.Text += "登录服务器成功！\n";
                this.textBox5.Text += "连接服务器成功！" + Environment.NewLine;
                this.button3.Enabled = true;
                this.button1.Enabled = false;
                this.button2.Enabled = true;
                //每次连接后需要重新注册
                IsReg = false;
                _waitHandle.Set();
            }
            catch (Exception ex)
            {
                WriteLog.WriteError(ex.Message);
                _waitHandle.Set();
            }
            //注册
            RegisterCS();
        }
        #region 定时抄表
        /// <summary>
        /// 开启定时抄表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerStart_Click(object sender, EventArgs e)
        {
            int time = Convert.ToInt32(this.txtTime.Text.Trim());
            timer = new System.Timers.Timer(1000 * time);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_TimesUP);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
            this.timerStart.Enabled = false;
            this.timerStop.Enabled = true;
            this.textBox3.Text += "定时抄表已开启！\n";
        }
        /// <summary>
        /// 关闭定时抄表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerStop_Click(object sender, EventArgs e)
        {
            timer.Stop();
            this.timerStop.Enabled = false;
            this.timerStart.Enabled = true;
            this.textBox3.Text += "定时抄表已关闭！\n";
        }
        /// <summary>
        /// 定时抄表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_TimesUP(object sender, System.Timers.ElapsedEventArgs e) {
            int hour = DateTime.Now.Hour;
            int minute = DateTime.Now.Minute;
            try
            {
                //服务器对时
                

                //每半小时自动抄表：液位计
                if ((minute>=0 && minute<1) || (minute>=30&&minute<31))
                {
                    string msg = "0008915180900002|101|自动抄表|液位计|" + Guid.NewGuid();
                    this.textBox5.Text += msg + "\r\n";
                    myQueue.SendMessage(msg);
                    
                }
                //清空TextBox
                if ((hour>=0 && hour < 1) && (minute>=0 && minute < 1))
                {
                    string s1 = "0008915180900002|1|自动抄表|对时|" + Guid.NewGuid();
                    string s2 = "0008915180900003|101|自动抄表|对时|" + Guid.NewGuid();
                    string s3 = "0008915181100004|330018020615|自动抄表|对时|" + Guid.NewGuid();
                    myQueue.SendMessage(s1);
                    myQueue.SendMessage(s2);
                    myQueue.SendMessage(s3);
                    this.textBox5.Text = "";
                }
            }
            catch (Exception ex) {
                WriteLog.WriteError(ex.Message);
            }
            
        }
        #endregion

        private void button4_Click(object sender, EventArgs e)
        {
            //转浮点数
            //byte[] b = { 0x9B, 0xAA,0x99, 0x3A, };
            //byte[] data = { 0x8D, 0x83, 0x58, 0x40};
            //float f = BitConverter.ToSingle(b,0 );
            //MessageBox.Show(""+f);
            //流量计
            //string flow = SpliceMsgFlow("SENDTO", "0042475858FFFFFF", "0008915180900003", "01", "08");
            //服务器对时
            string s1 = SpliceMsgServerTime("SENDTO", "0042475858FFFFFF", "0008915181100004", "", "");
            string s2 = SpliceMsgServerTime("SENDTO", "0042475858FFFFFF", "0008915180900003", "", "");
            string s3 = SpliceMsgServerTime("SENDTO", "0042475858FFFFFF", "0008915180900002", "", "");
        }

        private Decimal ChangeDataToD(string strData)
        {
            Decimal dData = 0.0M;
            if (strData.Contains("E"))
            {
                dData = Convert.ToDecimal(Decimal.Parse(strData.ToString(), System.Globalization.NumberStyles.Float));
            }
            return dData;
        }

        public static string GetConnectionStringsConfig(string connectionName)
        {
            //指定config文件读取
            string file = System.Windows.Forms.Application.ExecutablePath;
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(file);
            string connectionString =
                config.ConnectionStrings.ConnectionStrings[connectionName].ConnectionString.ToString();
            return connectionString;
        }
    }
    /// <summary>
    /// 消息体参数对象
    /// </summary>
    class LoraMessage {
        public string CMD { get; set; }
        public int CODE { get; set; }
        public string AppEUI { get; set; }
        public int Token { get; set; }
        public string DevEUI { get; set; }
        public string MSG { get; set; }
        public string payload { get; set; }
        public int AppNonce { get; set; }
        public string Challenge { get; set; }
        public int Port { get; set; }
        public int PRIOR { get; set; }
        public bool Confirm { get; set; }
        public int Qlen { get;set; }
        public string TXGW { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Messaging;
using System.Configuration;

namespace TCPClient
{
    class MyQueue
    {

        //服务器地址
        private string Path = "";
        public MyQueue() {
            string ipaddr = GetAppConfig("ipaddr");
            Path = "FormatName:Direct=TCP:" + ipaddr + "\\private$\\queue";
        }
        public static bool flag = true;//心跳标志位
        /// <summary>
        /// 1.通过Create方法创建使用指定路径的新消息队列
        /// </summary>
        /// <param name="queuePath"></param>
        public void Createqueue(string queuePath)
        {
            try
            {
                if (!MessageQueue.Exists(queuePath))
                {
                    MessageQueue.Create(queuePath);
                }
                else
                {
                    Console.WriteLine(queuePath + "已经存在！");
                }
                Path = queuePath;
            }
            catch (MessageQueueException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        ///  2.连接消息队列并发送消息到队列
        /// 远程模式：MessageQueue rmQ = new MessageQueue("FormatName:Direct=OS:machinename//private$//queue");
        ///     rmQ.Send("sent to regular queue - Atul");对于外网的MSMQ只能发不能收
        ///     如果想要使用机器IP地址，表达式的语法为“FormatName:Direct=TCP:ipaddress//private$//queuename”
        ///例：
        ///   MessageQueue rmQ = new MessageQueue
        ///                                   ("FormatName:Direct=TCP:121.0.0.1//private$//queue");
        ///   rmQ.Send("sent to regular queue - Atul");
        /// </summary>
        public void  SendMessage(string message)
        {
            try
            {
                //连接到队列
                MessageQueue myQueue = new MessageQueue(Path);

                //MessageQueue myQueue = new MessageQueue("FormatName:Direct=TCP:192.168.12.79//Private$//myQueue1");

                //MessageQueue rmQ = new MessageQueue("FormatName:Direct=TCP:121.0.0.1//private$//queue");--远程格式

                Message myMessage = new Message();

                myMessage.Body = message;

                myMessage.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

                //发生消息到队列中

                myQueue.Send(myMessage);

                //return "发送成功！";

            }

            catch (ArgumentException e)
            {
               // return e.Message;
                //Console.WriteLine(e.Message);

            }
        }

        /// <summary>
        /// 3.连接消息队列并从队列中接收消息
        /// </summary>
        public object  ReceiveMessage()
        {
            MessageQueue myQueue = new MessageQueue(Path);

            myQueue.Formatter = new XmlMessageFormatter(new Type[]{typeof(string)});

            try
            {
                Message myMessage = myQueue.Receive(new TimeSpan(0, 0, 10));
                //myQueue.Peek();//接收后不消息从队列中移除
                object context = myMessage.Body;
                return context;
            }
            catch (MessageQueueException e)
            {
                //WriteLog.WriteError(e.Message);
                return "读取错误";
            }
            catch (InvalidCastException e)
            {
                //WriteLog.WriteError(e.Message);
                return "读取错误";
            }
        }

        /// <summary>
        /// 4.清空指定队列的消息
        /// </summary>
        public void ClearMessage()
        {
            MessageQueue myQueue = new MessageQueue(Path);
            myQueue.Purge();
        }

        /// <summary>
        /// 5.连接队列并获取队列的全部消息
        /// </summary>
        public string[] GetAllMessage()
        {
            try
            {
                MessageQueue myQueue = new MessageQueue(Path);

                Message[] allMessage = myQueue.GetAllMessages();

                XmlMessageFormatter formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
                string[] ss = new string[allMessage.Length];
               
                for (int i = 0; i < allMessage.Length; i++)
                {

                    allMessage[i].Formatter = formatter;
                    ss[i] = allMessage[i].Body as string;
                }
                //ClearMessage();
                return ss;
            }
            catch 
            {
                return null;
            }
        }

        public List<string> GetMessage()
        {
            try
            {
                MessageQueue myQueue = new MessageQueue(Path);
                myQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
                MessageEnumerator me = myQueue.GetMessageEnumerator2();
                string msg = string.Empty;
                List<string> msgs = new List<string>();
                while (me.MoveNext())
                {
                    msg = me.RemoveCurrent().Body.ToString();
                    msgs.Add(msg);
                }

                return msgs;
            }
            catch (Exception ex)
            {
                return null;
            }
           
        }

        public static string GetAppConfig(string strKey)
        {
            string file = System.Windows.Forms.Application.ExecutablePath;
            Configuration config = ConfigurationManager.OpenExeConfiguration(file);
            foreach (string key in config.AppSettings.Settings.AllKeys)
            {
                if (key == strKey)
                {
                    return config.AppSettings.Settings[strKey].Value.ToString();
                }
            }
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TCPClient
{
    public class WriteLog
    {
        private static StreamWriter streamWriter; //写文件    

        public static void WriteError(string message)
        {
            try
            {
                //DateTime dt = new DateTime();  
                string directPath = System.Windows.Forms.Application.StartupPath + "/log";    //在获得文件夹路径  
                if (!Directory.Exists(directPath))   //判断文件夹是否存在，如果不存在则创建  
                {
                    Directory.CreateDirectory(directPath);
                }
                directPath += string.Format(@"\{0}.log", DateTime.Now.ToString("yyyy-MM-dd"));
                if (streamWriter == null)
                {
                    streamWriter = !File.Exists(directPath) ? File.CreateText(directPath) : File.AppendText(directPath);    //判断文件是否存在如果不存在则创建，如果存在则添加。  
                }
                if (message != null)
                {
                    streamWriter.WriteLine(DateTime.Now.ToString("HH:mm:ss ")+ message);
                }
            }
            finally
            {
                if (streamWriter != null)
                {
                    streamWriter.Flush();
                    streamWriter.Dispose();
                    streamWriter = null;
                }
            }
        }
    }  
}

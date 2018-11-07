using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace TCPClient
{
    public class DBHelper
    {
        static string strConn = GetConnectionStringsConfig("connectString");
        /// 获取数据库连接字符串
        /// </summary>
        /// <param name="connectionName"></param>
        /// <returns></returns>
        public static string GetConnectionStringsConfig(string connectionName)
        {
            //指定config文件读取
            string file = System.Windows.Forms.Application.ExecutablePath;
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(file);
            string connectionString =
                config.ConnectionStrings.ConnectionStrings[connectionName].ConnectionString.ToString();
            return connectionString;
        }
        /// 执行非查询存储过程和SQL语句
        /// 增、删、改
        /// </summary>
        /// <param name="strSQL">要执行的SQL语句</param>
        /// <param name="paras">参数列表，没有参数填入null</param>
        /// <param name="cmdType">Command类型</param>
        /// <returns>返回影响行数</returns>
        public static int ExcuteSQL(string strSQL, SqlParameter[] paras, CommandType cmdType)
        {
            int i = 0;
            using (SqlConnection conn = new SqlConnection(strConn))
            {
                try
                {
                    SqlCommand cmd = new SqlCommand(strSQL, conn);
                    cmd.CommandType = cmdType;
                    if (paras != null)
                    {
                        cmd.Parameters.AddRange(paras);
                    }
                    conn.Open();
                    i = cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    WriteLog.WriteError(ex.Message);
                }
                finally {
                    conn.Close();
                }
                
            }
            return i;
        }
        /// <summary>
        /// 执行查询，返回DataTable对象
        /// </summary>
        /// <param name="strSQL">sql语句</param>
        /// <param name="pas">参数数组</param>
        /// <param name="cmdtype">Command类型</param>
        /// <returns>DataTable对象</returns>
        public static DataTable GetTable(string strSQL, SqlParameter[] pas, CommandType cmdtype)
        {
            DataTable dt = new DataTable(); ;
            using (SqlConnection conn = new SqlConnection(strConn))
            {
                try
                {
                    SqlDataAdapter da = new SqlDataAdapter(strSQL, conn);
                    da.SelectCommand.CommandType = cmdtype;
                    if (pas != null)
                    {
                        da.SelectCommand.Parameters.AddRange(pas);
                    }
                    da.Fill(dt);
                }
                catch (Exception ex) {
                    WriteLog.WriteError(ex.Message);
                }
            }
            return dt;
        }
        /// <summary>
        /// 返回Table的第一行第一列
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static int GetFirst(string strSQL, SqlParameter[] pas, CommandType cmdtype)
        {
            DataTable dt = GetTable(strSQL, pas, cmdtype);
            int r = 0;
            if (dt != null && dt.Rows.Count > 0) {
                r = Convert.ToInt32(dt.Rows[0][0]);
            }
            return r;
        }

        /// <summary>    
        /// 将BCD一字节数据转换到byte 十进制数据    
        /// </summary>    
        /// <param name="b" />字节数    
        /// <returns>返回转换后的BCD码</returns>    
        public static byte ConvertBCDToInt(byte b)
        {
            //高四位    
            byte b1 = (byte)((b >> 4) & 0xF);
            //低四位    
            byte b2 = (byte)(b & 0xF);

            return (byte)(b1 * 10 + b2);
        }
    }
}

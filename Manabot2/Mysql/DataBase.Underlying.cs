using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.Mysql
{
    public partial class DataBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DataBase));

        public bool busy = false;
        public static DataBase? me => ConnectionPool.LastInstance?.Connection;
        public MySqlConnection sql;

        public bool connected { get => sql.State == ConnectionState.Open; }

        #region 底层封装
        public DataBase(MySqlConnection sql)
        {
            this.sql = sql;
        }

        [Obsolete("不必再使用此函数。connected属性现在会直接检查连接状态")]
        public bool checkconnection()
        {
            //connected = ;
            return connected;
        }

        public bool execsql(string cmd_, Dictionary<string, object> args)
        {
            busy = true;
            lock (sql)
            {
                checkconnection();
                try
                {
                    log.Debug(cmd_);
                    using (MySqlCommand cmd = new MySqlCommand(cmd_, sql))
                    {
                        foreach (KeyValuePair<string, object> arg in args)
                        {
                            cmd.Parameters.AddWithValue(arg.Key, arg.Value);
                        }
                        cmd.ExecuteNonQuery();
                    }
                    busy = false;
                    return true;
                }
                catch (Exception e)
                {
                    log.Error(e.Message);
                    //connected = false;
                    busy = false;
                    return false;
                }
            }
        }

        public struct ArgPack
        {
            public string Name;
            public object Value;
            public MySqlDbType Type;
        }

        public bool execsql(string cmd_, Dictionary<string, ArgPack> args)
        {
            busy = true;
            lock (sql)
            {
                checkconnection();
                try
                {
                    log.Debug(cmd_);
                    using (MySqlCommand cmd = new MySqlCommand(cmd_, sql))
                    {
                        foreach (KeyValuePair<string, ArgPack> arg in args)
                        {
                            cmd.Parameters.Add(arg.Key, arg.Value.Type).Value = arg.Value.Value;
                        }
                        cmd.ExecuteNonQuery();
                    }
                    busy = false;
                    return true;
                }
                catch (Exception e)
                {
                    log.Error(e.Message);
                    //connected = false;
                    busy = false;
                    return false;
                }
            }
        }

        public bool execsql(string cmd_, Dictionary<string, object> args, out int rolls)
        {
            busy = true;
            lock (sql)
            {
                checkconnection();
                try
                {
                    log.Debug(cmd_);
                    using (MySqlCommand cmd = new MySqlCommand(cmd_, sql))
                    {
                        foreach (KeyValuePair<string, object> arg in args)
                        {
                            cmd.Parameters.AddWithValue(arg.Key, arg.Value);
                        }
                        rolls = cmd.ExecuteNonQuery();
                    }
                    busy = false;
                    return true;
                }
                catch (Exception e)
                {
                    log.Error(e.Message);
                    //connected = false;
                    busy = false;
                    rolls = 0;
                    return false;
                }
            }
        }


        public int execsql_firstmatch(string sqlc, Dictionary<string, object> args)
        {
            busy = true;
            lock (sql)
            {
                try
                {
                    checkconnection();
                    int id = -1;
                    using (MySqlCommand cmd = new MySqlCommand(sqlc, sql))
                    {
                        foreach (KeyValuePair<string, object> arg in args)
                        {
                            cmd.Parameters.AddWithValue(arg.Key, arg.Value);
                        }
                        if (cmd.ExecuteScalar() != null)
                        {
                            id = (int)cmd.ExecuteScalar();
                        }
                    }
                    busy = false;
                    return id;
                }
                catch (Exception e)
                {
                    log.Error(e.Message);
                    //connected = false;
                    busy = false;
                    return -1;
                }
            }
        }

        public List<List<string>> querysql(string cmd_, Dictionary<string, object> args, List<int> rolls)
        {
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(cmd_, sql))
                {
                    busy = true;
                    lock (sql)
                    {
                        checkconnection();
                        foreach (KeyValuePair<string, object> arg in args)
                        {
                            cmd.Parameters.AddWithValue(arg.Key, arg.Value);
                        }
                        MySqlDataReader mdr = cmd.ExecuteReader();
                        List<List<string>> data = new List<List<string>>();
                        if (mdr.HasRows)
                        {
                            while (mdr.Read())
                            {
                                List<string> line = new List<string>();
                                foreach (int roll in rolls)
                                {
                                    line.Add(mdr.GetString(roll));
                                }
                                data.Add(line);
                            }
                        }
                        mdr.Close();
                        busy = false;
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                //connected = false;
                return null;
            }
        }

        public int count(string sql, Dictionary<string, object> args)
        {
            checkconnection();
            List<int> rolls = new List<int>
            {
                0
            };
            string rtv = querysql(sql, args, rolls)[0][0];
            return int.Parse(rtv);
        }

        public static string get_uft8(string unicodeString)
        {
            UTF8Encoding utf8 = new UTF8Encoding();
            byte[] encodedBytes = utf8.GetBytes(unicodeString);
            string decodedString = utf8.GetString(encodedBytes);
            return decodedString;
        }

        #endregion


        public static string DatetimeConvert(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static DateTime GetDateTime(long strLongTime)
        {
            long begtime = strLongTime * 10000000;//100毫微秒为单位,textBox1.text需要转化的int日期
            DateTime dt_1970 = new DateTime(1970, 1, 1, 8, 0, 0);
            long tricks_1970 = dt_1970.Ticks;//1970年1月1日刻度
            long time_tricks = tricks_1970 + begtime;//日志日期刻度
            DateTime dt = new DateTime(time_tricks);//转化为DateTim
            return dt;
        }
    }
}

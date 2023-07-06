using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.Mysql
{
    internal class ConnectionPool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConnectionPool));
        public static ConnectionPool? LastInstance;
        private List<DataBase> LiveConnections = new List<DataBase>();
        private SqlDbConfig dbconfig;
        public int ConnectionCount { private set; get; }
        public ConnectionPool(SqlDbConfig dbcfg, int connections)
        {
            ConnectionCount = connections;
            dbconfig = dbcfg;
            string conStr = "server=" + dbconfig.ServerAddress +
                ";port=" + dbconfig.ServerPort +
                ";user=" + dbconfig.UserName +
                ";password=\"" + dbconfig.UserPassword +
                "\"; database=" + dbconfig.DbName +
                ";Allow User Variables=True";
            for (int i = 0; i < ConnectionCount; i++)
            {
                var sql = new MySqlConnection(conStr);
                sql.OpenAsync();
                LiveConnections.Add(new DataBase(sql));
            }
            LastInstance = this;
        }

        public DataBase Connection
        {
            get
            {
                log.Debug("Acquiring lend connection lock...");
                lock (LiveConnections)
                {
                    string conStr = "server=" + dbconfig.ServerAddress +
                    ";port=" + dbconfig.ServerPort +
                    ";user=" + dbconfig.UserName +
                    ";password=\"" + dbconfig.UserPassword +
                    "\"; database=" + dbconfig.DbName +
                    ";Allow User Variables=True";
                    int i = 0;
                    while (true)
                    {
                        log.Debug("Lending connection...");
                        List<DataBase> abandoned_conns = new List<DataBase>();
                        foreach (var cn in LiveConnections)
                        {
                            if (cn.busy) continue;
                            if (cn.sql.State == System.Data.ConnectionState.Connecting) continue;
                            if (cn.sql.State != System.Data.ConnectionState.Open)
                            {
                                abandoned_conns.Add(cn);
                            }
                            else
                            {
                                log.Debug("Connection lent.");
                                return cn;
                            }
                        }

                        foreach (var cn in abandoned_conns)
                        {
                            LiveConnections.Remove(cn);
                            var sql = new MySqlConnection(conStr);
                            sql.OpenAsync();
                            LiveConnections.Add(new DataBase(sql));
                        }
                        i++;
                        if (i == 5)
                        {
                            i = 0;
                            log.Warn("Failed to allocate db connection after retrying for 5 times.");
                            log.Warn("Connection to the database might be down.");
                            log.Warn("Will try again after 30s.");
                            Thread.Sleep(30000);
                        }
                        log.Debug("No live connections! Will retry after 500 ms.");
                        Thread.Sleep(500);
                    }
                }
            }
        }

        public struct SqlDbConfig
        {
            public string ServerAddress,
                DbName, UserName, UserPassword;
            public int ServerPort;
        }
    }
}

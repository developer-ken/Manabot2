using BiliApi;
using BiliveDanmakuAgent;
using Manabot2.EventHandlers;
using Mirai.CSharp.HttpApi.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Manabot2.Mysql.ConnectionPool;

namespace Manabot2
{
    internal static class Global
    {
        public static string Server = String.Empty, Key = String.Empty;
        public static long MyQQ, StreammerUID, LiveroomId, LogGroup;
        public const long Streammer = 1249695750;
        public const long CrewGroup = 781858343;
        public static int Port;
        public static IMiraiHttpSession qqsession;
        public static BiliSession bilisession;
        public static BiliLiveRoom bliveroom;
        public static DanmakuApi liveroom;
        public static BiliDanmakuHandler danmakuhan;
        public static BiliPrivHandler privhan;
        public static SqlDbConfig dbcfg;
        public static List<Task> BackgroundTasks = new List<Task>();
        public static List<long> LevelLowQQs = new List<long>();
        public static bool IsLive = false;

        public static int GetUnixTimestamp(DateTime time)
        {
            return (int)(time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}

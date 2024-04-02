using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.Mysql
{
    public partial class DataBase
    {
        /// <summary>
        /// 记录一条用户上舰
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="len"></param>
        /// <param name="level"></param>
        /// <param name="lid"></param>
        /// <returns></returns>
        public bool recUserBuyGuard(long uid, int len, int level, int lid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid },
                { "@len", len },
                { "@level", level },
                { "@lid", lid }
            };
            //SELECT * from (SELECT * FROM A ORDER BY time) a GROUP BY a.id;
            return execsql("INSERT INTO bili_crew (uid, len, level, lid, timestamp) VALUES (@uid, @len, @level, @lid, NOW());", args);
        }

        /// <summary>
        /// 记录直播开始
        /// </summary>
        /// <param name="lid"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public bool recBLive(int lid, string title)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@lid", lid},
                { "@title", title }
            };
            return execsql("INSERT INTO bili_lives (lid, livetime, livetitle) VALUES (@lid, Now(), @title);", args);
        }

        /// <summary>
        /// 记录直播结束
        /// </summary>
        /// <param name="lid"></param>
        /// <param name="newviersers"></param>
        /// <param name="act_viewers"></param>
        /// <param name="peakviewers"></param>
        /// <param name="selvercoins"></param>
        /// <param name="goldcoins"></param>
        /// <returns></returns>
        public bool recBLiveEnd(int lid, int newviersers, int act_viewers, int peakviewers = 0, int selvercoins = 0, int goldcoins = 0)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@lid", lid},
                { "@nvs", newviersers},
                { "@pvs", peakviewers},
                { "@scn", selvercoins},
                { "@gcn", goldcoins},
                { "@act", act_viewers}
            };
            return execsql("UPDATE bili_lives SET liveend = Now(), newviewers = @nvs, peakviewers = @pvs, gold_coins = @gcn, selver_coins = @scn, activeviewers = @act WHERE lid = @lid;", args);
        }

        public bool isCrewGroup(long group)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@gpid", group}
            };
            bool res = count("SELECT COUNT(*) from qq_crewgroup where gpid like @gpid ;", args) > 0;
            return res;
        }

        public bool isBiliUserGuard(long uid)
        {
            return getBiliUserGuardCount(uid) > 0;
        }

        public int getBiliUserGuardCount(long uid)
        {

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid}
            };
            return count("SELECT COUNT(*) from bili_crew where uid like @uid ;", args);
        }

        public bool isUserBlacklisted(long user)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@qq", user}
            };
            return (count("SELECT COUNT(*) from blacklist_q where qq like @qq ;", args) > 0);
        }

        public bool isUserOperator(long user)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@qq", user}
            };
            return (count("SELECT COUNT(*) from qq_operator where qq like @qq ;", args) > 0);
        }

        public bool isUserBoundedQQ(long uid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid}
            };
            return (count("SELECT COUNT(*) from bili_qqbound where uid like @uid and qq is not null and type <= 5;", args) > 0);
        }

        public bool isUserBoundedQQOrPending(long uid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid}
            };
            return (count("SELECT COUNT(*) from bili_qqbound where uid like @uid and qq is not null;", args) > 0);
        }

        public bool isSessionExists(long uid, string cid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid},
                { "@cid", cid}
            };
            return (count("SELECT COUNT(*) from webapi_auth_state where uid like @uid and cid like @cid;", args) > 0);
        }

        public void setSession(long uid, string cid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid},
                { "@cid", cid}
            };
            execsql("INSERT INTO webapi_auth_state (cid, uid) VALUES (@cid, @uid) ON DUPLICATE KEY UPDATE uid = @uid;", args);
        }

        public void removeSession(string cid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@cid", cid}
            };
            execsql("DELETE FROM webapi_auth_state WHERE cid = @cid;", args);
        }

        public long getUserBoundedQQ(long uid)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid}
                };
                List<int> vs = new List<int>
                {
                    1
                };
                List<List<string>> re = querysql("SELECT * from bili_qqbound where uid like @uid and qq is not null and type <= 5;", args, vs);
                long group = 0;
                foreach (List<string> line in re)
                {
                    long gpn = long.Parse(line[0]);
                    group = gpn;
                }
                return group;
            }
            catch (Exception e)
            {
                log.Error("Unable to determin user_bounded_qq", e);
                return -1;
            }
        }

        public bool isUserBoundedUID(long qq)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@qq", qq}
            };
            return (count("SELECT COUNT(*) from bili_qqbound where qq like @qq and uid is not null and type <= 5;", args) > 0);
        }

        public long getUserBoundedUID(long qq)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@qq", qq}
                };
                List<int> vs = new List<int>
                {
                    0
                };
                List<List<string>> re = querysql("SELECT * from bili_qqbound where qq like @qq and type <= 5;", args, vs);
                long group = 0;
                foreach (List<string> line in re)
                {
                    long gpn = long.Parse(line[0]);
                    group = gpn;
                }
                return group;
            }
            catch
            {
                return 0;
            }
        }

        public bool boundBiliWithQQ(long uid, long qq)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid},
                    { "@qq", qq}
                };
            execsql("UPDATE bili_qqbound SET qq = @qq , type = 0 WHERE uid = @uid;", args, out int a);
            return (a > 0);
        }

        public bool setCrewAuthCode(long uid, int authcode)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid},
                    { "@code", authcode}
                };
            execsql("INSERT INTO bili_qqbound (uid, qq, type) VALUES (@uid, @code, 10) ON DUPLICATE KEY UPDATE qq = @code , type = 10;", args, out int a);
            return (a > 0);
        }

        public long getUidByAuthcode(int authcode)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@qq", authcode}
                };
                List<int> vs = new List<int>
                {
                    0
                };
                List<List<string>> re = querysql("SELECT * from bili_qqbound where qq like @qq and type = 10;", args, vs);
                long group = 0;
                foreach (List<string> line in re)
                {
                    long gpn = long.Parse(line[0]);
                    group = gpn;
                }
                return group;
            }
            catch
            {
                return 0;
            }
        }

        public int getAuthcodeByUid(long uid)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@uid", uid}
            };
            List<int> vs = new List<int>
            {
                1
            };
            List<List<string>> re = querysql("SELECT * from bili_qqbound where uid like @uid and qq is not null and type = 10;", args, vs);
            int group = 0;
            foreach (List<string> line in re)
            {
                int gpn = int.Parse(line[0]);
                group = gpn;
            }
            return group;
        }

        public string getClaimedActivationCode(long uid)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@claimer", uid}
                };
                List<int> vs = new List<int>
                {
                    1
                };
                List<List<string>> re = querysql("SELECT * from activation_codes where claimer like @claimer;", args, vs);
                string group = null;
                foreach (List<string> line in re)
                {
                    string gpn = line[0];
                    group = gpn;
                }
                return group;
            }
            catch
            {
                return null;
            }
        }

        public string claimActivationCode(long uid)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@claimer", uid}
                };
                List<int> vs = new List<int>
                {
                    1
                };
                List<List<string>> re = querysql("START TRANSACTION;\r\nSELECT @id:=id, @code:=code FROM activation_codes WHERE claimer = 0 LIMIT 1;\r\nUPDATE activation_codes SET claimer = @claimer WHERE id = @id;\r\nCOMMIT;", args, vs);
                string group = null;
                foreach (List<string> line in re)
                {
                    string gpn = line[0];
                    group = gpn;
                }
                return group;
            }
            catch
            {
                return null;
            }
        }

        public bool setUserUUID(long uid, string UUID)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid},
                    { "@code", UUID}
                };
            execsql("INSERT INTO webauth_uuid (uuid, uid) VALUES (@code, @uid) ON DUPLICATE KEY UPDATE uuid = @code;", args, out int a);
            return (a > 0);
        }

        public string getUserUUIDbyUID(long uid)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid}
                };
                List<int> vs = new List<int>
                {
                    0
                };
                List<List<string>> re = querysql("SELECT * from webauth_uuid where uid like @uid and uuid is not null;", args, vs);
                string group = null;
                foreach (List<string> line in re)
                {
                    group = line[0];
                }
                return group;
            }
            catch (Exception e)
            {
                log.Error("Unable to determin user uuid by uid provided", e);
                return null;
            }
        }

        public DateTime GetLatestCrewRecordTime(long uid)
        {
            try
            {
                Dictionary<string, object> args = new Dictionary<string, object>
                {
                    { "@uid", uid}
                };
                List<int> vs = new List<int>
                {
                    3
                };
                List<List<string>> re = querysql("SELECT * FROM bili_crew WHERE uid = @uid order by id desc LIMIT 1;", args, vs);
                DateTime group = default;
                foreach (List<string> line in re)
                {
                    string gpn = line[0];
                    group = DateTime.Parse(gpn);
                }
                return group;
            }
            catch
            {
                return default;
            }
        }

        public bool removeUserBlklist(long q)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@qq", q}
            };
            return execsql("delete from blacklist_q where qq = @qq;", args);
        }

        public bool addUserBlklist(long q, string reason, long op)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@qq", q},
                { "@reason", reason },
                { "@op", op}
            };
            execsql("delete from blacklist_q where qq = @qq;", args);//清除这个记录(如果有)
            return execsql("INSERT INTO blacklist_q (qq, reason, operator, ban_time) VALUES (@qq, @reason, @op, NOW());", args);
        }
    }
}

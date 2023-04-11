using BiliApi;
using BiliApi.BiliPrivMessage;
using BiliveDanmakuAgent;
using BiliveDanmakuAgent.Core;
using log4net;
using Manabot2.Mysql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.EventHandlers
{
    internal class BiliDanmakuHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BiliDanmakuHandler));
        private LiveRoom lr;
        private BiliLiveRoom blr;
        private Random rand;
        public int lid { get; private set; }
        public BiliDanmakuHandler(BiliSession session, int liveid)
        {
            string str = "";
            foreach (Cookie c in session.CookieContext)
            {
                str += c.Name + "=" + c.Value + ";";
            }
            lr = new LiveRoom(liveid, str);
            lr.sm.ReceivedDanmaku += Sm_ReceivedDanmaku;
            lr.sm.StreamStarted += Sm_StreamStarted;
            lr.init_connection();
            blr = new BiliLiveRoom(liveid, session);
        }

        private void Sm_StreamStarted(object sender, BiliveDanmakuAgent.Core.StreamStartedArgs e)
        {
            //throw new NotImplementedException();
        }

        private void Sm_ReceivedDanmaku(object sender, BiliveDanmakuAgent.Core.ReceivedDanmakuArgs e)
        {
            switch (e.Danmaku.MsgType)
            {
                case MsgTypeEnum.GuardBuy:
                    string dpword = "??未知??";
                    switch (e.Danmaku.UserGuardLevel)
                    {
                        case 1:
                            dpword = "总督";
                            break;
                        case 2:
                            dpword = "提督";
                            break;
                        case 3:
                            dpword = "舰长";
                            break;
                    }
                    bool isnew = !DataBase.me.isBiliUserGuard(e.Danmaku.UserID);
                    if (lid > 0)
                    {
                        if (isnew)
                        {
                            blr.sendDanmaku("欢迎新" + dpword + "！请留意私信哦~");
                        }
                        else
                        {
                            blr.sendDanmaku("感谢<" + e.Danmaku.UserName + ">续航" + dpword + "！");
                        }
                    }
                    else
                    {
                        if (isnew)
                        {
                            blr.sendDanmaku("欢迎新 虚空·" + dpword + "！请留意私信哦~");
                        }
                        else
                        {
                            blr.sendDanmaku("感谢<" + e.Danmaku.UserName + ">续航 虚空·" + dpword + "！");
                        }
                    }
                    DataBase.me.recUserBuyGuard(e.Danmaku.UserID, e.Danmaku.GiftCount, e.Danmaku.UserGuardLevel, lid);


                    var timestamp = TimestampHandler.GetTimeStamp(DateTime.Now);

                    if (!DataBase.me.isUserBoundedQQOrPending(e.Danmaku.UserID))
                    {
                    generate_auth_code:
                        int authcode = rand.Next(100000, 999999);
                        if (DataBase.me.getUidByAuthcode(authcode) > 0) goto generate_auth_code; // 发生冲突

                        DataBase.me.setCrewAuthCode(e.Danmaku.UserID, authcode);

                        PrivMessageSession session = PrivMessageSession.openSessionWith(e.Danmaku.UserID, GlobalVar.bilisession);
                        session.sendMessage("感谢您加入鹿野灸的大航海！\n舰长QQ群号：781858343\n加群验证码：" + authcode + "\n加群时，请使用上面的6位验证码作为验证问题的答案。\n验证码使用后即刻失效，请勿外传。");
                        session.Close();
                    }
                    break;
            }
        }
    }
}

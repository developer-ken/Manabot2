using BiliApi;
using BiliApi.BiliPrivMessage;
using BiliveDanmakuAgent;
using BiliveDanmakuAgent.Core;
using log4net;
using Manabot2.Mysql;
using Mirai.CSharp.HttpApi.Models.ChatMessages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private Random rand = new Random();
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
            GlobalVar.IsLive = true;
            if (GlobalVar.LevelLowQQs.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[自动消息]");
                sb.AppendLine("有以下舰长QQ号等级不足16，需要添加好友：");
                foreach (var qn in GlobalVar.LevelLowQQs)
                {
                    sb.AppendLine(qn.ToString());
                }
                GlobalVar.LevelLowQQs.Clear();
                GlobalVar.qqsession.SendFriendMessageAsync(GlobalVar.Streammer, new PlainMessage(sb.ToString()));
            }
        }

        private void Sm_ReceivedDanmaku(object sender, BiliveDanmakuAgent.Core.ReceivedDanmakuArgs e)
        {
            switch (e.Danmaku.MsgType)
            {
                case MsgTypeEnum.LiveEnd:
                    {
                        GlobalVar.IsLive = false;
                        if (GlobalVar.LevelLowQQs.Count > 0)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("[自动消息]");
                            sb.AppendLine("有以下舰长QQ号等级不足16，需要添加好友：");
                            foreach (var qn in GlobalVar.LevelLowQQs)
                            {
                                sb.AppendLine(qn.ToString());
                            }
                            GlobalVar.LevelLowQQs.Clear();
                            GlobalVar.qqsession.SendFriendMessageAsync(GlobalVar.Streammer, new PlainMessage(sb.ToString()));
                        }
                    }
                    break;
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

                    log.Debug("SendCode?");
                    if (!DataBase.me.isUserBoundedQQOrPending(e.Danmaku.UserID))
                    {
                        SendCrewCode(e.Danmaku.UserID);
                    }
                    break;
            }
        }

        public void SendCrewCode(long userid)
        {
        generate_auth_code:
            int authcode = rand.Next(100000, 999999);
            log.Debug("Try code:" + authcode);
            if (DataBase.me.getUidByAuthcode(authcode) > 0)
            {
                log.Debug("Conflict! Generate new one...");
                goto generate_auth_code; // 发生冲突
            }

            log.Debug("Will use that code. Send to db...");
            DataBase.me.setCrewAuthCode(userid, authcode);

            log.Debug("Send priv message...");
            PrivMessageSession session = PrivMessageSession.openSessionWith(userid, GlobalVar.bilisession);
            session.sendMessage("感谢您加入鹿野灸的大航海！\n舰长QQ群号：781858343\n加群验证码：" + authcode + "\n加群时，请使用上面的6位验证码作为验证问题的答案。\n验证码使用后即刻失效，请勿外传。");
            session.Close();
            log.Info("Use code:" + authcode);
        }
    }
}

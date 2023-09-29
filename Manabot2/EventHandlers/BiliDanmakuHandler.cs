using BiliApi;
using BiliApi.BiliPrivMessage;
using BiliveDanmakuAgent;
using BiliveDanmakuAgent.Model;
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
        private DanmakuApi lr;
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
            lr = new DanmakuApi(liveid, str);
            //lr.sm.ReceivedDanmaku += Sm_ReceivedDanmaku;
            //lr.sm.StreamStarted += Sm_StreamStarted;
            lr.ConnectAsync().Wait();
            lr.DanmakuMsgReceivedEvent += Sm_ReceivedDanmaku;
            lr.LiveStartEvent += Sm_StreamStarted;
            blr = new BiliLiveRoom(liveid, session);
        }

        private void Sm_StreamStarted(object sender, BiliveDanmakuAgent.Model.RoomEventArgs e)
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

        private void Sm_ReceivedDanmaku(object sender, BiliveDanmakuAgent.Model.DanmakuReceivedEventArgs e)
        {
            switch (e.Danmaku.MsgType)
            {
                case DanmakuMsgType.LiveEnd:
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
                case DanmakuMsgType.GuardBuy:
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
            var uuuid = DataBase.me.getUidByAuthcode(authcode);
            if (uuuid > 0 && uuuid != userid)
            {
                log.Debug("Conflict! Generate new one...");
                goto generate_auth_code; // 发生冲突
            }

            log.Debug("Will use that code. Send to db...");
            for (int a = 1; a <= 5 && !DataBase.me.setCrewAuthCode(userid, authcode); a++)
            {
                log.Warn("DB Error! Retry (" + a + "/5)");
                if (a >= 5)
                {
                    log.Error("Failed to save AuthCode to database after 5 (re)tries.");
                    PrivMessageSession session1 = PrivMessageSession.openSessionWith(userid, GlobalVar.bilisession);
                    session1.sendMessage("感谢您加入鹿野灸的大航海！\nAuthCode(#" + authcode + ")\nDB_CONNECTION_FALIURE\n请与舰长群技术负责人鸡蛋(QQ:1250542735)取得联系并提供本页面截图，他将帮助您登记信息、加入舰长群。");
                    session1.Close();

                    log.Info("Bili UID:" + userid);
                    log.Info("Use code:" + authcode);
                    log.Warn("ATTENTION! Additional manual operation required.");
                    GlobalVar.qqsession.SendGroupMessageAsync(GlobalVar.LogGroup, new PlainMessage("#" + userid + "\n新上舰，正确的验证码为：" + authcode + "\n ### 中央数据库断开，请人工核对入群申请 ###"));
                    return;
                }
            }

            log.Debug("Send priv message...");
            PrivMessageSession session = PrivMessageSession.openSessionWith(userid, GlobalVar.bilisession);
            session.sendMessage("感谢您加入鹿野灸的大航海！\n舰长QQ群号：781858343\n加群验证码：" + authcode + "\n加群时，请使用上面的6位验证码作为验证问题的答案。\n验证码使用后即刻失效，请勿外传。");
            session.Close();
            log.Info("Bili UID:" + userid);
            log.Info("Use code:" + authcode);
            GlobalVar.qqsession.SendGroupMessageAsync(GlobalVar.LogGroup, new PlainMessage("#" + userid + "\n新上舰，已发送验证码"));
        }
    }
}

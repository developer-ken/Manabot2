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
            lr.LiveEndEvent += Sm_StreamStopped;
            blr = new BiliLiveRoom(liveid, session);
        }

        private void Sm_StreamStarted(object sender, BiliveDanmakuAgent.Model.RoomEventArgs e)
        {
            Global.IsLive = true;
            if (lid <= 0)
            {
                lid = Global.GetUnixTimestamp(DateTime.Now);
                DataBase.me.recBLive(lid, e.RoomInfo.Title);
                Global.qqsession.SendGroupMessageAsync(Global.LogGroup, new PlainMessage($"[直播开始#{lid}]\n{e.RoomInfo.Title}"));
            }
            else
            {
                Global.qqsession.SendGroupMessageAsync(Global.LogGroup, new PlainMessage($"[直播恢复#{lid}]\n{{主播->B站}}直播推流可能不稳定。"));
            }
            if (Global.LevelLowQQs.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[自动消息]");
                sb.AppendLine("有以下舰长QQ号等级不足16，需要添加好友：");
                foreach (var qn in Global.LevelLowQQs)
                {
                    sb.AppendLine(qn.ToString());
                }
                Global.LevelLowQQs.Clear();
                Global.qqsession.SendFriendMessageAsync(Global.Streammer, new PlainMessage(sb.ToString()));
            }
        }

        private void Sm_StreamStopped(object sender, BiliveDanmakuAgent.Model.RoomEventArgs e)
        {
            Global.IsLive = false;
            DataBase.me.recBLiveEnd(lid, -1, -1, -1, -1, -1);
            Global.qqsession.SendGroupMessageAsync(Global.LogGroup, new PlainMessage($"[直播结束#{lid}]\n根据系统策略，不允许记录收益和粉丝数据"));
            lid = -1;
        }

        private async void Sm_ReceivedDanmaku(object sender, BiliveDanmakuAgent.Model.DanmakuReceivedEventArgs e)
        {
            await Task.Run(() =>
            {
                try
                {
                    switch (e.Danmaku.MsgType)
                    {
                        case DanmakuMsgType.LiveEnd:
                            {
                                Global.IsLive = false;
                                if (Global.LevelLowQQs.Count > 0)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine("[自动消息]");
                                    sb.AppendLine("有以下舰长QQ号等级不足16，需要添加好友：");
                                    foreach (var qn in Global.LevelLowQQs)
                                    {
                                        sb.AppendLine(qn.ToString());
                                    }
                                    Global.LevelLowQQs.Clear();
                                    Global.qqsession.SendFriendMessageAsync(Global.Streammer, new PlainMessage(sb.ToString()));
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
                catch (Exception ex)
                {
                    log.Error("Error happened when processing danmaku.", ex);
                    log.Error("ContentDump:");
                    log.Error(e.Danmaku.RawString ?? "_NULL_CONTENT_");
                }
            }).ConfigureAwait(false);
        }

        public void SendCrewCode(long userid)
        {
            var authcode = DataBase.me.getAuthcodeByUid(userid);
        generate_auth_code:
            if (authcode > 0)
            {
                //使用之前的验证码
                log.Debug("Already have an authcode for that uid. Just reuse it.");
            }
            else
            {
                //生成新的验证码
                authcode = rand.Next(100000, 999999);
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
                        PrivMessageSession session1 = PrivMessageSession.openSessionWith(userid, Global.bilisession);
                        session1.sendMessage("感谢您加入鹿野灸的大航海！\nAuthCode(#" + authcode + ")\nDB_CONNECTION_FALIURE\n请与舰长群技术负责人鸡蛋(QQ:1250542735)取得联系并提供本页面截图，他将帮助您登记信息、加入舰长群。\n\n由于B站防打扰策略，请关注我或回复本条消息，以便接收后续通知！");
                        session1.Close();

                        log.Info("Bili UID:" + userid);
                        log.Info("Use code:" + authcode);
                        log.Warn("ATTENTION! Additional manual operation required.");
                        Global.qqsession.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("#" + userid + "\n新上舰，正确的验证码为：" + authcode + "\n ### 中央数据库断开，请人工核对入群申请 ###"));
                        return;
                    }
                }
            }
            log.Debug("Send priv message...");
            PrivMessageSession session = PrivMessageSession.openSessionWith(userid, Global.bilisession);
            session.sendMessage($"感谢您加入鹿野灸的大航海！\n" +
                $"舰长QQ群号：{Global.CrewGroup}\n" +
                $"加群验证码：{authcode}\n" +
                $"加群时，请使用上面的6位验证码作为验证问题的答案。\n" +
                $"如果您已经在群里了，可以回复\"绑定:QQ号\"来完成信息绑定(如\"绑定:1250542735\")，这样就不会再次收到此消息了。\n" +
                $"验证码使用后即刻失效，请勿外传。\n\n" +
                $"如果您无法加群，请联系技术负责人鸡蛋(QQ:1250542735)并提供本页截图。\n\n请关注本账号或回复一条消息，以免B站免打扰系统拦截后续消息。");
            //session.sendMessage("感谢您加入鹿野灸的大航海！\n舰长QQ群号：781858343\n加群验证码：" + authcode + "\n加群时，请使用上面的6位验证码作为验证问题的答案。\n验证码使用后即刻失效，请勿外传。\n\n由于B站防打扰策略，请关注我或回复本条消息，以便接收后续通知！");
            session.Close();
            log.Info("Bili UID:" + userid);
            log.Info("Use code:" + authcode);
            Global.qqsession.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("#" + userid + "\n已发送验证码"));
        }
    }
}

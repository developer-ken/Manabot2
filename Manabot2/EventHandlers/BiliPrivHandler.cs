using BiliApi;
using BiliApi.BiliPrivMessage;
using log4net;
using Manabot2.Mysql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.EventHandlers
{
    public class BiliPrivHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BiliPrivHandler));
        PrivMsgReceiverLite sessmgn;
        BiliSession sess;
        volatile bool run = false;

        public BiliPrivHandler(BiliSession session)
        {
            sessmgn = new PrivMsgReceiverLite(session);
            sess = session;
        }

        public void Start()
        {
            run = true;
            new Thread(() =>
            {
                while (run)
                {
                    var sessions = sessmgn.GetNewSessions();
                    foreach (var ssess in sessions)
                    {
                        if (ssess.lastmessage.content is null) continue;
                        if (ssess.lastmessage.talker.uid == Global.bilisession.getCurrentUserId()) continue;
                        log.Debug($"priv: #{ssess.talker_id} >{ssess.lastmessage.content}");
                        if (ssess.lastmessage.content.Contains("激活码"))
                        {
                            log.Info($"#{ssess.talker_id} requesting activation code.");
                            if (EventHandler.IsCurrentlyCrew(ssess.talker_id))
                            {
                                var code = DataBase.me.getClaimedActivationCode(ssess.talker_id);
                                if (code is null || code.Length == 0)
                                {
                                    log.Info($"#{ssess.talker_id} new claim. code={code}");
                                    code = DataBase.me.claimActivationCode(ssess.talker_id);
                                    if (code is null || code.Length == 0)
                                        ssess.sendMessage($"激活码已经抢完，感谢您的支持！");
                                    else
                                        ssess.sendMessage($"感谢您参加本次活动。您的激活码是：\n{code}");
                                }
                                else
                                {
                                    log.Info($"#{ssess.talker_id} redundant. code={code}");
                                    ssess.sendMessage($"您已经领取过激活码了。您的激活码是：\n{code}");
                                }
                            }
                            else
                            {
                                log.Info($"#{ssess.talker_id} not crew. refuse.");
                                ssess.sendMessage("抱歉，只有在舰舰长可以获取激活码。");
                            }
                        }
                        if (ssess.lastmessage.content.Contains("验证码"))
                        {
                            log.Info($"#{ssess.talker_id} requesting authcode.");
                            var qq = DataBase.me.getUserBoundedQQ(ssess.talker_id);
                            if (qq > 0)
                            {
                                ssess.sendMessage($"您已经绑定了QQ：{qq}。\n发送\"绑定:xxx\"绑定新的QQ号，如\"绑定:1250542735\"。换绑后原先的QQ号将被移除群聊。\n如有疑问，请联系鸡蛋：QQ1250542735");
                            }
                            else
                            if (EventHandler.IsCrew(ssess.talker_id))
                            {
                                Global.danmakuhan.SendCrewCode(ssess.talker_id);
                            }
                            else
                            {
                                ssess.sendMessage("抱歉，目前验证码入群功能仅供鹿野灸舰长使用。");
                            }
                        }
                        {
                            var qq = DataBase.me.getUserBoundedQQ(ssess.talker_id);
                            var data = ssess.lastmessage.content.Split(':');
                            switch (data[0])
                            {
                                case "绑定":
                                    {
                                        if (long.TryParse(data[1], out long newqq))
                                        {
                                            DataBase.me.boundBiliWithQQ(ssess.talker_id, newqq);
                                            if (qq > 0)
                                            {
                                                ssess.sendMessage($"您已经绑定新QQ({newqq})，之前绑定的QQ({qq})将被清退。");
                                                Global.qqsession.KickMemberAsync(qq, Global.CrewGroup, "您通过B站绑定了新的QQ");
                                            }
                                            else
                                            {
                                                ssess.sendMessage($"您已经绑定QQ({newqq})。该QQ将能享受您B站UID对应的权益。");
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        Thread.Sleep(1000);
                    }
                    Thread.Sleep(10000);
                }
            }).Start();
        }
    }
}

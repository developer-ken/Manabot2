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
        static DateTime claimCodeStart = new DateTime(2024, 2, 1, 0, 0, 0);

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
                            //ssess.sendMessage($"由于高峰期服务器压力大，请在直播结束后领取验证码。给您带来不便，敬请谅解。——来自除夕夜系统崩了的鸡蛋🥚");
                            //if (EventHandler.IsCurrentlyCrew(ssess.talker_id))
                            if (/*DataBase.me.GetLatestCrewRecordTime(ssess.talker_id) > claimCodeStart || */EventHandler.IsCurrentlyCrew(ssess.talker_id)) //领取要求
                            {
                                var code = DataBase.me.getClaimedActivationCode(ssess.talker_id);
                                if (code is null || code.Length == 0)
                                {
                                    code = DataBase.me.claimActivationCode(ssess.talker_id);
                                    if (code is null || code.Length == 0)
                                    {
                                        log.Info($"#{ssess.talker_id} new claim FAILED. STOCK_OUT");
                                        ssess.sendMessage($"激活码已经抢完，请联系管理补货！");
                                    }
                                    else
                                    {
                                        log.Info($"#{ssess.talker_id} new claim. code={code}");
                                        ssess.sendMessage($"感谢您参加本次活动。您的激活码是：\n{code}");
                                    }
                                }
                                else
                                {
                                    log.Info($"#{ssess.talker_id} redundant. code={code}");
                                    ssess.sendMessage($"您已经领取过激活码了。您的激活码是：\n{code}");
                                }
                            }
                            else
                            {
                                log.Info($"#{ssess.talker_id} not crew / wrong time. refuse.");
                                ssess.sendMessage("我们无法自动核实您的信息。\n" +
                                    //"如果您确信这是一个故障，请联系技术负责人鸡蛋🥚，QQ：1250542735。\n" +
                                    "· 您也可以联系管理员蛋黄，QQ：3584384914。" +
                                    "请不要担心，我们通过多种方式统计舰队名单。即使您暂时无法被自动确认，人工确认仍然可用。");
                            }
                        }
                        else
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
                                ssess.sendMessage("抱歉，我们无法自动核实您的舰长身份。\n\n" +
                                    "怎么办？\n" +
                                    "· 如果您已经上舰，请将粉丝勋章展示在主页勋章墙上，然后重试。\n" +
                                    //"· 若问题依旧，请联系技术负责人鸡蛋🥚，QQ:1250542735\n" +
                                    "· 您也可以联系管理员蛋黄，QQ：3584384914。" +
                                    "请不要担心，我们通过多种方式统计舰队名单。即使您暂时无法被自动确认，人工确认仍然可用。");
                            }
                        }
                        else
                        if (ssess.lastmessage.content.Contains("我的链接"))
                        {
                            log.Info($"#{ssess.talker_id} requesting auth_uri.");
                            var auuid = DataBase.me.getUserUUIDbyUID(ssess.talker_id);
                            if (auuid is null || auuid.Length == 0)
                            {
                                auuid = Guid.NewGuid().ToString();
                                DataBase.me.setUserUUID(ssess.talker_id, auuid);
                            }
                            //ssess.SendImage(QRGen.Url2PNGByte($"https://fans.luye9.top/{auuid}"));
                            //Thread.Sleep(1000);
                            log.Info($"#{ssess.talker_id} sending auth_uri.");
                            ssess.sendMessage($"{auuid}\n[此条消息不作为舰长或消费凭证]\n如有疑问，请联系鸡蛋：QQ1250542735");
                        }
                        else
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
                    Thread.Sleep(5000);
                }
            }).Start();
        }
    }
}

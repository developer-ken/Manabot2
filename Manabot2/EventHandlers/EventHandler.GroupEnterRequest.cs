using Mirai.CSharp.HttpApi.Models.EventArgs;
using Mirai.CSharp.HttpApi.Parsers.Attributes;
using Mirai.CSharp.HttpApi.Parsers;
using Mirai.CSharp.HttpApi.Handlers;
using Mirai.CSharp.HttpApi.Session;
using Mirai.CSharp.Models;
using Manabot2.Mysql;
using log4net;
using System.Text.RegularExpressions;
using BiliApi.BiliPrivMessage;
using Mirai.CSharp.HttpApi.Models.ChatMessages;
using BiliApi;
using BiliApi.Modules;

namespace Manabot2.EventHandlers
{
    [RegisterMiraiHttpParser(typeof(DefaultMappableMiraiHttpMessageParser<IGroupApplyEventArgs, GroupApplyEventArgs>))]
    internal partial class EventHandler : IMiraiHttpMessageHandler<IGroupApplyEventArgs>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EventHandler));
        public async Task HandleMessageAsync(IMiraiHttpSession session, IGroupApplyEventArgs e)
        {
            log.Info("Group enter request at " + e.FromGroup + " by " + e.FromQQ);
            /* 只处理舰长群 */
            if (!DataBase.me.isCrewGroup(e.FromGroup))
            {
                log.Info("Not associated. Ignore.");
                return;
            }
            /* 加群验证流程开始 */

            //黑名单？
            if (DataBase.me.isUserBlacklisted(e.FromQQ))
            {
                log.Info(e.FromQQ + " is banned. Deny access.");
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "您在黑名单中。如有疑问请联系管理员。");
                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：已被拉黑"));
                return;
            }
            //已经绑定信息？
            if (DataBase.me.isUserBoundedUID(e.FromQQ))
            {
                var uid = DataBase.me.getUserBoundedUID(e.FromQQ);
                var profile = (await session.GetUserProfileAsync(e.FromQQ));
                //对应UID是舰长？
                if (DataBase.me.isBiliUserGuard(uid) || IsCurrentlyCrew(uid))
                    //QQ等级足够？
                    if (profile.Level < 16)
                    {
                        log.Info(e.FromQQ + " already registered.");
                        log.Info(e.FromQQ + " level low (" + profile.Level + "/16). Deny access.");
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, $"等级不足({profile.Level}/16)");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "拒绝进入舰长群：等级低(\" + profile.Level + \"/16)\n" + UserInfo(e.FromQQ, uid)
                            ));
                    }
                    else
                    {
                        log.Info(e.FromQQ + " already registered. Allow.");
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "允许进入舰长群：已绑定的QQ\n" + UserInfo(e.FromQQ, uid)
                            ));
                    }
                else
                {
                    await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                        "需要人工核对：无法核实消费信息\n" + UserInfo(e.FromQQ, uid)
                        ));
                }
                return;
            }
            var match = Regex.Match(e.Message, "([0-9]+)");
            if (match.Success)
            {
                int code = int.Parse(match.Value);
                if (code < 100000)
                {
                    log.Info("Wait for manual approval.");
                    await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                        "需要人工核对：没有提供验证码或UID\n" + UserInfo(e.FromQQ)
                        ));

                    return;
                }
                if (code > 999999)
                {
                    if (DataBase.me.isUserBoundedQQ(code))
                    {
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, $"UID已被使用");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "拒绝进入舰长群：用户提供的UID已经与其它QQ绑定\n" +
                            "[用户提供的信息]\n" +
                            UserInfo(e.FromQQ, code) + "\n" +
                            "[数据库中的信息]\n" +
                            UserInfo(DataBase.me.getUserBoundedQQ(code), code)
                            ));
                    }
                    else
                    if (DataBase.me.isBiliUserGuard(code) || IsCurrentlyCrew(code))
                    {
                        Global.danmakuhan.SendCrewCode(code);
                        log.Info("Sent new code to " + code);
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, $"已重发验证码，请查看B站私信");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "拒绝进入舰长群：已重发验证码，等待用户携带验证码再次申请\n⚠下列信息不一定准确，因为用户UID未经严密确认\n" + UserInfo(e.FromQQ, code)
                            ));
                    }
                    else
                    {
                        log.Info("No crew record, Wait for manual approval.");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "需要人工核对：无法核实消费信息\n" + UserInfo(e.FromQQ, code)
                            ));
                    }
                    return;
                }
                var uid = DataBase.me.getUidByAuthcode(code);
                if (DataBase.me.isBiliUserGuard(uid) || IsCurrentlyCrew(uid))
                    if (uid > 0)
                    {
                        DataBase.me.boundBiliWithQQ(uid, e.FromQQ);
                        var profile = (await session.GetUserProfileAsync(e.FromQQ));
                        if (profile.Level < 16)
                        {
                            log.Info(e.FromQQ + " -> #" + uid);
                            log.Info(e.FromQQ + " level low (" + profile.Level + "/16). Deny access.");
                            Global.LevelLowQQs.Add(e.FromQQ);
                            if (Global.IsLive)
                            {
                                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在直播结束后与您取得联系");
                                PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, Global.bilisession);
                                bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                    "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                    "· 鹿野将在直播后添加您的QQ好友\n" +
                                    "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                    "感谢您的理解与支持。 若对上述信息存在异议，或超过12小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                                bss.Close();
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                                    "拒绝进入舰长群：QQ等级不足，触发计划任务(直播进行中)\n" + UserInfo(e.FromQQ, uid)
                                    ));
                            }
                            else if (DateTime.Now.Hour > 23 || DateTime.Now.Hour < 8) //夜晚免打扰
                            {
                                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在白天与您取得联系");
                                PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, Global.bilisession);
                                bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                    "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                    "· 鹿野将添加您的QQ好友\n" +
                                    "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                    "感谢您的理解与支持。 若对上述信息存在异议，或超过24小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                                bss.Close();
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                                    "拒绝进入舰长群：QQ等级不足，触发计划任务(夜晚免打扰)\n" + UserInfo(e.FromQQ, uid)
                                    ));
                            }
                            else
                            {
                                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在稍后与您取得联系");
                                PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, Global.bilisession);
                                bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                    "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                    "· 鹿野将添加您的QQ好友\n" +
                                    "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                    "感谢您的理解与支持。 若对上述信息存在异议，或超过24小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                                bss.Close();
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                                    "拒绝进入舰长群：QQ等级不足，触发计划任务(正常模式)\n" + UserInfo(e.FromQQ, uid)
                                    ));
                            }
                            return;
                        }
                        else
                        {
                            log.Info(e.FromQQ + " -> #" + uid + ". Allow.");
                            await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);

                            PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, Global.bilisession);
                            bss.sendMessage("您已使用QQ" + e.FromQQ + "加入舰长群。此QQ将与您的Uid绑定，以后无需再进行验证。\n" +
                                "若对上述信息存在异议，请联系技术负责人鸡蛋(QQ:1250542735)");
                            bss.Close();
                            await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                                "允许进入舰长群：条件核验通过\n" + UserInfo(e.FromQQ, uid)
                                ));
                        }
                    }
                    else
                    {
                        log.Info("Wrong code (" + code + "), Wait for manual approval.");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                            "需要人工核对：数据库中不存在对应的验证码\n" + UserInfo(e.FromQQ)
                            ));
                    }
                else
                {
                    log.Info("No crew record, Wait for manual approval.");
                    await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                        "需要人工核对：无法核实消费信息\n" + UserInfo(e.FromQQ, uid)
                        ));
                }
            }
            else
            {
                log.Info("Wait for manual approval.");
                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage("[入群申请处理]\n" +
                    "需要人工核对：未提供验证码或UID，且数据库中不存在相应绑定\n" + UserInfo(e.FromQQ)
                    ));
            }
        }

        public string UserInfo(long qq = -1, long uid = -1)
        {
            Medal? medal = null;
            if (uid > 0)
            {
                medal = BiliUser.getMedals(Global.bilisession, uid, true).Find((a) => a.TargetId == Global.StreammerUID);
            }
            return "QQ：" + (qq > 0 ? qq.ToString() : "<无信息>") + "\n" +
                   "B站 - " + (uid <= 0 ? "<无信息>" : ("\n" +
                   "ID：" + (BiliUser.getUser(uid, Global.bilisession).name) + "#" + uid + "\n" +
                   "牌子:" + (medal is null ? "<无信息>" : (medal.Level + "," + medal.GuardLevel + (medal.GuardLevel > 0 ? ",<在舰>" : "")))));
        }

        public static bool IsCurrentlyCrew(long uid)
        {
            var medals = BiliUser.getMedals(Global.bilisession, uid, false);
            foreach (var medal in medals)
            {
                if (medal.TargetId == Global.StreammerUID)
                    return medal.GuardLevel > 0 || medal.Level >= 21;
            }
            return false;
        }
    }
}

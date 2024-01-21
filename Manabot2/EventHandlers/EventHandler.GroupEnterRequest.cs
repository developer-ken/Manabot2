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

namespace Manabot2.EventHandlers
{
    [RegisterMiraiHttpParser(typeof(DefaultMappableMiraiHttpMessageParser<IGroupApplyEventArgs, GroupApplyEventArgs>))]
    internal partial class EventHandler : IMiraiHttpMessageHandler<IGroupApplyEventArgs>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EventHandler));
        public async Task HandleMessageAsync(IMiraiHttpSession session, IGroupApplyEventArgs e)
        {
            log.Info("Group enter request at " + e.FromGroup + " by " + e.FromQQ);
            if (!DataBase.me.isCrewGroup(e.FromGroup))
            {
                log.Info("Not associated. Ignore.");
                return;
            }
            if (DataBase.me.isUserBlacklisted(e.FromQQ))
            {
                log.Info(e.FromQQ + " is banned. Deny access.");
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "您在黑名单中。如有疑问请联系管理员。");
                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：已被拉黑"));
                return;
            }
            if (DataBase.me.isUserBoundedUID(e.FromQQ))
            {
                var uid = DataBase.me.getUserBoundedUID(e.FromQQ);
                var profile = (await session.GetUserProfileAsync(e.FromQQ));
                if (DataBase.me.isBiliUserGuard(uid) || IsCurrentlyCrew(uid))
                    if (profile.Level < 16)
                    {
                        log.Info(e.FromQQ + " already registered.");
                        log.Info(e.FromQQ + " level low (" + profile.Level + "/16). Deny access.");
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, $"等级不足({profile.Level}/16)");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：等级低(\" + profile.Level + \"/16)"));
                    }
                    else
                    {
                        log.Info(e.FromQQ + " already registered. Allow.");
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n通过加群申请：已知QQ"));
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
                    await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n加群申请需要人工核对：未提供验证码"));

                    return;
                }
                if (code > 999999)
                {
                    if (DataBase.me.isBiliUserGuard(code) || IsCurrentlyCrew(code))
                    {
                        Global.danmakuhan.SendCrewCode(code);
                        log.Info("Sent new code to " + code);
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, $"已重发验证码，请查看B站私信");
                    }
                    else
                    {
                        log.Info("No crew record, Wait for manual approval.");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n加群申请需要人工核对：\n用户正在通过提供UID完成验证\nUID无消费记录\n!! 注意核对上舰凭证 !!"));
                    }
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
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：等级低(\" + profile.Level + \"/16)\n触发新绑定低等级QQ任务(直播模式)"));
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
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：等级低(\" + profile.Level + \"/16)\n触发新绑定低等级QQ任务(免打扰模式)"));
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
                                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n拒绝进入舰长群：等级低(\" + profile.Level + \"/16)\n触发新绑定低等级QQ任务"));
                            }
                            return;
                        }
                        else
                        {
                            log.Info(e.FromQQ + " -> #" + uid + ". Allow.");
                            await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                            {
                                //var info = await session.GetGroupMemberInfoAsync(e.FromQQ, e.FromGroup);
                                //session.ChangeGroupMemberInfoAsync(e.FromQQ, e.FromGroup, new GroupMemberCardInfo($"*{BiliApi/}",null));
                                await session.SendGroupMessageAsync(e.FromGroup, new AtMessage(e.FromQQ),
                                    new PlainMessage("欢迎加入舰长群！您的QQ已经和B站UID绑定，感谢您的支持！"));
                            }
                            PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, Global.bilisession);
                            bss.sendMessage("您已使用QQ" + e.FromQQ + "加入舰长群。此QQ将与您的Uid绑定，以后无需再进行验证。\n" +
                                "若对上述信息存在异议，请联系技术负责人鸡蛋(QQ:1250542735)");
                            bss.Close();
                            await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n通过加群申请：验证码核验通过"));
                        }
                    }
                    else
                    {
                        log.Info("Wrong code (" + code + "), Wait for manual approval.");
                        await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n加群申请需要人工核对：提供的验证码不可查"));
                    }
                else
                {
                    log.Info("No crew record, Wait for manual approval.");
                    await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n加群申请需要人工核对：\n用户使用一个有效的验证码，但对应UID无消费信息。\n请核对舰长凭证。"));
                }
            }
            else
            {
                log.Info("Wait for manual approval.");
                await session.SendGroupMessageAsync(Global.LogGroup, new PlainMessage(e.FromQQ + "\n加群申请需要人工核对：未提供验证码\n建议人工核查程序：\n1.要求用户提供其UID\n2.在此处执行指令：\"#验证码 对方提供的UID\"\n3.要求对方提供B站私信接收的验证码，核对是否正确\n4.运行指令\"#QQ绑UID QQ号 B站UID\"将上述信息绑定\n5.同意加群"));
            }
        }

        private static bool IsCurrentlyCrew(long uid)
        {
            var medals = BiliUser.getMedals(Global.bilisession, uid);
            foreach (var medal in medals)
            {
                if (medal.TargetId == Global.StreammerUID)
                    return medal.GuardLevel > 0 || medal.Level >= 21;
            }
            return false;
        }
    }
}

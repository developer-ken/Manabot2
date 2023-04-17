﻿using Mirai.CSharp.HttpApi.Models.EventArgs;
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
using System.Text;

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
                return;
            }
            if (DataBase.me.isUserBoundedUID(e.FromQQ))
            {
                log.Info(e.FromQQ + " already registered. Allow.");
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                return;
            }
            var match = Regex.Match(e.Message, "([1-9][0-9][0-9][0-9][0-9][0-9])");
            if (match.Success)
            {
                int code = int.Parse(match.Value);
                var uid = DataBase.me.getUidByAuthcode(code);
                if (uid > 0)
                {
                    DataBase.me.boundBiliWithQQ(uid, e.FromQQ);
                    var profile = (await session.GetUserProfileAsync(e.FromQQ));
                    if (profile.Level < 16)
                    {
                        log.Info(e.FromQQ + " -> #" + uid);
                        log.Info(e.FromQQ + " level low (" + profile.Level + "/16). Deny access.");
                        GlobalVar.LevelLowQQs.Add(e.FromQQ);
                        if (GlobalVar.IsLive)
                        {
                            await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在直播结束后与您取得联系");
                            PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, GlobalVar.bilisession);
                            bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                "· 鹿野将在直播后添加您的QQ好友\n" +
                                "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                "感谢您的理解与支持。 若对上述信息存在异议，或超过12小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                            bss.Close();
                        }
                        else if(DateTime.Now.Hour>23 || DateTime.Now.Hour < 8) //夜晚免打扰
                        {
                            await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在白天与您取得联系");
                            PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, GlobalVar.bilisession);
                            bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                "· 鹿野将添加您的QQ好友\n" +
                                "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                "感谢您的理解与支持。 若对上述信息存在异议，或超过24小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                            bss.Close();
                        }
                        else
                        {
                            await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "鹿野将在稍后与您取得联系");
                            PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, GlobalVar.bilisession);
                            bss.sendMessage("您的QQ" + e.FromQQ + "已与当前Uid绑定，先前的验证码已自动失效。\n" +
                                "由于您的QQ等级不足(" + profile.Level + "/16)，您不能加入舰长群。我们将采取以下措施保护您的权益：\n" +
                                "· 鹿野将添加您的QQ好友\n" +
                                "· 您的加群资格将会被保留。当您的QQ等级达到16级后，申请加入舰长群，将通过绿色通道快速加入\n\n" +
                                "感谢您的理解与支持。 若对上述信息存在异议，或超过24小时仍未收到鹿野的好友申请，请联系技术负责人鸡蛋(QQ:1250542735)");
                            bss.Close();
                        }
                        return;
                    }
                    else
                    {
                        log.Info(e.FromQQ + " -> #" + uid + ". Allow.");
                        await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                        PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, GlobalVar.bilisession);
                        bss.sendMessage("您已使用QQ" + e.FromQQ + "加入舰长群。此QQ将与您的Uid绑定，以后无需再进行验证。\n" +
                            "若对上述信息存在异议，请联系技术负责人鸡蛋(QQ:1250542735)");
                        bss.Close();
                    }
                }
                else
                {
                    log.Info("Wrong code (" + code + "), Wait for manual approval.");
                }
            }
            else
            {
                log.Info("Wait for manual approval.");
            }
        }
    }
}

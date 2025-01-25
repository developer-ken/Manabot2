using Mirai.CSharp;
using Mirai.CSharp.Models;
using System;
using System.Collections.Generic;
using System.Text;
using BiliApi;
using BiliApi.BiliPrivMessage;
using System.IO;
using System.Drawing.Imaging;
using Mirai.CSharp.HttpApi.Models.EventArgs;
using Mirai.CSharp.HttpApi.Models.ChatMessages;
using Mirai.CSharp.HttpApi.Session;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using Manabot2.Mysql;
using log4net.Repository.Hierarchy;
using log4net;
using Manabot2.EventHandlers;

namespace Manabot2
{
    internal class Commands
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Commands));
        private static Random rand = new Random();

        public static List<string> getAllPictures(IGroupMessageEventArgs e)
        {
            List<string> list = new List<string>();
            foreach (IChatMessage msg in e.Chain)
            {
                if (msg.Type == "Image")
                {
                    ImageMessage imgmsg = (ImageMessage)msg;
                    list.Add(imgmsg.Url);
                }
            }
            return list;
        }

        public static List<long> getAllAts(IGroupMessageEventArgs e)
        {
            List<long> list = new List<long>();
            foreach (IChatMessage msg in e.Chain)
            {
                if (msg.Type == "At")
                {
                    AtMessage imgmsg = (AtMessage)msg;
                    list.Add(imgmsg.Target);
                }
            }
            return list;
        }

        public static async Task Proc(IMiraiHttpSession session, IGroupMessageEventArgs e, string clearstr)
        {
            if (clearstr == null || clearstr.Length < 2 || clearstr.IndexOf("#") != 0)
            {
                //不是指令
            }
            else
            {//是一条指令
                if (DataBase.me.isUserOperator(e.Sender.Id))//仅允许数据库中的管理员
                {
                    log.Info("OP[" + e.Sender.Id + "] " + clearstr);
                    async Task ReplyMsg(string text)
                    {
                        await Global.qqsession.SendGroupMessageAsync(e.Sender.Group.Id, new AtMessage(e.Sender.Id),
                                            new PlainMessage(text));
                    }
                    try
                    {
                        string[] cmd = clearstr.Split(' ');
                        switch (cmd[0])
                        {
                            case "#解除拉黑":
                            case "#unban":
                                {
                                    long qquin;
                                    if (!long.TryParse(cmd[1], out qquin))
                                    {
                                        log.Error("Wrong format for QQ uin.");
                                        await ReplyMsg("指定的QQ号格式不正确");
                                        break;
                                    }
                                    if (DataBase.me.removeUserBlklist(long.Parse(cmd[1])))
                                    {
                                        string name = "";
                                        try
                                        {
                                            var profile = Global.qqsession.GetUserProfileAsync(qquin).Result;
                                            name = "<" + profile.Nickname + ">";
                                        }
                                        catch
                                        {
                                            name = "* <#" + qquin + "]>";
                                        }
                                        await ReplyMsg("已将" + name + "从黑名单移除");
                                    }
                                    else
                                    {
                                        await ReplyMsg("执行操作时出现错误，请稍后重试。");
                                    }
                                }
                                break;
                            case "#手动拉黑":
                            case "#ban":
                                {
                                    long qquin;
                                    if (!long.TryParse(cmd[1], out qquin))
                                    {
                                        log.Error("Wrong format for QQ uin.");
                                        await ReplyMsg("指定的QQ号格式不正确");
                                        break;
                                    }
                                    if (DataBase.me.addUserBlklist(long.Parse(cmd[1]), (
                                        (cmd.Length > 2 ? (cmd[2]) : ("管理员手动拉黑"))
                                        ), e.Sender.Id))
                                    {
                                        string name = "";
                                        try
                                        {
                                            var profile = Global.qqsession.GetUserProfileAsync(qquin).Result;
                                            name = "<" + profile.Nickname + ">";
                                        }
                                        catch
                                        {
                                            name = "* <#" + qquin + "]>";
                                        }
                                        await ReplyMsg("已将" + name + "加入黑名单");
                                    }
                                    else
                                    {
                                        await ReplyMsg("执行操作时出现错误，请稍后重试。");
                                    }
                                }
                                break;
                            case "#uidcode":
                            case "#验证码":
                                {
                                    long uidd = long.Parse(cmd[1]);
                                    var bs = PrivMessageSession.openSessionWith(uidd, Global.bilisession);
                                    var code = rand.Next(100000, 999999);
                                    await ReplyMsg("[验证码]\n管理员发起验证，正确的验证码为：" + code + "\n此验证码已被发送至<UID:" + uidd + ">的B站私信，询问其验证码并与此正确代码核对。");
                                    bs.sendMessage("[验证码]\n验证码：" + code + "\n此次验证由<" + e.Sender.Name + ">发起。如果您不在与此管理组成员沟通，请不要向对方提供验证码。");
                                }
                                break;
                            case "#boundqq":
                            case "#QQ绑UID":
                                {
                                    long qquin, biliuid;
                                    if (!long.TryParse(cmd[2], out qquin))
                                    {
                                        log.Error("Wrong format for QQ uin.");
                                        await ReplyMsg("指定的QQ号格式不正确");
                                        break;
                                    }
                                    if (!long.TryParse(cmd[1], out biliuid))
                                    {
                                        log.Error("Wrong format for UID.");
                                        await ReplyMsg("指定的UID格式不正确");
                                        break;
                                    }
                                    var result = DataBase.me.boundBiliWithQQ(biliuid, qquin);
                                    if (!result)
                                    {
                                        DataBase.me.setCrewAuthCode(biliuid, 12345);          //Workaround.
                                        result = DataBase.me.boundBiliWithQQ(biliuid, qquin);
                                    }
                                    var buser = BiliApi.BiliUser.getUser(biliuid, Global.bilisession);
                                    var quser = Global.qqsession.GetUserProfileAsync(qquin).Result;

                                    await ReplyMsg("[手动QQ绑定]\n" + "Bili:" + buser.name + "\nQName:" + quser.Nickname + "\n" + (result ? "已建立绑定" : "无法绑定"));
                                    var bs = PrivMessageSession.openSessionWith(biliuid, Global.bilisession);
                                    bs.sendMessage("[自动回复] 管理员已将您当前账号绑定到QQ:" + long.Parse(cmd[2]) + "。\n此QQ将可以以您的身份领取相关福利。如果这不是你的QQ，请立即联系管理换绑！\n" +
                                                    "如需帮助，请联系管理员蛋黄(QQ:3584384914)");
                                }
                                break;
                            case "#验码":
                                {
                                    long qquin; int code;
                                    if (!long.TryParse(cmd[2], out qquin))
                                    {
                                        log.Error("Wrong format for QQ uin.");
                                        await ReplyMsg("指定的QQ号格式不正确");
                                        break;
                                    }
                                    if (!int.TryParse(cmd[1], out code) || code > 999999 || code < 100000)
                                    {
                                        log.Error("Wrong format for code.");
                                        await ReplyMsg("指定的验证码格式不正确");
                                        break;
                                    }
                                    var uid = DataBase.me.getUidByAuthcode(code);
                                    if (uid > 0)
                                    {
                                        DataBase.me.boundBiliWithQQ(uid, qquin);
                                        var profile = (await session.GetUserProfileAsync(qquin));
                                        if (profile.Level < 16)
                                        {
                                            await ReplyMsg("目标用户QQ等级不足16。应提醒添加鹿野好友，拒绝进群。\n验码结束，对应验证码已经失效");
                                        }
                                        else if (DataBase.me.isUserBlacklisted(qquin))
                                        {
                                            await ReplyMsg("黑名单QQ，应当拒绝进群。\n验码结束，对应验证码已经失效");
                                        }
                                        else
                                        {
                                            await ReplyMsg($"核验通过，绑定信息：\nB站UID：{uid}\nQQ号：{qquin}\n请通过群申请。\n\n验码结束，对应验证码已经失效");
                                        }
                                    }
                                    else
                                    {
                                        await ReplyMsg($"核验失败：验证码信息不可查。请进行人工核查。\n建议人工核查程序：\n1.要求用户提供其UID\n2.在此处执行指令：\"#验证码 对方提供的UID\"\n3.要求对方提供B站私信接收的验证码，核对是否正确\n4.运行指令\"#QQ绑UID QQ号 B站UID\"将上述信息绑定\n5.同意加群");
                                    }
                                }
                                break;
                            case "#setcrew":
                                {
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    catch (Exception err)
                    {
                        log.Error("执行指令出错", err);
                        await ReplyMsg("执行指令出错。\n" + "未处理的异常：" + err.Message + "\n堆栈追踪：" + err.StackTrace);
                    }
                }
            }
        }
    }
}
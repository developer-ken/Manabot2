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
using MySqlX.XDevAPI;
using Mirai.CSharp.HttpApi.Models;

namespace Manabot2.EventHandlers
{
    [RegisterMiraiHttpParser(typeof(DefaultMappableMiraiHttpMessageParser<IGroupMemberJoinedEventArgs, GroupMemberJoinedEventArgs>))]
    internal partial class EventHandler : IMiraiHttpMessageHandler<IGroupMemberJoinedEventArgs>
    {
        public async Task HandleMessageAsync(IMiraiHttpSession client, IGroupMemberJoinedEventArgs message)
        {   /* 只处理舰长群 */
            if (!DataBase.me.isCrewGroup(message.Member.Group.Id))
            {
                return;
            }
            var uid = DataBase.me.getUserBoundedUID(message.Member.Id);
            await Task.Delay(Random.Shared.Next(1000, 5000));
            {
                var info = await client.GetGroupMemberInfoAsync(message.Member.Id, message.Member.Group.Id);
                //await session.SendGroupMessageAsync(e.FromGroup, new AtMessage(e.FromQQ),
                //    new PlainMessage("欢迎加入舰长群！您的QQ已经和B站UID绑定，感谢您的支持！"));
            }
            await Task.Delay(Random.Shared.Next(1000, 5000));
            await client.SendTempMessageAsync(message.Member.Group.Id, message.Member.Id, new PlainMessage($"[b]欢迎进入舰长群！"));
            await Task.Delay(Random.Shared.Next(1000, 5000));
            if (uid <= 0)
            {
                await client.SendTempMessageAsync(message.Member.Group.Id, message.Member.Id, new PlainMessage($"[b]数据库中没有您的B站信息。建议您将B站UID发送给我。"));
            }
            else
            {
                await client.ChangeGroupMemberInfoAsync(message.Member.Id, message.Member.Group.Id,
                    new GroupMemberCardInfo($"*{BiliApi.BiliUser.getUser(uid, Global.bilisession).name}", null));
                await Task.Delay(Random.Shared.Next(1000, 2000));
                await client.SendTempMessageAsync(message.Member.Group.Id, message.Member.Id, new PlainMessage($"[b][账号信息]\n" + UserInfo(message.Member.Id, uid)));
                await Task.Delay(Random.Shared.Next(1000, 5000));
                await client.SendTempMessageAsync(message.Member.Group.Id, message.Member.Id, new PlainMessage($"[b]我们可能会定期将您的群昵称设置为您的B站昵称。如果您不想我们修改您的群昵称，请删除群昵称开头的'*'"));
                await Task.Delay(Random.Shared.Next(1000, 5000));
                await client.SendTempMessageAsync(message.Member.Group.Id, message.Member.Id, new PlainMessage($"[b]如果上述信息错误，可以直接在此聊天中提出。否则您不必回复此消息。"));
            }
        }
    }
}

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

namespace Manabot2.EventHandlers
{
    [RegisterMiraiHttpParser(typeof(DefaultMappableMiraiHttpMessageParser<IGroupApplyEventArgs, GroupApplyEventArgs>))]
    internal partial class EventHandler : IMiraiHttpMessageHandler<IGroupApplyEventArgs>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EventHandler));
        public async Task HandleMessageAsync(IMiraiHttpSession session, IGroupApplyEventArgs e)
        {
            if (!DataBase.me.isCrewGroup(e.FromGroup)) return;
            if (DataBase.me.isUserBlacklisted(e.FromQQ))
            {
                log.Info(e.FromQQ + " is banned. Deny access.");
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "您在黑名单中。如有疑问请联系管理员。");
                return;
            }
            var profile = (await session.GetUserProfileAsync(e.FromQQ));
            if (profile.Level < 16)
            {
                log.Info(e.FromQQ + " level low (" + profile.Level + "/16). Deny access.");
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Deny, "QQ至少需16级。如有疑问请联系管理员。");
                return;
            }
            if (DataBase.me.isUserBoundedUID(e.FromQQ))
            {
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                return;
            }
            var match = Regex.Match(e.Message, "([1-9][0-9][0-9][0-9][0-9][0-9])");
            var uid = DataBase.me.getUidByAuthcode(int.Parse(match.Value));
            if (uid > 1)
            {
                DataBase.me.boundBiliWithQQ(uid, e.FromQQ);
                await session.HandleGroupApplyAsync(e, GroupApplyActions.Allow);
                PrivMessageSession bss = PrivMessageSession.openSessionWith(uid, GlobalVar.bilisession);
                bss.sendMessage("您已使用QQ" + e.FromQQ + "加入舰长群。此QQ将与您的Uid绑定，以后无需再进行验证。");
                bss.Close();
            }
        }
    }
}

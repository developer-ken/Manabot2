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
using System.Text;
using BiliApi;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Xml;

namespace Manabot2.EventHandlers
{
    [RegisterMiraiHttpParser(typeof(DefaultMappableMiraiHttpMessageParser<IGroupMessageEventArgs, GroupMessageEventArgs>))]
    internal partial class EventHandler : IMiraiHttpMessageHandler<IGroupMessageEventArgs>
    {
        public async Task HandleMessageAsync(IMiraiHttpSession client, IGroupMessageEventArgs e)
        {
            try
            {
                var session = Global.qqsession;
                foreach (IChatMessage msg in e.Chain)
                {
                    if (msg is not UnknownChatMessage)//不处理UnknownChatMessage
                        switch (msg.Type)
                        {
                            case "Plain":
                                {
                                    PlainMessage message = (PlainMessage)msg;
                                    await Commands.Proc(session, e, message.Message);
                                }
                                break;
                            default:
                                break;
                        }
                }
            }
            catch (Exception err)
            {
                log.Error("An error happed when receiving message.",err);
            }
            return;
        }
    }
}

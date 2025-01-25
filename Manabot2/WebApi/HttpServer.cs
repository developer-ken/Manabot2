using BiliApi;
using BiliApi.Modules;
using Manabot2.Mysql;
using Manabot2.WebApi.Module;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TouchSocket.Core;
using TouchSocket.Http;
using TouchSocket.Sockets;

namespace Manabot2.WebApi
{
    internal class HttpServer : PluginBase, IHttpPlugin<IHttpSocketClient>
    {
        public static HttpService service = new HttpService();
        public static HttpServer Instance;

        public HttpServer()
        {
            Instance = this;
        }

        public async Task OnHttpRequest(IHttpSocketClient client, HttpContextEventArgs e)
        {
            if (e.Context.Request.RelativeURL.StartsWith("/qqinfo/"))
            {
                JObject jb = new JObject();
                try
                {
                    long qq = long.Parse(e.Context.Request.RelativeURL.Split("/")[2]);
                    var profile = Global.qqsession.GetUserProfileAsync(qq).Result;
                    jb.Add("code", 0);
                    jb.Add("data", JObject.FromObject(profile));
                }
                catch (Exception ex)
                {
                    jb.Add("code", 1);
                    jb.Add("msg", ex.Message);
                    jb.Add("stack", ex.StackTrace);
                }
                e.Context.Response.ContentType = "application/json";
                e.Context.Response.SetContent(jb.ToString());
                await e.Context.Response.AnswerAsync();
            }
            else
            if (e.Context.Request.RelativeURL.StartsWith("/medals/"))
            {
                JObject jb = new JObject();
                try
                {
                    long uid = long.Parse(e.Context.Request.RelativeURL.Split("/")[2]);
                    var medals = Global.bilisession.getBiliUserMedal(uid);
                    jb.Add("code", 0);
                    jb.Add("data", JObject.Parse(medals));
                }
                catch (Exception ex)
                {
                    jb.Add("code", 1);
                    jb.Add("msg", ex.Message);
                    jb.Add("stack", ex.StackTrace);
                }
                e.Context.Response.ContentType = "application/json";
                e.Context.Response.SetContent(jb.ToString());
                await e.Context.Response.AnswerAsync();
            }
            else
            if (e.Context.Request.RelativeURL.StartsWith("/isqqincrewgroup/"))
            {
                JObject jb = new JObject();
                try
                {
                    long qq = long.Parse(e.Context.Request.RelativeURL.Split("/")[2]);
                    var members = await Global.qqsession.GetGroupMemberListAsync(Global.CrewGroup);
                    bool hit = false;
                    foreach (var member in members)
                    {
                        if(member.Id == qq)
                        {
                            hit = true;
                            break;
                        }
                    }
                    jb.Add("code", 0);
                    jb.Add("hit", hit);
                }
                catch (Exception ex)
                {
                    jb.Add("code", 1);
                    jb.Add("msg", ex.Message);
                    jb.Add("stack", ex.StackTrace);
                }
                e.Context.Response.ContentType = "application/json";
                e.Context.Response.SetContent(jb.ToString());
                await e.Context.Response.AnswerAsync();
            }
        }

        public static async Task StartAsync()
        {
            service.Setup(new TouchSocketConfig()
            .SetListenIPHosts(6686)
            .ConfigureContainer(a =>
            {
                a.AddConsoleLogger();
            })
            .ConfigurePlugins(a =>
            {
                //以下即是插件
                a.UseWebSocket()//WebSocket
                    .SetWSUrl("/msgpush")
                    .UseAutoPong();//自动回应ping
                a.Add<HttpServer>();
                a.UseDefaultHttpServicePlugin();
            }));

            await service.StartAsync();
        }
    }
}

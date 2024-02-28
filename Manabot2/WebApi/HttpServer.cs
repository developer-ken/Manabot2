using BiliApi;
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
            if (e.Context.Request.RelativeURL.StartsWith("/api/v1"))
            {
                var dpath = e.Context.Request.RelativeURL.Split("/");
                var api_name = dpath[3];
                e.Context.Response.ContentType = "application/json";
                JObject jb = new JObject();
                var session = AuthenticationHandler.InterpSession(e.Context);

                // 滚码刷新下一个session
                if(session.IsValid)
                    AuthenticationHandler.EnsureSessionValid(e.Context, session);

                switch (api_name)
                {
                    case "auth":
                        {
                            if (e.Context.Request.Query.ContainsKey("uid") &&
                                e.Context.Request.Query.ContainsKey("cid"))
                            {
                                long uid = long.Parse(e.Context.Request.Query["uid"]);
                                string cid = e.Context.Request.Query["cid"];
                                var pubkey = AuthenticationHandler.GenerateToken(uid, cid);
                                BiliUser buser = new BiliUser(uid);
                                if (buser.sign.Contains(pubkey))
                                {
                                    session = Session.CreateEmptySession(uid);
                                    AuthenticationHandler.EnsureSessionValid(e.Context, session);
                                    jb["code"] = 0;
                                    jb["msg"] = "authentication success";
                                }
                                else
                                {
                                    jb["code"] = 1;
                                    jb["msg"] = "Verification code non-exists.";
                                    jb["pubkey"] = pubkey;
                                }
                            }
                            else
                            {
                                jb["code"] = 400;
                                jb["msg"] = "uid&cid required";
                            }
                        }
                        break;
                }


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

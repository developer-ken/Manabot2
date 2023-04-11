using BiliApi;
using BiliApi.Auth;
using BiliveDanmakuAgent;
using log4net;
using log4net.Config;
using Manabot2.Mysql;
using Microsoft.Extensions.DependencyInjection;
using Mirai.CSharp.Builders;
using Mirai.CSharp.HttpApi.Builder;
using Mirai.CSharp.HttpApi.Invoking;
using Mirai.CSharp.HttpApi.Models.ChatMessages;
using Mirai.CSharp.HttpApi.Options;
using Mirai.CSharp.HttpApi.Session;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Manabot2
{
    internal class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        static int Main(string[] args)
        {
            DateTime start = DateTime.Now;
            Console.WriteLine("Loading logger: log4net");
            if (File.Exists("log4net.config"))
            {
                XmlConfigurator.Configure(new System.IO.FileInfo("log4net.config"));
            }
            else
            {
                BasicConfigurator.Configure();
                log.Warn("Config file for log4net not exists, default logger loaded.");
            }
            log.Info("Logger loaded.");

            #region 读取配置
            if (!File.Exists("botconfig.json"))
            {
                log.Error("Main config file not exists: botconfig.json");
                return 1;
            }

            try
            {
                StreamReader cfile = new StreamReader("botconfig.json");
                JObject config = (JObject)JsonConvert.DeserializeObject(cfile.ReadToEnd());
                cfile.Close();
                GlobalVar.Server = config["mirai"].Value<string>("server");
                GlobalVar.Key = config["mirai"].Value<string>("key");
                GlobalVar.MyQQ = config["mirai"].Value<long>("user");
                try
                {
                    GlobalVar.Port = config["mirai"].Value<int>("port");
                }
                catch
                {
                    GlobalVar.Port = 8080;
                }
                GlobalVar.LiveroomId = config["bili"].Value<long>("roomid");
                GlobalVar.StreammerUID = config["bili"].Value<long>("streammer");
                GlobalVar.LogGroup = config.Value<long>("logGroup");

                GlobalVar.dbcfg = new ConnectionPool.SqlDbConfig
                {
                    ServerAddress = config["sql"].Value<string>("server"),
                    ServerPort = config["sql"]["port"] is null ? 3306 : config["sql"].Value<int>("port"),
                    DbName = config["sql"]["dbname"] is null ? "manabot" : config["sql"].Value<string>("dbname"),
                    UserName = config["sql"].Value<string>("user"),
                    UserPassword = config["sql"].Value<string>("passwd")
                };
            }
            catch (Exception ex)
            {
                log.Error("Error happened when parsing botconfig.json", ex);
                return 2;
            }
            #endregion

            #region 连接数据库
            try
            {
                ConnectionPool dbpool = new ConnectionPool(GlobalVar.dbcfg, 10);
                if (!dbpool.Connection.connected)
                {
                    log.Error("Failed to connect to database server.");
                }
            }
            catch (Exception ex)
            {
                log.Error("Fatal error happened when initializing Database Connection pool", ex);
                return 3;
            }
            #endregion

            #region 连接Mirai机器人
            try
            {
                log.Info("Initializing Mirai-CSharp...");
                IServiceProvider services = new ServiceCollection().AddMiraiBaseFramework()   // 表示使用基于基础框架的构建器
                                           .AddHandler<Manabot2.EventHandlers.EventHandler>()
                                           .Services
                                           .AddDefaultMiraiHttpFramework() // 表示使用 mirai-api-http 实现的构建器
                                           .ResolveParser<Manabot2.EventHandlers.EventHandler>()// 只提前解析 DynamicPlugin 将要用到的消息解析器
                                           .AddInvoker<MiraiHttpMessageHandlerInvoker>() // 使用默认的调度器
                                           .AddClient<MiraiHttpSession>() // 使用默认的客户端
                                           .Services
                                           // 由于 IMiraiHttpSession 使用 IOptions<MiraiHttpSessionOptions>, 其作为 Singleton 被注册
                                           // 配置此项将配置基于此 IServiceProvider 全局的连接配置
                                           // 如果你想一个作用域一个配置的话
                                           // 自行做一个实现类, 继承IMiraiHttpSession, 构造参数中使用 IOptionsSnapshot<MiraiHttpSessionOptions>
                                           // 并将其传递给父类的构造参数
                                           // 然后在每一个作用域中!先!配置好 IOptionsSnapshot<MiraiHttpSessionOptions>, 再尝试获取 IMiraiHttpSession
                                           .Configure<MiraiHttpSessionOptions>(options =>
                                           {
                                               options.Host = GlobalVar.Server;
                                               options.Port = GlobalVar.Port; // 端口
                                               options.AuthKey = GlobalVar.Key; // 凭据
                                           })
                                           .AddLogging()
                                           .BuildServiceProvider();
                IServiceScope scope = services.CreateScope();
                services = scope.ServiceProvider;
                IMiraiHttpSession session = services.GetRequiredService<IMiraiHttpSession>(); // 大部分服务都基于接口注册, 请使用接口作为类型解析
                log.Info("Connecting...");
                session.ConnectAsync(GlobalVar.MyQQ).Wait(); // 填入期望连接到的机器人QQ号
                log.Info("Mirai connected to: " + GlobalVar.MyQQ);
                GlobalVar.qqsession = session;
            }
            catch (Exception ex)
            {
                log.Error("Fatal error happened when initializing Mirai api", ex);
                return 3;
            }
            #endregion

            #region 登录Bilibili
            try
            {
                while (true)
                {
                    if (File.Exists("biliaccount.secrets.json"))
                    {
                        log.Info("Trying to login with: biliaccount.secrets.json");
                        var bililogin = new QRLogin(File.ReadAllText("biliaccount.secrets.json"));
                        if (bililogin.LoggedIn)
                        {
                            GlobalVar.bilisession = new BiliApi.BiliSession(bililogin.Cookies);
                            log.Info("Bili login success.");
                            break;
                        }
                        else
                        {
                            log.Warn("Secrets invalid. Fall back to QR login.");
                        }
                    }
                    var blogin = new QRLogin();
                    var qrmsg = QRGen.Url2ImageMessage(blogin.QRToken.ScanUrl);
                    var txtqr = QRGen.Url2BitString(blogin.QRToken.ScanUrl);
                    GlobalVar.qqsession.SendGroupMessageAsync(GlobalVar.LogGroup, new IChatMessage[] {
                            qrmsg,
                            new PlainMessage("Token="+blogin.QRToken.OAuthKey+"\n请使用Bilibili客户端扫码登录")
                            });
                    log.Warn("Scan the QR code below:");
                    Console.WriteLine(txtqr + "\n");
                    var token = blogin.Login();
                    GlobalVar.bilisession = new BiliApi.BiliSession(token);
                    log.Info("Bili login success.");
                    log.Info("Token saved to biliaccount.secrets.json.");
                    log.Info("DO NOT SHARE THAT FILE");
                    File.WriteAllText("biliaccount.secrets.json", blogin.Serilize());
                    break;
                }
            }catch(Exception ex)
            {
                log.Error("Fatal error happened in BiliLogin", ex);
                return 4;
            }
            #endregion
             
            #region 连接Bilibili直播间
            try
            {
                //TODO: 重构底层，支持long长度的LiveroonId
                GlobalVar.danmakuhan = new EventHandlers.BiliDanmakuHandler(GlobalVar.bilisession, (int)GlobalVar.LiveroomId);
            }catch(Exception ex)
            {
                log.Error("Fatal error happened in BiliDanmakuHandler", ex);
                return 5;
            }
            #endregion

            var seconds = (DateTime.Now - start).TotalSeconds;
            log.Info("Done! System loaded in " + seconds + "s.");

            #region 异步错误处理器
            while (true)
            {
                try
                {
                    Task.WaitAny(GlobalVar.BackgroundTasks.ToArray());
                }catch(Exception ex)
                {
                    log.Error("Exception in background task", ex);
                }
            }
            #endregion
        }
    }
}
using log4net;
using Manabot2.Mysql;
using Manabot2.WebApi.Module;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TouchSocket.Http;

namespace Manabot2.WebApi
{
    internal class AuthenticationHandler
    {
        public static string? Salt = null;
        public static byte? SaltPolicy = null;

        private static readonly ILog log = LogManager.GetLogger(typeof(AuthenticationHandler));

        public static string GenerateToken(long uid, string cid)
        {
            if (Salt is null)
            {
                log.Info("No salt generated. Generating new salt....");
                Salt = Guid.NewGuid().ToString();
            }
            if (SaltPolicy is null)
            {
                log.Info("No salt policy generated. Generating new salt policy....");
                SaltPolicy = (byte)new Random().Next(0, 6);
            }
            switch (SaltPolicy)
            {
                case 0:
                    return Sha1Signature(uid.ToString() + cid + Salt);
                case 1:
                    return Sha1Signature(uid.ToString() + Salt + cid);
                case 2:
                    return Sha1Signature(cid + uid.ToString() + Salt);
                case 3:
                    return Sha1Signature(cid + Salt + uid.ToString());
                case 4:
                    return Sha1Signature(Salt + uid.ToString() + cid);
                case 5:
                    return Sha1Signature(Salt + cid + uid.ToString());
            }
            return Sha1Signature(uid.ToString() + cid + Salt);
        }

        public static string GenerateTokenConsistent(long uid, string cid)
        {
            return Sha1Signature(uid.ToString() + cid);
        }

        public static bool VerifyConsistentToken(long uid, string cid, string token)
        {
            return token == GenerateTokenConsistent(uid, cid);
        }

        public static Session InterpSession(HttpContext context)
        {
            var cookiestr = context.Request.Headers.Get(HttpHeaders.Cookie);
            if (cookiestr is null)
            {
                return Session.InvalidSesion;
            }
            long uid = -1;
            string? cid = null, token = null;
            var cookies = cookiestr.Split(';');
            foreach (var c in cookies)
            {
                var sp = c.Split('=');
                var key = sp[0].Replace(" ", "");
                var value = sp[1].Replace(" ", "");
                switch (key)
                {
                    case "uid":
                        uid = long.Parse(value);
                        break;
                    case "cid":
                        cid = value;
                        break;
                    case "token":
                        token = value;
                        break;
                }
            }
            if (cid is null || token is null || uid < 0)
            {
                return Session.InvalidSesion;
            }
            if (VerifyConsistentToken(uid, cid, token) && DataBase.me.isSessionExists(uid, cid))
            {
                return new Session()
                {
                    Uid = uid,
                    Cid = cid,
                    IsValid = true
                };
            }
            else
            {
                return Session.InvalidSesion;
            }
        }

        public static void EnsureSessionValid(HttpContext context, Session session)
        {
            var cid = Guid.NewGuid().ToString();
            var token = AuthenticationHandler.GenerateTokenConsistent(session.Uid, cid);
            if (session.IsValid && session.Cid != null)
            {
                DataBase.me.removeSession(session.Cid);
            }
            context.Response.AddHeader(HttpHeaders.SetCookie, $"uid={session.Uid};max-age=3153600;");
            context.Response.AddHeader(HttpHeaders.SetCookie, $"cid={cid};max-age=3153600;");
            context.Response.AddHeader(HttpHeaders.SetCookie, $"token={token};max-age=3153600;");
            session.Cid = cid;
            session.IsValid = true;
            DataBase.me.setSession(session.Uid, cid);
        }

        private static string Sha1Signature(string str, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            var buffer = encoding.GetBytes(str);
            var data = SHA1.Create().ComputeHash(buffer);
            StringBuilder sub = new StringBuilder();
            foreach (var t in data)
            {
                sub.Append(t.ToString("x2"));
            }

            return sub.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Manabot2.WebApi.Module
{
    public class Session
    {
        public static Session InvalidSesion = new Session();
        public static Session CreateEmptySession(long uid)
        {
            return new Session()
            {
                Cid = "",
                Uid = uid,
                IsValid = true
            };
        }
        public string Cid = "";
        public long Uid = -1;
        public bool IsValid = false;
    }
}

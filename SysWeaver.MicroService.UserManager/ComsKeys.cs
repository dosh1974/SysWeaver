using System;


namespace SysWeaver.MicroService
{
    internal static class ComsKeys
    {

        public static ManagedMailMessage GetMail(this ManagedLanguageMessages m, UserManagerComOps ops)
        {
            var c = Keys[(int)ops];
            if (c == null)
                throw new Exception("Undefined mail message " + ops);
            return m.GetMail(c);
        }

        public static ManagedTextMessage GetText(this ManagedLanguageMessages m, UserManagerComOps ops)
        {
            var c = Keys[(int)ops];
            if (c == null)
                throw new Exception("Undefined text message " + ops);
            return m.GetText(c);
        }

        public static readonly String[] Keys;
        
        static ComsKeys()
        {
            int max = (int)UserManagerComOps.Count;
            var t = new String[max];
            var vals = Enum.GetValues<UserManagerComOps>();
            var names = Enum.GetNames<UserManagerComOps>();
            var c = vals.Length;
            for (int i = 0; i < c; ++ i)
            {
                var x = (int)vals[i];
                if (x < max)
                    t[x] = names[i].FastToLower();
            }
            Keys = t;
        }

    }

}

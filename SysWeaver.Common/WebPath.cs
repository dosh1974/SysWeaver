using System;

namespace SysWeaver
{
    public static class WebPath
    {


        public static bool IsRoot(String webPath)
        {
            var t = webPath.IndexOf("://");
            return t >= 0;
        }

        public static bool SplitServerLocal(out String server, out String local, String webPath)
        {
            var t = webPath.IndexOf("://");
            if (t < 0)
            {
                server = "";
                local = webPath;
                return false;
            }
            var e = webPath.IndexOf('/', t + 3);
            if (e < 0)
            {
                server = webPath;
                local = "";
                return true;
            }
            ++e;
            server = webPath.Substring(0, e);
            local = webPath.Substring(e);
            return true;
        }

        public static String Collapse(String webPath)
        {
            SplitServerLocal(out var server, out var local, webPath);
            var p = local.Split('/');
            var pl = p.Length - 1;
            int o = 0;
            for (int i = 0; i < pl; ++i)
            {
                var pp = p[i];
                if (pp == ".")
                    continue;
                if ((pp == "..") && (o > 0))
                {
                    --o;
                    continue;
                }
                p[o] = pp;
                ++o;
            }
            p[o] = p[pl];
            if (o == pl)
                return webPath;
            ++o;
            local = String.Join('/', p, 0, o);
            return server + local;
        }
    }

}
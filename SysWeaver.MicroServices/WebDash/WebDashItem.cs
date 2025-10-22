using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace SysWeaver.MicroService
{

    /// <summary>
    /// Represents an user defined item
    /// </summary>
    public class WebDashItem
    {
#if DEBUG
        public override string ToString() => String.Join(": ", Type, Name);
#endif//DEBUG
        /// <summary>
        /// 
        /// </summary>
        public WebDashTypes Type;

        public String Name;

        public String Data;

    }


    public sealed class WebDashboard
    {
        public String Id;

        public WebDashItem[] Items;
    }

}

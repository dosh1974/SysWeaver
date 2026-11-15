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
        /// The type of the item
        /// </summary>
        public WebDashTypes Type;

        /// <summary>
        /// Name of the item
        /// </summary>
        public String Name;

        /// <summary>
        /// Data (type dependent, the frame url or the inline http code)
        /// </summary>
        public String Data;

    }


    /// <summary>
    /// Represents a dashboard (a collection of items)
    /// </summary>
    public sealed class WebDashboard
    {
        /// <summary>
        /// Id of the dashboard
        /// </summary>
        public String Id;

        /// <summary>
        /// The items of the dashboard
        /// </summary>
        public WebDashItem[] Items;
    }

}

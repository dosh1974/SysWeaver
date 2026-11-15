using System;


namespace SysWeaver.MicroService
{
    /// <summary>
    /// Represents a web menu
    /// </summary>
    public sealed class WebMenu
    {
#if DEBUG
        public override string ToString() => (Items?.Length ?? 0) > 0 ? String.Concat(Items.Length, " items") : "Empty";
#endif//DEBUG

        /// <summary>
        /// The root uri that the item urls are relative to
        /// </summary>
        public String RootUri;
  
        /// <summary>
        /// Root items
        /// </summary>
        public WebMenuItem[] Items;
    }





}

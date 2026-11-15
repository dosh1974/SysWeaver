namespace SysWeaver.MicroService
{
    /// <summary>
    /// The type (action) of a menu item
    /// </summary>
    public enum WebMenuItemTypes
    {
        /// <summary>
        /// A path (folder) that only groups other items
        /// </summary>
        Path,
        /// <summary>
        /// Show a table data API
        /// </summary>
        Table,
        /// <summary>
        /// Show an embedded page
        /// </summary>
        Embedded,
        /// <summary>
        /// Navigate to an url
        /// </summary>
        Link,
        /// <summary>
        /// Navigate to an external url
        /// </summary>
        LinkExternal,
        /// <summary>
        /// Execute js code
        /// </summary>
        Js,
        /// <summary>
        /// Show a chart API
        /// </summary>
        Chart,
    }


}

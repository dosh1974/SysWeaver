using SysWeaver.Net;

namespace SysWeaver.MicroService
{
    public sealed class ApiInfo : ApiInfoBase
    {
        /// <summary>
        /// Argument type (can be null)
        /// </summary>
        public TypeDesc Arg;

        /// <summary>
        /// Return type (can be null)
        /// </summary>
        public TypeDesc Return;

        /// <summary>
        /// Name of the argument
        /// </summary>
        public string ArgName;

        /// <summary>
        /// Summary of the argument
        /// </summary>
        public string ArgSummary;

    }


}

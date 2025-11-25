namespace SysWeaver.MicroService
{
    public class SmConfigRequest
    {
        /// <summary>
        /// Name of the service
        /// </summary>
        [EditMin(1)]
        public string ServiceName;

        /// <summary>
        /// Configuration filename
        /// </summary>
        [EditMin(1)]
        public string Config;

        /// <summary>
        /// If true this is the master config
        /// </summary>
        public bool IsMaster;
    }



}

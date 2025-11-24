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
        public string FileName;
    }

}

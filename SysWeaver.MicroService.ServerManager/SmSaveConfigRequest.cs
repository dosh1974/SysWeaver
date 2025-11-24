namespace SysWeaver.MicroService
{


    public sealed class SmSaveConfigRequest : SmConfigRequest
    {
        /// <summary>
        /// Configuration content (only text based content is supported)
        /// </summary>
        [EditMultiline]
        [EditAllowNull]
        [EditDefault(null)]
        public string Data;
    }

}

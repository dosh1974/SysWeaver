namespace SysWeaver.MicroService
{
    public sealed class SmSaveKeyFileRequest
    {
        /// <summary>
        /// Key file filename
        /// </summary>
        [EditMin(1)]
        public string FileName;

        /// <summary>
        /// Key file data
        /// </summary>
        [EditMultiline]
        [EditAllowNull]
        [EditDefault(null)]
        public string Data;
    }

}

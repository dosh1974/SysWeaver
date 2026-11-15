namespace SysWeaver.MicroService
{
    /// <summary>
    /// A request to save a key file
    /// </summary>
    public sealed class SmSaveKeyFileRequest
    {
        /// <summary>
        /// Key file filename
        /// </summary>
        [EditMin(1)]
        public string FileName;

        /// <summary>
        /// Key file data, null to delete the key file
        /// </summary>
        [EditMultiline]
        [EditAllowNull]
        [EditDefault(null)]
        public string Data;
    }

}

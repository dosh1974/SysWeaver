namespace SysWeaver.FolderSync
{
    public enum FolderSyncEvents
    {
        /// <summary>
        /// Called when a hash of a local file have been computed
        /// </summary>
        Hashed = 0,
        /// <summary>
        /// Files on the source disc have been scanned
        /// </summary>
        Scanned,
        /// <summary>
        /// Files have been checked against remote server
        /// </summary>
        Checked,
        /// <summary>
        /// Called when a file have been uploaded
        /// </summary>
        Uploaded,
    }
}

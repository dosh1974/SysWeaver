namespace SysWeaver.FolderSync
{
    /// <summary>
    /// Parameters for the <see cref="FolderSyncer"/>
    /// </summary>
    public class FolderSyncerParams : CredentialParams
    {
        /// <summary>
        /// Server address (base url)
        /// </summary>
        public string Server;

        /// <summary>
        /// If true, any bad server certificates are accepted.. NOT RECOMMENDED!
        /// Defaults to false.
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// The maximum concurrency to use, zero or negative is relative to the number of hardware threads (ex: -1 is one less than the number of processors, at least 1).
        /// Defaults to -1.
        /// </summary>
        public int MaxConcurrency = -1;

        /// <summary>
        /// An optional comment
        /// </summary>
        public string Comment;

    }
}

namespace SysWeaver.FolderSync
{
    public class FolderSyncerParams : CredentialParams
    {
        /// <summary>
        /// Server address
        /// </summary>
        public string Server;

        /// <summary>
        /// If true, any bad server certificates are accepted.. NOT RECOMMENDED!
        /// </summary>
        public bool IgnoreCertErrors;

        /// <summary>
        /// The maximum concurrency to use, zero or negative is based on the number of hardware threads
        /// </summary>
        public int MaxConcurrency = -1;

        /// <summary>
        /// An optional comment
        /// </summary>
        public string Comment;

    }
}

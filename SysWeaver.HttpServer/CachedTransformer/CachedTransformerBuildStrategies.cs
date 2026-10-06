namespace SysWeaver.HttpTransformer
{
    /// <summary>
    /// How a <see cref="ICachedTransformer"/> builds missing cache entries.
    /// </summary>
    public enum CachedTransformerBuildStrategies
    {
        /// <summary>
        /// Always build the cache in a deferred manner (queued to background build tasks); the original is served until the build is done.
        /// </summary>
        AlwaysDefer = 0,
        /// <summary>
        /// Always build the cache directly, the request waits for the build to complete (used when the original can't be served as is).
        /// </summary>
        AlwaysDirect,
        /// <summary>
        /// If the request Accept header contains the original mime type, defer, else build directly.
        /// </summary>
        CheckAccept,
    }


}

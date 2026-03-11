namespace SysWeaver.HttpTransformer
{
    public enum CachedTransformerBuildStrategies
    {
        /// <summary>
        /// Always build the cache in a deferred manner
        /// </summary>
        AlwaysDefer = 0,
        /// <summary>
        /// Always build the cache directly
        /// </summary>
        AlwaysDirect,
        /// <summary>
        /// If the original is accepted, defer else build direct
        /// </summary>
        CheckAccept,
    }


}

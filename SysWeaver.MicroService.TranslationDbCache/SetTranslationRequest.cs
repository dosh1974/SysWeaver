using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Request to manually set a cached translation
    /// </summary>
    public sealed class SetTranslationRequest
    {
        /// <summary>
        /// The hash key of the specific translation
        /// </summary>
        public String Key;

        /// <summary>
        /// The current translation (must match the stored one, to detect that it was changed by someone else)
        /// </summary>
        public String OriginalTranslation;


        /// <summary>
        /// The new translation
        /// </summary>
        public String NewTranslation;
    }




}

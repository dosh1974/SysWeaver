using System;

namespace SysWeaver.MicroService
{
    public sealed class SetTranslationRequest
    {
        /// <summary>
        /// The hash key of the specific translation
        /// </summary>
        public String Key;

        /// <summary>
        /// The original translation
        /// </summary>
        public String OriginalTranslation;


        /// <summary>
        /// The new translation
        /// </summary>
        public String NewTranslation;
    }




}

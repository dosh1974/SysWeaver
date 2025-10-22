using System;

namespace SysWeaver.AI
{

#pragma warning disable CS0649


    /// <summary>
    /// Paramaters used to generate a logo for some text string, typically a company-, user- or product-name.
    /// </summary>
    sealed class OpenAiLogo
    {
        /// <summary>
        /// Set to true to generate an icon instead of a logo
        /// </summary>
        [OpenAiOptional]
        public bool Icon;

        /// <summary>
        /// The name to generate a logo or icon for.
        /// Company, user, product name.
        /// </summary>
        public String Name;

        /// <summary>
        /// An optional abbrevation, typically 2 letters.
        /// If omitted, it's generated from the name (typically using the uppercase letters).
        /// </summary>
        [OpenAiOptional]
        public String Abbrevation;

        /// <summary>
        /// A random seed, different seed gives different appearances
        /// </summary>
        [OpenAiOptional]
        public int Seed;

        /// <summary>
        /// The title of this image, used as filename etc.
        /// Max length is 64.
        /// </summary>
        public String Title;

    }

#pragma warning restore CS0649

}

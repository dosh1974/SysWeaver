using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiTokenEncodeRequest
    {
        /// <summary>
        /// The model or algorithm to use.
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String ModelOrAlgorithm;

        /// <summary>
        /// The text to encode
        /// </summary>
        [EditMin(1)]
        [EditMultiline]
        public String Text;
    }




}

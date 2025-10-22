using System;

namespace SysWeaver.AI
{
    public sealed class OpenAiTokenDecodeRequest
    {
        /// <summary>
        /// The model or algorithm to use.
        /// </summary>
        [EditDefault(null)]
        [EditAllowNull]
        public String ModelOrAlgorithm;

        /// <summary>
        /// The tokens to decode
        /// </summary>
        public int[] Tokens;
    }




}

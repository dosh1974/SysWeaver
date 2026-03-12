using System;

namespace SysWeaver.HttpTransformer
{
    public sealed class LosslessCompressionTransformerParams
    {
        public String[] Methods =
            [
                "br",
                "zstd",
                "deflate",
            ];


        public bool BuildDirect;

        public bool ThrowOnMissing;
    }

}

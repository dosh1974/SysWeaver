using System;
using SysWeaver.Converter.Js.Ast;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Reports constructs that can't be transformed for the target
    /// </summary>
    static class Checks
    {
        static bool Supports(TransformContext ctx, int es, double chrome, double firefox, double safari, double edge = 79)
        {
            var t = ctx.Target;
            if (t.EsLimit < es)
                return false;
            foreach (var b in t.Browsers)
            {
                var min = b.Key switch
                {
                    "chrome" => chrome,
                    "firefox" => firefox,
                    "safari" => safari,
                    "edge" => edge,
                    _ => 0,
                };
                if (b.Value < min)
                    return false;
            }
            return true;
        }

        public static void Run(ProgramNode p, TransformContext ctx)
        {
            bool bigInt = Supports(ctx, 2020, 67, 68, 14);
            bool dynImport = Supports(ctx, 2020, 63, 67, 11.1);
            bool importMeta = Supports(ctx, 2020, 64, 62, 11.1);
            bool topAwait = Supports(ctx, 2022, 89, 89, 15);
            var diag = ctx.Diag;
            int fnDepth = 0;
            Walker.Walk(p, n =>
            {
                switch (n)
                {
                    case BigIntLiteral when !bigInt:
                        diag.Warning(n.Start, "BigInt literals can't be converted, the target doesn't support BigInt");
                        break;
                    case ImportCall when !dynImport:
                        diag.Warning(n.Start, "Dynamic import() can't be converted, the target doesn't support it");
                        break;
                    case MetaProperty { Meta: "import" } when !importMeta:
                        diag.Warning(n.Start, "import.meta can't be converted, the target doesn't support it");
                        break;
                    case VarDecl { Kind: "using" or "await using" }:
                        diag.Error(n.Start, "using declarations (explicit resource management) are not supported");
                        break;
                    case AwaitExpr when fnDepth == 0 && !topAwait:
                        diag.Warning(n.Start, "Top level await can't be converted, the target doesn't support it");
                        break;
                    case ForOfStmt { Await: true } when fnDepth == 0 && !topAwait:
                        diag.Warning(n.Start, "Top level for await can't be converted, the target doesn't support it");
                        break;
                    case Function:
                        ++fnDepth;
                        break;
                }
                return true;
            }, n =>
            {
                if (n is Function)
                    --fnDepth;
            });
        }
    }
}

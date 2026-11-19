using System.Collections.Generic;

namespace SysWeaver.Converter.Js.Transforms
{
    /// <summary>
    /// Creates the transform passes in the order they must run (newest features first, so that each pass lowers the output of the previous passes)
    /// </summary>
    static class Pipeline
    {
        public static List<Pass> Create(TransformContext ctx)
        {
            var r = new List<Pass>();
            void Add(bool needed, Pass p)
            {
                if (needed)
                    r.Add(p);
            }
            //  ES2022
            Add(ctx.Needs(JsFeature.ClassFields) || ctx.Needs(JsFeature.PrivateMethods) || ctx.Needs(JsFeature.PrivateIn) || ctx.Needs(JsFeature.ClassStaticBlock), new ClassFeaturesPass(ctx));
            //  ES2021
            Add(ctx.Needs(JsFeature.LogicalAssignment), new LogicalAssignmentPass(ctx));
            //  ES2020
            Add(ctx.Needs(JsFeature.OptionalChaining) || ctx.Needs(JsFeature.NullishCoalescing), new OptionalChainingPass(ctx));
            //  ES2019
            Add(ctx.Needs(JsFeature.OptionalCatchBinding), new OptionalCatchBindingPass(ctx));
            //  ES2018
            Add(ctx.Needs(JsFeature.AsyncGenerators), new AsyncGeneratorsPass(ctx));
            Add(ctx.Needs(JsFeature.ObjectRestSpread), new ObjectRestSpreadPass(ctx));
            //  Regular expressions (ES2015 - ES2018)
            Add(ctx.Needs(JsFeature.RegexDotAll) || ctx.Needs(JsFeature.RegexNamedGroups) || ctx.Needs(JsFeature.RegexUnicode) || ctx.Needs(JsFeature.RegexSticky), new RegexPass(ctx));
            //  ES2017
            Add(ctx.Needs(JsFeature.AsyncFunctions), new AsyncFunctionsPass(ctx));
            //  ES2016
            Add(ctx.Needs(JsFeature.Exponentiation), new ExponentiationPass(ctx));
            //  ES2015
            Add(ctx.Needs(JsFeature.TemplateLiterals), new TemplateLiteralsPass(ctx));
            Add(ctx.Needs(JsFeature.NewTarget), new NewTargetPass(ctx));
            Add(ctx.Needs(JsFeature.Classes), new ClassesPass(ctx));
            Add(ctx.Needs(JsFeature.ShorthandProperties) || ctx.Needs(JsFeature.ComputedProperties), new ObjectLiteralsPass(ctx));
            Add(ctx.Needs(JsFeature.ArrowFunctions), new ArrowFunctionsPass(ctx));
            Add(ctx.Needs(JsFeature.Parameters), new ParametersPass(ctx));
            Add(ctx.Needs(JsFeature.ForOf), new ForOfPass(ctx));
            Add(ctx.Needs(JsFeature.Destructuring), new DestructuringPass(ctx));
            Add(ctx.Needs(JsFeature.Spread), new SpreadPass(ctx));
            Add(ctx.Needs(JsFeature.BlockScoping), new BlockScopingPass(ctx));
            Add(ctx.Needs(JsFeature.Generators), new GeneratorsPass(ctx));
            return r;
        }
    }
}

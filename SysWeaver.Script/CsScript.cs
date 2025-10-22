using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Text;
using System.IO;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime;


namespace SysWeaver.Script
{
    public static class CsScript
    {



        public static CsAsyncScript<T, R> CreateAsync<T, R>(String sourceCode, LanguageVersion csVersion = LanguageVersion.CSharp12)
            => CreateAsync<T, R>(sourceCode, null, null, csVersion);

        public static CsAsyncScript<T, R> CreateAsync<T, R>(String sourceCode, Assembly[] assemblies, Type[] types, LanguageVersion csVersion = LanguageVersion.CSharp12)
        {
            var cl = types?.Length ?? 0;
            var t = new Type[cl + 2];
            for (int i = 0; i < cl; ++i)
                t[i] = types[i];
            t[cl] = typeof(T);
            t[cl + 1] = typeof(R);
            InternalLoad(out var lc, out var type, sourceCode, assemblies, csVersion, t);
            var mi = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(T)]);
            if (mi.ReturnType != typeof(Task<>).MakeGenericType(typeof(R)))
                throw new Exception("Expected a static Main function returning a Task<" + typeof(R).FullName + ">, taking a single paramater of type " + typeof(T).FullName);
            return new CsAsyncScript<T, R>(lc, mi);
        }

        public static CsScript<T, R> Create<T, R>(String sourceCode, LanguageVersion csVersion = LanguageVersion.CSharp12)
            => Create<T, R>(sourceCode, null, null, csVersion);

        public static CsScript<T, R> Create<T, R>(String sourceCode, Assembly[] assemblies, Type[] types, LanguageVersion csVersion = LanguageVersion.CSharp12)
        {
            var cl = types?.Length ?? 0;
            var t = new Type[cl + 2];
            for (int i = 0; i < cl; ++i)
                t[i] = types[i];
            t[cl] = typeof(T);
            t[cl + 1] = typeof(R);
            InternalLoad(out var lc, out var type, sourceCode, assemblies, csVersion, t);
            var mi = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(T)]);
            var mems = type.GetMembers(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi.ReturnType != typeof(R))
                throw new Exception("Expected a static Main function returning a " + typeof(R).FullName + ", taking a single paramater of type " + typeof(T).FullName);
            return new CsScript<T, R>(lc, mi);
        }


        #region Internal

        static long InstanceId = (DateTime.UtcNow - new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks;


        const String ScriptPrefix =
@"
namespace CsScript
{
    public static class ";


        const String ScriptSuffix =
"""
    }
}
""";


        static readonly Type[] DefTypes = [
                typeof(object),
                typeof(Enumerable),
                typeof(Dictionary<,>),
                typeof(IReadOnlyDictionary<,>),
                typeof(IReadOnlyList<>),
                typeof(ConcurrentDictionary<,>),
                typeof(Directory),
                typeof(Task),
                typeof(Thread),
                typeof(AssemblyTargetedPatchBandAttribute),
                typeof(Type),
                typeof(JitInfo),

        ];

        static void InternalLoad(out SimpleUnloadableAssemblyLoadContext lc, out Type type, String sourceCode, Assembly[] assemblies, LanguageVersion csVersion, params Type[] extraTypes)
        {
            HashSet<Assembly> asms = new();
            HashSet<String> namespaces = new(StringComparer.Ordinal);
            foreach (var a in DefTypes)
            {
                namespaces.Add(a.Namespace);
                asms.Add(a.Assembly);
            }
            if (extraTypes != null)
                foreach (var a in extraTypes)
                {
                    namespaces.Add(a.Namespace);
                    asms.Add(a.Assembly);
                }
            if (assemblies != null)
                foreach (var a in assemblies)
                    asms.Add(a);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                asms.Add(a);

            var asmName = "CsScript_" + Interlocked.Increment(ref InstanceId);
            var refs = asms.Select(x => MetadataReference.CreateFromFile(x.Location)).ToList();
            sourceCode = String.Concat(String.Join('\n', namespaces.Select(x => String.Join(x, "using ", ";"))), ScriptPrefix, asmName, "\n\t\t{\n", sourceCode, ScriptSuffix);
            var codeString = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(csVersion);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

            var comp = CSharpCompilation.Create(asmName,
                            [parsedSyntaxTree],
                            refs,
                            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                            optimizationLevel: OptimizationLevel.Release,
                            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
            using var ms = new MemoryStream();
            var res = comp.Emit(ms);
            if (!res.Success)
            {
                StringBuilder b = new StringBuilder();
                foreach (var diagnostic in comp.GetDiagnostics().Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error))
                    b.Append(diagnostic.Id).Append(": ").AppendLine(diagnostic.GetMessage());
                throw new Exception("Failed to compile source code.\n" + b.ToString());
            }
            ms.Position = 0;

            lc = new SimpleUnloadableAssemblyLoadContext();
            var asm = lc.LoadFromStream(ms);
            type = asm.GetType("CsScript." + asmName);
        }

        #endregion//Internal


    }
}

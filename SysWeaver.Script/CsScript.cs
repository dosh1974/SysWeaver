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
    /// <summary>
    /// Compiles C# source code (with Roslyn) into a collectible assembly and exposes its static Main method as a function.
    /// The source code is the body of a static class (in the namespace "CsScript"), it must contain a static method "Main" with a single parameter of the input type (found with the default binder, so a parameter type that the input type widens to, ex: long for int, is accepted too).
    /// The script can use the namespaces of some default types (System, System.Linq, System.Collections.Generic, System.Collections.Concurrent, System.IO, System.Threading, System.Threading.Tasks, System.Runtime),
    /// of the input and result types and of the supplied types. All assemblies loaded from a file into the process are referenced.
    /// Scripts run with the full permissions of the process, only run trusted code
    /// </summary>
    public static class CsScript
    {



        /// <summary>
        /// Compile an async script, the source must contain a "static async Task&lt;R&gt; Main(T p)" method (or any static Main(T) returning a Task&lt;R&gt;)
        /// </summary>
        /// <typeparam name="T">The input type</typeparam>
        /// <typeparam name="R">The result type (of the task)</typeparam>
        /// <param name="sourceCode">The body of the static script class</param>
        /// <param name="csVersion">The C# language version to use</param>
        /// <returns>The script, dispose it to unload the compiled assembly</returns>
        /// <exception cref="ArgumentNullException">The source code is null</exception>
        /// <exception cref="ArgumentException">The source code doesn't compile (the message contains the compiler errors) or there is no matching Main method</exception>
        public static CsAsyncScript<T, R> CreateAsync<T, R>(String sourceCode, LanguageVersion csVersion = LanguageVersion.CSharp12)
            => CreateAsync<T, R>(sourceCode, null, null, csVersion);

        /// <summary>
        /// Compile an async script, the source must contain a "static async Task&lt;R&gt; Main(T p)" method (or any static Main(T) returning a Task&lt;R&gt;)
        /// </summary>
        /// <typeparam name="T">The input type</typeparam>
        /// <typeparam name="R">The result type (of the task)</typeparam>
        /// <param name="sourceCode">The body of the static script class</param>
        /// <param name="assemblies">Extra assemblies to reference (null for none), only assemblies loaded from a file can be referenced</param>
        /// <param name="types">Types whose namespaces are imported (with using) and whose assemblies are referenced (null for none)</param>
        /// <param name="csVersion">The C# language version to use</param>
        /// <returns>The script, dispose it to unload the compiled assembly</returns>
        /// <exception cref="ArgumentNullException">The source code is null</exception>
        /// <exception cref="ArgumentException">The source code doesn't compile (the message contains the compiler errors) or there is no matching Main method</exception>
        public static CsAsyncScript<T, R> CreateAsync<T, R>(String sourceCode, Assembly[] assemblies, Type[] types, LanguageVersion csVersion = LanguageVersion.CSharp12)
        {
            ArgumentNullException.ThrowIfNull(sourceCode);
            var cl = types?.Length ?? 0;
            var t = new Type[cl + 2];
            for (int i = 0; i < cl; ++i)
                t[i] = types[i];
            t[cl] = typeof(T);
            t[cl + 1] = typeof(R);
            InternalLoad(out var lc, out var type, sourceCode, assemblies, csVersion, t);
            var mi = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(T)]);
            if ((mi == null) || (mi.ReturnType != typeof(Task<>).MakeGenericType(typeof(R))))
            {
                lc.Unload();
                throw new ArgumentException("Expected a static Main function returning a Task<" + typeof(R).FullName + ">, taking a single paramater of type " + typeof(T).FullName, nameof(sourceCode));
            }
            return new CsAsyncScript<T, R>(lc, mi);
        }

        /// <summary>
        /// Compile a script, the source must contain a "static R Main(T p)" method
        /// </summary>
        /// <typeparam name="T">The input type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="sourceCode">The body of the static script class, ex: "static int Main(int x) => x * x;"</param>
        /// <param name="csVersion">The C# language version to use</param>
        /// <returns>The script, dispose it to unload the compiled assembly</returns>
        /// <exception cref="ArgumentNullException">The source code is null</exception>
        /// <exception cref="ArgumentException">The source code doesn't compile (the message contains the compiler errors) or there is no matching Main method</exception>
        public static CsScript<T, R> Create<T, R>(String sourceCode, LanguageVersion csVersion = LanguageVersion.CSharp12)
            => Create<T, R>(sourceCode, null, null, csVersion);

        /// <summary>
        /// Compile a script, the source must contain a "static R Main(T p)" method
        /// </summary>
        /// <typeparam name="T">The input type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="sourceCode">The body of the static script class, ex: "static int Main(int x) => x * x;"</param>
        /// <param name="assemblies">Extra assemblies to reference (null for none), only assemblies loaded from a file can be referenced</param>
        /// <param name="types">Types whose namespaces are imported (with using) and whose assemblies are referenced (null for none)</param>
        /// <param name="csVersion">The C# language version to use</param>
        /// <returns>The script, dispose it to unload the compiled assembly</returns>
        /// <exception cref="ArgumentNullException">The source code is null</exception>
        /// <exception cref="ArgumentException">The source code doesn't compile (the message contains the compiler errors) or there is no matching Main method</exception>
        public static CsScript<T, R> Create<T, R>(String sourceCode, Assembly[] assemblies, Type[] types, LanguageVersion csVersion = LanguageVersion.CSharp12)
        {
            ArgumentNullException.ThrowIfNull(sourceCode);
            var cl = types?.Length ?? 0;
            var t = new Type[cl + 2];
            for (int i = 0; i < cl; ++i)
                t[i] = types[i];
            t[cl] = typeof(T);
            t[cl + 1] = typeof(R);
            InternalLoad(out var lc, out var type, sourceCode, assemblies, csVersion, t);
            var mi = type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, [typeof(T)]);
            if ((mi == null) || (mi.ReturnType != typeof(R)))
            {
                lc.Unload();
                throw new ArgumentException("Expected a static Main function returning a " + typeof(R).FullName + ", taking a single paramater of type " + typeof(T).FullName, nameof(sourceCode));
            }
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
            //  Only assemblies loaded from a file can be referenced (in memory ones, like other scripts, have no location)
            var refs = asms.Where(x => (!x.IsDynamic) && (!String.IsNullOrEmpty(x.Location))).Select(x => MetadataReference.CreateFromFile(x.Location)).ToList();
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
                throw new ArgumentException("Failed to compile source code.\n" + b.ToString(), nameof(sourceCode));
            }
            ms.Position = 0;

            lc = new SimpleUnloadableAssemblyLoadContext();
            var asm = lc.LoadFromStream(ms);
            type = asm.GetType("CsScript." + asmName);
        }

        #endregion//Internal


    }
}

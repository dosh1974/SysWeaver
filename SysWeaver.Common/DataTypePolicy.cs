using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SysWeaver
{
    /// <summary>
    /// Decides which types (serialized) data may name, ex: using "$type" in json, see <see cref="TypeFinder.GetForData"/>.
    /// Any type that <see cref="TypeFinder"/> can resolve (in a loaded assembly, or an assembly in the executable folder) is allowed, except:
    /// <list type="bullet">
    /// <item>Denied types (see <see cref="Denied"/> and <see cref="IsDenied"/>), also as an array element or generic argument (ex: List&lt;Process&gt;).</item>
    /// <item>Delegates and reflection types (<see cref="MemberInfo"/> including <see cref="Type"/>, <see cref="Assembly"/>, <see cref="Module"/>, <see cref="ParameterInfo"/>), unless explicitly allowed using <see cref="AllowType"/>.</item>
    /// </list>
    /// A denied type is never allowed (not even if explicitly allowed).
    /// <see cref="AllowAssembly"/> and <see cref="SerializableTypesAttribute"/> are kept for compatibility, all assemblies are allowed.
    /// </summary>
    /// <remarks>
    /// The decision is cached per type (at most <see cref="TypeFinder.MaxCachedNames"/> types).
    /// Changes (Allow*, Deny) clears the cache, call them during startup.
    /// Thread safe.
    /// </remarks>
    public static class DataTypePolicy
    {
        /// <summary>
        /// Check if (serialized) data may name the type
        /// </summary>
        /// <param name="type">The type</param>
        /// <returns>True if allowed</returns>
        public static bool IsAllowed(Type type)
        {
            if (type == null)
                return false;
            var c = Decisions;
            if (c.TryGetValue(type, out var allowed))
                return allowed;
            allowed = Compute(type);
            if (c.Count < TypeFinder.MaxCachedNames)
                c.TryAdd(type, allowed);
            return allowed;
        }

        /// <summary>
        /// Check if a type is denied (it's full name, or the full name of the generic type definition, is in the <see cref="Denied"/> set).
        /// </summary>
        /// <param name="type">The type</param>
        /// <returns>True if the type is denied</returns>
        public static bool IsDenied(Type type)
        {
            var n = (type.IsGenericType && !type.IsGenericTypeDefinition) ? type.GetGenericTypeDefinition().FullName : type.FullName;
            return (n != null) && Denied.Contains(n);
        }

        /// <summary>
        /// Allow all types in an assembly (except denied types, delegates and reflection types).
        /// </summary>
        /// <remarks>Kept for compatibility, all assemblies are allowed.</remarks>
        /// <param name="assembly">The assembly</param>
        public static void AllowAssembly(Assembly assembly)
        {
            lock (Lock)
            {
                AllowedAssemblies = [.. AllowedAssemblies, assembly];
                Reset();
            }
        }

        /// <summary>
        /// Allow a type (if it's not denied), also allows delegates and reflection types.
        /// For a generic type definition, all constructed types (with allowed type arguments) are allowed.
        /// </summary>
        /// <param name="type">The type</param>
        public static void AllowType(Type type)
        {
            lock (Lock)
            {
                AllowedTypes = [.. AllowedTypes, type];
                Reset();
            }
        }

        /// <summary>
        /// Deny types (in addition to the built-in list).
        /// </summary>
        /// <param name="fullNames">The full names of the types, for a generic type use the name of the definition, ex: "MyNamespace.MyType`1"</param>
        public static void Deny(params String[] fullNames)
        {
            lock (Lock)
            {
                Denied = ReadOnlyData.Set(StringComparer.Ordinal, Denied.Concat(fullNames));
                Reset();
            }
        }

        /// <summary>
        /// The full names of the types that are never allowed (known deserialization gadgets and types with dangerous side effects).
        /// </summary>
        public static IReadOnlySet<String> Denied { get; private set; } = ReadOnlyData.Set(StringComparer.Ordinal, GetBuiltInDenied());

        /// <summary>
        /// Known deserialization gadgets (mostly from the ysoserial.net gadget list) and other types with dangerous side effects
        /// </summary>
        static IEnumerable<String> GetBuiltInDenied()
        {
            String[] names =
            [
                //  Process / code execution
                "System.Diagnostics.Process",
                "System.Diagnostics.ProcessStartInfo",
                "System.Windows.Data.ObjectDataProvider",
                "System.Windows.ResourceDictionary",
                "System.Windows.Markup.XamlReader",
                "System.Windows.Forms.BindingSource",
                "System.Windows.Forms.AxHost+State",
                "System.Configuration.Install.AssemblyInstaller",
                "System.Activities.Presentation.WorkflowDesigner",
                "System.Workflow.ComponentModel.Serialization.ActivitySurrogateSelector",
                "System.Management.Automation.PSObject",
                "Microsoft.VisualStudio.Text.Formatting.TextFormattingRunProperties",
                "System.Web.UI.WebControls.ObjectDataSource",
                "System.Web.UI.ObjectStateFormatter",
                "System.Web.UI.LosFormatter",
                "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter",
                "System.Runtime.Serialization.Formatters.Soap.SoapFormatter",
                "System.Runtime.Serialization.NetDataContractSerializer",
                "System.Runtime.Remoting.ObjRef",
                "System.Configuration.SettingsPropertyValue",
                "System.Resources.ResourceSet",
                "System.Resources.ResourceReader",
                "System.Resources.ResXResourceSet",
                "System.Resources.ResXResourceReader",
                "System.Xml.XmlDocument",
                "System.Xml.XmlDataDocument",
                "System.Xml.Xsl.XslCompiledTransform",
                "System.Xml.Xsl.XslTransform",
                "System.Activator",
                "System.AppDomain",
                //  Identity / security
                "System.Security.Principal.WindowsIdentity",
                "System.Security.Principal.WindowsPrincipal",
                "System.Security.Claims.ClaimsIdentity",
                "System.Security.Claims.ClaimsPrincipal",
                "Microsoft.IdentityModel.Claims.WindowsClaimsIdentity",
                "System.IdentityModel.Tokens.SessionSecurityToken",
                "System.IdentityModel.Tokens.SessionSecurityTokenHandler",
                "System.Web.Security.RolePrincipal",
                //  Data
                "System.Data.DataSet",
                "System.Data.DataTable",
                "System.Data.DataViewManager",
                //  File system / network
                "System.IO.FileInfo",
                "System.IO.DirectoryInfo",
                "System.IO.FileSystemInfo",
                "System.IO.FileSystemWatcher",
                "System.CodeDom.Compiler.TempFileCollection",
                "System.Net.WebClient",
                "System.Net.Http.HttpClient",
                "System.Net.Sockets.Socket",
            ];
            //  System.Data.Services.Internal.ExpandedWrapper`1 .. `13
            return names.Concat(Enumerable.Range(1, 13).Select(x => "System.Data.Services.Internal.ExpandedWrapper`" + x));
        }

        #region Implementation

        static bool Compute(Type t)
        {
            if (t.IsByRef || t.IsPointer || t.IsGenericParameter || t.ContainsGenericParameters)
                return false;
            if (IsDenied(t))
                return false;
            if (t.IsArray)
                return IsAllowed(t.GetElementType());
            if (t.IsGenericType)
            {
                foreach (var a in t.GetGenericArguments())
                    if (!IsAllowed(a))
                        return false;
                if (AllowedTypes.Contains(t.GetGenericTypeDefinition()))
                    return true;
            }
            else if (AllowedTypes.Contains(t))
                return true;
            return !IsDangerousKind(t);
        }

        /// <summary>
        /// Delegates and reflection types
        /// </summary>
        static bool IsDangerousKind(Type t)
            => typeof(Delegate).IsAssignableFrom(t)
            || typeof(MemberInfo).IsAssignableFrom(t)
            || typeof(Assembly).IsAssignableFrom(t)
            || typeof(Module).IsAssignableFrom(t)
            || typeof(ParameterInfo).IsAssignableFrom(t);

        static void Reset() => Decisions.Clear();

        /// <summary>
        /// The decision per type
        /// </summary>
        static readonly LowAllocConcurrentDictionary<Type, bool> Decisions = new ();

        static readonly Object Lock = new Object();

        static volatile Assembly[] AllowedAssemblies = [];

        static volatile Type[] AllowedTypes = [];

        #endregion//Implementation
    }


    /// <summary>
    /// Marks an assembly whose types (serialized) data may name, see <see cref="DataTypePolicy"/>.
    /// Usage: [assembly: SerializableTypes]
    /// Kept for compatibility: the policy currently allows all assemblies (except denied types, delegates and reflection types).
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class SerializableTypesAttribute : Attribute
    {
    }


    /// <summary>
    /// Thrown when (serialized) data names a type that is not allowed by the <see cref="DataTypePolicy"/>, or that isn't assignable to the declared type.
    /// </summary>
    public sealed class DataTypeNotAllowedException : Exception
    {
        /// <summary>
        /// Create the exception
        /// </summary>
        /// <param name="typeName">The type name in the data</param>
        /// <param name="type">The resolved type</param>
        /// <param name="expectedType">The declared type, null if the type isn't allowed by the policy</param>
        public DataTypeNotAllowedException(String typeName, Type type, Type expectedType)
            : base(expectedType == null
                ? String.Concat("The type \"", typeName, "\" is not allowed in serialized data")
                : String.Concat("The type \"", typeName, "\" is not assignable to \"", expectedType.FullName, "\""))
        {
            TypeName = typeName;
            Type = type;
            ExpectedType = expectedType;
        }

        /// <summary>
        /// The type name in the data
        /// </summary>
        public readonly String TypeName;

        /// <summary>
        /// The resolved type
        /// </summary>
        public readonly Type Type;

        /// <summary>
        /// The declared type, null if the type isn't allowed by the policy
        /// </summary>
        public readonly Type ExpectedType;
    }

}

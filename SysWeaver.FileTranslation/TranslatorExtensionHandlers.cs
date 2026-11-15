using System;
using SysWeaver.MicroService.ExtensionHandlers;

namespace SysWeaver.Translation
{
    /// <summary>
    /// Registers the translation handlers for js, html and htm files (registered when created, unregistered when disposed)
    /// </summary>
    public sealed class TranslatorExtensionHandlers : IDisposable
    {
        /// <inheritdoc />
        public override string ToString() => "Handle .js file translations";


        /// <summary>
        /// Register the translation handlers for "js", "html" and "htm" files
        /// </summary>
        public static void Register()
        {
            LanguageTemplate.AddHandler("js", JsTranslation.CreateTemplate);
            LanguageTemplate.AddHandler("html", HtmlTranslation.CreateTemplate);
            LanguageTemplate.AddHandler("htm", HtmlTranslation.CreateTemplate);
        }

        /// <summary>
        /// Unregister the translation handlers for "js", "html" and "htm" files
        /// </summary>
        public static void Unregister()
        {
            LanguageTemplate.RemoveHandler("htm");
            LanguageTemplate.RemoveHandler("html");
            LanguageTemplate.RemoveHandler("js");
        }

        /// <summary>
        /// Create the instance and register the handlers (<see cref="Register"/>)
        /// </summary>
        public TranslatorExtensionHandlers()
        {
            Register();
        }

        /// <summary>
        /// Unregister the handlers (<see cref="Unregister"/>)
        /// </summary>
        public void Dispose()
        {
            Unregister();
        }


    }


}

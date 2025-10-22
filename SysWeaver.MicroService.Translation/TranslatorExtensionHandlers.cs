using System;
using SysWeaver.MicroService.ExtensionHandlers;

namespace SysWeaver.MicroService
{
    public sealed class TranslatorExtensionHandlers : IDisposable
    {
        public override string ToString() => "Handle .js file translations";


        public static void Register()
        {
            LanguageTemplate.AddHandler("js", JsTranslation.CreateTemplate);
            LanguageTemplate.AddHandler("html", HtmlTranslation.CreateTemplate);
            LanguageTemplate.AddHandler("htm", HtmlTranslation.CreateTemplate);
        }

        public static void Unregister()
        {
            LanguageTemplate.RemoveHandler("htm");
            LanguageTemplate.RemoveHandler("html");
            LanguageTemplate.RemoveHandler("js");
        }

        public TranslatorExtensionHandlers()
        {
            Register();
        }

        public void Dispose()
        {
            Unregister();
        }


    }


}

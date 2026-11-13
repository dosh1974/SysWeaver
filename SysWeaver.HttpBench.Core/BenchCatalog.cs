using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.HttpBench
{
    /// <summary>
    /// One end point that a test can call.
    /// </summary>
    public sealed class BenchEndPoint
    {
        /// <summary>
        /// A unique id ("module.name"), used by the client to select it.
        /// </summary>
        public String Id;

        /// <summary>
        /// The id of the module (group) of the end point.
        /// </summary>
        public String Module;

        /// <summary>
        /// The display name.
        /// </summary>
        public String Name;

        /// <summary>
        /// The http method: GET or POST.
        /// </summary>
        public String Method;

        /// <summary>
        /// The url relative to the root of the target server (may include a query string).
        /// </summary>
        public String Path;

        /// <summary>
        /// The json body of a POST (null for none).
        /// </summary>
        public String Body;

        /// <summary>
        /// True for an api (Accept: application/json), false for a file.
        /// </summary>
        public bool IsApi;

        /// <summary>
        /// What it does and what part of SysWeaver it exercises.
        /// </summary>
        public String Desc;

        public override string ToString() => String.Concat(Method, ' ', Path);
    }

    /// <summary>
    /// A group of end points that the user toggles on and off together.
    /// </summary>
    public sealed class BenchModule
    {
        /// <summary>
        /// A unique id.
        /// </summary>
        public String Id;

        /// <summary>
        /// The display name.
        /// </summary>
        public String Name;

        /// <summary>
        /// What the end points of this module exercise.
        /// </summary>
        public String Desc;

        /// <summary>
        /// An icon (inline icon name of the Sw ui library).
        /// </summary>
        public String Icon;

        /// <summary>
        /// True if the module is selected by default.
        /// </summary>
        public bool Default;

        /// <summary>
        /// The end points of this module.
        /// </summary>
        public BenchEndPoint[] EndPoints;
    }

    /// <summary>
    /// The end points that can be tested.
    /// A selection of public (no sign in needed) end points of the services that a typical SysWeaver server runs
    /// (the services of SwServerManager, except the folder sync, server manager and the http server hosts), picked to hit as many of the
    /// common (frequently used) modules as possible: static and templated files, generated images, the icon module, the application and
    /// session apis, auth helpers, type information and table data, the explore listings and the media service.
    /// This is not a coverage test.
    /// </summary>
    public static class BenchCatalog
    {
        static BenchEndPoint File(String name, String path, String desc) => new BenchEndPoint
        {
            Name = name,
            Method = "GET",
            Path = path,
            Desc = desc,
        };

        static BenchEndPoint Get(String name, String path, String desc) => new BenchEndPoint
        {
            Name = name,
            Method = "GET",
            Path = path,
            IsApi = true,
            Desc = desc,
        };

        static BenchEndPoint Post(String name, String path, String body, String desc) => new BenchEndPoint
        {
            Name = name,
            Method = "POST",
            Path = path,
            Body = body,
            IsApi = true,
            Desc = desc,
        };

        static BenchModule Module(String id, String name, String icon, bool def, String desc, params BenchEndPoint[] endPoints)
        {
            foreach (var e in endPoints)
            {
                e.Module = id;
                e.Id = String.Concat(id, '.', e.Name);
            }
            return new BenchModule
            {
                Id = id,
                Name = name,
                Icon = icon,
                Default = def,
                Desc = desc,
                EndPoints = endPoints,
            };
        }

        /// <summary>
        /// The urls that a page asks Api/auth/CanCall about (Sw.Access asks about every api a page uses in one request).
        /// </summary>
        const String CanCallUrls = "[" +
            "\"Api/auth/GetUser\",\"Api/application/GetMenu\",\"Api/application/GetLanguage\",\"Api/application/GetLocalizedLanguages\"," +
            "\"Api/application/GetSupportedLanguages\",\"Api/GetTableExporters\",\"Api/GetChartExporters\",\"Api/TableChart\"," +
            "\"Api/TableChartAvailable\",\"Api/ExportTableData\",\"Api/jobs/GetJobs\",\"Api/debug/ApiTable\",\"Api/debug/LogTable\"," +
            "\"Api/debug/PerfTable\",\"Api/debug/ActiveSessions\",\"Api/admin/GetServerConfig\",\"edit/GetTypeInfo\",\"edit/GetTypeInfoTable\"," +
            "\"mediaView/Api/GetBackgroundEffects\",\"upload/CheckStatus\"" +
            "]";

        /// <summary>
        /// All modules, in display order.
        /// </summary>
        public static readonly BenchModule[] Modules =
        [
            Module("Files", "Pages and scripts", "fileText", true,
                "Static files embedded in the service assemblies (pre-compressed with brotli) and the templated files that the server fills in per request: the file module, the static data module and the template engine.",
                File("index.html", "index.html", "The start page (a template: variables are filled in per request)"),
                File("Home.html", "app/Home.html", "The home page of the app shell (a template)"),
                File("core.js", "sw/core.js", "The core script bundle that every page loads (about 60 KB compressed)"),
                File("ui.js", "sw/ui.js", "The ui component bundle (about 110 KB compressed)"),
                File("ui.css", "sw/ui.css", "The ui style sheet"),
                File("theme.css", "common/theme.css", "The theme colours and fonts (a template, depends on the app seed)"),
                File("table.js", "explore/table.js", "The data table script (explore module)"),
                File("chart.js", "chart/chart.js", "The chart viewer script (ChartJs service)"),
                File("Edit.js", "edit/Edit.js", "The object editor script (Edit service)"),
                File("fonts.css", "brand/fonts.css", "The brand fonts as data urls, the largest common file (about 150 KB)"),
                File("manifest.json", "app/manifest.json", "The web app manifest (a template)")
            ),

            Module("Images", "Icons and branding", "image", true,
                "The app images that the server generates from the app seed (svg and rendered png bitmaps, cached in memory) and single icons of the icon module.",
                File("favicon.ico", "favicon.ico", "The browser tab icon (generated, several bitmap sizes)"),
                File("icon.svg", "icon.svg", "The app icon (generated svg)"),
                File("logo.svg", "logo.svg", "The app logo (generated svg)"),
                File("banner.svg", "banner.svg", "The app banner (generated svg)"),
                File("icon-192.png", "icon-192.png", "The app icon rendered as a 192 px png"),
                File("logo.png", "logo.png", "The app logo rendered as a png"),
                File("icons/chart.svg", "icons/chart.svg", "A mask icon of the icon module"),
                File("icons/api.svg", "icons/api.svg", "A colour icon of the icon module")
            ),

            Module("Application", "Application and session", "home", true,
                "The apis that the app shell and every page call: menus, languages, the signed in user, access checks and the server clock. They create and use the session of each worker.",
                File("serverTime", "serverTime", "The server clock (the shell syncs its time with it)"),
                Get("GetMenu", "Api/application/GetMenu", "The main menu of the session (built from the menu attributes of all services, filtered by auth)"),
                Get("GetLanguage", "Api/application/GetLanguage", "The language of the session"),
                Get("GetSupportedLanguages", "Api/application/GetSupportedLanguages", "The languages the server can translate to"),
                Get("GetLocalizedLanguages", "Api/application/GetLocalizedLanguages", "The language names, localized to the session language (about 2 KB)"),
                Get("GetUser", "Api/auth/GetUser", "The signed in user of the session (none for a worker)"),
                Post("CanCall", "Api/auth/CanCall", CanCallUrls, "Which of 20 apis the session may call (Sw.Access asks this for the buttons of a page)"),
                Get("HaveService", "Api/HaveService?%22SysWeaver.MicroService.ChartJsService%22", "Is a service running (the type name is a json string in the query)"),
                Get("GetTableExporters", "Api/GetTableExporters", "The export formats of the data tables"),
                Get("GetJobs", "Api/jobs/GetJobs", "The background jobs of the session (the activity list of the shell)")
            ),

            Module("Auth", "Auth and validation", "shield", true,
                "Public helpers of the auth service that parse and validate input (POSTed json): password policies, e-mail addresses and phone numbers (iso data look ups).",
                Get("GetPasswordPolicy", "Api/auth/GetPasswordPolicy", "The password policy of the server"),
                Post("ValidateEmailAddress", "Api/auth/ValidateEmailAddress", "\"someone.else@example.com\"", "Validate an e-mail address"),
                Post("ValidatePhoneNumber", "Api/auth/ValidatePhoneNumber", "\"+46 70 123 45 67\"", "Validate a phone number"),
                Post("GetPhoneNumberInfo", "Api/auth/GetPhoneNumberInfo", "\"+46701234567\"", "The country and parts of a phone number"),
                Post("GetPhonePrefixInfo", "Api/auth/GetPhonePrefixInfo", "\"+4670\"", "The countries that a partial phone number may belong to")
            ),

            Module("Types", "Types and tables", "db", true,
                "Reflection based type information (the object editor uses it), the table data engine (sorting, paging and filtering of rows) and the viewer look ups.",
                Post("GetTypeInfo", "edit/GetTypeInfo", "\"SysWeaver.Data.TableDataRequest\"", "The description of a type (members, editors, docs)"),
                Get("GetDefaultInstance", "edit/GetDefaultInstance?%22SysWeaver.Data.TableDataRequest%22", "A default instance of a type, serialized as json"),
                Post("GetTypeInfoTable", "edit/GetTypeInfoTable", "{\"Param\":\"SysWeaver.Data.TableDataRequest\",\"MaxRowCount\":20,\"Order\":[\"-Name\"]}", "The members of a type as table data (sorted, paged)"),
                Get("GetExtensionViewer", "Api/GetExtensionViewer?%22.json%22", "The viewer page of a file extension"),
                Get("TableChartAvailable", "Api/TableChartAvailable", "Can tables be shown as charts (ChartJs service)")
            ),

            Module("Explore", "Explore and media", "folder", true,
                "Listings that are built per request (the explore module walks the end points of the server; public when the explore service has \"Auth\": null, as in SwServerManager) and the effect lists of the media service.",
                File("explore", "explore", "The explore listing of the site root (an html page built per request)"),
                File("Api/explore", "Api/explore", "The explore listing of the api root"),
                File("icons/explore", "icons/explore", "The explore listing of the icon folder (about 1 200 icons)"),
                Get("GetBackgroundEffects", "mediaView/Api/GetBackgroundEffects", "The background effects of the media service"),
                Get("GetImageEffects", "mediaView/Api/GetImageEffects", "The image effects of the media service"),
                Get("GetCollageEffects", "mediaView/Api/GetCollageEffects", "The collage effects of the media service")
            ),

            Module("Errors", "Error cases", "alert", false,
                "Urls that don't exist, to measure how fast the server refuses them (404 Not found). They are counted as errors, so the error rate of a test with them is never 0.",
                File("invalid_url", "invalid_url", "A file that doesn't exist: the request passes every http module of the server and ends in a 404"),
                Get("Api/invalid_url", "Api/invalid_url", "An api that doesn't exist: a 404 from the api module")
            ),
        ];

        /// <summary>
        /// All end points by id.
        /// </summary>
        public static readonly IReadOnlyDictionary<String, BenchEndPoint> ById = Modules.SelectMany(x => x.EndPoints).ToDictionary(x => x.Id, StringComparer.Ordinal);

        /// <summary>
        /// All modules by id.
        /// </summary>
        public static readonly IReadOnlyDictionary<String, BenchModule> ModuleById = Modules.ToDictionary(x => x.Id, StringComparer.Ordinal);
    }
}

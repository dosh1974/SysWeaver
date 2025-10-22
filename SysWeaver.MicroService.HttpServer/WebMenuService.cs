using SysWeaver.Docs;
using SysWeaver.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SysWeaver.Translation;

namespace SysWeaver.MicroService
{
    [IsMicroService]
    [RequiredDep<ApiHttpServerModule>]
    [WebApiUrl("application")]
    public sealed class WebMenuService : IDisposable
    {

        public override string ToString()
        {
            var rs = Menus;
            var r = rs.Count;
            return String.Concat(r, r == 1 ? " menu: " : " menues: ", String.Join(", ", rs.Select(x => x.Key.ToQuoted())));
        }

        public WebMenuService(ServiceManager manager)
        {
            Manager = manager;
            var api = manager.Get<ApiHttpServerModule>();
            Api = api;
            if (api != null)
            {
                foreach (var a in api.Apis)
                    TryAddApi(a);
                api.OnApiAdded += TryAddApi;
                api.OnApiRemoved += TryRemoveApi;
            }
            foreach (var i in manager.OrderedUniqueInstances)
                TryAddInstance(i, null);
            manager.OnServiceAdded += TryAddInstance;
            manager.OnServiceRemoved += TryRemoveInstance;
        }

        public void Dispose()
        {
            Manager.OnServiceRemoved -= TryRemoveInstance;
            Manager.OnServiceAdded -= TryAddInstance;
            var api = Api;
            if (api != null)
            {
                api.OnApiRemoved -= TryRemoveApi;
                api.OnApiAdded -= TryAddApi;
            }
        }

        void TryAddApi(IApiHttpServerEndPoint api)
        {
            var m = api.MethodInfo;
            var a = m.GetCustomAttributes(typeof(WebMenuAttribute), true);
            if (a.Length <= 0)
                return;
            var mid = m.Name;
            var mname = StringTools.RemoveCamelCase(mid);
            var mtitle = m.XmlDoc().ToTitle();
            var authS = api.Auth;
            var auth = authS == null ? null : String.Join(',', authS);
            foreach (WebMenuAttribute attr in a)
            {
                var type = attr.Type;
                var id = attr.Id;
                if (String.IsNullOrEmpty(id))
                    id = "{0}";
                id = String.Format(id, mid);
                var name = attr.Name;
                if (String.IsNullOrEmpty(name))
                    name = "{0}";
                name = String.Format(name, mname);
                var title = attr.Title;
                if (title != null)
                    title = String.Format(title, mname, mid);
                else
                    title = mtitle;
                var aa = auth;
                if (attr.Auth == null)
                {
                    if (attr.NoUser)
                        aa = null;
                }else
                {
                    aa = attr.Auth;
                }
                var adata = attr.Data;
                if (adata != null)
                    adata = String.Format(adata, api.Uri);
                switch (type)
                {
                    case WebMenuItemTypes.Table:
                        SetItem(api.Instance, attr.Menu, id, type, name, adata ?? api.Uri, title, attr.IconClass, aa, attr.Order, attr.NoUser, attr.Dynamic);
                        break;
                    case WebMenuItemTypes.Chart:
                        SetItem(api.Instance, attr.Menu, id, type, name, adata ?? api.Uri, title, attr.IconClass, aa, attr.Order, attr.NoUser, attr.Dynamic);
                        break;
                    default:
                        throw new Exception("Can't add " + type + " types to API's");
                }
            }
        }



        void TryRemoveApi(IApiHttpServerEndPoint api)
        {
            var m = api.MethodInfo;
            var a = m.GetCustomAttributes(typeof(WebMenuAttribute), true);
            if (a.Length <= 0)
                return;
        }

        void TryAddInstance(Object inst, ServiceInfo info)
        {
            var t = inst.GetType();
            var a = t.GetCustomAttributes(typeof(WebMenuAttribute), true);
            if (a.Length <= 0)
                return;

            var mid = t.Name;
            var mname = StringTools.RemoveCamelCase(mid);
            var mtitle = t.XmlDoc().ToTitle();
            foreach (WebMenuAttribute attr in a)
            {
                var type = attr.Type;
                var id = attr.Id;
                if (String.IsNullOrEmpty(id))
                    id = "{0}";
                id = String.Format(id, mid);
                var name = attr.Name;
                if (String.IsNullOrEmpty(name))
                    name = "{0}";
                name = String.Format(name, mname);
                var title = attr.Title;
                if (title != null)
                    title = String.Format(title, mname, mid);
                else
                    title = mtitle;
                switch (type)
                {
                    case WebMenuItemTypes.Path:
                        SetItem(inst, attr.Menu, id, type, name, null, title, attr.IconClass, null, attr.Order, false, attr.Dynamic);
                        break;
                    case WebMenuItemTypes.Embedded:
                    case WebMenuItemTypes.Link:
                    case WebMenuItemTypes.LinkExternal:
                    case WebMenuItemTypes.Js:
                    case WebMenuItemTypes.Table:
                        SetItem(inst, attr.Menu, id, type, name, attr.Data, title, attr.IconClass, attr.Auth, attr.Order, attr.NoUser, attr.Dynamic);
                        break;
                    default:
                        throw new Exception("Can't add " + type + " types to API's");
                }
            }
        }

        void TryRemoveInstance(Object inst, ServiceInfo info)
        {
        }


        readonly ServiceManager Manager;
        readonly ApiHttpServerModule Api;
        readonly String DefaultMenu = "Default";

        public static String GetParent(String name)
        {
            var i = name.LastIndexOf('/');
            if (i < 0)
                return null;
            return name.Substring(0, i);
        }


        public static String GetId(out String parent, String name)
        {
            var i = name.LastIndexOf('/');
            if (i < 0)
            {
                parent = null;
                return name;
            }
            parent = name.Substring(0, i);
            return name.Substring(i + 1);
        }

        public void SetItem(Object instance, String menu, String id, WebMenuItemTypes? type, String name, String data, String title, String iconClass, String auth, float order, bool noUserRequired, String dynamic)
        {
            menu = menu ?? DefaultMenu;
            var ms = Menus;
            if (!ms.TryGetValue(menu, out var m))
            {
                m = new Menu();
                if (!ms.TryAdd(menu, m))
                    m = ms[menu];
            }
            var items = m.Items;
            if (!items.TryGetValue(id, out var item))
            {
                item = new Item();
                if (!items.TryAdd(id, item))
                    item = items[id];
            }
            lock (item)
            {
                if (type != null)
                    item.Type = type ?? WebMenuItemTypes.Path;
                if (name != null)
                    item.Name = name;
                if (data != null)
                    item.Data = data;
                if (title != null)
                    item.Title = title;
                if (iconClass != null)
                    item.IconClass = iconClass;
                if (auth != null)
                    item.Auth = auth;
                item.Order = order;
                item.NoUser = noUserRequired;
                if (dynamic != null)
                {
                    var p = dynamic.Split('.');
                    var pl = p.Length - 1;
                    Type dtype;
                    if (pl > 0)
                    {
                        var dtypen = String.Join('.', p, 0, pl);
                        dtype = TypeFinder.Get(dtypen);
                        if (dtype == null)
                            throw new Exception("Can't find type " + dtypen.ToQuoted());
                    }else
                    {
                        dtype = instance.GetType();
                    }
                    var methodName = p[pl];
                    var pReq = ParamReq;
                    var pItem = ParamItem;
                    var pAuth = ParamAuth;

                    var method = dtype.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, DynTypes);
                    if ((method == null) || (method.ReturnType != typeof(Task<Boolean>)))
                    {
                        method = dtype.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, DynTypes2);
                        if ((method == null) || (method.ReturnType != typeof(Task<Boolean>)))
                            throw new Exception("No method named " + methodName.ToQuoted() + " with the signature " + DynSig + " or " + DynSig2 + " found in the type " + dtype.FullName.ToQuoted());
                        item.Dynamic = Expression.Lambda<Func<HttpServerRequest, WebMenuItem, Task<bool>>>(method.IsStatic ? Expression.Call(method, pAuth, pItem) : Expression.Call(Expression.Constant(instance, instance.GetType()), method, pAuth, pItem), Params).Compile();
                    }
                    else
                    {
                        item.Dynamic = Expression.Lambda<Func<HttpServerRequest, WebMenuItem, Task<bool>>>(method.IsStatic ? Expression.Call(method, pReq, pItem) : Expression.Call(Expression.Constant(instance, instance.GetType()), method, pReq, pItem), Params).Compile();
                    }
                }
            }
            for (; ;)
            { 
                id = GetParent(id);
                if (id == null)
                    break;
                if (items.TryGetValue(id, out item))
                    break;
                name = GetId(out var p, id);
                item = new Item
                {
                    Name = StringTools.RemoveCamelCase(name),
                    Order = -1,
                };
                if (!items.TryAdd(id, item))
                    break;
            }
        }

        static readonly ParameterExpression ParamReq = Expression.Parameter(typeof(HttpServerRequest), "req");
        static readonly Expression ParamAuth = Expression.PropertyOrField(Expression.PropertyOrField(ParamReq, nameof(HttpServerRequest.Session)), nameof(HttpSession.Auth));
        static readonly ParameterExpression ParamItem = Expression.Parameter(typeof(WebMenuItem), "item");
        static readonly Type[] DynTypes = [typeof(HttpServerRequest), typeof(WebMenuItem) ];
        static readonly Type[] DynTypes2 = [typeof(Auth.Authorization), typeof(WebMenuItem)];
        static readonly String DynSig = String.Concat(typeof(Task<Boolean>), " Method(", String.Join(", ", DynTypes.Select(x => x.ToString())), ')');
        static readonly String DynSig2 = String.Concat(typeof(Task<Boolean>), " Method(", String.Join(", ", DynTypes2.Select(x => x.ToString())), ')');
        static readonly ParameterExpression[] Params = [ParamReq, ParamItem];


        /// <summary>
        /// Get a menu
        /// </summary>
        /// <param name="menu">The menu to retrieve</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns></returns>
        [WebApi]
        [WebApiClientCache(1)]
        [WebApiRequestCache(60)]
        [WebApiCompression("br:Best, deflate:Best, gzip:Best")]
        public async Task<WebMenu> GetMenu(String menu, HttpServerRequest context)
        {
            menu = menu ?? DefaultMenu;
            var author = context?.Session?.Auth;
            List<KeyValuePair<String, Item>> items = new();
            foreach (var x in menu.Split(','))
            {

                if (!Menus.TryGetValue(x.Trim(), out var m))
                    return null;
                var its = m.Items.ToList();
                its.Sort((a, b) => a.Key.CompareTo(b.Key));
                foreach (var y in its)
                    items.Add(y);
            }
            var itemCount = items.Count;
            if (itemCount == 0) 
                return null;
            BuildItem root = new BuildItem(new WebMenuItem(), 0);
            Dictionary<String, BuildItem> nodes = new (StringComparer.Ordinal);
            for (int i = 0; i < itemCount; ++ i)
            {
                var item = items[i];
                var val = item.Value;
            //  Make sure auth is there
                var auth = val.Auth;
                if (auth != null)
                {
                    if (author == null)
                        continue;
                    if (!author.IsValid(auth))
                        continue;
                }
            //  May not return item if there is no user
                if (val.NoUser)
                {
                    if (author != null)
                        continue;
                }
            //  Get name and parent
                var id = GetId(out var parentId, item.Key);
                var parentNode = parentId == null ? root : nodes[parentId];
                var dit = await val.Get(id, context).ConfigureAwait(false);
                if (dit == null)
                    continue;
                var newItem = new BuildItem(dit, val.Order);
                nodes[item.Key] = newItem;
                parentNode.Children.Add(newItem);
            }
            root.Keep();
            var ritems = root.Item.Children;
            return new WebMenu
            {
                RootUri = context.Prefix,
                Items = ritems,
            };
        }

        /*

        async Task<WebMenuItem> TranslateMenu(ITranslator translator, String language, WebMenuItem item)
            => await TranslationCache.GetOrUpdateAsync(ValueTuple.Create(item, language), async key =>
            {
                var name = item.Name;
                var title = item.Title;
                String titleDesc = String.IsNullOrEmpty(title) ? "" : String.Concat("\nThe description (as shown using a tool tip) for this item is \"", title, "\".");
                String newName;
                String newTitle = null;
                if (String.IsNullOrEmpty(title))
                {
                    newName = await translator.TranslateOne(new TranslateRequest
                    {
                        From = "en",
                        To = language,
                        Text = name,
                        Context = "This is the text for a menu item, keep it short." + titleDesc
                    }).ConfigureAwait(false);
                }
                else
                {
                    Task<String>[] tasks = new Task<string>[2];
                    tasks[0] = translator.TranslateOne(new TranslateRequest
                    {
                        From = "en",
                        To = language,
                        Text = name,
                        Context = "This is the text for a menu item, keep it short." + titleDesc
                    });
                    tasks[1] = translator.TranslateOne(new TranslateRequest
                    {
                        From = "en",
                        To = language,
                        Text = title,
                        Context = String.Concat("This is the description (shown using a tool tip) for a menu item named \"", name, "\".")
                    });
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    newName = tasks[0].Result;
                    newTitle = tasks[1].Result;
                }
                var c = item.Children;
                var newChildren = c;
                if (c != null)
                {
                    var cl = c.Length;
                    if (cl > 0)
                    {
                        Task<WebMenuItem>[] tasks = new Task<WebMenuItem>[cl];
                        for (int i = 0; i < cl; ++i)
                            tasks[i] = TranslateMenu(translator, language, c[i]);
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        for (int i = 0; i < cl; ++i)
                            newChildren[i] = tasks[i].Result;
                    }
                }
                return new WebMenuItem
                {
                    Children = newChildren,
                    Data = item.Data,
                    IconClass = item.IconClass,
                    Id = item.Id,
                    Name = newName,
                    Title = newTitle,
                    Type = item.Type,
                };
            }).ConfigureAwait(false);


        readonly FastMemCache<ValueTuple<WebMenuItem, String>, WebMenuItem> TranslationCache = new FastMemCache<(WebMenuItem, string), WebMenuItem>(TimeSpan.FromHours(48));

        */


        readonly ConcurrentDictionary<String, Menu> Menus = new (StringComparer.Ordinal);
        sealed class Menu
        {
            public readonly ConcurrentDictionary<String, Item> Items = new (StringComparer.Ordinal);
        }


        sealed class BuildItem
        {
#if DEBUG
            public override string ToString() => Item?.ToString();
#endif//DEBUG
            public BuildItem(WebMenuItem i, float order)
            {
                Item = i;
                Order = order;
            }
            public readonly WebMenuItem Item;
            public readonly List<BuildItem> Children = new ();
            public readonly float Order;

            public bool Keep()
            {
                var items = Children;
                var il = items.Count;
                int o = 0;
                for (int i = 0; i < il; ++ i)
                {
                    var item = items[i];
                    if (!items[i].Keep())
                        continue;
                    items[o] = item;
                    ++o;
                }
                if (o != il)
                    items.RemoveRange(o, il - o);
                if (o <= 0)
                    return Item.Type != WebMenuItemTypes.Path;
                if (o > 1)
                    items.Sort((a, b) =>
                    {
                        var ci = a.Order.CompareTo(b.Order);
                        if (ci != 0)
                            return ci;
                        return a.Item.Name.CompareTo(b.Item.Name);
                    });
                Item.Children = items.Select(x => x.Item).ToArray();
                return true;
            }


        }

        sealed class Item
        {
#if DEBUG
            public override string ToString() => String.Concat('"', Name, "\" ", Type, " \"", Data, '"', String.IsNullOrEmpty(IconClass) ? "" : String.Join(IconClass, " (Icon: ", ')'), String.IsNullOrEmpty(Title) ? "" : String.Join(Title, " (Title: ", ')'));
#endif//DEBUG
            /// <summary>
            /// The type of the item
            /// </summary>
            public WebMenuItemTypes Type;
            /// <summary>
            /// Title (tool tip)
            /// </summary>
            public String Title;
            /// <summary>
            /// Class name for an icon
            /// </summary>
            public String IconClass;
            /// <summary>
            /// Data (type dependent, typically an url).
            /// </summary>
            public String Data;

            public String Name;

            public float Order;

            public String Auth;

            /// <summary>
            /// If true, only return if there is no user
            /// </summary>
            public bool NoUser;

            public Func<HttpServerRequest, WebMenuItem, Task<bool>> Dynamic;

            public async Task<WebMenuItem> Get(String id, HttpServerRequest req)
            {
                var t = new WebMenuItem
                {
                    Id = id,
                    Name = Name,
                    Data = Data,
                    IconClass = IconClass,
                    Title = Title,  
                    Type = Type,   
                };
                var d = Dynamic;
                if (d == null)
                    return t;
                return (await d(req, t).ConfigureAwait(false)) ? t : null;
            }

        }


    }


}

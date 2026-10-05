using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Auth;
using SysWeaver.Chat;
using SysWeaver.Data;
using SysWeaver.IsoData;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Net;
using SysWeaver.Parser;
using SysWeaver.Serialization;

namespace SysWeaver.AI
{


    [WebApiUrl("../ai")]
    public abstract partial class AiServiceBase 
    {

        public const String RequestAiToolContext = OpenAiToolExt.RequestAiToolContext;

        #region Default tools


        #region Get "static" data


        /// <summary>
        /// Get an url (svg) to a predefined image
        /// </summary>
        /// <param name="type">The type of image to display</param>
        /// <param name="request"></param>
        /// <returns>An url to the svg image</returns>
        [AiTool("🖼️📥")]
        String GetPredefinedImage(AiImages type, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return null;
            var aip = AiTools.IconRoot;
            switch (type)
            {
                case AiImages.ApplicationLogo:
                    return "../logo.svg";
                case AiImages.ApplicationIcon:
                    return "../icon.svg";
                case AiImages.AgentLogo:
                    //  The agent is only known in an AI chat (not when the tool is used by an external client, ex: over MCP)
                    if (c is AiToolContext ac)
                        return ac.Session.AgentImageUrl;
                    throw new Exception("The agent logo is only available in an AI chat");
                case AiImages.AngrySmiley:
                    return aip + "Smiley_Angry.svg";
                case AiImages.HappySmiley:
                    return aip + "Smiley_HappyCrying.svg";
                case AiImages.LoveSmiley:
                    return aip + "Smiley_Love.svg";
                case AiImages.SadSmiley:
                    return aip + "Smiley_Sad.svg";
                case AiImages.TeasingSmiley:
                    return aip + "Smiley_Tounge.svg";
            }
            return null;
        }

        /// <summary>
        /// Get an url (svg) to the image used for a given file extension
        /// </summary>
        /// <param name="fileExtension">The file extension to get an image for</param>
        /// <param name="request"></param>
        /// <returns>An url to the svg image</returns>
        [AiTool("📁📥")]
        String GetFileExtensionIcon(String fileExtension, HttpServerRequest request)
        {
            return "../icons/ext/" + fileExtension.FastTrimStartToLower('.') + ".svg";
        }

        /// <summary>
        /// Get an url (svg) to the image containing the flag for a given country.
        /// Do NOT use any other source for flags, unless this function fails.
        /// </summary>
        /// <param name="iso3166">The 2 letter, ISO 3166 alpha 2, code for the desired country.
        /// Besides the country codes, the following flags are defined:
        /// "arab": Arabic flag
        /// "eu": The European Union flag.
        /// "eac": East African Community.
        /// "aq": Antartica.
        /// Some regional flags: "es-ct", "es-ga", "es-pv", "gb-eng", "gb-nir", "gb-sct", "gb-wls", "sh-ac", "sh-hl", "sh-ta",
        /// </param>
        /// <param name="request"></param>
        /// <returns>An url to the svg image</returns>
        [AiTool("🏳️📥")]
        String GetCountryFlagIcon(String iso3166, HttpServerRequest request)
            => "../icons/flags/" + iso3166.FastToLower() + ".svg";

        #endregion//Get "static" data


        #region Generate data



        /// <summary>
        /// Get an url (svg) to a generated logo or icon for some given text.
        /// </summary>
        /// <param name="logo">Paramaters for the generation</param>
        /// <param name="request"></param>
        /// <returns>An url to the generated svg image</returns>
        [AiTool("🏷️✨")]
        String BuildLogo(AiLogo logo, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return null;
            var enc = Encoding.UTF8;
            var svgS = logo.Icon ? new SvgScene(256, 256) : new SvgScene(512, 384);
            var name = logo.Name;
            var cols = new HashColors(name, logo.Seed);
            svgS.AddFavIcon(String.IsNullOrEmpty(logo.Abbrevation) ? name : logo.Abbrevation, logo.Icon ? null : name, cols);
            var svgText = svgS.ToSvg();
            return c.AddMessageFile("image/svg+xml", svgText, logo.Title ?? (logo.Icon ? "Icon" : "Logo"));
        }


        /// <summary>
        /// Get an url (svg) to a generated QR code with the specificed content encoded.
        /// </summary>
        /// <param name="qrCodeContent">The text string to encode in the QR code</param>
        /// <param name="request"></param>
        /// <returns>An url to the generated svg image</returns>
        [AiTool("🔗✨")]
        String BuildQrCode(String qrCodeContent, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return null;
            var qr = QrCode;
            if (qr == null)
                return null;
            var data = qr.CreateQrCode(qrCodeContent);
            return c.AddMessageFile("image/svg+xml", data, "QR_" + qrCodeContent);
        }


        #endregion//Generate data

                /// <summary>
                /// Use this function to convert some data (typically text based) into an URL.
                /// </summary>
                /// <param name="data">Data paramaters</param>
                /// <param name="request"></param>
                /// <returns>An url to the data</returns>
                [AiTool("🛢️✨")]
        String BuildData(AiData data, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return null;
            FixData(data);
            return c.AddMessageFile(data.MimeType, data.Data, data.Title);
        }

        static void FixData(AiData data)
        {
            data.MimeType = MimeTypeMap.TryGetMimeType(data.MimeType, out var mt) ? mt.Item1 : data.MimeType;
            if (data.MimeType.FastEndsWith(MimeTypeMap.Utf8Suffix))
                data.Data = data.Data.Replace("<head>", "<head><base href='../../../../../'></base>");
        }


        /// <summary>
        /// Use this function to convert some data (typically text based) into an URL and then display it.
        /// Works perfect for html files, text files etc.
        /// </summary>
        /// <param name="data">Data paramaters</param>
        /// <param name="request"></param>
        /// <returns>True when sucessful</returns>
        [AiTool("🛢️🖥️")]
        [AiHideMcp]
        bool DisplayData(AiData data, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return false;
            FixData(data);
            var u = c.AddMessageFile(data.MimeType, data.Data, data.Title);
            c.AddLink(u);
            return true;
        }

        /// <summary>
        /// Use this function to display data to the user.
        /// ALWAYS use this method to display ANY url's returned by a tool.
        /// Do not output the url or link to the url or in your text output.
        /// </summary>
        /// <param name="url">The url to display</param>
        /// <param name="request"></param>
        /// <returns>True when successful</returns>
        [AiTool("🔗🖥️")]
        [AiHideMcp]
        bool DisplayUrl(String url, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;
        }

        static readonly MethodInfo Method_GetPredefinedImage = typeof(AiServiceBase).GetMethod(nameof(GetPredefinedImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_GetFileExtensionIcon = typeof(AiServiceBase).GetMethod(nameof(GetFileExtensionIcon), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_GetCountryFlagIcon = typeof(AiServiceBase).GetMethod(nameof(GetCountryFlagIcon), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildLogo = typeof(AiServiceBase).GetMethod(nameof(BuildLogo), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildData = typeof(AiServiceBase).GetMethod(nameof(BuildData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildTable = typeof(AiServiceBase).GetMethod(nameof(BuildTable), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildQrCode = typeof(AiServiceBase).GetMethod(nameof(BuildQrCode), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);


        static readonly MethodInfo Method_DisplayUrl = typeof(AiServiceBase).GetMethod(nameof(DisplayUrl), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_DisplayData = typeof(AiServiceBase).GetMethod(nameof(DisplayData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_DisplayTable = typeof(AiServiceBase).GetMethod(nameof(DisplayTable), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        static readonly MethodInfo Method_Calculate = typeof(AiServiceBase).GetMethod(nameof(Calculate), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_ElapsedTime = typeof(AiServiceBase).GetMethod(nameof(ElapsedTime), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetPredefinedImage(IAiChatSession s) =>
            s.AddTool(this, Method_GetPredefinedImage, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetFileExtensionIcon(IAiChatSession s) =>
            s.AddTool(this, Method_GetFileExtensionIcon, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetCountryFlagIcon(IAiChatSession s) =>
            s.AddTool(this, Method_GetCountryFlagIcon, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildLogo(IAiChatSession s) =>
            s.AddTool(this, Method_BuildLogo, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildQrCode(IAiChatSession s)
        {
            if (QrCode != null)
                s.AddTool(this, Method_BuildQrCode, null, PerfMon);
        }


        

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildData(IAiChatSession s) =>
            s.AddTool(this, Method_BuildData, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayData(IAiChatSession s) =>
            s.AddTool(this, Method_DisplayData, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayUrl(IAiChatSession s) =>
            s.AddTool(this, Method_DisplayUrl, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayTable(IAiChatSession s) =>
            s.AddTool(this, Method_DisplayTable, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_Calculate(IAiChatSession s) =>
            s.AddTool(this, Method_Calculate, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_ElapsedTime(IAiChatSession s) =>
            s.AddTool(this, Method_ElapsedTime, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildTable(IAiChatSession s) =>
            s.AddTool(this, Method_BuildTable, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="useAdvanced">If true, use an advanced charting method (may fails more often, but more powerful)</param>
        public void AddTool_BuildGraph(IAiChatSession s, bool useAdvanced = false) =>
            s.AddRegistredTool(useAdvanced ? "BuildAdvancedChart" : "BuildChart");

        /// <summary>
        /// Add this tool to the chat session (if not included by default).
        /// </summary>
        /// <param name="s"></param>
        /// <param name="useAdvanced">If true, use an advanced charting method (may fails more often, but more powerful)</param>
        public void AddTool_DisplayGraph(IAiChatSession s, bool useAdvanced = false) =>
            s.AddRegistredTool(useAdvanced ? "DisplayAdvancedChart" : "DisplayChart");


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildMap(IAiChatSession s)
        {
            s.AddRegistredTool("BuildMap");
            s.AddRegistredTool("GetMapRegions");
        }



        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_Store(IAiChatSession s)
        {
            if (UserStorage == null)
                return;
            s.AddRegistredTool("StoreFile");
            s.AddRegistredTool("StoreLink");
            s.AddRegistredTool("GetStoredFiles");
            s.AddRegistredTool("GetStoredLinks");

        }


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_ValidateRestApiCall(IAiChatSession s)
        {
            if (Api == null)
                return;
            s.AddTool(this, Method_ValidateRestApiCall);
        }

        #endregion//Default tools



        static String GetDescFmt(String titleFmt, int fmtIndex, int rawIndex)
        {
            if (String.IsNullOrEmpty(titleFmt))
                return titleFmt;
            titleFmt = titleFmt.Replace("{Value}", String.Join(fmtIndex.ToString(), '{', '}'));
            titleFmt = titleFmt.Replace("{Raw}", String.Join(rawIndex.ToString(), '{', '}'));
            return titleFmt;
        }



        static readonly IReadOnlySet<Char> FilterValues = new HashSet<char>(" \t\r\n,").Freeze();

        /// <summary>
        /// Get an url (html) to some data as a table.
        /// </summary>
        /// <param name="table">Data paramaters</param>
        /// <param name="request"></param>
        /// <returns>An url (html) to the table</returns>
        [AiTool("📅✨")]
        String BuildTable(AiTable table, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return null;
            var srcCols = table.Columns;
            var colLen = srcCols.Length;
            List<TableDataColumn> cols = new List<TableDataColumn>(colLen * 2 + 1);

            var ci = CultureInfo.InvariantCulture;
            Action<String, Object[]>[] colWriters = new Action<string, Object[]>[colLen];
            var filterValues = FilterValues;
            List<int> removeIfAllNull = new List<int>(colLen);
            var unique = new HashSet<String>[colLen];
            int[] inToOut = new int[colLen];
            for (int i = 0; i < colLen; ++ i)
            {
                unique[i] = new HashSet<string>(StringComparer.Ordinal);
                inToOut[i] = cols.Count;
                var s = srcCols[i];
                int destIndex = cols.Count;
                var d = new TableDataColumn
                {
                    Name = "M" + destIndex,
                    Title = s.Name,
                    Desc = s.ColDesc,
                };
                var valFormat = StringExt.JoinNonEmpty("", s.ValuePrefix, "{0}", s.ValueSuffix);
                var valDesc = s.ValueDesc;
                cols.Add(d);
                switch (s.Type)
                {
                    case AiTableColumnTypes.Text:
                        d.Type = typeof(String).CleanTypename();
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case AiTableColumnTypes.Float:
                        d.Type = typeof(Decimal).CleanTypename();
                        d.Format = new TableDataNumberAttribute(s.NumDecimals, valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Decimal.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0M; 
                        break;
                    case AiTableColumnTypes.Integer:
                        d.Type = typeof(Int64).CleanTypename();
                        d.Format = new TableDataNumberAttribute(0, valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Int64.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0L;
                        break;
                    case AiTableColumnTypes.DateTime:
                        d.Type = typeof(DateTime).CleanTypename();
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = DateTime.TryParse(srcText, ci, out var x) ? x : DateTime.MinValue;
                        break;
                    case AiTableColumnTypes.Image:
                        d.Type = typeof(String).CleanTypename();
                        d.Format = new TableDataImgAttribute(valFormat, null, GetDescFmt(valDesc, 2, 0)).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case AiTableColumnTypes.Link:
                        d.Type = typeof(String).CleanTypename();
                        d.Format = new TableDataUrlAttribute(valFormat, "+{2}", GetDescFmt(valDesc, 2, 0)).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case AiTableColumnTypes.Boolean:
                        d.Type = typeof(Boolean).CleanTypename();
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Boolean.TryParse(srcText, out var x) ? x : false;
                        break;
                    case AiTableColumnTypes.Amount:
                        d.Type = typeof(Decimal).CleanTypename();
                        d.Format = new TableDataNumberAttribute(s.NumDecimals, valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) =>
                        {
                            var t = srcText.LastIndexOf(' ');
                            var e = srcText.Length - 4;
                            bool wasCurrency = false;
                            if ((e > 0) && (e == t))
                            {
                                var cc = srcText.Substring(t + 1);
                                if (cc.IsLetters(false))
                                {
                                    srcText = srcText.Substring(0, t);
                                    var cci = IsoCurrency.TryGet(cc);
                                    if (cci != null)
                                    {
                                        destData[destIndex] = Decimal.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0M;
                                        destData[destIndex + 1] = cci.Iso4217;
                                        wasCurrency = true;
                                    }

                                }
                            }
                            if (!wasCurrency)
                            {
                                destData[destIndex] = Decimal.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0M;
                                destData[destIndex + 1] = null;
                            }
                        };
                        removeIfAllNull.Add(cols.Count);
                        cols.Add(new TableDataColumn
                        {
                            Name = "M" + (destIndex + 1),
                            Title = "ISO 4217 currency code",
                            Desc = s.ColDesc,
                            Type = typeof(String).CleanTypename(),
                            Format = new TableDataIsoCurrencyAttribute().Value,
                        });
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            var colCount = cols.Count;

            var srcRows = table.Rows;
            var rowCount = srcRows.Length;
            Object[][] rows = new object[rowCount][];
            for (int i = 0; i < rowCount; ++ i)
            {
                var d = new object[colCount];
                rows[i] = d;
                var s = srcRows[i].ColumnData;
                for (int j = 0; j < colLen; ++j)
                {
                    var sv = s[j];
                    unique[j].Add(sv);
                    colWriters[j](sv, d);
                }
            }
            bool havePrimary = false;
            TableDataColumn numPrim = null;
            for (int i = 0; i < colLen; ++ i)
            {
                var u = unique[i];
                if (u.Count != rowCount)
                    continue;
                var col = cols[inToOut[i]];
                if (havePrimary)
                {
                    col.Props |= TableDataColumnProps.IsKey;
                    continue;
                }
                bool isNumeric = true;
                foreach (var x in u)
                {
                    isNumeric &= x.IsNumeric(true, true, true);
                    if (!isNumeric)
                        break;
                }
                if (isNumeric)
                {
                    if (numPrim != null)
                    {
                        col.Props |= TableDataColumnProps.IsKey;
                        continue;
                    }
                    numPrim = col;
                    col.Props |= TableDataColumnProps.IsPrimaryKey1;
                    continue;
                }
                if (numPrim != null)
                {
                    col.Props &= ~TableDataColumnProps.IsPrimaryKey1;
                    col.Props |= TableDataColumnProps.IsKey;
                    numPrim = null;
                }
                havePrimary = true;
                col.Props |= TableDataColumnProps.IsPrimaryKey1;
            }
            foreach (var col in cols)
            {
                if ((col.Props & TableDataColumnProps.IsPrimaryKey1) != 0)
                {
                    col.Props &= ~TableDataColumnProps.CanChart;
                    break;
                }
            }
            var anull = removeIfAllNull.Count;
            if (anull > 0)
            {
                int removedCount = 0;
                int orgCols = colCount;
                while (anull > 0)
                {
                    --anull;
                    var colIndex = removeIfAllNull[anull];
                    bool foundNonNull = false;
                    for (int i = 0; i < rowCount; ++i)
                    {
                        foundNonNull = rows[i][colIndex] != null;
                        if (foundNonNull)
                            break;
                    }
                    if (foundNonNull)
                        continue;
                    --colCount;
                    for (int j = colIndex; j < colCount; ++j)
                        cols[j] = cols[j + 1];
                    for (int i = 0; i < rowCount; ++i)
                    {
                        var row = rows[i];
                        for (int j = colIndex; j < colCount; ++j)
                            row[j] = row[j + 1];
                    }
                    ++removedCount;
                }
                if (removedCount > 0)
                { 
                    cols.RemoveRange(colCount, removedCount);
                    for (int i = 0; i < rowCount; ++i)
                        Array.Resize(ref rows[i], colCount);
                }
            }
            //  Outside of an AI chat (ex: over MCP) the table can't be stored in the chat session, attach it as a csv file
            if (c is not AiToolContext ac)
                return c.AddMessageFile("text/csv", ToCsv(cols, rows), table.Title ?? "Table");
            var getter = TableDataTools.GetStaticTableFn(cols.ToArray(), rows, table.Title);
            var name = ac.AddMessageData(getter);
            var url = "../explore/table.html?q=" + AiTools.WebRoot + "MessageTable&p=" + name;
            //c.AddLink(url);
            return url;
        }


        /// <summary>
        /// Use this function to display some data as a table.
        /// </summary>
        /// <param name="table">Data paramaters</param>
        /// <param name="request"></param>
        /// <returns>True if successful</returns>
        [AiTool("📅🖥️")]
        [AiHideMcp]
        bool DisplayTable(AiTable table, HttpServerRequest request)
        {
            var url = BuildTable(table, request);
            if (url == null)
                return false;
            var c = request.Properties[RequestAiToolContext] as IAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;
        }

        static String ToCsv(List<TableDataColumn> cols, Object[][] rows)
        {
            var sb = new StringBuilder();
            void Add(String v, bool first)
            {
                if (!first)
                    sb.Append(',');
                v ??= "";
                if (v.IndexOfAny([',', '"', '\r', '\n']) >= 0)
                    sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
                else
                    sb.Append(v);
            }
            var cc = cols.Count;
            for (int i = 0; i < cc; ++i)
                Add(cols[i].Title ?? cols[i].Name, i == 0);
            sb.Append("\r\n");
            var ci = CultureInfo.InvariantCulture;
            foreach (var row in rows)
            {
                for (int i = 0; i < cc; ++i)
                    Add(i < row.Length ? Convert.ToString(row[i], ci) : null, i == 0);
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Use this function to compute a value from some expression.
        /// Decimal and integer values are supported.
        /// </summary>
        /// <param name="expression">Data paramaters</param>
        /// <returns>The value of the expression</returns>
        [AiTool("🧮")]
        Decimal Calculate(String expression)
        {
            try
            {
                return ExpressionEvaluator.Decimal.Value(expression);
            }
            catch
            {
            }
            try
            {
                return (Decimal)ExpressionEvaluator.Double.Value(expression);
            }
            catch
            {
            }
            try
            {
                return (Decimal)ExpressionEvaluator.Int64.Value(expression);
            }
            catch
            {
            }
            return (Decimal)ExpressionEvaluator.UInt64.Value(expression);
        }

        /// <summary>
        /// Computes the elapsed time from a time interval.
        /// </summary>
        /// <param name="time">The interval (start, stop) and the desired return unit (seconds, minutes, hours, days)</param>
        /// <returns>The elapsed time in the specified unit</returns>
        [AiTool("⏲️")]
        Double ElapsedTime(AiTimeInterval time)
        {
            var dt = time.To - time.From;
            switch (time.ReturnUnit)
            {
                case AiTimeIntervalUnits.Minutes:
                    return dt.TotalMinutes;
                case AiTimeIntervalUnits.Hours:
                    return dt.TotalHours;
                case AiTimeIntervalUnits.Days:
                    return dt.TotalDays;
                default:
                    return dt.TotalSeconds;
            }

        }


        /// <summary>
        /// Do not use directly!
        /// Get used internally by an AI tool to display tables generated by the AI.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [WebApi]
        public async Task<TableData> MessageTable(TableDataRequest r, HttpServerRequest context)
        {
            var l = r.Param;
            if (String.IsNullOrEmpty(l))
                throw new Exception("Must supply a parameter!");
            var t = l.Split('_');
            if (t.Length != 5)
                throw new Exception("Invalid parameter!");
            if (!long.TryParse(t[3], out var msgId))
                throw new Exception("Invalid parameter!");
            var providerName = t[1];
            if (providerName != Name)
                throw new Exception("Invalid parameter!");
            var providerChatId = t[2];
            var name = t[4];
            var msg = await GetChatMessage(providerChatId, msgId, context).ConfigureAwait(false);
            if (msg == null)
                throw new Exception("Message have been removed, data no longer available");
            var to = msg.GetTo();
            if (to != null)
                if (to != (context.Session?.Auth?.Guid))
                    throw new Exception("Not authorized to read this message!");
            var fn = msg.GetData(name) as Func<TableDataRequest, TableData>;
            if (fn == null)
                throw new Exception("Invalid parameter!");
            var data = fn(r);
            data.RefreshRate = 5 * 60 * 1000;
            return data;
        }


    }


}

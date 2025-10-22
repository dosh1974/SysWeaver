using OpenAI;
using OpenAI.Chat;
using System;
using System.Buffers;
using System.ClientModel;
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

    public sealed partial class OpenAiService 
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
        [OpenAiTool("🎨")]
        String GetPredefinedImage(OpenAiImages type, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return null;
            const String aip = "../openAI/icons/";
            switch (type)
            {
                case OpenAiImages.ApplicationLogo:
                    return "../logo.svg";
                case OpenAiImages.ApplicationIcon:
                    return "../icon.svg";
                case OpenAiImages.AgentLogo:
                    return c.Session.AgentImageUrl;
                case OpenAiImages.AngrySmiley:
                    return aip + "Smiley_Angry.svg";
                case OpenAiImages.HappySmiley:
                    return aip + "Smiley_HappyCrying.svg";
                case OpenAiImages.LoveSmiley:
                    return aip + "Smiley_Love.svg";
                case OpenAiImages.SadSmiley:
                    return aip + "Smiley_Sad.svg";
                case OpenAiImages.TeasingSmiley:
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
        [OpenAiTool("📁")]
        String GetFileExtensionIcon(String fileExtension, HttpServerRequest request)
        {
            return "../icons/ext/" + fileExtension.TrimStart('.').FastToLower() + ".svg";
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
        [OpenAiTool("🏳️")]
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
        [OpenAiTool("🖌️")]
        String BuildLogo(OpenAiLogo logo, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
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
        [OpenAiTool("📷")]
        String BuildQrCode(String qrCodeContent, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return null;
            var qr = QrCode;
            if (qr == null)
                return null;
            var data = qr.CreateQrCode(qrCodeContent);
            return c.AddMessageFile("image/svg+xml", data, "QR_" + qrCodeContent);
        }


        /// <summary>
        /// Get an url (png) to an AI (dall-e-3) generated image from a prompt.
        /// </summary>
        /// <param name="prompt">Paramaters for the generation</param>
        /// <param name="request"></param>
        /// <returns>An url to the generated png image</returns>
        [OpenAiTool("🖼️")]
        async Task<String> GenerateImage(OpenAiImagePrompt prompt, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return null;
            var bin = await GenImage(prompt, request).ConfigureAwait(false);
            var us = UserStorage;
            var filename = prompt.Title ?? "Image";
            if (us == null)
            {
                var data = "data:image/png;base64," + Convert.ToBase64String(bin.Span);
                return c.AddMessageFile("image/png", data, filename);
            }
            var s = c.Session;
            if (s.IsPrivate)
                return "../" + await us.StorePrivateFile(request, filename + ".png", bin).ConfigureAwait(false);
            return "../" + await us.StorePublicFile(request, filename + ".png", bin, String.Join(',', s.JoinAuth)).ConfigureAwait(false);
        }

        #endregion//Generate data

        /// <summary>
        /// Use this function to convert some data (typically text based) into an URL.
        /// </summary>
        /// <param name="data">Data paramaters</param>
        /// <param name="request"></param>
        /// <returns>An url to the data</returns>
        [OpenAiTool("🗜️")]
        String BuildData(OpenAiData data, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return null;
            FixData(data);
            return c.AddMessageFile(data.MimeType, data.Data, data.Title);
        }

        static void FixData(OpenAiData data)
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
        [OpenAiTool("📺")]
        bool DisplayData(OpenAiData data, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return false;
            FixData(data);
            var u = c.AddMessageFile(data.MimeType, data.Data, data.Title);
            c.AddLink(u);
            return true;
        }

        /// <summary>
        /// Use this function to display an URL to the user.
        /// Prefer this to displaying hyperlinks, embedding images etc in the text response.
        /// </summary>
        /// <param name="url">The url to display</param>
        /// <param name="request"></param>
        /// <returns>True when successful</returns>
        [OpenAiTool("📺")]
        bool DisplayUrl(String url, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;
        }

        static readonly MethodInfo Method_GetPredefinedImage = typeof(OpenAiService).GetMethod(nameof(GetPredefinedImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_GetFileExtensionIcon = typeof(OpenAiService).GetMethod(nameof(GetFileExtensionIcon), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_GetCountryFlagIcon = typeof(OpenAiService).GetMethod(nameof(GetCountryFlagIcon), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildLogo = typeof(OpenAiService).GetMethod(nameof(BuildLogo), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildData = typeof(OpenAiService).GetMethod(nameof(BuildData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildTable = typeof(OpenAiService).GetMethod(nameof(BuildTable), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_BuildQrCode = typeof(OpenAiService).GetMethod(nameof(BuildQrCode), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        static readonly MethodInfo Method_GenerateImage = typeof(OpenAiService).GetMethod(nameof(GenerateImage), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        static readonly MethodInfo Method_DisplayUrl = typeof(OpenAiService).GetMethod(nameof(DisplayUrl), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_DisplayData = typeof(OpenAiService).GetMethod(nameof(DisplayData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_DisplayTable = typeof(OpenAiService).GetMethod(nameof(DisplayTable), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        static readonly MethodInfo Method_Calculate = typeof(OpenAiService).GetMethod(nameof(Calculate), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static readonly MethodInfo Method_ElapsedTime = typeof(OpenAiService).GetMethod(nameof(ElapsedTime), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetPredefinedImage(OpenAiChatSession s) =>
            s.AddTool(this, Method_GetPredefinedImage, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetFileExtensionIcon(OpenAiChatSession s) =>
            s.AddTool(this, Method_GetFileExtensionIcon, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GetCountryFlagIcon(OpenAiChatSession s) =>
            s.AddTool(this, Method_GetCountryFlagIcon, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildLogo(OpenAiChatSession s) =>
            s.AddTool(this, Method_BuildLogo, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildQrCode(OpenAiChatSession s)
        {
            if (QrCode != null)
                s.AddTool(this, Method_BuildQrCode, null, PerfMon);
        }

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_GenerateImage(OpenAiChatSession s) =>
            s.AddTool(this, Method_GenerateImage, null, PerfMon, "Debug,Content");

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildData(OpenAiChatSession s) =>
            s.AddTool(this, Method_BuildData, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayData(OpenAiChatSession s) =>
            s.AddTool(this, Method_DisplayData, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayUrl(OpenAiChatSession s) =>
            s.AddTool(this, Method_DisplayUrl, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_DisplayTable(OpenAiChatSession s) =>
            s.AddTool(this, Method_DisplayTable, null, PerfMon);

        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_Calculate(OpenAiChatSession s) =>
            s.AddTool(this, Method_Calculate, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_ElapsedTime(OpenAiChatSession s) =>
            s.AddTool(this, Method_ElapsedTime, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildTable(OpenAiChatSession s) =>
            s.AddTool(this, Method_BuildTable, null, PerfMon);


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        /// <param name="useAdvanced">If true, use an advanced charting method (may fails more often, but more powerful)</param>
        public void AddTool_BuildGraph(OpenAiChatSession s, bool useAdvanced = false) =>
            s.AddRegistredTool(useAdvanced ? "BuildAdvancedChart" : "BuildChart");

        /// <summary>
        /// Add this tool to the chat session (if not included by default).
        /// </summary>
        /// <param name="s"></param>
        /// <param name="useAdvanced">If true, use an advanced charting method (may fails more often, but more powerful)</param>
        public void AddTool_DisplayGraph(OpenAiChatSession s, bool useAdvanced = false) =>
            s.AddRegistredTool(useAdvanced ? "DisplayAdvancedChart" : "DisplayChart");


        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_BuildMap(OpenAiChatSession s)
        {
            s.AddRegistredTool("BuildMap");
            s.AddRegistredTool("GetMapRegions");
        }



        /// <summary>
        /// Add this tool to the chat session (if not included by default)
        /// </summary>
        /// <param name="s"></param>
        public void AddTool_Store(OpenAiChatSession s)
        {
            if (UserStorage == null)
                return;
            s.AddRegistredTool("StoreFile");
            s.AddRegistredTool("StoreLink");
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
        [OpenAiTool("📋")]
        String BuildTable(OpenAiTable table, HttpServerRequest request)
        {
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
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
                    case OpenAiTableColumnTypes.Text:
                        d.Type = typeof(String).FullName;
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case OpenAiTableColumnTypes.Float:
                        d.Type = typeof(Decimal).FullName;
                        d.Format = new TableDataNumberAttribute(s.NumDecimals, valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Decimal.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0M; 
                        break;
                    case OpenAiTableColumnTypes.Integer:
                        d.Type = typeof(Int64).FullName;
                        d.Format = new TableDataNumberAttribute(0, valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Int64.TryParse(srcText.RemoveChars(filterValues), ci, out var x) ? x : 0L;
                        break;
                    case OpenAiTableColumnTypes.DateTime:
                        d.Type = typeof(DateTime).FullName;
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        d.Props |= TableDataColumnProps.CanChart;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = DateTime.TryParse(srcText, ci, out var x) ? x : DateTime.MinValue;
                        break;
                    case OpenAiTableColumnTypes.Image:
                        d.Type = typeof(String).FullName;
                        d.Format = new TableDataImgAttribute(valFormat, null, GetDescFmt(valDesc, 2, 0)).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case OpenAiTableColumnTypes.Link:
                        d.Type = typeof(String).FullName;
                        d.Format = new TableDataUrlAttribute(valFormat, "+{2}", GetDescFmt(valDesc, 2, 0)).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = srcText;
                        break;
                    case OpenAiTableColumnTypes.Boolean:
                        d.Type = typeof(Boolean).FullName;
                        d.Format = new TableDataFormatAttribute(valFormat, GetDescFmt(valDesc, 0, 2), true).Value;
                        colWriters[i] = (srcText, destData) => destData[destIndex] = Boolean.TryParse(srcText, out var x) ? x : false;
                        break;
                    case OpenAiTableColumnTypes.Amount:
                        d.Type = typeof(Decimal).FullName;
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
                            Type = typeof(String).FullName,
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
            var getter = TableDataTools.GetStaticTableFn(cols.ToArray(), rows, table.Title);
            var name = c.AddMessageData(getter);
            var url = "../explore/table.html?q=../openAI/MessageTable&p=" + name;
            //c.AddLink(url);
            return url;
        }


        /// <summary>
        /// Use this function to display some data as a table.
        /// </summary>
        /// <param name="table">Data paramaters</param>
        /// <param name="request"></param>
        /// <returns>True if successful</returns>
        [OpenAiTool("📋")]
        bool DisplayTable(OpenAiTable table, HttpServerRequest request)
        {
            var url = BuildTable(table, request);
            if (url == null)
                return false;
            var c = request.Properties[RequestAiToolContext] as OpenAiToolContext;
            if (c == null)
                return false;
            c.AddLink(url);
            return true;
        }

        /// <summary>
        /// Use this function to compute a value from some expression.
        /// Decimal and integer values are supported.
        /// </summary>
        /// <param name="expression">Data paramaters</param>
        /// <returns>The value of the expression</returns>
        [OpenAiTool("🧮")]
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


        enum TimeIntervalUnits
        {
            /// <summary>
            /// The number of seconds ellapsed
            /// </summary>
            Seconds = 0,
            /// <summary>
            /// The number of minutes ellapsed
            /// </summary>
            Minutes,
            /// <summary>
            /// The number of hours ellapsed
            /// </summary>
            Hours,
            /// <summary>
            /// The number of days ellapsed
            /// </summary>
            Days,
        }

#pragma warning disable CS0649

        sealed class TimeInterval
        {
            /// <summary>
            /// The start time
            /// </summary>
            public DateTime From;

            /// <summary>
            /// The end time
            /// </summary>
            public DateTime To;

            /// <summary>
            /// What to return
            /// </summary>
            [OpenAiOptional]
            public TimeIntervalUnits ReturnUnit;
        }

#pragma warning restore CS0649

        /// <summary>
        /// Computes the elapsed time from a time interval.
        /// </summary>
        /// <param name="time">The interval (start, stop) and the desired return unit (seconds, minutes, hours, days)</param>
        /// <returns>The elapsed time in the specified unit</returns>
        [OpenAiTool("⏲️")]
        Double ElapsedTime(TimeInterval time)
        {
            var dt = time.To - time.From;
            switch (time.ReturnUnit)
            {
                case TimeIntervalUnits.Minutes:
                    return dt.TotalMinutes;
                case TimeIntervalUnits.Hours:
                    return dt.TotalHours;
                case TimeIntervalUnits.Days:
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
        /// <exception cref="Exception"></exception>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Knowledge
{

    public sealed class City : Info
    {
        internal City(string name, string desc, string category, String country, bool isCapital, long pop, Info[] parent) : base(name, desc, category, parent, true)
        {
            IsCapital = isCapital;
            Population = pop;
            Country = country;
        }
        /// <summary>
        /// From what country
        /// </summary>
        public readonly String Country;
        
        /// <summary>
        /// The approximate population (Q1 2024) if known, or 0
        /// </summary>
        public readonly long Population;

        /// <summary>
        /// True if it's a capital
        /// </summary>
        public readonly bool IsCapital;

    }

    public static class Cities
    {
        public const String Group = "Cities";

        public static bool TryGet(String name, out City info) => Tags.TryGetValue(name.FastToLower(), out info);

        public static IEnumerable<KeyValuePair<String, City>> All => Tags;

        public static int Count => Tags.Count;

        #region Setup

        static readonly Dictionary<String, City> Tags = new Dictionary<string, City>(StringComparer.Ordinal);
        
        static Info[] Append(Info[] to, Info x)
        {
            if (x == null)
                return to;
            return [.. to, x];
        }

        static void Reg(String names, String country, long pop, bool isCapital)
        {
            var ci = IsoData.IsoCountry.TryGet(country);
            if (ci == null)
                ci = IsoData.IsoCountry.TryGet("GB");
            country = ci.CommonName;
            var ns = names.Split(';').Select(x => x.Trim()).ToList();
            AllInfo.TryGet(country, out var countryInfo);
            var name = ns[0];
            String desc = "";
            if (isCapital)
                desc += " of " + country + ".";
            else
                desc += ", located in the country " + country + ".";
            if (ns.Count > 1)
                desc += "\nAKA: " + String.Join(", ", ns.Where(x => x != name)) + ".";
            if (pop > 0)
                desc += "\nEstimated population: " + pop + " (Q1 2024).";

            City tag;
            if (isCapital)
                tag = new City(name, " " + name + " the capital city" + desc, Group, country, isCapital, pop, Append(Capital, countryInfo));
            else
                tag = new City(name, "The city " + name + desc, Group, country, isCapital, pop, Append(City, countryInfo));
            Tags.Add(name.FastToLower(), tag);
            foreach (var nn in ns)
            {
                var key = nn.FastToLower();
                AllInfo.TryAdd(key, tag, true, false);
            }
        }

        static readonly Info TagCity = new Info("City", "Any city", Group, null);
        static readonly Info TagCapital = new Info("Capital", "Any capital city", Group, [ TagCity ]);

        static readonly Info[] City = [
            TagCity,
        ];

        static readonly Info[] Capital = [
            TagCapital,
            TagCity,
        ];


        static void Reg(Info i)
        {
            AllInfo.TryAdd(i.Name, i, false, true);
        }

        #region Data

        static Cities()
        {
            Reg(TagCity);
            Reg(TagCapital);

            Reg("Tirana;Tirane", "AL", 0, true);
            Reg("Andorra la Vella", "AD", 0, true);
            Reg("Saint John's", "AG", 0, true);
            Reg("Canberra", "AU", 0, true);
            Reg("Nassau", "BS", 0, true);
            Reg("Manama", "BH", 0, true);
            Reg("Bridgetown", "BB", 0, true);
            Reg("Belmopan", "BZ", 0, true);
            Reg("Porto Novo", "BJ", 0, true);
            Reg("Thimphu", "BT", 0, true);
            Reg("Sarajevo", "BA", 0, true);
            Reg("Gaborone", "BW", 0, true);
            Reg("Bandar Seri Begawan", "BN", 0, true);
            Reg("Gitega", "BI", 0, true);
            Reg("Praia", "CV", 0, true);
            Reg("Bangui", "CF", 0, true);
            Reg("N'Djamena", "TD", 0, true);
            Reg("Moroni", "KM", 0, true);
            Reg("Yamoussoukro", "CI", 0, true);
            Reg("Zagreb", "HR", 0, true);
            Reg("Nicosia", "CY", 0, true);
            Reg("Djibouti", "DJ", 0, true);
            Reg("Roseau", "DM", 0, true);
            Reg("Dili", "TL", 0, true);
            Reg("Malabo", "GQ", 0, true);
            Reg("Tallinn", "EE", 0, true);
            Reg("Mbabane", "SZ", 0, true);
            Reg("Palikir", "FM", 0, true);
            Reg("Suva", "FJ", 0, true);
            Reg("Sucre", "BO", 0, true);
            Reg("Libreville", "GA", 0, true);
            Reg("Banjul", "GM", 0, true);
            Reg("Saint George's", "GD", 0, true);
            Reg("Bissau", "GW", 0, true);
            Reg("Georgetown", "GY", 0, true);
            Reg("Reykjavik", "IS", 0, true);
            Reg("New Delhi", "IN", 0, true);
            Reg("Jerusalem", "IL, PS", 0, true);
            Reg("Kingston", "JM", 0, true);
            Reg("Tarawa Atoll", "KI", 0, true);
            Reg("Pristina", "XK", 0, true);
            Reg("Vientiane", "LA", 0, true);
            Reg("Riga", "LV", 0, true);
            Reg("Maseru", "LS", 0, true);
            Reg("Vaduz", "LI", 0, true);
            Reg("Vilnius", "LT", 0, true);
            Reg("Luxembourg", "LU", 0, true);
            Reg("Male", "MV", 0, true);
            Reg("Valletta", "MT", 0, true);
            Reg("Majuro", "MH", 0, true);
            Reg("Port Louis", "MU", 0, true);
            Reg("Chisinau", "MD", 0, true);
            Reg("Monaco", "MC", 0, true);
            Reg("Podgorica", "ME", 0, true);
            Reg("Nay Pyi Taw", "MM", 0, true);
            Reg("Windhoek", "NA", 0, true);
            Reg("No official capital", "NR", 0, true);
            Reg("Wellington", "NZ", 0, true);
            Reg("Skopje", "MK", 0, true);
            Reg("Belfast", "Northern Ireland", 0, true);
            Reg("Melekeok", "PW", 0, true);
            Reg("Port Moresby", "PG", 0, true);
            Reg("Doha", "QA", 0, true);
            Reg("Basseterre", "KN", 0, true);
            Reg("Castries", "LC", 0, true);
            Reg("Kingstown", "VC", 0, true);
            Reg("Apia", "WS", 0, true);
            Reg("San Marino", "SM", 0, true);
            Reg("Sao Tome", "ST", 0, true);
            Reg("Edinburgh", "Scotland", 0, true);
            Reg("Victoria", "SC", 0, true);
            Reg("Bratislava", "SK", 0, true);
            Reg("Ljubljana", "SI", 0, true);
            Reg("Honiara", "SB", 0, true);
            Reg("Bloemfontein", "ZA", 0, true);
            Reg("Juba", "SS", 0, true);
            Reg("Sri Jayawardenapura Kotte", "LK", 0, true);
            Reg("Paramaribo", "SR", 0, true);
            Reg("Bern", "CH", 0, true);
            Reg("Dodoma", "TZ", 0, true);
            Reg("Nuku'alofa", "TO", 0, true);
            Reg("Port of Spain", "TT", 0, true);
            Reg("Ashgabat", "TM", 0, true);
            Reg("Funafuti", "TV", 0, true);
            Reg("Kyiv or Kiev", "UA", 0, true);
            Reg("Washington D.C.", "US", 0, true);
            Reg("Port Vila", "VU", 0, true);
            Reg("Vatican City", "VA", 0, true);
            Reg("Cardiff", "Wales", 0, true);
            Reg("Sana'a", "YE", 0, true);
            Reg("Jos", "NG", 1001155, false);
            Reg("Morelia", "MX", 1002461, false);
            Reg("Lubango", "AO", 1003016, false);
            Reg("Jiujiang", "CN", 1004247, false);
            Reg("Odesa", "UA", 1007596, false);
            Reg("San Pedro Sula", "HN", 1008220, false);
            Reg("Jixi Heilongjiang", "CN", 1008989, false);
            Reg("Bordeaux", "FR", 1009594, false);
            Reg("Laiwu", "CN", 1009727, false);
            Reg("Krasnodar", "RU", 1010552, false);
            Reg("Misratah", "LY", 1011119, false);
            Reg("Nampula", "MZ", 1012582, false);
            Reg("Dushanbe", "TJ", 1012794, true);
            Reg("Leiyang", "CN", 1013191, false);
            Reg("Nonthaburi", "TH", 1013672, false);
            Reg("Dehradun", "IN", 1016402, false);
            Reg("Valparaiso", "CL", 1016585, false);
            Reg("Songkhla", "TH", 1017784, false);
            Reg("Zhucheng", "CN", 1019600, false);
            Reg("Pingxiang Jiangxi", "CN", 1021575, false);
            Reg("Rotterdam", "NL", 1021919, false);
            Reg("Owerri", "NG", 1022922, false);
            Reg("Chengde", "CN", 1024521, false);
            Reg("Kayseri", "TR", 1025507, false);
            Reg("Warri", "NG", 1031425, false);
            Reg("Acapulco De Juarez", "MX", 1032772, false);
            Reg("Hamah", "SY", 1034498, false);
            Reg("San Miguel De Tucuman", "AR", 1039226, false);
            Reg("Cebu City", "PH", 1042613, false);
            Reg("Cancun", "MX", 1045005, false);
            Reg("Xintai", "CN", 1049486, false);
            Reg("Saltillo", "MX", 1050320, false);
            Reg("Padang", "ID", 1052849, false);
            Reg("Changwon", "KR", 1055373, false);
            Reg("Warangal", "IN", 1055604, false);
            Reg("Guiping", "CN", 1055921, false);
            Reg("Teresina", "BR", 1059657, false);
            Reg("Antwerp", "BE", 1061089, false);
            Reg("Tampico", "MX", 1062567, false);
            Reg("Ilorin", "NG", 1063713, false);
            Reg("Yueyang", "CN", 1064166, false);
            Reg("Qujing", "CN", 1066897, false);
            Reg("Marrakech", "MA", 1067172, false);
            Reg("Aracaju", "BR", 1070122, false);
            Reg("Blantyre Limbe", "MW", 1070625, false);
            Reg("Toulouse", "FR", 1070746, false);
            Reg("Denpasar", "ID", 1075244, false);
            Reg("Voronezh", "RU", 1083724, false);
            Reg("Perm", "RU", 1084120, false);
            Reg("Tbilisi", "GE", 1084471, true);
            Reg("Mersin", "TR", 1084817, false);
            Reg("Lille", "FR", 1085199, false);
            Reg("Solapur", "IN", 1088131, false);
            Reg("Xuchang", "CN", 1091744, false);
            Reg("Huzhou", "CN", 1091811, false);
            Reg("Changshu", "CN", 1093687, false);
            Reg("Cartagena", "CO", 1096463, false);
            Reg("Yerevan", "AM", 1097542, true);
            Reg("Kirkuk", "IQ", 1100390, false);
            Reg("Oslo", "NO", 1100868, true);
            Reg("Nyala", "SD", 1101314, false);
            Reg("Kermanshah", "IR", 1101611, false);
            Reg("Johor Bahru", "MY", 1107001, false);
            Reg("Managua", "NI", 1107118, true);
            Reg("Jingzhou Hubei", "CN", 1108067, false);
            Reg("Dezhou", "CN", 1108356, false);
            Reg("Asmara", "ER", 1111748, true);
            Reg("Diyarbakir", "TR", 1113333, false);
            Reg("Goyang", "KR", 1114079, false);
            Reg("Aden", "YE", 1116193, false);
            Reg("Leshan", "CN", 1116497, false);
            Reg("Sekondi Takoradi", "GH", 1119534, false);
            Reg("Chenzhou", "CN", 1119791, false);
            Reg("Yichun Jiangxi", "CN", 1122041, false);
            Reg("San Salvador", "SV", 1123376, true);
            Reg("Zhaoqing", "CN", 1127601, false);
            Reg("Bishkek", "KG", 1127721, true);
            Reg("Samarinda", "ID", 1128176, false);
            Reg("Shangrao", "CN", 1130887, false);
            Reg("Tshikapa", "CD", 1131226, false);
            Reg("Fuzhou Jiangxi", "CN", 1131718, false);
            Reg("Chihuahua", "MX", 1135342, false);
            Reg("Rostov on Don", "RU", 1139641, false);
            Reg("Cuernavaca", "MX", 1140169, false);
            Reg("Ma'anshan", "CN", 1140264, false);
            Reg("Panjin", "CN", 1142542, false);
            Reg("Jalandhar", "IN", 1142682, false);
            Reg("Bien Hoa", "VN", 1142997, false);
            Reg("Ikorodu", "NG", 1145224, false);
            Reg("Fuyang", "CN", 1146343, false);
            Reg("Ufa", "RU", 1146786, false);
            Reg("Yongin", "KR", 1147967, false);
            Reg("Cologne", "DE", 1149014, false);
            Reg("Zaozhuang", "CN", 1150859, false);
            Reg("Samara", "RU", 1154451, false);
            Reg("Wenling", "CN", 1159259, false);
            Reg("Siliguri", "IN", 1159371, false);
            Reg("Xinxiang", "CN", 1160840, false);
            Reg("Krasnoyarsk", "RU", 1173095, false);
            Reg("Hargeysa", "SO", 1176617, false);
            Reg("Aguascalientes", "MX", 1179301, false);
            Reg("Omsk", "RU", 1180677, false);
            Reg("Shimkent", "KZ", 1181020, false);
            Reg("Amsterdam", "NL", 1181817, true);
            Reg("Bobo Dioulasso", "BF", 1185053, false);
            Reg("Bandar Lampung", "ID", 1186233, false);
            Reg("Haifa", "IL", 1186475, false);
            Reg("Tripoli", "LY", 1192436, true);
            Reg("Maputo", "MZ", 1193253, true);
            Reg("Salem", "IN", 1194757, false);
            Reg("Mexicali", "MX", 1196982, false);
            Reg("Guwahati", "IN", 1199455, false);
            Reg("Yueqing", "CN", 1201400, false);
            Reg("Hubli Dharwad", "IN", 1205428, false);
            Reg("Saharanpur", "IN", 1207856, false);
            Reg("Guilin", "CN", 1218067, false);
            Reg("Kaduna", "NG", 1221451, false);
            Reg("Quetta", "PK", 1221495, false);
            Reg("Bazhong", "CN", 1225591, false);
            Reg("Chiang Mai", "TH", 1228773, false);
            Reg("Aba", "NG", 1230407, false);
            Reg("Binzhou", "CN", 1233584, false);
            Reg("Benxi", "CN", 1237550, false);
            Reg("Jinzhou", "CN", 1238377, false);
            Reg("Merida", "MX", 1239654, false);
            Reg("Chandigarh", "IN", 1239699, false);
            Reg("Xiongan", "CN", 1240158, false);
            Reg("Luohe", "CN", 1241152, false);
            Reg("Mendoza", "AR", 1242319, false);
            Reg("Chelyabinsk", "RU", 1243883, false);
            Reg("Tiruchirappalli", "IN", 1244978, false);
            Reg("Pizhou", "CN", 1247177, false);
            Reg("Xiangtan Hunan", "CN", 1249380, false);
            Reg("Nanyang Henan", "CN", 1250300, false);
            Reg("Nizhniy Novgorod", "RU", 1250302, false);
            Reg("Da Nang", "VN", 1253228, false);
            Reg("Bogor", "ID", 1256155, false);
            Reg("Maracay", "VE", 1256553, false);
            Reg("Tasikmalaya", "ID", 1258124, false);
            Reg("Huaibei", "CN", 1264856, false);
            Reg("Islamabad", "PK", 1266792, true);
            Reg("Barquisimeto", "VE", 1267872, false);
            Reg("Liupanshui", "CN", 1272872, false);
            Reg("Zhenjiang Jiangsu", "CN", 1275395, false);
            Reg("Bujumbura", "BI", 1277050, false);
            Reg("Zhuzhou", "CN", 1277238, false);
            Reg("Chifeng", "CN", 1279544, false);
            Reg("Puning", "CN", 1282756, false);
            Reg("Dublin", "IE", 1284551, true);
            Reg("Pingdingshan Henan", "CN", 1285750, false);
            Reg("Sofia", "BG", 1287540, true);
            Reg("Kigali", "RW", 1287952, true);
            Reg("Bhubaneswar", "IN", 1289254, false);
            Reg("Durg Bhilainagar", "IN", 1289673, false);
            Reg("Baoji", "CN", 1290231, false);
            Reg("San Luis Potosi", "MX", 1292133, false);
            Reg("Dallas", "US", 1295447, false);
            Reg("Jinhua", "CN", 1296065, false);
            Reg("Kazan", "RU", 1296232, false);
            Reg("Nnewi", "NG", 1300993, false);
            Reg("Bukavu", "CD", 1308469, false);
            Reg("Ahvaz", "IR", 1309372, false);
            Reg("Florianopolis", "BR", 1309895, false);
            Reg("Port Elizabeth", "ZA", 1312631, false);
            Reg("Fes", "MA", 1313311, false);
            Reg("Ruian", "CN", 1314784, false);
            Reg("Abomey Calavi", "BJ", 1314916, false);
            Reg("Mysore", "IN", 1316461, false);
            Reg("Fushun Liaoning", "CN", 1317276, false);
            Reg("Jieyang", "CN", 1320779, false);
            Reg("Astana", "KZ", 1324111, true);
            Reg("Prague", "CZ", 1327947, true);
            Reg("Porto", "PT", 1329301, false);
            Reg("Lilongwe", "MW", 1333096, true);
            Reg("Maoming", "CN", 1333930, false);
            Reg("Pekan Baru", "ID", 1334532, false);
            Reg("Moradabad", "IN", 1335966, false);
            Reg("Aligarh", "IN", 1346018, false);
            Reg("Helsinki", "FI", 1346810, true);
            Reg("Freetown", "SL", 1347559, true);
            Reg("Tanger", "MA", 1348848, false);
            Reg("Ad Dammam", "SA", 1352912, false);
            Reg("Yingkou", "CN", 1355564, false);
            Reg("Tengzhou", "CN", 1360779, false);
            Reg("Joinville", "BR", 1361992, false);
            Reg("Antalya", "TR", 1372400, false);
            Reg("Qom", "IR", 1373800, false);
            Reg("San Diego", "US", 1375452, false);
            Reg("Maceio", "BR", 1375984, false);
            Reg("Samut Prakan", "TH", 1376146, false);
            Reg("Mianyang Sichuan", "CN", 1376449, false);
            Reg("Mwanza", "TZ", 1378014, false);
            Reg("Suweon", "KR", 1378229, false);
            Reg("Adelaide", "AU", 1379280, false);
            Reg("Pointe Noire", "CD", 1379368, false);
            Reg("Bareilly", "IN", 1380715, false);
            Reg("Taizhong", "TW", 1381855, false);
            Reg("Shiyan", "CN", 1387377, false);
            Reg("Copenhagen", "DK", 1391205, true);
            Reg("Uyo", "NG", 1393453, false);
            Reg("Bucaramanga", "CO", 1396632, false);
            Reg("Zhanjiang", "CN", 1399310, false);
            Reg("Zunyi", "CN", 1406091, false);
            Reg("Belgrade", "RS", 1410697, true);
            Reg("Dongying", "CN", 1410791, false);
            Reg("Nanchong", "CN", 1412714, false);
            Reg("Dhanbad", "IN", 1414532, false);
            Reg("Kharkiv", "UA", 1418978, false);
            Reg("Rizhao", "CN", 1426583, false);
            Reg("Liuan", "CN", 1427894, false);
            Reg("Liuyang", "CN", 1428802, false);
            Reg("Konya", "TR", 1429935, false);
            Reg("Cochabamba", "BO", 1430688, false);
            Reg("Kaifeng", "CN", 1434463, false);
            Reg("Joao Pessoa", "BR", 1435125, false);
            Reg("Queretaro", "MX", 1436818, false);
            Reg("Taian Shandong", "CN", 1441911, false);
            Reg("Zurich", "CH", 1443349, false);
            Reg("Ottawa", "CA", 1451571, true);
            Reg("Hai Phong", "VN", 1463650, false);
            Reg("Weihai", "CN", 1465926, false);
            Reg("Jiaxing", "CN", 1470917, false);
            Reg("Chon Buri", "TH", 1472709, false);
            Reg("Taizhou Jiangsu", "CN", 1477600, false);
            Reg("Amritsar", "IN", 1480470, false);
            Reg("San Jose", "CR", 1482460, true);
            Reg("Kisangani", "CD", 1483513, false);
            Reg("Basra", "IQ", 1485156, false);
            Reg("Allahabad", "IN", 1493346, false);
            Reg("Mombasa", "KE", 1495223, false);
            Reg("Niamey", "NE", 1496258, true);
            Reg("Homs", "SY", 1499603, false);
            Reg("San Antonio", "US", 1506593, false);
            Reg("Ganzhou", "CN", 1508037, false);
            Reg("Gwalior", "IN", 1508846, false);
            Reg("Chaozhou", "CN", 1520628, false);
            Reg("Yiwu", "CN", 1525749, false);
            Reg("Gwangju", "KR", 1532902, false);
            Reg("Yekaterinburg", "RU", 1532970, false);
            Reg("Philadelphia", "US", 1533916, false);
            Reg("Asansol", "IN", 1534081, false);
            Reg("Grande Sao Luis", "BR", 1536017, false);
            Reg("Huainan", "CN", 1538603, false);
            Reg("Jabalpur", "IN", 1551004, false);
            Reg("Nouakchott", "MR", 1552146, true);
            Reg("Natal", "BR", 1556413, false);
            Reg("Kota", "IN", 1558468, false);
            Reg("Gaoxiong", "TW", 1559085, false);
            Reg("Mandalay", "MM", 1563021, false);
            Reg("Edmonton", "CA", 1567615, false);
            Reg("Zhangjiakou", "CN", 1568122, false);
            Reg("Daejon", "KR", 1581705, false);
            Reg("Ranchi", "IN", 1584237, false);
            Reg("Munich", "DE", 1584507, false);
            Reg("Abu Dhabi", "AE", 1593284, true);
            Reg("Jining Shandong", "CN", 1595963, false);
            Reg("Medina", "SA", 1598976, false);
            Reg("Karaj", "IR", 1603011, false);
            Reg("Harare", "ZW", 1603201, true);
            Reg("Ciudad Juarez", "MX", 1604085, false);
            Reg("Tegucigalpa", "HN", 1609261, true);
            Reg("Rosario", "AR", 1613041, false);
            Reg("Kathmandu", "NP", 1621642, true);
            Reg("Jodhpur", "IN", 1625325, false);
            Reg("Cordoba", "AR", 1625937, false);
            Reg("Marseille", "FR", 1635707, false);
            Reg("N Djamena", "TD", 1655618, false);
            Reg("Qiqihaer", "CN", 1662407, false);
            Reg("Calgary", "CA", 1665023, false);
            Reg("Muscat", "OM", 1676167, true);
            Reg("Phoenix", "US", 1676481, false);
            Reg("Tabriz", "IR", 1678028, false);
            Reg("Auckland", "NZ", 1692770, false);
            Reg("Anyang", "CN", 1693375, false);
            Reg("Jilin", "CN", 1693701, false);
            Reg("Onitsha", "NG", 1694913, false);
            Reg("Ulaanbaatar", "MN", 1699363, true);
            Reg("Novosibirsk", "RU", 1701510, false);
            Reg("Hengyang", "CN", 1702012, false);
            Reg("Makassar", "ID", 1704930, false);
            Reg("Xining", "CN", 1705078, false);
            Reg("Glasgow", "GB", 1708147, false);
            Reg("Anshan", "CN", 1713452, false);
            Reg("Stockholm", "SE", 1719604, true);
            Reg("Qinhuangdao", "CN", 1723728, false);
            Reg("Aurangabad", "IN", 1725283, false);
            Reg("Suqian", "CN", 1726327, false);
            Reg("Jamshedpur", "IN", 1730521, false);
            Reg("Tiruppur", "IN", 1731862, false);
            Reg("Monrovia", "LR", 1735365, true);
            Reg("Srinagar", "IN", 1737502, false);
            Reg("Kananga", "CD", 1738716, false);
            Reg("Shiraz", "IR", 1742750, false);
            Reg("Yinchuan", "CN", 1757699, false);
            Reg("Yichang", "CN", 1758100, false);
            Reg("Bucharest", "RO", 1767520, true);
            Reg("Xiangyang", "CN", 1772318, false);
            Reg("Lyon", "FR", 1774395, false);
            Reg("Budapest", "HU", 1780391, true);
            Reg("Montevideo", "UY", 1781363, true);
            Reg("Hamburg", "DE", 1787280, false);
            Reg("Varanasi", "IN", 1789047, false);
            Reg("Jiangmen", "CN", 1797469, false);
            Reg("Warsaw", "PL", 1799451, true);
            Reg("Turin", "IT", 1805727, false);
            Reg("Batam", "ID", 1806147, false);
            Reg("La Laguna", "MX", 1826135, false);
            Reg("Gaziantep", "TR", 1833006, false);
            Reg("Meerut", "IN", 1835403, false);
            Reg("Cixi", "CN", 1840039, false);
            Reg("Mosul", "IQ", 1847691, false);
            Reg("Palembang", "ID", 1852673, false);
            Reg("Santa Cruz", "BO", 1855732, false);
            Reg("Adana", "TR", 1856638, false);
            Reg("Raipur", "IN", 1871107, false);
            Reg("Madurai", "IN", 1871912, false);
            Reg("Sharjah", "AE", 1872199, false);
            Reg("Datong", "CN", 1913674, false);
            Reg("Matola", "MZ", 1915035, false);
            Reg("Quanzhou", "CN", 1920733, false);
            Reg("Leon De Los Aldamas", "MX", 1924435, false);
            Reg("Zhuhai", "CN", 1930373, false);
            Reg("Can Tho", "VN", 1938915, false);
            Reg("West Yorkshire", "GB", 1942470, false);
            Reg("Baixada Santista", "BR", 1965110, false);
            Reg("La Paz", "BO", 1965570, true);
            Reg("Benin City", "NG", 1972558, false);
            Reg("Quito", "EC", 1986667, true);
            Reg("Ludhiana", "IN", 1988438, false);
            Reg("Rabat", "MA", 1989197, true);
            Reg("Vienna", "AT", 1990487, true);
            Reg("Davao City", "PH", 1991457, false);
            Reg("Valencia", "VE", 2007265, false);
            Reg("Semarang", "ID", 2013571, false);
            Reg("Almaty", "KZ", 2015209, false);
            Reg("Panama City", "PA", 2015735, true);
            Reg("Yancheng Jiangsu", "CN", 2034326, false);
            Reg("Lianyungang", "CN", 2035631, false);
            Reg("Lome", "TG", 2042734, true);
            Reg("Daqing", "CN", 2044371, false);
            Reg("Haikou", "CN", 2056646, false);
            Reg("Hiroshima", "JP", 2062884, false);
            Reg("Minsk", "BY", 2064733, true);
            Reg("Rajkot", "IN", 2096981, false);
            Reg("Bursa", "TR", 2115513, false);
            Reg("Linyi Shandong", "CN", 2120049, false);
            Reg("Brussels", "BE", 2132178, true);
            Reg("Perth", "AU", 2143491, false);
            Reg("Baoding", "CN", 2149703, false);
            Reg("Taizhou Zhejiang", "CN", 2152226, false);
            Reg("Havana", "CU", 2152518, true);
            Reg("Yangzhou", "CN", 2175102, false);
            Reg("Conakry", "GN", 2178596, true);
            Reg("Daegu", "KR", 2179929, false);
            Reg("Naples", "IT", 2180027, false);
            Reg("Kollam", "IN", 2181940, false);
            Reg("Mecca", "SA", 2184560, false);
            Reg("Wuhu Anhui", "CN", 2195262, false);
            Reg("Grande Vitoria", "BR", 2196818, false);
            Reg("Multan", "PK", 2205407, false);
            Reg("Putian", "CN", 2250104, false);
            Reg("Amman", "JO", 2252688, true);
            Reg("Vijayawada", "IN", 2290785, false);
            Reg("Nashik", "IN", 2294299, false);
            Reg("Esfahan", "IR", 2294589, false);
            Reg("Tijuana", "MX", 2297216, false);
            Reg("Houston", "US", 2305889, false);
            Reg("Aleppo", "SY", 2317650, false);
            Reg("Xuzhou", "CN", 2332531, false);
            Reg("Taoyuan", "TW", 2338724, false);
            Reg("Sendai", "JP", 2341433, false);
            Reg("Phnom Penh", "KH", 2352680, true);
            Reg("Barranquilla", "CO", 2373302, false);
            Reg("Vadodara", "IN", 2373365, false);
            Reg("Baotou", "CN", 2381242, false);
            Reg("Visakhapatnam", "IN", 2385110, false);
            Reg("Liuzhou", "CN", 2397410, false);
            Reg("Maracaibo", "VE", 2400826, false);
            Reg("Beirut", "LB", 2402485, true);
            Reg("Kannur", "IN", 2405664, false);
            Reg("Manaus", "BR", 2406854, false);
            Reg("Agra", "IN", 2422342, false);
            Reg("Rawalpindi", "PK", 2430388, false);
            Reg("Belem", "BR", 2432177, false);
            Reg("San Juan", "PR", 2436620, false);
            Reg("Hohhot", "CN", 2443686, false);
            Reg("Baku", "AZ", 2464162, true);
            Reg("Gujranwala", "PK", 2479058, false);
            Reg("Medan", "ID", 2479070, false);
            Reg("Peshawar", "PK", 2480546, false);
            Reg("Tunis", "TN", 2510673, true);
            Reg("Brisbane", "AU", 2536449, false);
            Reg("Nantong", "CN", 2555722, false);
            Reg("Tangerang", "ID", 2570980, false);
            Reg("Chicago", "US", 2590002, false);
            Reg("Bhopal", "IN", 2624865, false);
            Reg("Patna", "IN", 2633243, false);
            Reg("Tashkent", "UZ", 2633661, true);
            Reg("Sapporo", "JP", 2660947, false);
            Reg("Luoyang", "CN", 2666744, false);
            Reg("Toluca De Lerdo", "MX", 2674336, false);
            Reg("Vancouver", "CA", 2682509, false);
            Reg("Birmingham", "GB", 2684807, false);
            Reg("Damascus", "SY", 2685361, true);
            Reg("Bandung", "ID", 2714215, false);
            Reg("Accra", "GH", 2721165, true);
            Reg("Brazzaville", "CD", 2724566, true);
            Reg("Mogadishu", "SO", 2726815, true);
            Reg("Taipei", "TW", 2766334, true);
            Reg("Manchester", "GB", 2811756, false);
            Reg("Huizhou", "CN", 2827610, false);
            Reg("Zibo", "CN", 2828435, false);
            Reg("Yantai", "CN", 2834508, false);
            Reg("Incheon", "KR", 2861686, false);
            Reg("Shaoxing", "CN", 2882171, false);
            Reg("Pretoria", "ZA", 2889899, true);
            Reg("Goiania", "BR", 2890418, false);
            Reg("Cali", "CO", 2890433, false);
            Reg("Lubumbashi", "CD", 2933962, false);
            Reg("Shizuoka", "JP", 2935527, false);
            Reg("Algiers", "DZ", 2952115, true);
            Reg("Thiruvananthapuram", "IN", 2984154, false);
            Reg("Caracas", "VE", 2991727, true);
            Reg("Weifang", "CN", 2994537, false);
            Reg("Lisbon", "PT", 3014607, true);
            Reg("Kiev", "UA", 3020228, false);
            Reg("Mbuji Mayi", "CD", 3022855, false);
            Reg("Bamako", "ML", 3050570, true);
            Reg("Dubai", "AE", 3051016, false);
            Reg("Zhongshan", "CN", 3051065, false);
            Reg("Port Au Prince", "HT", 3060169, true);
            Reg("Huaian", "CN", 3071048, false);
            Reg("Coimbatore", "IN", 3083721, false);
            Reg("Handan", "CN", 3085998, false);
            Reg("Surabaya", "ID", 3088748, false);
            Reg("Nagpur", "IN", 3106340, false);
            Reg("Izmir", "TR", 3120340, false);
            Reg("Depok", "ID", 3133298, false);
            Reg("Athens", "GR", 3154591, true);
            Reg("Guatemala City", "GT", 3159631, true);
            Reg("Milan", "IT", 3160631, false);
            Reg("Pyongyang", "KP", 3183135, true);
            Reg("Guayaquil", "EC", 3193267, false);
            Reg("Durban", "ZA", 3262128, false);
            Reg("Kanpur", "IN", 3286142, false);
            Reg("Lusaka", "ZM", 3324219, true);
            Reg("Kuwait City", "KW", 3353602, true);
            Reg("Ouagadougou", "BF", 3358934, true);
            Reg("Lanzhou", "CN", 3365910, false);
            Reg("Indore", "IN", 3393380, false);
            Reg("Puebla", "MX", 3394342, false);
            Reg("Sanaa", "YE", 3407814, false);
            Reg("Mashhad", "IR", 3415532, false);
            Reg("Campinas", "BR", 3458441, false);
            Reg("Busan", "KR", 3477419, false);
            Reg("Wuxi", "CN", 3498740, false);
            Reg("Kochi", "IN", 3507053, false);
            Reg("Dakar", "SN", 3540462, true);
            Reg("Asuncion", "PY", 3568830, true);
            Reg("Berlin", "DE", 3576873, true);
            Reg("Santo Domingo", "DO", 3587402, true);
            Reg("Thrissur", "IN", 3605238, false);
            Reg("Port Harcourt", "NG", 3636547, false);
            Reg("Guiyang", "CN", 3661446, false);
            Reg("Los Angeles", "US", 3748640, false);
            Reg("Faisalabad", "PK", 3800193, false);
            Reg("Bekasi", "ID", 3830678, false);
            Reg("Curitiba", "BR", 3852459, false);
            Reg("Kumasi", "GH", 3903481, false);
            Reg("Tangshan Hebei", "CN", 3925206, false);
            Reg("Casablanca", "MA", 3950408, false);
            Reg("Salvador", "BR", 3994982, false);
            Reg("Fuzhou Fujian", "CN", 3998754, false);
            Reg("Ibadan", "NG", 4004316, false);
            Reg("Xiamen", "CN", 4007468, false);
            Reg("Wenzhou", "CN", 4009531, false);
            Reg("Nanchang", "CN", 4016037, false);
            Reg("Abuja", "NG", 4025735, true);
            Reg("Lucknow", "IN", 4038214, false);
            Reg("Antananarivo", "MG", 4048666, true);
            Reg("Kampala", "UG", 4050826, true);
            Reg("Changzhou", "CN", 4085502, false);
            Reg("Medellin", "CO", 4137386, false);
            Reg("Malappuram", "IN", 4184921, false);
            Reg("Ekurhuleni", "ZA", 4190832, false);
            Reg("Douala", "CM", 4203108, false);
            Reg("Taiyuan Shanxi", "CN", 4226782, false);
            Reg("Porto Alegre", "BR", 4239867, false);
            Reg("Kozhikode", "IN", 4243962, false);
            Reg("Fortaleza", "BR", 4246399, false);
            Reg("Nanning", "CN", 4291463, false);
            Reg("Recife", "BR", 4305127, false);
            Reg("Jaipur", "IN", 4308510, false);
            Reg("Rome", "IT", 4331974, true);
            Reg("Montreal", "CA", 4341638, false);
            Reg("Shijiazhuang", "CN", 4454132, false);
            Reg("Kano", "NG", 4490734, false);
            Reg("Tel Aviv", "IL", 4495727, false);
            Reg("New Taipei", "TW", 4534877, false);
            Reg("Shantou", "CN", 4656525, false);
            Reg("Ningbo", "CN", 4659830, false);
            Reg("Yaounde", "CM", 4681768, true);
            Reg("Hefei", "CN", 4727290, false);
            Reg("Kabul", "AF", 4728384, true);
            Reg("Changchun", "CN", 4802447, false);
            Reg("Kunming", "CN", 4861079, false);
            Reg("Brasilia", "BR", 4935274, true);
            Reg("Jiddah", "SA", 4943210, false);
            Reg("Cape Town", "ZA", 4977833, true);
            Reg("Urumqi", "CN", 5005964, false);
            Reg("Changsha", "CN", 5027975, false);
            Reg("Sydney", "AU", 5184896, false);
            Reg("Monterrey", "MX", 5195355, false);
            Reg("Melbourne", "AU", 5315600, false);
            Reg("Hanoi", "VN", 5431801, true);
            Reg("Ankara", "TR", 5477087, true);
            Reg("Fukuoka", "JP", 5478076, false);
            Reg("Guadalajara", "MX", 5499678, false);
            Reg("Chittagong", "BD", 5513609, false);
            Reg("Nairobi", "KE", 5541172, true);
            Reg("Saint Petersburg", "RU", 5581707, false);
            Reg("Alexandria", "EG", 5696131, false);
            Reg("Addis Ababa", "ET", 5703628, true);
            Reg("Yangon", "MM", 5709678, false);
            Reg("Barcelona", "ES", 5711917, false);
            Reg("Abidjan", "CI", 5866704, false);
            Reg("Ji Nan Shandong", "CN", 5940698, false);
            Reg("Zhengzhou", "CN", 6014887, false);
            Reg("Qingdao", "CN", 6104597, false);
            Reg("Singapore", "SG", 6119203, true);
            Reg("Dalian", "CN", 6217487, false);
            Reg("Belo Horizonte", "BR", 6300409, false);
            Reg("Johannesburg", "ZA", 6324351, false);
            Reg("Toronto", "CA", 6431430, false);
            Reg("Khartoum", "SD", 6542070, true);
            Reg("Madrid", "ES", 6783241, true);
            Reg("Haerbin", "CN", 6938008, false);
            Reg("Santiago", "CL", 6950952, true);
            Reg("Pune", "IN", 7345848, false);
            Reg("Dongguan", "CN", 7675146, false);
            Reg("Foshan", "CN", 7704935, false);
            Reg("Hong Kong", "HK", 7725859, false);
            Reg("Riyadh", "SA", 7820551, true);
            Reg("Shenyang", "CN", 7830377, false);
            Reg("Baghdad", "IQ", 7921134, true);
            Reg("New York", "US", 7931147, false);
            Reg("Dar Es Salaam", "TZ", 8161231, false);
            Reg("Surat", "IN", 8330528, false);
            Reg("Suzhou", "CN", 8350625, false);
            Reg("Hangzhou", "CN", 8419842, false);
            Reg("Kuala Lumpur", "MY", 8815630, true);
            Reg("Wuhan", "CN", 8850850, false);
            Reg("Ahmedabad", "IN", 8854444, false);
            Reg("Xi An Shaanxi", "CN", 9013837, false);
            Reg("Nagoya", "JP", 9556879, false);
            Reg("Ho Chi Minh City", "VN", 9567656, false);
            Reg("Tehran", "IR", 9616007, true);
            Reg("Luanda", "AO", 9651032, true);
            Reg("London", "GB", 9748033, true);
            Reg("Chengdu", "CN", 9828110, false);
            Reg("Nanjing", "CN", 9947548, false);
            Reg("Seoul", "KR", 10004840, true);
            Reg("Hyderabad", "IN", 11068877, false);
            Reg("Bangkok", "TH", 11233869, true);
            Reg("Paris", "FR", 11276701, true);
            Reg("Lima", "PE", 11361938, true);
            Reg("Jakarta", "ID", 11436004, true);
            Reg("Bogota", "CO", 11658211, true);
            Reg("Chennai", "IN", 12053697, false);
            Reg("Moscow", "RU", 12712305, true);
            Reg("Shenzhen", "CN", 13311855, false);
            Reg("Rio De Janeiro", "BR", 13824347, false);
            Reg("Bangalore", "IN", 14008262, false);
            Reg("Lahore", "PK", 14407074, false);
            Reg("Tianjin", "CN", 14470873, false);
            Reg("Guangzhou", "CN", 14590096, false);
            Reg("Manila", "PH", 14941953, true);
            Reg("Kolkata", "IN", 15570786, false);
            Reg("Buenos Aires", "AR", 15618288, true);
            Reg("Istanbul", "TR", 16047350, false);
            Reg("Lagos", "NG", 16536018, false);
            Reg("Kinshasa", "CD", 17032322, true);
            Reg("Karachi", "PK", 17648555, false);
            Reg("Chongqing", "CN", 17773923, false);
            Reg("Osaka", "JP", 18967459, false);
            Reg("Mumbai", "IN", 21673149, false);
            Reg("Beijing", "CN", 22189082, true);
            Reg("Mexico City", "MX", 22505315, true);
            Reg("Cairo", "EG", 22623874, true);
            Reg("Sao Paulo", "BR", 22806704, false);
            Reg("Dhaka", "BD", 23935652, true);
            Reg("Shanghai", "CN", 29867918, false);
            Reg("Delhi", "IN", 33807403, false);
            Reg("Tokyo", "JP", 37115035, true);

        }

        #endregion//Data

        #endregion//Setup

    }


}




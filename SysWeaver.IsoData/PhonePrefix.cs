using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Data;

namespace SysWeaver
{


    [TableDataPrimaryKey(nameof(CountryCode), nameof(RegionPrefix))]
    public sealed class PhonePrefix
    {
        public PhonePrefix(String countryCode, String name, String iso3166a2, String intPrefix, String natPrefix, String regionPrefix, int rank, params int[] localCounts)
        {
            CountryCode = countryCode;
            Name = name;
            IsoCountry = iso3166a2;
            IntPrefix = intPrefix;
            NatPrefix = natPrefix;
            RegionPrefix = regionPrefix;
            LocalCounts = localCounts;
            Rank = rank;
        }

        /// <summary>
        /// Flag (same as IsoCountry), don't use
        /// </summary>
        [TableDataIsoCountryImage]
        [TableDataOrder(-1)]
        public String Flag => IsoCountry;

        /// <summary>
        /// The number of valid local digits 
        /// </summary>
        [TableDataOrder(1)]
        public String NumLocalDigits
        {
            get
            {
                var l = LocalCounts;
                if (l == null)
                    return null;
                var t = StringTools.JoinWithSpecialLast(", ", " or ", l);
                return t;
            }
        }

        /// <summary>
        /// International dialing prefix.
        /// </summary>
        [TableDataKey]
        public readonly String CountryCode;

        /// <summary>
        /// Optional prefix(es) required to identify the region, can be multiple values seprated by a comma or null if no region is required.
        /// </summary>
        public readonly String RegionPrefix;

        /// <summary>
        /// Name of phone prefix.
        /// </summary>
        [TableDataKey]
        public readonly String Name;

        /// <summary>
        /// Two letter ISO-3166a2 country code, null means that this is not a country.
        /// </summary>
        [TableDataIsoCountry]
        public readonly String IsoCountry;

        /// <summary>
        /// Prefix(es) for dialing international number in the region, null means that this is not a region.
        /// </summary>
        public readonly String IntPrefix;

        /// <summary>
        /// Prefix(es) for dialing a national number in the region, null means that no prefix is required.
        /// </summary>
        public readonly String NatPrefix;

        /// <summary>
        /// Valid number of digits (excluding region prefix, national prefix and country code)
        /// </summary>
        public readonly IReadOnlyList<int> LocalCounts;

        /// <summary>
        /// Approximate rank according to population density in the country.
        /// Methods returning multiple phone prefixes should return the highest ranked first and so on.
        /// </summary>
        [TableDataKey]
        public readonly int Rank;

        public override string ToString() =>
            IsoCountry == null
                ? (
                    RegionPrefix == null 
                        ? 
                        String.Concat('+', CountryCode, ' ', Name) 
                        : 
                        String.Concat('+', CountryCode, " (", RegionPrefix, ") ", Name)
                ) :(
                    RegionPrefix == null
                        ?
                        String.Concat('+', CountryCode, ' ', Name, " [", IsoCountry, ']')
                        :
                        String.Concat('+', CountryCode, " (", RegionPrefix, ") ", Name, " [", IsoCountry, ']')
                );



        static readonly Level Root;

        /// <summary>
        /// Given the start of an international telephone number, identify was phone code(s) that it can be
        /// </summary>
        /// <param name="nextChar">The position of the next character (the first character that wasn't parsed)</param>
        /// <param name="internationalNumber">An international telephone number as a string, only digits are respected, all other chars are ignored</param>
        /// <param name="matchExact">If true, only the exact matches are returned</param>
        /// <returns>Null if no digits are found, else a list of phone codes that this number could be</returns>
        public static IReadOnlyList<PhonePrefix> Identify(out int nextChar, String internationalNumber, bool matchExact = false)
        {
            nextChar = -1;
            if (String.IsNullOrEmpty(internationalNumber))
                return null;
            var l = internationalNumber.Length;
            var node = Root;
            var exact = node;
            var exactIndex  = 0;
            int i;
            for (i = 0; i < l; ++ i)
            {
                var nexts = node.Next;
                if (nexts == null)
                {
                    --i;
                    break;
                }
                var c = internationalNumber[i];
                if (c < '0')
                    continue;
                if (c > '9')
                    continue;
                var v = (int)(c - '0');
                var next = nexts[v];
                if (next == null)
                {
                    matchExact = true;
                    break;
                }
                node = next;
                if (node.Exact.Count > 0)
                {
                    exact = node;
                    exactIndex = i + 1;
                }
            }
            if (node == Root)
                return null;
            if (matchExact)
            {
                nextChar = exactIndex;
                return exact.Exact;
            }
            nextChar = i;
            if (exact != node)
            {
                var comb = new List<PhonePrefix>(exact.Exact);
                comb.AddRange(node.Partial);
                comb.Sort((a, b) => b.Rank - a.Rank);
                return comb;
            }
            return node.Partial;
        }

        /// <summary>
        /// Given the start of an international telephone number, identify was phone code(s) that it can be
        /// </summary>
        /// <param name="internationalNumber">An international telephone number as a string, only digits are respected, all other chars are ignored</param>
        /// <param name="matchExact">If true, only the exact matches are returned</param>
        /// <returns>Null if no digits are found, else a list of phone codes that this number could be</returns>
        public static IReadOnlyList<PhonePrefix> Identify(String internationalNumber, bool matchExact = false)
            => Identify(out var _, internationalNumber, matchExact);


        static String GetNumbers(String s, int start, int stop)
        {
            var sb = new Char[stop - start];
            int o = 0;
            for (int i = start; i < stop; ++ i)
            {
                var c = s[i];
                if (c < '0')
                    continue;
                if (c > '9')
                    continue;
                sb[o] = c;
                ++o;
            }
            return new string(sb, 0, o);
        }

        static readonly HashSet<Char> Vowels = new HashSet<char>("AEIOUYÅÄÖaeiouyåäö");

        /// <summary>
        /// Get the details of an international telephone number
        /// </summary>
        /// <param name="prefix">The prefix digits with a + at the start</param>
        /// <param name="localNumber">The local number digits</param>
        /// <param name="internationalNumber">An international telephone number as a string, only digits are respected, all other chars are ignored</param>
        /// <returns>The phone prefix(es) for this number</returns>
        /// <exception cref="Exception"></exception>
        public static IReadOnlyList<PhonePrefix> GetValidatedPhoneNumber(out String prefix, out String localNumber, String internationalNumber)
        {
            if (internationalNumber == null)
                throw new Exception("The phone number may not be null");
            var pcs = Identify(out var prefixEnd, internationalNumber, true);
            var pcsl = pcs?.Count ?? 0;
            if (pcsl <= 0)
                throw new Exception("The phone number isn't using a valid international country code prefix");
            prefix = GetNumbers(internationalNumber, 0, prefixEnd);
            var preLen = prefix.Length;
            localNumber = GetNumbers(internationalNumber, prefixEnd, internationalNumber.Length);
            var name = StringTools.JoinWithSpecialLast(", ", " or ", pcs.Select(x => x.Name));
            var pre = (Vowels.Contains(name[0]) ? "An " : "A ") + name;
            var ll = localNumber.Length;
            List<PhonePrefix> validPcs = new List<PhonePrefix>(pcsl);
            --pcsl;
            for (int pci = 0; pci <= pcsl; ++pci)
            {
                void Throw(String s)
                {
                    if ((pci == pcsl) && (validPcs.Count <= 0))
                        throw new Exception(s);
                }
                var pc = pcs[pci];
                var valid = pc.LocalCounts;
                if (valid == null)
                {
                    Throw(name + " is a reserved an unsupported phone number");
                    continue;
                }
                var vc = valid.Count;
                if (vc == 0)
                { 
                    Throw(name + " is a reserved an unsupported phone number");
                    continue;
                }
                var min = valid[0];
                if (ll < min)
                { 
                    Throw(pre + " phone number must contain at least " + (preLen + min) + " digits");
                    continue;
                }
                var max = valid[vc - 1];
                if (ll > max)
                { 
                    Throw(pre + " phone number must contain at most " + (preLen + max) + " digits");
                    continue;
                }
                bool isValid = false;
                for (int i = 0; i < vc; ++i)
                {
                    isValid = valid[i] == ll;
                    if (isValid)
                        break;
                }
                if (!isValid)
                { 
                    Throw(pre + " phone numbers must contain " + StringTools.JoinWithSpecialLast(", ", " or ", valid.Select(x => preLen + x)) + " digits");
                    continue;
                }
                validPcs.Add(pc);
            }
            var l = validPcs[0].CountryCode.Length;
            var pl = prefix.Length;
            if (l != pl)
                prefix = String.Concat("+", prefix.Substring(0, l), " ", prefix.Substring(l));
            else
                prefix = "+" + prefix;
            return validPcs.ToArray();
        }


        /// <summary>
        /// Get the details of an international telephone number
        /// </summary>
        /// <param name="name">Name(s) of the countries that the number can be in</param>
        /// <param name="isoCountry">Two letter ISO-3166a2 country code of the highest ranked country</param>
        /// <param name="prefix">The prefix digits with a + at the start</param>
        /// <param name="localNumber">The local number digits</param>
        /// <param name="internationalNumber">An international telephone number as a string, only digits are respected, all other chars are ignored</param>
        /// <returns>The phone prefix(es) for this number</returns>
        public static IReadOnlyList<PhonePrefix> GetValidatedPhoneNumber(out String name, out String isoCountry, out String prefix, out String localNumber, String internationalNumber)
        {
            var pcs = GetValidatedPhoneNumber(out prefix, out localNumber, internationalNumber);
            pcs = pcs.Where(x => x.IsoCountry != null).ToList();
            if (pcs.Count <= 0)
                throw new Exception("The prefix " + prefix + " is not for a country");
            isoCountry = pcs.First().IsoCountry;
            name = StringTools.JoinWithSpecialLast(", ", " or ", pcs.Select(x => x.Name));
            return pcs;
        }


        sealed class Level
        {
#if DEBUG
            public override string ToString() => String.Concat(Partial.Count, " (", Exact.Count, ')');
#endif//DEBUG

            public Level[] Next = new Level[10];
            public List<PhonePrefix> Partial = new List<PhonePrefix>();
            public List<PhonePrefix> Exact = new List<PhonePrefix>();
        }

        static PhonePrefix()
        {
            var root = new Level();
            String[] noPrefix = [""]; 
            Root = root;
            foreach (var x in Codes)
            {
                var prefixes = x.RegionPrefix?.Split(',') ?? noPrefix;
                foreach (var pre in prefixes)
                {
                    var nums = x.CountryCode + pre;
                    var node = root;
                    foreach (var c in nums)
                    {
                        var v = (int)(c - '0');
                        var next = node.Next[v];
                        if (next == null)
                        {
                            next = new();
                            node.Next[v] = next;
                        }
                        next.Partial.Add(x);
                        node = next;
                    }
                    node.Exact.Add(x);
                }
            }

            void FixCodes(List<PhonePrefix> codes)
            {
                codes.Sort((a, b) => b.Rank - a.Rank);
                int l = codes.Count;
                int o = 0;
                for (int i = 1; i < l; ++i)
                {
                    var nc = codes[i];
                    if (nc == codes[o])
                        continue;
                    ++o;
                    codes[o] = nc;
                }
                ++o;
                if (o < l)
                    codes.RemoveRange(o, l - o);
            }

            void FixRec(Level level)
            {
                if (level == null)
                    return;
                FixCodes(level.Partial);
                FixCodes(level.Exact);
                if (level.Next == null)
                    return;
                if (!level.Next.Any(x => x != null))
                {
                    level.Next = null;
                    return;
                }
                foreach (var x in level.Next)
                    FixRec(x);
            }

            FixRec(root);
        }

        /// <summary>
        /// List of known phone codes
        /// </summary>
        public static readonly IReadOnlyList<PhonePrefix> Codes = new[]
        {
            new PhonePrefix("93", "Afghanistan", "AF", "00", "0", null, 205, 9),
            new PhonePrefix("355", "Albania", "AL", "00", "0", null, 102, 3, 4, 5, 6, 7, 8, 9),
            new PhonePrefix("213", "Algeria", "DZ", "00", "0", null, 209, 8, 9),
            new PhonePrefix("1", "American Samoa", "AS", "011", "1", "684", 41, 7),
            new PhonePrefix("376", "Andorra", "AD", "00", null, null, 46, 6, 8, 9),
            new PhonePrefix("244", "Angola", "AO", "00", "0", null, 198, 9),
            new PhonePrefix("1", "Anguilla", "AI", "011", "1", "264", 29, 7),
            new PhonePrefix("1", "Antigua and Barbuda", "AG", "011", "1", "268", 47, 7),
            new PhonePrefix("54", "Argentina", "AR", "00", "0", null, 210, 10),
            new PhonePrefix("374", "Armenia", "AM", "00", "0", null, 105, 8),
            new PhonePrefix("297", "Aruba", "AW", "00", null, null, 51, 7),
            new PhonePrefix("61", "Australia", "AU", "0011", "0", null, 186, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
            new PhonePrefix("672", "Australian External Territories", "AU", "00", "0", null, 187, 6),
            new PhonePrefix("43", "Austria", "AT", "00", "0", null, 144, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13),
            new PhonePrefix("994", "Azerbaijan", "AZ", "00", "0", null, 151, 8, 9),
            new PhonePrefix("1", "Bahamas", "BS", "011", "1", "242", 67, 7),
            new PhonePrefix("973", "Bahrain", "BH", "00", null, null, 90, 8),
            new PhonePrefix("880", "Bangladesh", "BD", "00", "0", null, 236, 6, 7, 8, 9, 10),
            new PhonePrefix("1", "Barbados", "BB", "011", "1", "246", 62, 7),
            new PhonePrefix("375", "Belarus", "BY", "810", "8", null, 145, 9, 10),
            new PhonePrefix("32", "Belgium", "BE", "00", "0", null, 160, 8, 9),
            new PhonePrefix("501", "Belize", "BZ", "00", null, null, 68, 7),
            new PhonePrefix("229", "Benin", "BJ", "00", null, null, 164, 8),
            new PhonePrefix("1", "Bermuda", "BM", "011", "1", "441", 43, 7),
            new PhonePrefix("975", "Bhutan", "BT", "00", null, null, 79, 7, 8),
            new PhonePrefix("591", "Bolivia (Plurinational State of)", "BO", "00", "0", null, 161, 8),
            new PhonePrefix("599", "Saba", "BQ", "00", "0", null, 9, 7),
            new PhonePrefix("387", "Bosnia and Herzegovina", "BA", "00", "0", null, 107, 8),
            new PhonePrefix("267", "Botswana", "BW", "00", null, null, 97, 7, 8),
            new PhonePrefix("55", "Brazil", "BR", "00", "0", null, 238, 10),
            new PhonePrefix("1", "British Virgin Islands", "VG", "011", "1", "284", 32, 7),
            new PhonePrefix("673", "Brunei Darussalam", "BN", "00", null, null, 70, 7),
            new PhonePrefix("359", "Bulgaria", "BG", "00", "0", null, 134, 7, 8, 9),
            new PhonePrefix("226", "Burkina Faso", "BF", "00", null, null, 182, 8),
            new PhonePrefix("257", "Burundi", "BI", "00", null, null, 163, 8),
            new PhonePrefix("855", "Cambodia", "KH", "001,007", "0", null, 170, 8),
            new PhonePrefix("237", "Cameroon", "CM", "00", null, null, 190, 7, 8, 9),
            new PhonePrefix("1", "Canada", "CA", "011", "1", null, 203, 10),
            new PhonePrefix("238", "Cape Verde", "CV", "00", null, null, 73, 7),
            new PhonePrefix("1", "Cayman Islands", "KY", "011", "1", "345", 44, 7),
            new PhonePrefix("236", "Central African Rep.", "CF", "00", null, null, 117, 8),
            new PhonePrefix("235", "Chad", "TD", "00", null, null, 169, 8),
            new PhonePrefix("56", "Chile", "CL", "1YZ0", "1YZ", null, 178, 8, 9),
            new PhonePrefix("86", "China", "CN", "00", "0", null, 243, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("57", "Colombia", "CO", "009,007,005", "09,07,05", null, 213, 8, 10),
            new PhonePrefix("269", "Comoros", "KM", "00", null, null, 81, 7),
            new PhonePrefix("242", "Congo", "CG", "00", null, null, 124, 9),
            new PhonePrefix("682", "Cook Islands", "CK", "00", null, null, 30, 5),
            new PhonePrefix("506", "Costa Rica", "CR", "00", null, null, 120, 8),
            new PhonePrefix("225", "Côte d'Ivoire", "CI", "00", null, null, 189, 8),
            new PhonePrefix("385", "Croatia", "HR", "00", "0", null, 112, 8, 9, 10, 11, 12),
            new PhonePrefix("53", "Cuba", "CU", "119", "0", null, 158, 6, 7, 8),
            new PhonePrefix("599", "Curaçao", "CW", "00", "0", null, 55, 7, 8),
            new PhonePrefix("357", "Cyprus", "CY", "00", null, null, 84, 8, 11),
            new PhonePrefix("420", "Czech Rep.", "CZ", "00", null, null, 155, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("850", "Dem. People's Rep. of Korea", "KP", "00", "0", null, 214, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17),
            new PhonePrefix("243", "Dem. Rep. of the Congo", "CD", "00", "0", null, 228, 5, 6, 7, 8, 9),
            new PhonePrefix("45", "Denmark", "DK", "00", null, null, 126, 8),
            new PhonePrefix("246", "Diego Garcia", "GB", "00", null, null, 222, 7),
            new PhonePrefix("253", "Djibouti", "DJ", "00", null, null, 16, 6),
            new PhonePrefix("1", "Dominica", "DM", "011", "1", "767", 45, 7),
            new PhonePrefix("1", "Dominican Rep.", "DO", "011", "1", "809,829", 156, 7),
            new PhonePrefix("593", "Ecuador", "EC", "00", "0", null, 174, 8),
            new PhonePrefix("20", "Egypt", "EG", "00", "0", null, 230, 7, 8, 9),
            new PhonePrefix("503", "El Salvador", "SV", "00", null, null, 129, 7, 8, 11),
            new PhonePrefix("240", "Equatorial Guinea", "GQ", "00", null, null, 89, 9),
            new PhonePrefix("291", "Eritrea", "ER", "00", "0", null, 109, 7),
            new PhonePrefix("372", "Estonia", "EE", "00", null, null, 87, 7, 8, 9, 10),
            new PhonePrefix("251", "Ethiopia", "ET", "00", "0", null, 232, 9),
            new PhonePrefix("500", "Falkland Islands (Malvinas)", "FK", "00", null, null, 13, 5),
            new PhonePrefix("298", "Faroe Islands", "FO", "00", null, null, 39, 6),
            new PhonePrefix("679", "Fiji", "FJ", "00", null, null, 82, 7),
            new PhonePrefix("358", "Finland", "FI", "00,99X", "0", null, 125, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("33", "France", "FR", "00", "0", null, 220, 9),
            new PhonePrefix("262", "French Dep. and Territories in the Indian Ocean", "FR", "00", null, null, 221, 9),
            new PhonePrefix("594", "French Guiana", "GF", "00", null, null, 63, 9),
            new PhonePrefix("689", "French Polynesia", "PF", "00", null, null, 60, 6),
            new PhonePrefix("241", "Gabon", "GA", "00", null, null, 96, 6, 7),
            new PhonePrefix("220", "Gambia", "GM", "00", null, null, 98, 7),
            new PhonePrefix("995", "Georgia", "GE", "00", "0", null, 110, 9),
            new PhonePrefix("49", "Germany", "DE", "00", "0", null, 225, 6, 7, 8, 9, 10, 11, 12, 13),
            new PhonePrefix("233", "Ghana", "GH", "00", "0", null, 195, 5, 6, 7, 8, 9),
            new PhonePrefix("350", "Gibraltar", "GI", "00", null, null, 33, 8),
            new PhonePrefix("881", "Global Mobile Satellite System (GMSS), shared", null, null, null, null, 0),
            new PhonePrefix("30", "Greece", "GR", "00", "0", null, 154, 10),
            new PhonePrefix("299", "Greenland", "GL", "00", null, null, 20, 6),
            new PhonePrefix("1", "Grenada", "GD", "011", "1", "473", 52, 7),
            new PhonePrefix("388", "Group of countries, shared code", null, null, null, null, 1),
            new PhonePrefix("590", "Guadeloupe", "GP", "00", null, null, 69, 9),
            new PhonePrefix("1", "Guam", "GU", "011", "1", "671", 56, 7),
            new PhonePrefix("502", "Guatemala", "GT", "00", null, null, 175, 8),
            new PhonePrefix("224", "Guinea", "GN", "00", null, null, 166, 8),
            new PhonePrefix("245", "Guinea-Bissau", "GW", "00", null, null, 92, 7),
            new PhonePrefix("592", "Guyana", "GY", "001", null, null, 80, 7),
            new PhonePrefix("509", "Haiti", "HT", "00", null, null, 159, 8),
            new PhonePrefix("504", "Honduras", "HN", "00", null, null, 149, 8),
            new PhonePrefix("852", "Hong Kong", "HK", "001", null, null, 137, 4, 8, 9),
            new PhonePrefix("36", "Hungary", "HU", "00", "06", null, 147, 8, 9),
            new PhonePrefix("354", "Iceland", "IS", "00", null, null, 65, 7, 9),
            new PhonePrefix("91", "India", "IN", "00", "0", null, 242, 7, 8, 9, 10),
            new PhonePrefix("62", "Indonesia", "ID", "001,008", "0", null, 240, 5, 6, 7, 8, 9, 10),
            new PhonePrefix("870", "Inmarsat SNAC", null, "00", null, null, 2, 9),
            new PhonePrefix("800", "International Freephone Service", null, null, null, null, 3, 8),
            new PhonePrefix("882", "International Networks, shared code", null, null, null, null, 5),
            new PhonePrefix("883", "International Networks, shared code", null, null, null, null, 4),
            new PhonePrefix("979", "International Premium Rate Service (IPRS)", null, null, null, null, 6, 9),
            new PhonePrefix("808", "International Shared Cost Service (ISCS)", null, null, null, null, 7, 8),
            new PhonePrefix("98", "Iran", "IR", "00", "0", null, 226, 6, 7, 8, 9, 10),
            new PhonePrefix("964", "Iraq", "IQ", "00", "0", null, 206, 8, 9, 10),
            new PhonePrefix("353", "Ireland", "IE", "00", "0", null, 118, 7, 8, 9, 10, 11),
            new PhonePrefix("972", "Israel", "IL", "00,012,013,014", "0", null, 141, 8, 9),
            new PhonePrefix("39", "Italy", "IT", "00", null, null, 219, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11),
            new PhonePrefix("1", "Jamaica", "JM", "011", "1", "876", 104, 7),
            new PhonePrefix("81", "Japan", "JP", "010", "0", null, 233, 5, 6, 7, 8, 9, 10, 11, 12, 13),
            new PhonePrefix("962", "Jordan", "JO", "00", "0", null, 153, 5, 6, 7, 8, 9),
            new PhonePrefix("7", "Kazakhstan", "KZ", "810", "8", null, 177, 10),
            new PhonePrefix("254", "Kenya", "KE", "000", "0", null, 215, 6, 7, 8, 9, 10),
            new PhonePrefix("686", "Kiribati", "KI", "00", null, null, 54, 5),
            new PhonePrefix("82", "South Korea", "KR", "001,002", "0,082", null, 188, 8, 9, 10, 11),
            new PhonePrefix("965", "Kuwait", "KW", "00", null, null, 113, 7, 8),
            new PhonePrefix("996", "Kyrgyzstan", "KG", "00", "0", null, 130, 9),
            new PhonePrefix("856", "Lao P.D.R.", "LA", "00", "0", null, 136, 8, 9, 10),
            new PhonePrefix("371", "Latvia", "LV", "00", null, null, 91, 7, 8),
            new PhonePrefix("961", "Lebanon", "LB", "00", "0", null, 132, 7, 8),
            new PhonePrefix("266", "Lesotho", "LS", "00", null, null, 95, 8),
            new PhonePrefix("231", "Liberia", "LR", "00", null, null, 119, 7, 8),
            new PhonePrefix("218", "Libya", "LY", "00", "0", null, 133, 8, 9),
            new PhonePrefix("423", "Liechtenstein", "LI", "00", null, null, 35, 7, 8, 9),
            new PhonePrefix("370", "Lithuania", "LT", "00", "0", null, 100, 8),
            new PhonePrefix("352", "Luxembourg", "LU", "00", null, null, 75, 4, 5, 6, 7, 8, 9, 10, 11),
            new PhonePrefix("853", "Macao", "MO", "00", null, null, 77, 7, 8),
            new PhonePrefix("261", "Madagascar", "MG", "00", null, null, 191, 9, 10),
            new PhonePrefix("265", "Malawi", "MW", "00", null, null, 179, 7, 8),
            new PhonePrefix("60", "Malaysia", "MY", "00", "0", null, 197, 7, 8, 9),
            new PhonePrefix("960", "Maldives", "MV", "00", null, null, 72, 7),
            new PhonePrefix("223", "Mali", "ML", "00", null, null, 181, 8),
            new PhonePrefix("356", "Malta", "MT", "00", null, null, 71, 8),
            new PhonePrefix("692", "Marshall Islands", "MH", "011", "1", null, 22, 7),
            new PhonePrefix("596", "Martinique", "MQ", "00", null, null, 66, 9),
            new PhonePrefix("222", "Mauritania", "MR", "00", null, null, 115, 7),
            new PhonePrefix("230", "Mauritius", "MU", "00", null, null, 85, 7),
            new PhonePrefix("52", "Mexico", "MX", "00", "01", null, 234, 10),
            new PhonePrefix("691", "Micronesia", "FM", "011", "1", null, 53, 7),
            new PhonePrefix("373", "Moldova", "MD", "00", "0", null, 111, 8),
            new PhonePrefix("377", "Monaco", "MC", "00", null, null, 37, 5, 6, 7, 8, 9),
            new PhonePrefix("976", "Mongolia", "MN", "001", "0", null, 106, 7, 8),
            new PhonePrefix("382", "Montenegro", "ME", "00", "0", null, 76, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("1", "Montserrat", "MS", "011", "1", "664", 19, 7),
            new PhonePrefix("212", "Morocco", "MA", "00", "0", null, 202, 9),
            new PhonePrefix("258", "Mozambique", "MZ", "00", null, null, 196, 8, 9),
            new PhonePrefix("95", "Myanmar", "MM", "00", "0", null, 216, 7, 8, 9),
            new PhonePrefix("264", "Namibia", "NA", "00", "0", null, 99, 6, 7, 8, 9, 10),
            new PhonePrefix("674", "Nauru", "NR", "00", null, null, 25, 4, 7),
            new PhonePrefix("977", "Nepal", "NP", "00", "0", null, 193, 8, 9),
            new PhonePrefix("31", "Netherlands", "NL", "00", "0", null, 172, 9),
            new PhonePrefix("687", "New Caledonia", "NC", "00", null, null, 61, 6),
            new PhonePrefix("64", "New Zealand", "NZ", "00", "0", null, 116, 3, 4, 5, 6, 7, 8, 9, 10),
            new PhonePrefix("505", "Nicaragua", "NI", "00", null, null, 131, 8),
            new PhonePrefix("227", "Niger", "NE", "00", null, null, 185, 8),
            new PhonePrefix("234", "Nigeria", "NG", "009", "0", null, 237, 7, 8, 9, 10),
            new PhonePrefix("683", "Niue", "NU", "00", null, null, 18, 4),
            new PhonePrefix("1", "Northern Marianas", "MP", "011", "1", "670", 42, 7),
            new PhonePrefix("47", "Norway", "NO", "00", null, null, 122, 5, 8),
            new PhonePrefix("968", "Oman", "OM", "00", null, null, 121, 7, 8),
            new PhonePrefix("92", "Pakistan", "PK", "00", "0", null, 239, 8, 9, 10, 11),
            new PhonePrefix("680", "Palau", "PW", "011", null, null, 31, 7),
            new PhonePrefix("507", "Panama", "PA", "00", null, null, 114, 7, 8),
            new PhonePrefix("675", "Papua New Guinea", "PG", "00", null, null, 143, 4, 5, 6, 7, 8, 9, 10, 11),
            new PhonePrefix("595", "Paraguay", "PY", "00", "0", null, 135, 5, 6, 7, 8, 9),
            new PhonePrefix("51", "Peru", "PE", "00", "0", null, 199, 8, 9, 10, 11),
            new PhonePrefix("63", "Philippines", "PH", "00", "0", null, 231, 8, 9, 10),
            new PhonePrefix("48", "Poland", "PL", "00", "0", null, 204, 6, 7, 8, 9),
            new PhonePrefix("351", "Portugal", "PT", "00", null, null, 152, 9, 11),
            new PhonePrefix("1", "Puerto Rico", "PR", "011", "1", "787,939", 101, 7),
            new PhonePrefix("974", "Qatar", "QA", "00", null, null, 103, 3, 4, 5, 6, 7, 8),
            new PhonePrefix("40", "Romania", "RO", "00", "0", null, 180, 9),
            new PhonePrefix("7", "Russian Federation", "RU", "810", "8", null, 235, 10),
            new PhonePrefix("250", "Rwanda", "RW", "00", null, null, 165, 9),
            new PhonePrefix("247", "Saint Helena", "SH", "00", null, null, 24, 4),
            new PhonePrefix("290", "Saint Helena", "SH", "00", null, null, 23, 4),
            new PhonePrefix("1", "Saint Kitts and Nevis", "KN", "011", "1", "869", 40, 7),
            new PhonePrefix("1", "Saint Lucia", "LC", "011", "1", "758", 57, 7),
            new PhonePrefix("508", "Saint Pierre and Miquelon", "PM", "00", null, null, 21, 6),
            new PhonePrefix("1", "Saint Vincent and the Grenadines", "VC", "011", "1", "784", 26, 7),
            new PhonePrefix("685", "Samoa", "WS", "0", null, null, 58, 3, 4, 5, 6, 7),
            new PhonePrefix("378", "San Marino", "SM", "00", null, null, 34, 6, 7, 8, 9, 10),
            new PhonePrefix("239", "Sao Tome and Principe", "ST", "00", null, null, 59, 7),
            new PhonePrefix("966", "Saudi Arabia", "SA", "00", "0", null, 201, 8, 9),
            new PhonePrefix("221", "Senegal", "SN", "00", null, null, 171, 9),
            new PhonePrefix("381", "Serbia", "RS", "00", "0", null, 142, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("248", "Seychelles", "SC", "00", null, null, 48, 7),
            new PhonePrefix("232", "Sierra Leone", "SL", "00", "0", null, 138, 8),
            new PhonePrefix("65", "Singapore", "SG", "001,008", null, null, 127, 8, 9, 10, 11, 12),
            new PhonePrefix("1", "Sint Maarten (Dutch part)", "SX", "011", "1", "721", 38, 7),
            new PhonePrefix("421", "Slovakia", "SK", "00", "0", null, 123, 4, 5, 6, 7, 8, 9),
            new PhonePrefix("386", "Slovenia", "SI", "00", "0", null, 93, 8),
            new PhonePrefix("677", "Solomon Islands", "SB", "00", null, null, 78, 5),
            new PhonePrefix("252", "Somalia", "SO", "00", null, null, 168, 5, 6, 7, 8),
            new PhonePrefix("27", "South Africa", "ZA", "00", "0", null, 217, 9),
            new PhonePrefix("211", "South Sudan", "SS", "00", "0", null, 157),
            new PhonePrefix("34", "Spain", "ES", "00", null, null, 212, 9),
            new PhonePrefix("94", "Sri Lanka", "LK", "00", "0", null, 183, 9),
            new PhonePrefix("249", "Sudan", "SD", "00", "0", null, 208, 9),
            new PhonePrefix("597", "Suriname", "SR", "00", "0", null, 74, 6, 7),
            new PhonePrefix("268", "Swaziland", "SZ", "00", null, null, 83, 7, 8),
            new PhonePrefix("46", "Sweden", "SE", "00", "0", null, 150, 7, 8, 9, 10, 11, 12, 13),
            new PhonePrefix("41", "Switzerland", "CH", "00", "0", null, 140, 4, 5, 6, 7, 8, 9, 10, 11, 12),
            new PhonePrefix("963", "Syrian Arab Republic", "SY", "00", "0", null, 173, 8, 9, 10),
            new PhonePrefix("886", "Taiwan", "TW", "002", "0", null, 184, 8, 9),
            new PhonePrefix("992", "Tajikistan", "TJ", "810", "8", null, 146, 9),
            new PhonePrefix("255", "Tanzania", "TZ", "000", "0", null, 218, 9),
            new PhonePrefix("888", "Telecommunications for Disaster Relief (TDR)", null, null, null, null, 10),
            new PhonePrefix("66", "Thailand", "TH", "001", "0", null, 224, 8, 9),
            new PhonePrefix("389", "North Macedonia", "MK", "00", "0", null, 94, 8),
            new PhonePrefix("670", "Timor-Leste", "TL", "00", null, null, 86, 7),
            new PhonePrefix("228", "Togo", "TG", "00", null, null, 139, 8),
            new PhonePrefix("690", "Tokelau", "TK", "00", null, null, 17, 4),
            new PhonePrefix("676", "Tonga", "TO", "00", null, null, 50, 5, 7),
            new PhonePrefix("991", "Trial of a proposed new international service", null, null, null, null, 11),
            new PhonePrefix("1", "Trinidad and Tobago", "TT", "011", "1", "868", 88, 7),
            new PhonePrefix("216", "Tunisia", "TN", "00", null, null, 162, 8),
            new PhonePrefix("90", "Turkey", "TR", "00", "0", null, 227, 10),
            new PhonePrefix("993", "Turkmenistan", "TM", "810", "8", null, 128, 8),
            new PhonePrefix("1", "Turks and Caicos Islands", "TC", "0", "1", "649", 36, 7),
            new PhonePrefix("688", "Tuvalu", "TV", "00", null, null, 28, 5, 6),
            new PhonePrefix("256", "Uganda", "UG", "000", "0", null, 211, 9),
            new PhonePrefix("380", "Ukraine", "UA", "00", "0", null, 207, 9),
            new PhonePrefix("971", "United Arab Emirates", "AE", "00", "0", null, 148, 8, 9),
            new PhonePrefix("44", "United Kingdom", "GB", "00", "0", null, 223, 7, 8, 9, 10),
            new PhonePrefix("1", "United States", "US", "011", "1", null, 241, 10),
            new PhonePrefix("1", "United States Virgin Islands", "VI", "011", "1", "340", 49, 7),
            new PhonePrefix("878", "Universal Personal Telecommunication (UPT)", null, null, null, null, 12),
            new PhonePrefix("598", "Uruguay", "UY", "00", "0", null, 108, 4, 5, 6, 7, 8, 9, 10, 11),
            new PhonePrefix("998", "Uzbekistan", "UZ", "810", "8", null, 200, 9),
            new PhonePrefix("678", "Vanuatu", "VU", "00", null, null, 64, 5, 7),
            new PhonePrefix("379", "Vatican", "VA", null, null, null, 15),
            new PhonePrefix("39", "Vatican", "VA", "00", null, null, 14, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11),
            new PhonePrefix("58", "Venezuela", "VE", "00", "0", null, 192, 10),
            new PhonePrefix("84", "Vietnam", "VN", "00", "0", null, 229, 7, 8, 9, 10),
            new PhonePrefix("681", "Wallis and Futuna", "WF", "00", null, null, 27, 6),
            new PhonePrefix("967", "Yemen", "YE", "00", "0", null, 194, 6, 7, 8, 9),
            new PhonePrefix("260", "Zambia", "ZM", "00", "0", null, 176, 9),
            new PhonePrefix("263", "Zimbabwe", "ZW", "00", "0", null, 167, 5, 6, 7, 8, 9, 10),
            new PhonePrefix("970", "Reserved", null, null, null, null, 8),
        };

    }


}

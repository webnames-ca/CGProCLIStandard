using System;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Data;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Sockets;

namespace WNStandard
{
    /// <summary>
    /// Webnames common utility functions and extension methods.
    /// </summary>
    public static class Common
    {
        /// <summary>
        /// Serializes the specified object to a String using Newtonsoft.Json.JsonConvert (indented style.)
        /// </summary>
        public static string NewtonsoftJsonSerialize(this object oThis, bool bNoIndenting = false, bool bEscapeHTML = false, bool bThrowExceptionOnReferenceLoops = false, int? iMaxDepth = null, bool bExcludeEFCoreLazyLoadRelationCastleProxies = false, bool bExcludeFromCache = false)
        {
            return JsonConvert.SerializeObject(oThis,
                                               bNoIndenting ? Formatting.None : Formatting.Indented,
                                               new JsonSerializerSettings()
                                               {
                                                   StringEscapeHandling = bEscapeHTML ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
                                                   ReferenceLoopHandling = bThrowExceptionOnReferenceLoops ? ReferenceLoopHandling.Error : ReferenceLoopHandling.Ignore,
                                                   MaxDepth = iMaxDepth,
                                                   ContractResolver = new ExcludeEFCoreLazyLoadRelationCastleProxiesAndFromCache_ContractResolver(bExcludeEFCoreLazyLoadRelationCastleProxies, bExcludeFromCache),

                                               }); ;
        }

        /// <summary>
        /// Serializes the specified object to a String using Newtonsoft.Json.JsonConvert (non-indented style, with HTML escaped for safety in passing to client code, and excluding relations via EFCore lazy load and FromCache property suffixes.)
        /// </summary>
        public static string ToJsonSafeAndUgly(this object oThis)
        {
            return NewtonsoftJsonSerialize(oThis, bNoIndenting: true, bEscapeHTML: true, bExcludeEFCoreLazyLoadRelationCastleProxies: true, bExcludeFromCache: true);
        }

        /// <summary>
        /// Override of NewtonsoftJsonSerialize() that ignores FromCache properties, EFCoreLazyLoadRelationCastleProxies, and has a max depth of 1
        /// </summary>
        public static string JsonSerializeForLogging(this object oThis)
        {
            return oThis.NewtonsoftJsonSerialize(iMaxDepth: 1, bExcludeEFCoreLazyLoadRelationCastleProxies: true, bExcludeFromCache: true);
        }

        public class ExcludeEFCoreLazyLoadRelationCastleProxiesAndFromCache_ContractResolver : DefaultContractResolver
        {
            bool bExcludeEFCoreLazyLoadRelationCastleProxies;
            bool bExcludeFromCache;

            public ExcludeEFCoreLazyLoadRelationCastleProxiesAndFromCache_ContractResolver(bool bExcludeEFCoreLazyLoadRelationCastleProxies = false, bool bExcludeFromCache = false)
            {
                this.bExcludeEFCoreLazyLoadRelationCastleProxies = bExcludeEFCoreLazyLoadRelationCastleProxies;
                this.bExcludeFromCache = bExcludeFromCache;
            }

            protected override IList<JsonProperty> CreateProperties(Type oType, MemberSerialization oMemberSerialization)
            {
                var ljpProps = base.CreateProperties(oType, oMemberSerialization);
                if (bExcludeEFCoreLazyLoadRelationCastleProxies)
                {
                    ljpProps = ljpProps.Where(p => !((p?.DeclaringType?.FullName)?.StartsWith("Castle.Proxies.")).GetValueOrDefault()).ToList();
                }
                if (bExcludeFromCache)
                {
                    ljpProps = ljpProps.Where(p => !((p?.PropertyName)?.EndsWith("FromCache")).GetValueOrDefault()).ToList();
                }
                return ljpProps;
            }
        }

        /// <summary>
        /// Determines whether this string is equal to another string, using the ordinal case insensitivity.
        /// </summary>
        public static bool EqualsIgnoreCase(this string sThis, string s)
        {
            return string.Equals(sThis, s, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether this string is equal to another string, using the ordinal case insensitivity.
        /// Null strings are considered to be empty, and non-null strings are trimmed.
        /// </summary>
        public static bool EqualsIgnoreCaseTrimEmpty(this string sThis, string s)
        {
            return string.Equals(sThis.EmptyIfNothing().Trim(), s.EmptyIfNothing().Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns an empty string (not a Null/Nothing string) if the specified string is Null/Nothing.
        /// </summary>
        public static string EmptyIfNothing(this string sThis)
        {
            return sThis ?? "";
        }

        /// <summary>
        /// Returns a Nothing if the String is empty
        /// </summary>
        public static string NothingIfEmpty(this string sThis)
        {
            return sThis == "" ? null : sThis;
        }

        /// <summary>
        /// Returns a default string if this string is null/Nothing or equal to String.Empty; otherwise returns this string.
        /// </summary>
        public static string IfEmptyOrNothing(this string sThis, string sDefault)
        {
            return string.IsNullOrEmpty(sThis) ? sDefault : sThis;
        }

        /// <summary>
        /// Returns a default string if this string is null/Nothing, empty, or whitespace.
        /// Otherwise returns the string trimmed.
        /// </summary>
        public static string TrimAndDefaultEmpty(this string sThis, string sDefault = "")
        {
            sThis = sThis.EmptyIfNothing().Trim();
            return sThis != "" ? sThis : sDefault;
        }

        /// <summary>
        /// Returns a default string if this string is null/Nothing, empty, or whitespace.
        /// Otherwise returns the string trimmed and lowercase.
        /// </summary>
        public static string TrimAndLowerDefaultEmpty(this string sThis, string sDefault = "")
        {
            sThis = sThis.EmptyIfNothing().Trim().ToLowerInvariant();
            return sThis != "" ? sThis : sDefault;
        }

        /// <summary>
        /// Returns a default string if this string is null/Nothing, empty, or whitespace.
        /// Otherwise returns the string trimmed and lowercase.
        /// </summary>
        public static string TrimAndLower(this string sThis)
        {
            return sThis.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Returns a nullable Integer that is null if the specified integer is less than or equal to zero. Otherwise returns a nullable integer with the integer's positive value.
        /// </summary>
        public static Int32? NullIntIfNotPositive(this int iThis)
        {
            return iThis > 0 ? new Int32?(iThis) : new Int32?();
        }

        /// <summary>
        /// Returns a string containing the specified number of characters from the left side of this string.
        /// If this string is empty or null/Nothing, or a negative number is requested, an empty string is returned.
        /// If the number of characters requested is greater than or equal to the number of characters in the string, 
        /// the entire string is returned.
        /// </summary>
        /// <param name="sThis">This string.</param>
        /// <param name="iNumberOfCharacters">Number of left-most characters to return.</param>
        public static string Left(this string sThis, int iNumberOfCharacters)
        {
            if (sThis == null || iNumberOfCharacters < 0)
                return "";
            else if (iNumberOfCharacters >= sThis.Length)
                return sThis;
            else
                return sThis.Substring(0, iNumberOfCharacters);
        }

        /// <summary>
        /// Returns a string containing the specified number of characters from the right side of this string.
        /// If this string is empty or null/Nothing, or a negative number is requested, an empty string is returned.
        /// If the number of characters requested is greater than or equal to the number of characters in the string, 
        /// the entire string is returned.
        /// </summary>
        /// <param name="sThis">This string.</param>
        /// <param name="iNumberOfCharacters">Number of right-most characters to return.</param>
        public static string Right(this string sThis, int iNumberOfCharacters)
        {
            if (sThis == null || iNumberOfCharacters < 0)
                return "";
            else if (iNumberOfCharacters >= sThis.Length)
                return sThis;
            else
                return sThis.Substring(sThis.Length - iNumberOfCharacters);
        }

        /// <summary>
        /// Trims the specified string from the end of the this string, if present.
        /// </summary>
        /// <param name="sThis">This string.</param>
        /// <param name="sEndString">String to trim from the end of this string.</param>
        /// <param name="bSingleOnly">If set to False (the default), multiple adjacent copies of the string found at the end are trimmed off.
        /// If set to True, only a single copy of the string is trimmed from the end.</param>
        public static string TrimEndString(this string sThis, string sEndString, bool bSingleOnly = false)
        {
            if (string.IsNullOrEmpty(sThis) || string.IsNullOrEmpty(sEndString))
                return sThis;
            else if (!sThis.EndsWith(sEndString))
                return sThis;
            else
            {
                sThis = sThis.Substring(0, sThis.Length - sEndString.Length);
                if (!bSingleOnly)
                {
                    while (sThis.EndsWith(sEndString))
                        sThis = sThis.Substring(0, sThis.Length - sEndString.Length);
                }
                return sThis;
            }
        }

        /// <summary>
        /// Trims the specified string from the start of the this string, if present.
        /// </summary>
        /// <param name="sThis">This string.</param>
        /// <param name="sStartString">String to trim from the start of this string.</param>
        /// <param name="bSingleOnly">If set to False (the default), multiple adjacent copies of the string found at the start are trimmed off.
        /// If set to True, only a single copy of the string is trimmed from the start.</param>
        public static string TrimStartString(this string sThis, string sStartString, bool bSingleOnly = false)
        {
            if (string.IsNullOrEmpty(sThis) || string.IsNullOrEmpty(sStartString))
                return sThis;
            else if (!sThis.StartsWith(sStartString))
                return sThis;
            else
            {
                sThis = sThis.Substring(sStartString.Length);
                if (!bSingleOnly)
                {
                    while (sThis.StartsWith(sStartString))
                        sThis = sThis.Substring(sStartString.Length);
                }
                return sThis;
            }
        }

        /// <summary>
        /// Returns this string truncated to the specified maximum length, including a trailing ellipsis.
        /// If the string is less than or equal to the maximum length, it is returned unchanged.
        /// E.g. "abcedfg".TruncateWithEllipsisIfNecessary(6) => "abc..."
        /// </summary>
        /// <param name="iMaxLength">Maximum length of the string.</param>
        public static string TruncateWithEllipsisIfNecessary(this string sThis, Int32 iMaxLength)
        {
            if (iMaxLength <= 0)
                throw new ArgumentException("iMaxLength must be greater than 0.", "iMaxLength");

            if (sThis == null)
                throw new ArgumentNullException("sThis");

            var sEllipsis = "...";

            if (sThis.Length <= iMaxLength)
                return sThis;
            else if (iMaxLength <= sEllipsis.Length)
                return sEllipsis.Substring(0, iMaxLength);
            else
                return sThis.Substring(0, iMaxLength - sEllipsis.Length) + sEllipsis;
        }

        /// <summary>
        /// Returns the boolean value of this string. The value is True if the trimmed string is
        /// '1', 'true', 'y', or 'yes' (case-insensitively); otherwise it is False.
        /// </summary>
        public static bool IsTrueValue(this string sThis)
        {
            switch (sThis.EmptyIfNothing().Trim().ToUpperInvariant())
            {
                case "1":
                case "Y":
                case "YES":
                case "TRUE":
                    {
                        return true;
                    }

                default:
                    {
                        return false;
                    }
            }
        }

        /// <summary>
        /// Return the decimal value rounded to the nearest hundredth, using symmetric arithmetic rounding (MidpointRounding.AwayFromZero).
        /// </summary>
        public static decimal RoundMoney(this decimal dThis)
        {
            return Math.Round(dThis, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Return the decimal value rounded to the lowest hundredth value (penny) toward zero.
        /// E.g. $6.4399 -> $6.43
        /// </summary>
        public static decimal RoundMoneyDown(this decimal dThis)
        {
            return Math.Truncate(dThis * 100) / 100m;
        }

        /// <summary>
        /// Return the double-precision floating point value rounded to the nearest whole integer, symmetric arithmetic rounding (MidpointRounding.AwayFromZero).
        /// </summary>
        public static Int32 RoundToInt(this double dThis)
        {
            return Convert.ToInt32(Math.Round(dThis, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Truncate (toward zero) the fractional pennies from a decimal value.
        /// E.g. (42.0566D).TruncateMoney() => 42.05
        ///      (-42.0566D).TruncateMoney() => -42.05
        /// </summary>
        public static decimal TruncateMoney(this decimal dThis)
        {
            return Math.Truncate(dThis * 100) / 100m;
        }

        /// <summary>
        /// Returns the fractional pennies from a decimal value.
        /// E.g. (42.0566D).FractionalPennies() => 0.0066
        ///      (-42.0566D).FractionalPennies() => -0.0066
        /// </summary>
        public static decimal FractionalPennies(this decimal dThis)
        {
            return decimal.Remainder(dThis, 0.01m);
        }

        public static int? NullIfZero(this int? iThis)
        {
            if (iThis.GetValueOrDefault() == 0)
                return null;
            else
                return iThis;
        }

        /// <summary>
        /// Returns a single comma-separated string representing the enumeration of strings
        /// (or an empty string for an empty enumeration.)
        /// NOTE: This function is not appropriate for use with dynamic SQL; instead, please use 
        /// WebnamesDataAccess.DataAccess.SQLServer.InClauseList() as it supports proper escaping 
        /// and wraps the output in parentheses.                
        /// </summary>
        public static string ToListByComma(this IEnumerable<string> iesThis)
        {
            var sb = new StringBuilder();
            foreach (var s in iesThis)
            {
                sb.Append(s);
                sb.Append(", ");
            }
            if (sb.Length > 0)
                sb.Length -= 2;// trim off trailing comma-space
            return sb.ToString();
        }

        /// <summary>
        /// Create a concatenated string of results with the desired separator (default separator is ", ").
        /// Optionally specify a different second-last separator, e.g. to create a "apples, oranges, and bananas" type list.
        /// Pass bTrim = False to add a separator after the final list item (the default is to trim that off.)
        /// </summary>
        public static string ToListBySeparator<T>(this IEnumerable<T> iesThis, string sSeparator = ", ", bool bTrim = true, string sLastSeparator = null)
        {
            var sb = new StringBuilder();

            var iCount = iesThis.Count();
            var i = 0;

            foreach (var s in iesThis)
            {
                sb.Append(s);
                i += 1;
                if (i == iCount - 1 && sLastSeparator != null)
                    sb.Append(sLastSeparator);
                else if (i < iCount || !bTrim)
                    sb.Append(sSeparator);
            }

            return sb.ToString();
        }

        public static string UrlEncode(this string s)
        {
            return System.Net.WebUtility.UrlEncode(s.EmptyIfNothing());
        }

        public static string HtmlEncode(this string s)
        {
            return System.Net.WebUtility.HtmlEncode(s.EmptyIfNothing());
        }
        public static string UrlDecode(this string s)
        {
            return System.Net.WebUtility.UrlDecode(s.EmptyIfNothing());
        }

        public static string HtmlDecode(this string s)
        {
            return System.Net.WebUtility.HtmlDecode(s.EmptyIfNothing());
        }

        /// <summary>
        /// Gets the specified value by key from the specified dictionary, or returns a verbose exception with a
        /// string representation of the missing key.
        /// </summary>
        public static TValue GetValueOrVerboseException<TKey, TValue>(this IDictionary<TKey, TValue> dicThis, TKey oKey)
        {
            TValue oValue = default(TValue);
            if (!dicThis.TryGetValue(oKey, out oValue))
                throw new KeyNotFoundException("Key not found: " + Convert.ToString(oKey));

            return oValue;
        }

        /// <summary>
        /// Gets the specified value by key from the specified dictionary, or returns a verbose exception with a
        /// string representation of the missing key.
        /// </summary>
        public static TValue GetValOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dicThis, TKey oKey, TValue oDefault = default(TValue))
        {
            return dicThis.TryGetValue(oKey, out var oValue) ? oValue : oDefault;
        }

        /// <summary>
        /// Returns the inner-most exception recursively via the InnerException property. (If InnerException is null/Nothing,
        /// the same exception is returned.)
        /// </summary>
        public static Exception GetInnermostException(this Exception exThis)
        {
            if (exThis.InnerException == null)
                return exThis;
            else
                return exThis.InnerException.GetInnermostException();
        }

        /// <summary>
        /// Returns the first element of a sequence.
        /// If the sequence is empty, an InvalidOperationException is thrown with the specified message.
        /// </summary>
        public static T FirstOrException<T>(this IEnumerable<T> ieThis, string sMessage)
        {
            var aFirst = ieThis.Take(1).ToArray();
            if (aFirst.Length <= 0)
                throw new InvalidOperationException(sMessage);
            else
                return aFirst[0];
        }

        /// <summary>
        /// Returns the first element of a queryable (e.g. LINQ to SQL) sequence.
        /// If the sequence is empty, an InvalidOperationException is thrown with the specified message.
        ///  </summary>
        public static T FirstOrException<T>(this IQueryable<T> iqThis, string sMessage)
        {
            var aFirst = iqThis.Take(1).ToArray();
            if (aFirst.Length <= 0)
                throw new InvalidOperationException(sMessage);
            else
                return aFirst[0];
        }

        /// <summary>
        /// Returns the IPv4 or IPv6 address in standard notation. Any transitional suffix (i.e. an IPv4-like address
        /// displayed in place of the final two segments of an IPv6 address) returned by .NET is converted to standard colon notation.
        /// See http://stackoverflow.com/questions/4863352/what-dictates-the-formatting-of-ipv6-addresses-by-system-net-ipaddress-tostring.
        /// </summary>
        public static string ToStringNonTransitional(this System.Net.IPAddress oIPAddress)
        {
            var sIP = oIPAddress.ToString();

            if (oIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return sIP;// Return IPv4 addresses untouched.

            if (oIPAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                throw new Exception(string.Format("Can't handle '{0}' in '{1}' format. (Only IPv4 or IPv6 supported.)", sIP, oIPAddress.AddressFamily.ToString()));

            if (!sIP.Contains("."))
                return sIP;

            try
            {
                var iTransitionalStart = sIP.LastIndexOf(":") + 1;
                var sTransitionalPart = sIP.Substring(iTransitionalStart);
                sIP = sIP.Substring(0, iTransitionalStart);
                var asOctects = sTransitionalPart.Split('.');
                sIP += string.Format("{0:x2}{1:x2}", Convert.ToInt16(asOctects[0]), Convert.ToInt16(asOctects[1])).TrimStart('0');
                sIP += ":" + string.Format("{0:x2}{1:x2}", Convert.ToInt16(asOctects[2]), Convert.ToInt16(asOctects[3])).TrimStart('0');

                return sIP;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to convert IPv6 address to standard notation: " + sIP, ex);
            }
        }

        /// <summary>
        /// Returns true if the IP address is local loopback or private.
        /// https://stackoverflow.com/questions/8113546/how-to-determine-whether-an-ip-address-in-private:
        /// ::1          -   IPv6  loopback
        /// 10.0.0.0     -   10.255.255.255  (10/8 prefix)
        /// 127.0.0.0    -   127.255.255.255  (127/8 prefix)
        /// 172.16.0.0   -   172.31.255.255  (172.16/12 prefix)
        /// 192.168.0.0  -   192.168.255.255 (192.168/16 prefix)
        /// </summary>
        public static bool IsPrivateOrLocal(this System.Net.IPAddress oIPAddress)
        {
            if (oIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return oIPAddress.ToStringNonTransitional() == "::1";

            var abIP = oIPAddress.GetAddressBytes();

            return abIP[0] == 10 || abIP[0] == 127 || (abIP[0] == 172 && abIP[1] >= 16 && abIP[1] < 32) || (abIP[0] == 192 && abIP[1] == 168);
        }

        public static Int32 ToInt32HostOrder(this IPAddress oIPAddress)
        {
            if (oIPAddress?.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new Exception($"Null or non-IPv4 address: {oIPAddress.NewtonsoftJsonSerialize()}");
            }

            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(oIPAddress.GetAddressBytes(), 0));
        }

        /// <summary>
        /// Returns an unsigned integer version of the specified IPv4 address.
        /// E.g. A.B.C.D => (A * 256^3) + (B * 256^2) + (C * 256) + D
        /// </summary>
        public static uint ToUint(this IPAddress oIPAddress)
        {
            if ((oIPAddress?.AddressFamily == AddressFamily.InterNetwork) || (oIPAddress?.IsIPv4MappedToIPv6).GetValueOrDefault())
            {
                var abBytes = oIPAddress.GetAddressBytes();
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(abBytes);

                return BitConverter.ToUInt32(abBytes, 0);
            }

            throw new ArgumentOutOfRangeException("address", "Address must be IPv4 or IPv4 mapped to IPv6: " + oIPAddress?.ToString());
        }

        /// <summary>
        /// Gets the value specified by the cache key, or caches it and returns it based on the provided value constructor function,
        /// in an atomic/thread-safe manner, with an absolute expiration after the specified number of minutes and a NotRemovable (highest) priority.
        /// </summary>
        public static T GetOrAdd<T>(this MemoryCache oMemoryCache, string sCacheKey, int iMinutesToCache, Func<T> fValue)
        {
            var oNewValue = new Lazy<T>(fValue); // Only initialized when accessed.
            var oOldValue = oMemoryCache.AddOrGetExisting(sCacheKey, oNewValue, new CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(iMinutesToCache), Priority = CacheItemPriority.NotRemovable }) as Lazy<T>; // Will return null if the item is newly added to the cache.

            try
            {
                return (oOldValue ?? oNewValue).Value;
            }
            catch
            {
                oMemoryCache.Remove(sCacheKey);
                throw;
            }
        }

        public static IEnumerable<T> ContainsList<T, L>(this IEnumerable<T> table, IEnumerable<L> list, Func<T, L> key)
        {
            return table.Join(list, key, ci => ci, (c, ci) => c);
        }
    }
}


/*
MIT LICENSE

Copyright (c) 2024 Webnames.ca Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the “Software”), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
THE SOFTWARE.

 Author: Jordan Rieger
         Software Development Manager - Webnames.ca Inc.
         jordan@webnames.ca
         www.webnames.ca
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Antlr4.Runtime.Tree;
using CGProCLI;

namespace CGProCLI
{
    public static class CLIUtils
    {
        public const string s_REGEX_ATOM_CHARs = "[A-Za-z0-9@_-]";

        /// <summary>
        /// Encodes the specified string according to https://support.mailspec.com/en/guides/communigate-pro-manual/atomic-objects.
        /// Strings are quoted only if necessary (i.e. containing certain characters.)
        /// </summary>
        public static string EncodeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            var sbResult = new StringBuilder(s.Length);

            var bRequiresQuotes = false;
            var bLastCharWasCarriageReturn = false;

            foreach (char c in s)
            {
                var sChar = c.ToString();

                if (!Regex.IsMatch(sChar, s_REGEX_ATOM_CHARs))
                    bRequiresQuotes = true;

                sChar = sChar.Replace("\\", "\\\\");
                sChar = sChar.Replace("\"", "\\\"");
                sChar = sChar.Replace("\t", "\\t");

                // CRLF two-character combination requires some state remembrance.
                if (sChar == "\r")
                {
                    sChar = "\\r";
                    bLastCharWasCarriageReturn = true;
                }
                else
                {
                    if (sChar == "\n" && bLastCharWasCarriageReturn)
                    {
                        sbResult.Length -= 2; // Remove last encoded carriage return (\r).
                        sChar = "\\e"; // Special line break code in CGPro.
                    }
                    else
                    {
                        sChar = sChar.Replace("\n", "\\n");
                    }

                    bLastCharWasCarriageReturn = false;
                }

                sbResult.Append(sChar);
            }

            if (bRequiresQuotes)
            {
                sbResult.Insert(0, "\"");
                sbResult.Append("\"");
            }

            return sbResult.ToString();
        }

        public static string DecodeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            var sbResult = new StringBuilder(s.Length);

            // Strip off quotes if present.
            if (s.StartsWith("\"") && s.EndsWith("\""))
                s = s.Substring(1, s.Length - 2);

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case '\\':
                            sbResult.Append('\\');
                            break;
                        case '"':
                            sbResult.Append('"');
                            break;
                        case 'r':
                            sbResult.Append('\r');
                            break;
                        case 'n':
                            sbResult.Append('\n');
                            break;
                        case 'e':
                            sbResult.Append("\r\n");
                            break;
                        case 't':
                            sbResult.Append('\t');
                            break;
                        case 'u':
                            // Handle Unicode escape sequence \u'FFFFFF' (3-6 hex characters.)
                            if (i + 1 < s.Length && s[i + 1] == '\'')
                            {
                                int iEndQuote = s.IndexOf('\'', i + 2);
                                if (iEndQuote != -1 && iEndQuote - (i + 2) >= 3 && iEndQuote - (i + 2) <= 6)
                                {
                                    string sHexCode = s.Substring(i + 2, iEndQuote - (i + 2));
                                    if (int.TryParse(sHexCode, System.Globalization.NumberStyles.HexNumber, null, out int iUnicodeVal))
                                    {
                                        sbResult.Append(char.ConvertFromUtf32(iUnicodeVal));
                                        i = iEndQuote;
                                    }
                                }
                            }
                            break;
                        default:
                            // Handle ASCII decimal escape sequence \000 (3 decimal characters.)
                            if (char.IsDigit(s[i]) && i + 2 < s.Length && char.IsDigit(s[i + 1]) && char.IsDigit(s[i + 2]))
                            {
                                sbResult.Append((char)Int32.Parse(s.Substring(i, 3)));
                                i += 2;
                            }
                            break;
                    }
                }
                else
                {
                    sbResult.Append(s[i]);
                }
            }

            return sbResult.ToString();
        }

        /// <summary>
        /// Encodes the specified string according to https://support.mailspec.com/en/guides/communigate-pro-manual/atomic-objects.
        /// Strings are quoted only if necessary (i.e. containing certain characters.)
        /// </summary>
        public static string CLIEncode(this string s)
        {
            return EncodeString(s);
        }

        /// <summary>
        /// Returns the domain name part of this email address, or null if not found.
        /// E.g. 'me@server.tld' => 'server.tld'
        /// </summary>
        public static string GetDomainFromEmail(this string sEmailAddress)
        {
            return (sEmailAddress?.Contains("@")).GetValueOrDefault() ? sEmailAddress.Substring(0, sEmailAddress.IndexOf("@")).ToLower() : null;
        }

        /// <summary>
        /// Return the numeric CGPro CLI result code repsented by the string at this node, or -1 if the node is not an integer.
        /// </summary>
        public static int GetResultCode(this IParseTree oParseTree)
        {
            return Int32.TryParse(oParseTree?.GetChildTerminalNodeText(0), out var iResult) ? iResult : -1;
        }

        /// <summary>
        /// Get the text of the specified child node, recursively finding its terminal, or null if the child is not found.
        /// </summary>
        public static string GetChildTerminalNodeText(this IParseTree oParseTree, int iChildIndex)
        {
            return oParseTree.GetChildTerminalNode(iChildIndex)?.GetText();
        }

        /// <summary>
        /// Get the specified child node's terminal, or null if not found.
        /// </summary>
        public static TerminalNodeImpl GetChildTerminalNode(this IParseTree oParseTree, int iChildIndex)
        {
            if (oParseTree is TerminalNodeImpl)
                return (TerminalNodeImpl)oParseTree;

            if (oParseTree == null || oParseTree.ChildCount < iChildIndex + 1)
                return null;

            var oChild = oParseTree.GetChild(iChildIndex);

            if (oChild.Payload != null && oChild is TerminalNodeImpl)
                return (TerminalNodeImpl)oChild;

            return oChild.GetChildTerminalNode(iChildIndex);
        }

        /// <summary>
        /// Get the first sub-object or child at this node of the specified type, if possible, or null if there is none.
        /// </summary>
        public static T GetFirstSubObjectOrDefault<T>(this IParseTree oParseTree)
        {
            T o = oParseTree.GetSubObject<T>();

            if (o == null && oParseTree.ChildCount > 1)
            {
                for (int i = 1; i < oParseTree.ChildCount && o == null; ++i)
                {
                    o = oParseTree.GetChild(i).GetSubObject<T>();
                }
            }

            return o;
        }

        /// <summary>
        /// Get the sub-object at this node, if possible, one that is NOT the generic CliObjectContext, or null if there is none.
        /// </summary>
        public static object GetSubObject(this IParseTree oParseTree)
        {
            if (oParseTree == null)
                return null;

            if (oParseTree is CGProCLI.CGProCLIParser.CliObjectContext && oParseTree.ChildCount == 1)
                return oParseTree.GetChild(0).GetSubObject();
            else
                return oParseTree;
        }

        /// <summary>
        /// Get the sub-object at this node of the specified type, if possible, or null if there is none.
        /// </summary>
        public static T GetSubObject<T>(this IParseTree oParseTree)
        {
            var oTree = oParseTree.GetSubObject();
            if (oTree != null && oTree is T)
            {
                return (T)oTree;
            }
            else
            {
                return default(T);
            }
        }

        public static List<object> ParseArray(this CGProCLI.CGProCLIParser.CliArrayContext oCLIArray)
        {
            if (oCLIArray == null)
                return null;

            var oList = new List<object>();

            foreach (var oTree in oCLIArray.children)
            {
                if (oTree is TerminalNodeImpl || oTree is CGProCLI.CGProCLIParser.CliStringContext)
                {
                    var sText = oTree.GetText();

                    if (String.IsNullOrWhiteSpace(sText) || new string[] { "(", ")", "," }.Contains(sText))
                        continue;

                    oList.Add(DecodeString(sText));
                }
                else
                {
                    var oSubObject = oTree.GetSubObject();

                    if (oSubObject is CGProCLI.CGProCLIParser.CliDictionaryContext)
                        oList.Add(((CGProCLI.CGProCLIParser.CliDictionaryContext)oSubObject).ParseDictionary());
                    else if (oSubObject is CGProCLI.CGProCLIParser.CliArrayContext)
                        oList.Add(((CGProCLI.CGProCLIParser.CliArrayContext)oSubObject).ParseArray());
                    else if (oSubObject is CGProCLI.CGProCLIParser.CliStringContext)
                        oList.Add(DecodeString(oTree.GetText()));
                    else
                        oList.Add(oTree.GetText()); // Unknown format, just dump text.
                }
            }

            return oList;
        }

        public static Dictionary<string, object> ParseDictionary(this CGProCLI.CGProCLIParser.CliDictionaryContext oCLIDictionary)
        {
            if (oCLIDictionary == null)
                return null;

            var oDictionary = new Dictionary<string, object>();

            string sKey = null;
            object oValue = null;
            var bGotValue = false;

            foreach (var oTree in oCLIDictionary.children)
            {
                if (oTree is TerminalNodeImpl || oTree is CGProCLI.CGProCLIParser.CliStringContext)
                {
                    var sText = oTree.GetText();

                    if (String.IsNullOrWhiteSpace(sText) || new string[] { "{", "}", "=", ";" }.Contains(sText))
                        continue;

                    sText = DecodeString(sText);

                    if (sKey == null)
                    {
                        sKey = sText;
                    }
                    else
                    {
                        oValue = sText;
                        bGotValue = true;
                    }
                }
                else
                {
                    var oSubObject = oTree.GetSubObject();

                    if (oSubObject is CGProCLI.CGProCLIParser.CliDictionaryContext)
                        oValue = ((CGProCLI.CGProCLIParser.CliDictionaryContext)oSubObject).ParseDictionary();
                    else if (oSubObject is CGProCLI.CGProCLIParser.CliArrayContext)
                        oValue = ((CGProCLI.CGProCLIParser.CliArrayContext)oSubObject).ParseArray();
                    else if (oSubObject is CGProCLI.CGProCLIParser.CliStringContext)
                        oValue = DecodeString(oTree.GetText());
                    else
                        oValue = oTree.GetText(); // Unknown format, just dump text.

                    bGotValue = true;
                }

                if (sKey != null && bGotValue)
                {
                    oDictionary.Add(sKey, oValue);
                    sKey = null;
                    oValue = null;
                    bGotValue = false;
                }
            }

            return oDictionary;
        }

        public static string EncodeObject(this object o)
        {
            if (o is IDictionary)
            {
                return EncodeDictionary(o as IDictionary);
            }
            else if (o is IEnumerable && !(o is String))
            {
                return EncodeArray(o as IEnumerable);
            }
            else
            {
                return o?.ToString().CLIEncode();
            }
        }

        public static string EncodeDictionary(IDictionary oDictionary)
        {
            var sbResult = new StringBuilder("{");

            foreach (var sKey in oDictionary.Keys)
            {
                sbResult.Append($"{sKey.ToString().CLIEncode()}={oDictionary[sKey].EncodeObject()};");
            }

            return sbResult.Append("}").ToString();
        }

        public static string EncodeArray(IEnumerable oArray)
        {
            var sbResult = new StringBuilder("(");

            foreach (var oItem in oArray)
            {
                sbResult.Append($"{oItem.EncodeObject()},");
            }

            return sbResult.ToString().TrimEnd(',') + ")";
        }
    }
}

//===============================================================================
// RFC2047 (Encoded Word) Decoder
// https://github.com/grumpydev/RFC2047-Encoded-Word-Encoder-Decoder/blob/master/EncodedWord/RFC2047.cs
// http://tools.ietf.org/html/rfc2047
//===============================================================================
// Copyright © Steven Robbins.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================
namespace EncodedWord
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides support for decoding RFC2047 (Encoded Word) encoded text
    /// </summary>
    public static class RFC2047
    {
        /// <summary>
        /// Regex for parsing encoded word sections
        /// From http://tools.ietf.org/html/rfc2047#section-3
        /// encoded-word = "=?" charset "?" encoding "?" encoded-text "?="
        /// </summary>
        private static readonly Regex EncodedWordFormatRegEx = new Regex(@"=\?(?<charset>.*?)\?(?<encoding>[qQbB])\?(?<encodedtext>.*?)\?=", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Regex for removing CRLF SPACE separators from between encoded words
        /// </summary>
        private static readonly Regex EncodedWordSeparatorRegEx = new Regex(@"\?=\r\n =\?", RegexOptions.Compiled);

        /// <summary>
        /// Replacement string for removing CRLF SPACE separators
        /// </summary>
        private const string SeparatorReplacement = @"?==?";

        /// <summary>
        /// The maximum line length allowed
        /// </summary>
        private const int MaxLineLength = 75;

        /// <summary>
        /// Regex for "Q-Encoding" hex bytes from http://tools.ietf.org/html/rfc2047#section-4.2
        /// </summary>
        private static readonly Regex QEncodingHexCodeRegEx = new Regex(@"(=(?<hexcode>[0-9a-fA-F][0-9a-fA-F]))", RegexOptions.Compiled);

        /// <summary>
        /// Regex for replacing _ with space as declared in http://tools.ietf.org/html/rfc2047#section-4.2
        /// </summary>
        private static readonly Regex QEncodingSpaceRegEx = new Regex("_", RegexOptions.Compiled);

        /// <summary>
        /// Format for an encoded string
        /// </summary>
        private const string EncodedStringFormat = @"=?{0}?{1}?{2}?=";

        /// <summary>
        /// Special characters, as defined by RFC2047
        /// </summary>
        private static readonly char[] SpecialCharacters = { '(', ')', '<', '>', '@', ',', ';', ':', '<', '>', '/', '[', ']', '?', '.', '=', '\t' };

        /// <summary>
        /// Represents a content encoding type defined in RFC2047
        /// </summary>
        public enum ContentEncoding
        {
            /// <summary>
            /// Unknown / invalid encoding
            /// </summary>
            Unknown,

            /// <summary>
            /// "Q Encoding" (reduced character set) encoding
            /// http://tools.ietf.org/html/rfc2047#section-4.2
            /// </summary>
            QEncoding,

            /// <summary>
            /// Base 64 encoding
            /// http://tools.ietf.org/html/rfc2047#section-4.1
            /// </summary>
            Base64
        }

        /// <summary>
        /// Encode a string into RFC2047
        /// </summary>
        /// <param name="plainString">Plain string to encode</param>
        /// <param name="contentEncoding">Content encoding to use</param>
        /// <param name="characterSet">Character set used by plainString</param>
        /// <returns>Encoded string</returns>
        public static string Encode(string plainString, ContentEncoding contentEncoding = ContentEncoding.QEncoding, string characterSet = "iso-8859-1")
        {
            if (String.IsNullOrEmpty(plainString)) {
                return String.Empty;
            }

            if (contentEncoding == ContentEncoding.Unknown) {
                throw new ArgumentException("contentEncoding cannot be unknown for encoding.", "contentEncoding");
            }

            if (!IsSupportedCharacterSet(characterSet)) {
                throw new ArgumentException("characterSet is not supported", "characterSet");
            }

            var textEncoding = Encoding.GetEncoding(characterSet);

            var encoder = GetContentEncoder(contentEncoding);

            var encodedContent = encoder.Invoke(plainString, textEncoding);

            return BuildEncodedString(characterSet, contentEncoding, encodedContent);
        }

        /// <summary>
        /// Decode a string containing RFC2047 encoded sections
        /// </summary>
        /// <param name="encodedString">String contaning encoded sections</param>
        /// <returns>Decoded string</returns>
        public static string Decode(string encodedString)
        {
            // Remove separators
            var decodedString = EncodedWordSeparatorRegEx.Replace(encodedString, SeparatorReplacement);

            return EncodedWordFormatRegEx.Replace(
                decodedString,
                m => {
                    var contentEncoding = GetContentEncodingType(m.Groups["encoding"].Value);
                    if (contentEncoding == ContentEncoding.Unknown) {
                        // Regex should never match, but return anyway
                        return string.Empty;
                    }

                    var characterSet = m.Groups["charset"].Value;
                    if (!IsSupportedCharacterSet(characterSet)) {
                        // Fall back to iso-8859-1 if invalid/unsupported character set found
                        characterSet = @"iso-8859-1";
                    }

                    var textEncoding = Encoding.GetEncoding(characterSet);
                    var contentDecoder = GetContentDecoder(contentEncoding);
                    var encodedText = m.Groups["encodedtext"].Value;

                    return contentDecoder.Invoke(encodedText, textEncoding);
                });
        }

        /// <summary>
        /// Determines if a character set is supported
        /// </summary>
        /// <param name="characterSet">Character set name</param>
        /// <returns>Bool representing whether the character set is supported</returns>
        private static bool IsSupportedCharacterSet(string characterSet)
        {
            return Encoding.GetEncodings()
                           .Where(e => String.Equals(e.Name, characterSet, StringComparison.InvariantCultureIgnoreCase))
                           .Any();
        }

        /// <summary>
        /// Gets the content encoding type from the encoding character
        /// </summary>
        /// <param name="contentEncodingCharacter">Content contentEncodingCharacter character</param>
        /// <returns>ContentEncoding type</returns>
        private static ContentEncoding GetContentEncodingType(string contentEncodingCharacter)
        {
            switch (contentEncodingCharacter) {
                case "Q":
                case "q":
                    return ContentEncoding.QEncoding;
                case "B":
                case "b":
                    return ContentEncoding.Base64;
                default:
                    return ContentEncoding.Unknown;
            }
        }

        /// <summary>
        /// Gets the content decoder delegate for the given content encoding type
        /// </summary>
        /// <param name="contentEncoding">Content encoding type</param>
        /// <returns>Decoding delegate</returns>
        private static Func<string, Encoding, string> GetContentDecoder(ContentEncoding contentEncoding)
        {
            switch (contentEncoding) {
                case ContentEncoding.Base64:
                    return DecodeBase64;
                case ContentEncoding.QEncoding:
                    return DecodeQEncoding;
                default:
                    // Will never get here, but return a "null" delegate anyway
                    return (s, e) => String.Empty;
            }
        }

        /// <summary>
        /// Gets the content encoder delegate for the given content encoding type
        /// </summary>
        /// <param name="contentEncoding">Content encoding type</param>
        /// <returns>Encoding delegate</returns>
        private static Func<string, Encoding, string> GetContentEncoder(ContentEncoding contentEncoding)
        {
            switch (contentEncoding) {
                case ContentEncoding.Base64:
                    return EncodeBase64;
                case ContentEncoding.QEncoding:
                    return EncodeQEncoding;
                default:
                    // Will never get here, but return a "null" delegate anyway
                    return (s, e) => String.Empty;
            }
        }

        /// <summary>
        /// Decodes a base64 encoded string
        /// </summary>
        /// <param name="encodedText">Encoded text</param>
        /// <param name="textEncoder">Encoding instance for the code page required</param>
        /// <returns>Decoded string</returns>
        private static string DecodeBase64(string encodedText, Encoding textEncoder)
        {
            var encodedBytes = Convert.FromBase64String(encodedText);

            return textEncoder.GetString(encodedBytes);
        }

        /// <summary>
        /// Encodes a base64 encoded string
        /// </summary>
        /// <param name="plainText">Plain text</param>
        /// <param name="textEncoder">Encoding instance for the code page required</param>
        /// <returns>Encoded string</returns>
        private static string EncodeBase64(string plainText, Encoding textEncoder)
        {
            var plainTextBytes = textEncoder.GetBytes(plainText);

            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Decodes a "Q encoded" string
        /// </summary>
        /// <param name="encodedText">Encoded text</param>
        /// <param name="textEncoder">Encoding instance for the code page required</param>
        /// <returns>Decoded string</returns>
        private static string DecodeQEncoding(string encodedText, Encoding textEncoder)
        {
            var decodedText = QEncodingSpaceRegEx.Replace(encodedText, " ");

            decodedText = QEncodingHexCodeRegEx.Replace(
                decodedText,
                m => {
                    var hexString = m.Groups["hexcode"].Value;

                    int characterValue;
                    if (!int.TryParse(hexString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out characterValue)) {
                        return String.Empty;
                    }

                    return textEncoder.GetString(new[] { (byte)characterValue });
                });

            return decodedText;
        }

        /// <summary>
        /// Encodes a "Q encoded" string
        /// </summary>
        /// <param name="plainText">Plain text</param>
        /// <param name="textEncoder">Encoding instance for the code page required</param>
        /// <returns>Encoded string</returns>
        private static string EncodeQEncoding(string plainText, Encoding textEncoder)
        {
            if (textEncoder.GetByteCount(plainText) != plainText.Length) {
                throw new ArgumentException("Q encoding only supports single byte encodings", "textEncoder");
            }

            var specialBytes = textEncoder.GetBytes(SpecialCharacters);

            var sb = new StringBuilder(plainText.Length);

            var plainBytes = textEncoder.GetBytes(plainText);

            // Replace "high" values
            for (int i = 0; i < plainBytes.Length; i++) {
                if (plainBytes[i] <= 127 && !specialBytes.Contains(plainBytes[i])) {
                    sb.Append(Convert.ToChar(plainBytes[i]));
                }
                else {
                    sb.Append("=");
                    sb.Append(Convert.ToString(plainBytes[i], 16).ToUpper());
                }
            }

            return sb.ToString().Replace(" ", "_");
        }

        /// <summary>
        /// Builds the full encoded string representation
        /// </summary>
        /// <param name="characterSet">Characterset to use</param>
        /// <param name="contentEncoding">Content encoding to use</param>
        /// <param name="encodedContent">Content, encoded to the above parameters</param>
        /// <returns>Valid RFC2047 string</returns>
        private static string BuildEncodedString(string characterSet, ContentEncoding contentEncoding, string encodedContent)
        {
            var encodingCharacter = String.Empty;

            switch (contentEncoding) {
                case ContentEncoding.Base64:
                    encodingCharacter = "B";
                    break;
                case ContentEncoding.QEncoding:
                    encodingCharacter = "Q";
                    break;
            }

            var wrapperLength = string.Format(EncodedStringFormat, characterSet, encodingCharacter, String.Empty).Length;
            var chunkLength = MaxLineLength - wrapperLength;

            if (encodedContent.Length <= chunkLength) {
                return string.Format(EncodedStringFormat, characterSet, encodingCharacter, encodedContent);
            }

            var sb = new StringBuilder();
            foreach (var chunk in SplitStringByLength(encodedContent, chunkLength)) {
                sb.AppendFormat(EncodedStringFormat, characterSet, encodingCharacter, chunk);
                sb.Append("\r\n ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Splits a string into chunks
        /// </summary>
        /// <param name="inputString">Input string</param>
        /// <param name="chunkSize">Size of each chunk</param>
        /// <returns>String collection of chunked strings</returns>
        public static IEnumerable<string> SplitStringByLength(this string inputString, int chunkSize)
        {
            for (int index = 0; index < inputString.Length; index += chunkSize) {
                yield return inputString.Substring(index, Math.Min(chunkSize, inputString.Length - index));
            }
        }
    }
}
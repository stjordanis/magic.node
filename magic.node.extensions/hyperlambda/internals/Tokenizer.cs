﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace magic.node.extensions.hyperlambda.internals
{
    /*
     * Internal tokenizer class, for tokenizing a stream of characters into tokens
     * required for the Hyperlambda parser.
     */
    internal sealed class Tokenizer
    {
        // Underlaying streamn, from where tokenizing process is conducted.
        readonly StreamReader _reader;

        /*
         * Dictionary containing functors for handling characters fetched
         * from the stream reader during parsing from a Hyperlambda stream.
         *
         * This architecture allows you to easily expand upon the tokenizer process
         */
        static readonly Dictionary<char, Func<StreamReader, StringBuilder, string>> _characterFunctors = new Dictionary<char, Func<StreamReader, StringBuilder, string>>
        {
            {':', HandleColonToken},
            {'@', HandleAlphaToken},
            {'"', HandleDoubleQuoteToken},
            {'\'', HandleSingleQuoteToken},
            {'\r', HandleCRToken},
            {'\n', HandleLFToken},
            {'/', HandleSlashToken},
            {' ', HandleSPToken},
        };

        internal Tokenizer(StreamReader reader)
        {
            _reader = reader;
        }

        /*
         * Method responsible for actually retrieving tokens from stream.
         */
        internal IEnumerable<string> GetTokens()
        {
            var builder = new StringBuilder();
            while (!_reader.EndOfStream)
            {
                var current = (char)_reader.Peek();
                if (_characterFunctors.ContainsKey(current))
                {
                    var result = _characterFunctors[current](_reader, builder);
                    if (result != null)
                        yield return result;
                }
                else
                {
                    builder.Append(current);
                    _reader.Read();
                }
            }

            // Returning the last token, if any.
            if (builder.Length > 0)
                yield return builder.ToString();
        }

        #region [ -- Builtin tokenizer functors -- ]

        /*
         * Handles the ':' token, since it might be the separation of a node's value,
         * and its name.
         */
        static string HandleColonToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                reader.Read(); // Discarding ':'.
                return ":";
            }
            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        /*
         * Handles the '@' character, since it might imply a multiline string.
         */
        static string HandleAlphaToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                reader.Read(); // Discarding '@'.
                var next = (char)reader.Read();
                if (next == '"')
                {
                    var result = StringLiteralParser.ReadMultiLineString(reader);
                    builder.Clear();
                    return result;
                }
                builder.Append('@').Append(next);
            }
            else
            {
                builder.Append('@');
                reader.Read();
            }
            return null;
        }

        /*
         * Handles the '"' character, since it might imply a single line string.
         */
        static string HandleDoubleQuoteToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                var result = StringLiteralParser.ReadQuotedString(reader);
                builder.Clear();
                return result;
            }
            builder.Append('"');
            reader.Read();
            return null;
        }

        /*
         * Handles the '\'' character, since it might imply a multiline string.
         */
        static string HandleSingleQuoteToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                var result = StringLiteralParser.ReadQuotedString(reader);
                builder.Clear();
                return result;
            }
            builder.Append('\'');
            reader.Read();
            return null;
        }

        /*
         * Handles the '\r' character, assuming it's a part of a CR/LF sequence.
         */
        static string HandleCRToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                reader.Read(); // Discarding '\r'.
                if (reader.EndOfStream || (char)reader.Read() != '\n')
                    throw new ArgumentException("CR/LF error in Hyperlambda");
                return "\r\n";
            }
            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        /*
         * Handles the '\n' character, handling it as a CR/LF sequence.
         */
        static string HandleLFToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                reader.Read(); // Discarding '\n'.
                return "\r\n";
            }
            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        /*
         * Handles the '/' character, since it might be the beginning of a comment,
         * either multiline comment or single line comment.
         */
        static string HandleSlashToken(StreamReader reader, StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                reader.Read(); // Discarding current '/'.
                if (reader.Peek() == '/')
                {
                    while (!reader.EndOfStream && (char)reader.Peek() != '\n')
                        reader.Read();
                }
                else if (reader.Peek() == '*')
                {
                    // Eating until "*/".
                    var seenEndOfComment = false;
                    while (!reader.EndOfStream && !seenEndOfComment)
                    {
                        var idxComment = reader.Read();
                        if (idxComment == '*' && reader.Peek() == '/')
                        {
                            reader.Read();
                            seenEndOfComment = true;
                        }
                    }
                    if (!seenEndOfComment && reader.EndOfStream)
                        throw new ArgumentException("Syntax error in comment close to end of Hyperlambda");
                }
                else
                {
                    builder.Append('/'); // Only a part of the current token.
                }
            }
            else
            {
                reader.Read(); // Discarding '/' character.
                builder.Append('/');
            }
            return null;
        }

        /*
         * Handles the ' ' token (SP), since it's probably the beginning
         * of a 'scope declaration'.
         */
        static string HandleSPToken(StreamReader reader, StringBuilder builder)
        {
            reader.Read(); // Discarding current ' '.
            if (builder.Length > 0)
            {
                builder.Append(' ');
                return null;
            }
            builder.Append(' ');
            while (!reader.EndOfStream && (char)reader.Peek() == ' ')
            {
                reader.Read();
                builder.Append(' ');
            }
            if (!reader.EndOfStream && builder.Length % 3 != 0)
                throw new ArgumentException("Odd number of spaces found in Hyperlambda file");
            var result = builder.ToString();
            builder.Clear();
            return result;
        }

        #endregion
    }
}

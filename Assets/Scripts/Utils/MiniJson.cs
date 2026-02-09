using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Project.Utils
{
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        private sealed class Parser : IDisposable
        {
            private const string WordBreak = "{}[],:\"";
            private readonly StringReader json;

            private Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                {
                    return instance.ParseValue();
                }
            }

            public void Dispose()
            {
                json.Dispose();
            }

            private char PeekChar()
            {
                int peek = json.Peek();
                return peek == -1 ? '\0' : Convert.ToChar(peek);
            }

            private char NextChar()
            {
                int next = json.Read();
                return next == -1 ? '\0' : Convert.ToChar(next);
            }

            private string NextWord()
            {
                var sb = new StringBuilder();
                while (!IsWordBreak(PeekChar()))
                {
                    sb.Append(NextChar());
                }
                return sb.ToString();
            }

            private void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar()))
                {
                    json.Read();
                }
            }

            private object ParseValue()
            {
                EatWhitespace();
                char c = PeekChar();
                switch (c)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case '\0':
                        return null;
                    default:
                        return ParseWord();
                }
            }

            private IDictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                NextChar();
                while (true)
                {
                    EatWhitespace();
                    char c = PeekChar();
                    if (c == '\0')
                        return null;
                    if (c == '}')
                    {
                        NextChar();
                        return table;
                    }

                    string key = ParseString();
                    EatWhitespace();
                    NextChar();
                    object value = ParseValue();
                    table[key] = value;
                }
            }

            private IList ParseArray()
            {
                var array = new List<object>();
                NextChar();
                while (true)
                {
                    EatWhitespace();
                    char c = PeekChar();
                    if (c == '\0')
                        return null;
                    if (c == ']')
                    {
                        NextChar();
                        return array;
                    }

                    object value = ParseValue();
                    array.Add(value);
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                NextChar();
                while (true)
                {
                    char c = NextChar();
                    if (c == '\0')
                        return null;
                    if (c == '"')
                        return sb.ToString();
                    if (c == '\\')
                    {
                        c = NextChar();
                        switch (c)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                sb.Append(c);
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++)
                                    hex[i] = NextChar();
                                sb.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            private object ParseWord()
            {
                string word = NextWord();
                if (word == "true") return true;
                if (word == "false") return false;
                if (word == "null") return null;

                if (double.TryParse(word, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                    return num;

                return word;
            }

            private static bool IsWordBreak(char c)
            {
                return char.IsWhiteSpace(c) || WordBreak.IndexOf(c) != -1;
            }
        }
    }
}

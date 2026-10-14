using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SickOfIt.Json
{
    /// <summary>
    /// Parsing and formatting for config JSON text.
    ///
    /// Parsing is strict: duplicate keys and trailing content are errors, dates stay strings, and line
    /// numbers are kept for error messages. Comments are allowed but are not preserved when a file is rewritten.
    ///
    /// Formatting is deterministic, so the same data always produces the same bytes: two-space indent,
    /// "\n" line endings, and short arrays of plain values kept on one line ("position": [1.5, 0.0]).
    /// </summary>
    public static class JsonText
    {
        const int InlineArrayWidth = 100;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load,
        };

        /// <summary>Parses <paramref name="text"/>, or returns null after adding an error to <paramref name="issues"/>.</summary>
        public static JToken Parse(string text, string file, JsonIssueList issues)
        {
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text ?? string.Empty)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;

                    var token = JToken.ReadFrom(reader, LoadSettings);
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.Comment)
                        {
                            issues.Error(file, "", "Unexpected content after the end of the JSON value.", reader.LineNumber);
                            return null;
                        }
                    }
                    return token;
                }
            }
            catch (JsonReaderException e)
            {
                issues.Error(file, "", "Invalid JSON: " + StripLocation(e.Message), e.LineNumber);
                return null;
            }
        }

        public static int LineOf(JToken token)
        {
            return token is IJsonLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
        }

        public static string Format(JToken token)
        {
            var sb = new StringBuilder();
            Write(sb, token, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>Single-line JSON with no spaces, using the same number formatting as <see cref="Format"/>.</summary>
        public static string Compact(JToken token)
        {
            var sb = new StringBuilder();
            WriteCompact(sb, token);
            return sb.ToString();
        }

        static void WriteCompact(StringBuilder sb, JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var property in ((JObject)token).Properties())
                    {
                        if (!first)
                            sb.Append(',');
                        first = false;
                        sb.Append(JsonConvert.ToString(property.Name)).Append(':');
                        WriteCompact(sb, property.Value);
                    }
                    sb.Append('}');
                    return;
                }
                case JTokenType.Array:
                {
                    sb.Append('[');
                    var array = (JArray)token;
                    for (int i = 0; i < array.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(',');
                        WriteCompact(sb, array[i]);
                    }
                    sb.Append(']');
                    return;
                }
                default:
                    sb.Append(Scalar(token));
                    return;
            }
        }

        /// <summary>Shortest text for <paramref name="value"/> that parses back to the same double, always with a decimal point or exponent.</summary>
        public static string FormatDouble(double value)
        {
            string text = value.ToString("R", Inv);
            if (double.Parse(text, Inv) != value)
                text = value.ToString("G17", Inv);
            if (text.IndexOf('.') < 0 && text.IndexOf('E') < 0 && text.IndexOf('e') < 0)
                text += ".0";
            return text;
        }

        static void Write(StringBuilder sb, JToken token, int indent)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    var obj = (JObject)token;
                    if (obj.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }
                    sb.Append("{\n");
                    int index = 0;
                    foreach (var property in obj.Properties())
                    {
                        Indent(sb, indent + 1);
                        sb.Append(JsonConvert.ToString(property.Name)).Append(": ");
                        Write(sb, property.Value, indent + 1);
                        if (++index < obj.Count)
                            sb.Append(',');
                        sb.Append('\n');
                    }
                    Indent(sb, indent);
                    sb.Append('}');
                    return;
                }
                case JTokenType.Array:
                {
                    var array = (JArray)token;
                    if (array.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }
                    string inline = TryInline(array, indent);
                    if (inline != null)
                    {
                        sb.Append(inline);
                        return;
                    }
                    sb.Append("[\n");
                    for (int i = 0; i < array.Count; i++)
                    {
                        Indent(sb, indent + 1);
                        Write(sb, array[i], indent + 1);
                        if (i < array.Count - 1)
                            sb.Append(',');
                        sb.Append('\n');
                    }
                    Indent(sb, indent);
                    sb.Append(']');
                    return;
                }
                default:
                    sb.Append(Scalar(token));
                    return;
            }
        }

        static string TryInline(JArray array, int indent)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                if (item.Type == JTokenType.Object || item.Type == JTokenType.Array)
                    return null;
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Scalar(item));
            }
            sb.Append(']');
            return sb.Length + indent * 2 <= InlineArrayWidth ? sb.ToString() : null;
        }

        static string Scalar(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Null:
                    return "null";
                case JTokenType.Boolean:
                    return (bool)token ? "true" : "false";
                case JTokenType.Integer:
                    return Convert.ToString(((JValue)token).Value, Inv);
                case JTokenType.Float:
                    return FormatDouble((double)token);
                case JTokenType.String:
                    return JsonConvert.ToString((string)token);
                default:
                    throw new NotSupportedException($"Unexpected JSON token type {token.Type}.");
            }
        }

        static void Indent(StringBuilder sb, int indent)
        {
            sb.Append(' ', indent * 2);
        }

        static string StripLocation(string message)
        {
            int at = message.IndexOf(" Path '", StringComparison.Ordinal);
            if (at < 0)
                at = message.IndexOf(", line ", StringComparison.Ordinal);
            return at > 0 ? message.Substring(0, at) : message;
        }
    }
}

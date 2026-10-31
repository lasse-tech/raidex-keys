// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Formats.Cbor;
using System.IO.Compression;
using System.Text;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.Core.Mdt;

/// <summary>
/// A route shared as MDT's export string - from chat, Discord or a website -
/// read without the game. Our own reader of the two formats MDT imports; no
/// code of MDT or its libraries.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>"!~MDT2~" + Base64 of raw Deflate of CBOR (the game's C_EncodingUtil),
/// MDT's format since 2026.</item>
/// <item>"!" + LibDeflate's printable 6-bit encoding of raw Deflate of an
/// AceSerializer string, the older format still on websites.</item>
/// </list>
/// Both carry the preset table MDT stores: text, uid, value.currentDungeonIdx
/// and value.pulls.
/// </remarks>
public static class MdtString
{
    public const string Prefix = "!~MDT2~";

    // A route is a few kilobytes; more than this is not a route but a bomb.
    private const int MaxInflated = 4 * 1024 * 1024;
    private const int MaxDepth = 32;

    /// <summary>Does the text look like an export string at all?</summary>
    public static bool LooksLikeOne(string? text) => Clean(text).StartsWith('!');

    /// <summary>The route in an export string; throws <see cref="FormatException"/> saying why it is none.</summary>
    public static MdtRoute Decode(string text)
    {
        var clean = Clean(text);
        LuaTable preset;
        if (clean.StartsWith(Prefix, StringComparison.Ordinal))
        {
            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(Pad(clean[Prefix.Length..]));
            }
            catch (FormatException)
            {
                throw new FormatException("not Base64 after " + Prefix);
            }
            preset = FromCbor(Inflate(packed));
        }
        else if (clean.StartsWith('!'))
        {
            var packed = FromPrintable(clean[1..]) ?? throw new FormatException("not LibDeflate's printable encoding");
            preset = AceDeserializer.Read(Encoding.UTF8.GetString(Inflate(packed)));
        }
        else
        {
            throw new FormatException("an MDT string starts with \"!\"");
        }

        var dungeonIndex = preset.Table("value")?.Integer("currentDungeonIdx")
                           ?? throw new FormatException("no dungeon in the route");
        return MdtReader.ReadPreset(preset, (int)dungeonIndex, selected: false)
               ?? throw new FormatException("a route without enemies");
    }

    /// <summary>
    /// A preset as MDT's own export string: "!~MDT2~" + Base64 of raw Deflate
    /// of CBOR. The mirror of <see cref="Decode"/>.
    /// </summary>
    /// <remarks>
    /// Read off MDT 6.2.16's own output (route compared against the same
    /// preset in its saved variables, 2026-09-12): a table whose keys are
    /// exactly 1 to n is written as a CBOR array and every other as a map,
    /// and Lua strings go as CBOR byte strings, not text strings. Encoding
    /// the tree of a real export by these rules gives its bytes back exactly,
    /// so what this writes is what MDT writes. Deflate itself may pack them
    /// differently - what MDT reads is the CBOR underneath.
    /// </remarks>
    public static string Encode(LuaTable preset)
    {
        var writer = new CborWriter(CborConformanceMode.Lax);
        Write(writer, preset, 0);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(writer.Encode());
        }
        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    /// <summary>
    /// The preset of a route with what MDT adds when exporting: which MDT
    /// wrote it and who. A route that came from a string already carries
    /// them and keeps what it had.
    /// </summary>
    /// <param name="addonVersion">The installed MDT's version as its .toc gives it; left out when unknown.</param>
    public static string Export(LuaTable preset, int? addonVersion, string? name, string? realm, int? classId)
    {
        var copy = new LuaTable();
        foreach (var (key, value) in preset.Entries)
        {
            copy.Set(key, value);
        }
        if (addonVersion is { } version && copy["addonVersion"] is null)
        {
            copy.Set("addonVersion", (long)version);
        }
        if (name is not null && realm is not null && copy["createdBy"] is null)
        {
            var by = new LuaTable();
            by.Set("name", name);
            by.Set("realm", realm);
            if (classId is { } id)
            {
                by.Set("classIdx", (long)id);
            }
            copy.Set("createdBy", by);
        }
        return Encode(copy);
    }

    /// <summary>
    /// The preset as an export string without who exported it (#27, Lars
    /// 2026-09-30): what goes to Raidex never carries a character name.
    /// </summary>
    public static string WithoutAuthor(LuaTable preset, int? addonVersion)
    {
        var copy = new LuaTable();
        foreach (var (key, value) in preset.Entries)
        {
            if (!Equals(key, "createdBy"))
            {
                copy.Set(key, value);
            }
        }
        if (addonVersion is { } version && copy["addonVersion"] is null)
        {
            copy.Set("addonVersion", (long)version);
        }
        return Encode(copy);
    }

    /// <summary>
    /// A table whose keys are exactly 1 to n - MDT writes those as arrays, and
    /// an empty one with them: in Lua an empty table is a sequence of nothing,
    /// and MDT's own output has it as an empty array, not an empty map.
    /// </summary>
    private static bool IsSequence(LuaTable table)
    {
        var keys = table.Entries.Keys.OfType<long>().ToList();
        return keys.Count == table.Entries.Count
               && keys.Order().SequenceEqual(Enumerable.Range(1, keys.Count).Select(i => (long)i));
    }

    private static void Write(CborWriter writer, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new FormatException("nested too deep for a route");
        }
        switch (value)
        {
            case null:
                writer.WriteNull();
                break;
            case bool b:
                writer.WriteBoolean(b);
                break;
            // A whole number is written as one, whichever way it was read: MDT
            // writes no floats in a route, and a 10.0 would come back as 10.
            case double d when d == Math.Floor(d) && !double.IsInfinity(d):
                writer.WriteInt64((long)d);
                break;
            case double d:
                writer.WriteDouble(d);
                break;
            case long l:
                writer.WriteInt64(l);
                break;
            case string s:
                writer.WriteByteString(Encoding.UTF8.GetBytes(s));
                break;
            case LuaTable table when IsSequence(table):
                writer.WriteStartArray(table.Entries.Count);
                foreach (var item in table.Entries.OrderBy(e => (long)e.Key).Select(e => e.Value))
                {
                    Write(writer, item, depth + 1);
                }
                writer.WriteEndArray();
                break;
            case LuaTable table:
                writer.WriteStartMap(table.Entries.Count);
                foreach (var (key, item) in table.Entries)
                {
                    Write(writer, key, depth + 1);
                    Write(writer, item, depth + 1);
                }
                writer.WriteEndMap();
                break;
            default:
                throw new FormatException($"a route holds no {value.GetType().Name}");
        }
    }

    /// <summary>The string as pasted, without spaces, line breaks and the backticks of a Discord code block.</summary>
    public static string Clean(string? text) =>
        new((text ?? "").Where(c => !char.IsWhiteSpace(c) && c != '`').ToArray());

    private static string Pad(string base64) => base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

    private static byte[] Inflate(byte[] packed)
    {
        try
        {
            using var deflate = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaxInflated)
                {
                    throw new FormatException("too large for a route");
                }
            }
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new FormatException("not Deflate data");
        }
    }

    /// <summary>
    /// LibDeflate's printable encoding: a-z A-Z 0-9 ( ) for 0 to 63, four
    /// characters for three bytes, the lowest bits first.
    /// </summary>
    private static byte[]? FromPrintable(string text)
    {
        var bytes = new List<byte>(text.Length * 3 / 4);
        var cache = 0L;
        var bits = 0;
        foreach (var c in text)
        {
            int value = c switch
            {
                >= 'a' and <= 'z' => c - 'a',
                >= 'A' and <= 'Z' => c - 'A' + 26,
                >= '0' and <= '9' => c - '0' + 52,
                '(' => 62,
                ')' => 63,
                _ => -1,
            };
            if (value < 0)
            {
                return null;
            }
            cache |= (long)value << bits;
            bits += 6;
            while (bits >= 8)
            {
                bytes.Add((byte)(cache & 0xFF));
                cache >>= 8;
                bits -= 8;
            }
        }
        return bytes.ToArray();
    }

    /// <summary>CBOR as the game writes Lua tables: maps, arrays for sequences, strings as byte strings.</summary>
    private static LuaTable FromCbor(byte[] data)
    {
        try
        {
            var reader = new CborReader(data, CborConformanceMode.Lax);
            return CborValue(reader, 0) as LuaTable ?? throw new FormatException("the route is no table");
        }
        catch (CborContentException e)
        {
            throw new FormatException("broken CBOR: " + e.Message);
        }
        catch (InvalidOperationException e)
        {
            throw new FormatException("broken CBOR: " + e.Message);
        }
    }

    private static object? CborValue(CborReader reader, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new FormatException("nested too deep for a route");
        }
        switch (reader.PeekState())
        {
            case CborReaderState.StartMap:
                {
                    var table = new LuaTable();
                    reader.ReadStartMap();
                    while (reader.PeekState() != CborReaderState.EndMap)
                    {
                        var key = CborValue(reader, depth + 1);
                        var value = CborValue(reader, depth + 1);
                        if (key is not null)
                        {
                            table.Set(key, value);
                        }
                    }
                    reader.ReadEndMap();
                    return table;
                }
            case CborReaderState.StartArray:
                {
                    var table = new LuaTable();
                    reader.ReadStartArray();
                    long index = 1;
                    while (reader.PeekState() != CborReaderState.EndArray)
                    {
                        table.Set(index++, CborValue(reader, depth + 1));
                    }
                    reader.ReadEndArray();
                    return table;
                }
            case CborReaderState.ByteString:
                return Encoding.UTF8.GetString(reader.ReadByteString());
            case CborReaderState.TextString:
                return reader.ReadTextString();
            case CborReaderState.UnsignedInteger:
            case CborReaderState.NegativeInteger:
                return reader.ReadInt64();
            case CborReaderState.HalfPrecisionFloat:
            case CborReaderState.SinglePrecisionFloat:
            case CborReaderState.DoublePrecisionFloat:
                return reader.ReadDouble();
            case CborReaderState.Boolean:
                return reader.ReadBoolean();
            case CborReaderState.Null:
                reader.ReadNull();
                return null;
            case CborReaderState.Tag:
                reader.ReadTag();
                return CborValue(reader, depth);
            default:
                reader.SkipValue();
                return null;
        }
    }

    /// <summary>
    /// AceSerializer's text: "^1", then values as "^" + type + data, "^^" at
    /// the end. ^S string (with "~" escapes), ^N number, ^F mantissa ^f
    /// exponent, ^B/^b true/false, ^Z nil, ^T … ^t a table of key, value pairs.
    /// </summary>
    private sealed class AceDeserializer
    {
        private readonly List<(char Type, string Data)> _tokens = [];
        private int _next;

        private AceDeserializer(string text)
        {
            var start = text.IndexOf('^');
            while (start >= 0 && start + 1 < text.Length)
            {
                var end = text.IndexOf('^', start + 2);
                _tokens.Add((text[start + 1], text[(start + 2)..(end < 0 ? text.Length : end)]));
                start = end;
            }
        }

        public static LuaTable Read(string text)
        {
            var reader = new AceDeserializer(text);
            if (reader._tokens.Count == 0 || reader._tokens[0] != ('1', ""))
            {
                throw new FormatException("not AceSerializer text");
            }
            reader._next = 1;
            return reader.Value(0) as LuaTable ?? throw new FormatException("the route is no table");
        }

        private (char Type, string Data) Take() =>
            _next < _tokens.Count ? _tokens[_next++] : throw new FormatException("AceSerializer text ends early");

        private object? Value(int depth)
        {
            if (depth > MaxDepth)
            {
                throw new FormatException("nested too deep for a route");
            }
            var (type, data) = Take();
            switch (type)
            {
                case 'S':
                    return Unescape(data);
                case 'N':
                    // Whole numbers stay whole: "? double : long" would make every one a double.
                    return long.TryParse(data, System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture, out var whole)
                        ? (object)whole
                        : double.Parse(data, System.Globalization.CultureInfo.InvariantCulture);
                case 'F':
                    var (expType, exponent) = Take();
                    if (expType != 'f')
                    {
                        throw new FormatException("AceSerializer float without exponent");
                    }
                    return double.Parse(data, System.Globalization.CultureInfo.InvariantCulture)
                           * Math.Pow(2, int.Parse(exponent, System.Globalization.CultureInfo.InvariantCulture));
                case 'B':
                    return true;
                case 'b':
                    return false;
                case 'Z':
                    return null;
                case 'T':
                    var table = new LuaTable();
                    while (_next < _tokens.Count && _tokens[_next].Type != 't')
                    {
                        var key = Value(depth + 1);
                        var value = Value(depth + 1);
                        if (key is not null)
                        {
                            table.Set(key, value);
                        }
                    }
                    Take();
                    return table;
                default:
                    throw new FormatException($"AceSerializer type '{type}' out of place");
            }
        }

        /// <summary>"~" + a character: control characters and space as char + 64; "~z" 30, "~{" DEL, "~|" "~", "~}" "^".</summary>
        private static string Unescape(string data)
        {
            if (!data.Contains('~'))
            {
                return data;
            }
            var sb = new StringBuilder(data.Length);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] != '~' || i + 1 >= data.Length)
                {
                    sb.Append(data[i]);
                    continue;
                }
                var e = data[++i];
                sb.Append(e switch
                {
                    'z' => '\x1E',
                    '{' => '\x7F',
                    '|' => '~',
                    '}' => '^',
                    _ => (char)(e - 64),
                });
            }
            return sb.ToString();
        }
    }
}

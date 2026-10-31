// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text;

namespace RaidexKeys.Core.SavedVariables;

/// <summary>A Lua table as WoW writes it: keys are strings or numbers.</summary>
/// <remarks>
/// Integer keys are stored as <see cref="long"/>, other numbers as
/// <see cref="double"/>, so <c>[586]</c> and the third item of a list are
/// found with the same lookup.
/// </remarks>
public sealed class LuaTable
{
    private readonly Dictionary<object, object?> _entries = [];

    public IReadOnlyDictionary<object, object?> Entries => _entries;

    internal void Set(object key, object? value) => _entries[Normalize(key)] = value;

    public object? this[object key] => _entries.GetValueOrDefault(Normalize(key));

    public LuaTable? Table(object key) => this[key] as LuaTable;

    public string? String(object key) => this[key] as string;

    public long? Integer(object key) => this[key] switch
    {
        long l => l,
        double d when d == Math.Floor(d) && !double.IsInfinity(d) => (long)d,
        double d => (long)Math.Round(d),
        _ => null,
    };

    public double? Number(object key) => this[key] switch
    {
        long l => l,
        double d => d,
        _ => null,
    };

    public bool? Boolean(object key) => this[key] as bool?;

    /// <summary>The list part, 1..n without gaps, as Lua's ipairs would walk it.</summary>
    public IEnumerable<object?> List()
    {
        for (long i = 1; _entries.TryGetValue(i, out var value); i++)
        {
            yield return value;
        }
    }

    private static object Normalize(object key) => key switch
    {
        int i => (long)i,
        double d when d == Math.Floor(d) && Math.Abs(d) < 9e15 => (long)d,
        _ => key,
    };
}

/// <summary>
/// Reads the Lua subset WoW writes into SavedVariables: assignments of
/// strings, numbers, booleans, nil and nested tables to global names.
/// </summary>
/// <remarks>
/// A parser, never an interpreter - nothing in the file is executed. It is
/// tolerant where WoW's writer varies (comments, bare-name keys, ';' as field
/// separator) and strict about everything else: a file cut off mid-write
/// throws, and the caller keeps what it had.
/// </remarks>
public sealed class LuaParser
{
    private readonly string _text;
    private int _pos;

    private LuaParser(string text) => _text = text;

    /// <summary>All top-level assignments: global name to value.</summary>
    public static IReadOnlyDictionary<string, object?> ParseGlobals(string text)
    {
        var parser = new LuaParser(text);
        var globals = new Dictionary<string, object?>(StringComparer.Ordinal);
        parser.SkipSpace();
        while (!parser.AtEnd)
        {
            var name = parser.ReadName();
            parser.Expect('=');
            globals[name] = parser.ReadValue();
            parser.SkipSpace();
        }
        return globals;
    }

    /// <summary>
    /// The table constructor starting at <paramref name="position"/> (spaces and
    /// comments before it allowed) - for Lua files that are data inside a little
    /// code, such as an addon's data tables. Only that table is read; the rest
    /// of the file is neither parsed nor run.
    /// </summary>
    public static LuaTable ParseTableAt(string text, int position)
    {
        var parser = new LuaParser(text) { _pos = position };
        parser.SkipSpace();
        if (parser.AtEnd || parser.Peek != '{')
        {
            throw parser.Error("expected a table");
        }
        return parser.ReadTable();
    }

    private bool AtEnd => _pos >= _text.Length;

    private char Peek => _text[_pos];

    private FormatException Error(string message) =>
        new($"SavedVariables: {message} at offset {_pos}");

    private void SkipSpace()
    {
        while (!AtEnd)
        {
            if (char.IsWhiteSpace(Peek))
            {
                _pos++;
            }
            else if (_text.AsSpan(_pos).StartsWith("--"))
            {
                _pos += 2;
                if (LongBracketLevel() is { } level)
                {
                    var close = "]" + new string('=', level) + "]";
                    var end = _text.IndexOf(close, _pos, StringComparison.Ordinal);
                    _pos = end < 0 ? _text.Length : end + close.Length;
                }
                else
                {
                    var end = _text.IndexOf('\n', _pos);
                    _pos = end < 0 ? _text.Length : end + 1;
                }
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>The level of a long bracket here - 0 for "[[", 2 for "[==[" - or null.</summary>
    private int? LongBracketLevel()
    {
        if (AtEnd || Peek != '[')
        {
            return null;
        }
        var level = 0;
        while (_pos + 1 + level < _text.Length && _text[_pos + 1 + level] == '=')
        {
            level++;
        }
        return _pos + 1 + level < _text.Length && _text[_pos + 1 + level] == '[' ? level : null;
    }

    private void Expect(char c)
    {
        SkipSpace();
        if (AtEnd || Peek != c)
        {
            throw Error($"expected '{c}'");
        }
        _pos++;
    }

    private string ReadName()
    {
        SkipSpace();
        var start = _pos;
        while (!AtEnd && (char.IsLetterOrDigit(Peek) || Peek == '_'))
        {
            _pos++;
        }
        if (start == _pos || char.IsDigit(_text[start]))
        {
            throw Error("expected a name");
        }
        return _text[start.._pos];
    }

    private object? ReadValue()
    {
        SkipSpace();
        if (AtEnd)
        {
            throw Error("unexpected end");
        }
        return Peek switch
        {
            '{' => ReadTable(),
            '"' or '\'' => ReadString(),
            '-' or '.' or (>= '0' and <= '9') => ReadNumber(),
            _ => ReadKeyword(),
        };
    }

    private object? ReadKeyword()
    {
        var word = ReadName();
        return word switch
        {
            "true" => true,
            "false" => false,
            "nil" => null,
            _ => throw Error($"unexpected '{word}'"),
        };
    }

    private LuaTable ReadTable()
    {
        Expect('{');
        var table = new LuaTable();
        long index = 1;
        while (true)
        {
            SkipSpace();
            if (AtEnd)
            {
                throw Error("unterminated table");
            }
            if (Peek == '}')
            {
                _pos++;
                return table;
            }

            if (Peek == '[')
            {
                _pos++;
                var key = ReadValue() ?? throw Error("nil table key");
                Expect(']');
                Expect('=');
                table.Set(key, ReadValue());
            }
            else if (char.IsLetter(Peek) || Peek == '_')
            {
                // Either a bare-name key (name = value) or a keyword value in a list.
                var save = _pos;
                var name = ReadName();
                SkipSpace();
                if (!AtEnd && Peek == '=')
                {
                    _pos++;
                    table.Set(name, ReadValue());
                }
                else
                {
                    _pos = save;
                    table.Set(index++, ReadValue());
                }
            }
            else
            {
                table.Set(index++, ReadValue());
            }

            SkipSpace();
            if (!AtEnd && (Peek == ',' || Peek == ';'))
            {
                _pos++;
            }
        }
    }

    private string ReadString()
    {
        var quote = Peek;
        _pos++;
        var sb = new StringBuilder();
        while (true)
        {
            if (AtEnd)
            {
                throw Error("unterminated string");
            }
            var c = _text[_pos++];
            if (c == quote)
            {
                return sb.ToString();
            }
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }
            if (AtEnd)
            {
                throw Error("unterminated escape");
            }
            var e = _text[_pos++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case '\n': sb.Append('\n'); break;
                case >= '0' and <= '9':
                    // \ddd: up to three decimal digits. WoW escapes only ASCII
                    // this way; non-ASCII text stands in the file as UTF-8.
                    var value = e - '0';
                    for (var i = 0; i < 2 && !AtEnd && char.IsAsciiDigit(Peek); i++)
                    {
                        value = value * 10 + (_text[_pos++] - '0');
                    }
                    sb.Append((char)value);
                    break;
                default: sb.Append(e); break;
            }
        }
    }

    private object ReadNumber()
    {
        var start = _pos;
        if (Peek == '-')
        {
            _pos++;
        }
        if (_text.AsSpan(_pos).StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            _pos += 2;
            var hexStart = _pos;
            while (!AtEnd && char.IsAsciiHexDigit(Peek))
            {
                _pos++;
            }
            var hex = long.Parse(_text.AsSpan(hexStart, _pos - hexStart), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return _text[start] == '-' ? -hex : hex;
        }
        while (!AtEnd && (char.IsAsciiDigit(Peek) || Peek is '.' or 'e' or 'E'
                          || ((Peek is '+' or '-') && _text[_pos - 1] is 'e' or 'E')))
        {
            _pos++;
        }
        var span = _text.AsSpan(start, _pos - start);
        if (long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }
        if (double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        throw Error($"bad number '{span}'");
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace JulesClient.Services;

public enum CodeTokenKind
{
    Plain,
    Keyword,
    Type,
    String,
    Comment,
    Number,
    Preprocessor
}

public sealed record CodeToken(CodeTokenKind Kind, string Text);

/// <summary>
/// Small, dependency-free lexer that colours fenced code blocks. It is not a
/// per-language grammar - it applies a broad union of keywords plus a handful of
/// comment/string/number rules chosen by language family. Over-matching a keyword
/// is harmless for highlighting, so unknown languages fall back to C-family rules.
/// </summary>
public static class CodeHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        // control flow / declarations shared across many C-family + scripting langs
        "if","else","elif","for","while","do","switch","case","default","break","continue",
        "return","goto","yield","await","async","throw","try","catch","finally","except",
        "raise","with","as","in","is","of","from","import","export","package","namespace",
        "using","module","require","include","public","private","protected","internal",
        "static","final","abstract","virtual","override","sealed","partial","readonly",
        "const","constexpr","consteval","constinit","mutable","volatile","register","extern",
        "inline","explicit","friend","typedef","typename","template","operator","this",
        "self","super","base","new","delete","sizeof","alignof","typeof","instanceof",
        "class","struct","enum","union","interface","record","trait","impl","fn","func",
        "function","def","lambda","let","var","val","auto","dyn","move","ref","out","params",
        "def","end","begin","then","elsif","unless","until","when","match","where","select",
        "and","or","not","xor","nil","none","true","false","null","undefined","void",
        "pass","global","nonlocal","del","assert","lambda","print","echo","local","set",
        "unset","export","source","fi","esac","done","function","declare",
        "pub","use","mod","crate","extern","unsafe","where","loop","box",
        "defer","chan","go","map","range","type","interface","fallthrough",
        "goog","get","set"
    };

    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "int","long","short","char","bool","boolean","float","double","void","byte",
        "signed","unsigned","string","wstring","wchar_t","size_t","ssize_t","ptrdiff_t",
        "int8_t","int16_t","int32_t","int64_t","uint8_t","uint16_t","uint32_t","uint64_t",
        "uint","ulong","ushort","sbyte","decimal","object","dynamic","var","String","Object",
        "Integer","Boolean","Double","Float","Number","Array","List","Dictionary","Map","Set",
        "vector","array","map","unordered_map","unordered_set","set","list","pair","tuple",
        "shared_ptr","unique_ptr","weak_ptr","optional","variant","function","std","string_view",
        "i8","i16","i32","i64","i128","isize","u8","u16","u32","u64","u128","usize","f32","f64","str","Vec","Box","Option","Result"
    };

    private sealed record Lang(
        bool LineSlash,
        bool LineHash,
        bool LineDash,
        bool Block,
        bool HashPreprocessor,
        bool BacktickString);

    private static Lang Resolve(string? language)
    {
        var l = (language ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

        switch (l)
        {
            case "c":
            case "h":
            case "cpp":
            case "cc":
            case "cxx":
            case "hpp":
            case "hxx":
            case "c++":
            case "objc":
            case "objective-c":
            case "cuda":
                return new Lang(LineSlash: true, LineHash: false, LineDash: false, Block: true, HashPreprocessor: true, BacktickString: false);

            case "cs":
            case "csharp":
            case "c#":
                return new Lang(true, false, false, true, HashPreprocessor: true, BacktickString: false);

            case "js":
            case "jsx":
            case "javascript":
            case "ts":
            case "tsx":
            case "typescript":
                return new Lang(true, false, false, true, false, BacktickString: true);

            case "java":
            case "kt":
            case "kotlin":
            case "swift":
            case "go":
            case "golang":
            case "rust":
            case "rs":
            case "scala":
            case "dart":
            case "php":
            case "groovy":
            case "proto":
                return new Lang(true, false, false, true, false, false);

            case "py":
            case "python":
            case "rb":
            case "ruby":
            case "sh":
            case "bash":
            case "shell":
            case "zsh":
            case "fish":
            case "yaml":
            case "yml":
            case "toml":
            case "ini":
            case "cfg":
            case "conf":
            case "dockerfile":
            case "makefile":
            case "make":
            case "r":
            case "perl":
            case "pl":
            case "ps1":
            case "powershell":
            case "elixir":
            case "nim":
            case "julia":
            case "jl":
                return new Lang(false, LineHash: true, false, false, false, false);

            case "sql":
            case "mysql":
            case "postgres":
            case "postgresql":
            case "plsql":
            case "tsql":
                return new Lang(false, false, LineDash: true, Block: true, false, false);

            case "json":
            case "json5":
            case "jsonc":
                return new Lang(false, false, false, false, false, false);

            default:
                // Unknown: assume C-family line/block comments, no preprocessor.
                return new Lang(true, false, false, true, false, false);
        }
    }

    public static IReadOnlyList<CodeToken> Highlight(string? code, string? language)
    {
        var tokens = new List<CodeToken>();
        if (string.IsNullOrEmpty(code)) return tokens;

        try
        {
            var lang = Resolve(language);
            var plain = new StringBuilder();
            int i = 0;
            int n = code.Length;
            bool atLineStart = true; // ignoring leading whitespace

            void FlushPlain()
            {
                if (plain.Length > 0)
                {
                    tokens.Add(new CodeToken(CodeTokenKind.Plain, plain.ToString()));
                    plain.Clear();
                }
            }

            void Emit(CodeTokenKind kind, string text)
            {
                FlushPlain();
                tokens.Add(new CodeToken(kind, text));
            }

            while (i < n)
            {
                char c = code[i];

                // Line comment: //
                if (lang.LineSlash && c == '/' && i + 1 < n && code[i + 1] == '/')
                {
                    int e = LineEnd(code, i);
                    Emit(CodeTokenKind.Comment, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // Line comment: -- (SQL)
                if (lang.LineDash && c == '-' && i + 1 < n && code[i + 1] == '-')
                {
                    int e = LineEnd(code, i);
                    Emit(CodeTokenKind.Comment, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // Block comment: /* ... */
                if (lang.Block && c == '/' && i + 1 < n && code[i + 1] == '*')
                {
                    int e = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    e = e < 0 ? n : e + 2;
                    Emit(CodeTokenKind.Comment, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // '#': preprocessor line, hash comment, or plain
                if (c == '#')
                {
                    if (lang.HashPreprocessor && atLineStart)
                    {
                        int e = LineEnd(code, i);
                        Emit(CodeTokenKind.Preprocessor, code.Substring(i, e - i));
                        i = e;
                        atLineStart = false;
                        continue;
                    }
                    if (lang.LineHash)
                    {
                        int e = LineEnd(code, i);
                        Emit(CodeTokenKind.Comment, code.Substring(i, e - i));
                        i = e;
                        atLineStart = false;
                        continue;
                    }
                }

                // Strings
                if (c == '"' || c == '\'' || (lang.BacktickString && c == '`'))
                {
                    int e = StringEnd(code, i, c);
                    Emit(CodeTokenKind.String, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // Numbers (not when glued to the end of an identifier)
                if (char.IsDigit(c) && (plain.Length == 0 || !IsIdentChar(plain[plain.Length - 1])))
                {
                    int e = NumberEnd(code, i);
                    Emit(CodeTokenKind.Number, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // Identifiers / keywords / types
                if (IsIdentStart(c))
                {
                    int e = i + 1;
                    while (e < n && IsIdentChar(code[e])) e++;
                    var word = code.Substring(i, e - i);
                    var kind = Keywords.Contains(word) ? CodeTokenKind.Keyword
                        : Types.Contains(word) ? CodeTokenKind.Type
                        : CodeTokenKind.Plain;
                    if (kind == CodeTokenKind.Plain) plain.Append(word);
                    else Emit(kind, word);
                    i = e;
                    atLineStart = false;
                    continue;
                }

                if (c == '\n') atLineStart = true;
                else if (!char.IsWhiteSpace(c)) atLineStart = false;

                plain.Append(c);
                i++;
            }

            FlushPlain();
        }
        catch
        {
            tokens.Clear();
            tokens.Add(new CodeToken(CodeTokenKind.Plain, code));
        }

        return tokens;
    }

    private static int LineEnd(string s, int from)
    {
        int e = s.IndexOf('\n', from);
        return e < 0 ? s.Length : e;
    }

    private static int StringEnd(string s, int start, char quote)
    {
        for (int i = start + 1; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\') { i++; continue; }
            if (c == quote) return i + 1;
            if (c == '\n' && quote != '`') return i; // unterminated: stop at line end
        }
        return s.Length;
    }

    private static int NumberEnd(string s, int start)
    {
        int i = start;
        int n = s.Length;

        if (s[i] == '0' && i + 1 < n && (s[i + 1] == 'x' || s[i + 1] == 'X'))
        {
            i += 2;
            while (i < n && (Uri.IsHexDigit(s[i]) || s[i] == '_')) i++;
        }
        else
        {
            while (i < n && (char.IsDigit(s[i]) || s[i] == '_')) i++;
            if (i < n && s[i] == '.' && i + 1 < n && char.IsDigit(s[i + 1]))
            {
                i++;
                while (i < n && (char.IsDigit(s[i]) || s[i] == '_')) i++;
            }
            if (i < n && (s[i] == 'e' || s[i] == 'E'))
            {
                int j = i + 1;
                if (j < n && (s[j] == '+' || s[j] == '-')) j++;
                if (j < n && char.IsDigit(s[j]))
                {
                    i = j;
                    while (i < n && char.IsDigit(s[i])) i++;
                }
            }
        }

        // numeric suffixes: 10ULL, 3.0f, 100L, 5u
        while (i < n && "uUlLfFdD".IndexOf(s[i]) >= 0) i++;
        return i;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';
}

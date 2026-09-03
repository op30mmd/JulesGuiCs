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
    Preprocessor,
    Function,
    Constant
}

public sealed record CodeToken(CodeTokenKind Kind, string Text);

/// <summary>
/// Small, dependency-free lexer that colours fenced code blocks. It is not a full
/// per-language grammar, but it does pick a language-specific keyword/type set
/// (falling back to a broad C-family union for unknown languages) and recognises
/// comments, strings, numbers, boolean/null constants, and function-call names.
/// Every input character is emitted in exactly one token, so joining the token
/// text always reconstructs the source.
/// </summary>
public static class CodeHighlighter
{
    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.Ordinal);
    private static HashSet<string> SetCI(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);

    // Boolean / null / "nothing" literals across languages. Case-insensitive so
    // Python's True/False/None and SQL's NULL match the same entries.
    private static readonly IReadOnlySet<string> Constants = SetCI(
        "true", "false", "null", "nil", "none", "undefined",
        "nullptr", "nullopt");

    // ---- Fallback sets (unknown languages, and anything C-like) --------------

    private static readonly HashSet<string> CommonKeywords = Set(
        "if", "else", "elif", "elsif", "for", "while", "do", "switch", "case", "default",
        "break", "continue", "return", "goto", "yield", "await", "async", "throw", "throws",
        "try", "catch", "finally", "except", "raise", "with", "as", "in", "is", "of", "from",
        "import", "export", "package", "namespace", "using", "module", "require", "include",
        "public", "private", "protected", "internal", "static", "final", "abstract", "virtual",
        "override", "sealed", "partial", "readonly", "const", "constexpr", "mutable", "volatile",
        "extern", "inline", "explicit", "friend", "sizeof", "typeof", "instanceof", "new",
        "delete", "this", "self", "super", "base", "class", "struct", "enum", "union",
        "interface", "record", "trait", "impl", "fn", "func", "function", "def", "lambda",
        "let", "var", "val", "auto", "end", "begin", "then", "unless", "until", "when",
        "match", "where", "and", "or", "not", "xor", "pass", "global", "nonlocal", "del",
        "assert", "pub", "use", "mod", "crate", "unsafe", "loop", "defer", "chan", "go",
        "range", "type", "select", "fallthrough", "operator", "template", "typename");

    private static readonly HashSet<string> CommonTypes = Set(
        "int", "long", "short", "char", "bool", "boolean", "float", "double", "byte",
        "signed", "unsigned", "string", "wchar_t", "size_t", "ssize_t", "ptrdiff_t",
        "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t",
        "uint", "ulong", "ushort", "sbyte", "decimal", "object", "dynamic", "String", "Object",
        "Integer", "Boolean", "Double", "Float", "Number", "Array", "List", "Dictionary", "Map", "Set",
        "vector", "map", "unordered_map", "unordered_set", "set", "list", "pair", "tuple",
        "shared_ptr", "unique_ptr", "weak_ptr", "optional", "variant", "std", "string_view",
        "i8", "i16", "i32", "i64", "isize", "u8", "u16", "u32", "u64", "usize", "f32", "f64",
        "str", "Vec", "Box", "Option", "Result");

    // ---- Per-language sets --------------------------------------------------

    private static readonly HashSet<string> KwC = Set(
        "auto", "break", "case", "const", "continue", "default", "do", "else", "enum", "extern",
        "for", "goto", "if", "inline", "register", "restrict", "return", "sizeof", "static",
        "struct", "switch", "typedef", "union", "volatile", "while", "_Bool", "_Static_assert");

    private static readonly HashSet<string> KwCpp = Set(
        "alignas", "alignof", "asm", "break", "case", "catch", "class", "concept", "const",
        "consteval", "constexpr", "constinit", "const_cast", "continue", "co_await", "co_return",
        "co_yield", "decltype", "default", "delete", "do", "dynamic_cast", "else", "enum",
        "explicit", "export", "extern", "final", "for", "friend", "goto", "if", "inline",
        "mutable", "namespace", "new", "noexcept", "operator", "override", "private", "protected",
        "public", "reinterpret_cast", "requires", "return", "sizeof", "static", "static_assert",
        "static_cast", "struct", "switch", "template", "this", "thread_local", "throw", "try",
        "typedef", "typeid", "typename", "union", "using", "virtual", "volatile", "while");

    private static readonly HashSet<string> TyCpp = Set(
        "int", "long", "short", "char", "bool", "float", "double", "void", "signed", "unsigned",
        "wchar_t", "char8_t", "char16_t", "char32_t", "size_t", "ssize_t", "ptrdiff_t", "intptr_t",
        "uintptr_t", "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t",
        "uint64_t", "FILE", "va_list", "std", "string", "wstring", "string_view", "vector", "array",
        "map", "unordered_map", "set", "unordered_set", "list", "deque", "pair", "tuple", "optional",
        "variant", "span", "shared_ptr", "unique_ptr", "weak_ptr", "function", "atomic");

    private static readonly HashSet<string> KwCSharp = Set(
        "abstract", "as", "async", "await", "base", "break", "case", "catch", "checked", "class",
        "const", "continue", "default", "delegate", "do", "else", "enum", "event", "explicit",
        "extern", "finally", "fixed", "for", "foreach", "get", "goto", "if", "implicit", "in",
        "init", "interface", "internal", "is", "lock", "nameof", "namespace", "new", "operator",
        "out", "override", "params", "private", "protected", "public", "readonly", "record", "ref",
        "return", "sealed", "set", "sizeof", "stackalloc", "static", "struct", "switch", "this",
        "throw", "try", "typeof", "unchecked", "unsafe", "using", "var", "virtual", "volatile",
        "when", "while", "with", "yield");

    private static readonly HashSet<string> TyCSharp = Set(
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long",
        "ulong", "short", "ushort", "nint", "nuint", "object", "string", "dynamic", "void",
        "Task", "ValueTask", "List", "Dictionary", "IEnumerable", "IList", "ICollection",
        "Span", "ReadOnlySpan", "Memory", "Nullable", "Guid", "DateTime", "TimeSpan");

    private static readonly HashSet<string> KwJs = Set(
        "abstract", "as", "async", "await", "break", "case", "catch", "class", "const", "continue",
        "debugger", "declare", "default", "delete", "do", "else", "enum", "export", "extends",
        "finally", "for", "from", "function", "get", "if", "implements", "import", "in", "infer",
        "instanceof", "interface", "is", "keyof", "let", "namespace", "new", "of", "package",
        "private", "protected", "public", "readonly", "return", "satisfies", "set", "static",
        "super", "switch", "this", "throw", "try", "type", "typeof", "var", "void", "while",
        "with", "yield");

    private static readonly HashSet<string> TyJs = Set(
        "string", "number", "boolean", "object", "symbol", "bigint", "any", "unknown", "never",
        "void", "Array", "Promise", "Record", "Map", "Set", "Partial", "Readonly", "Pick", "Omit",
        "ReturnType", "Awaited");

    private static readonly HashSet<string> KwPython = Set(
        "and", "as", "assert", "async", "await", "break", "case", "class", "continue", "def",
        "del", "elif", "else", "except", "finally", "for", "from", "global", "if", "import",
        "in", "is", "lambda", "match", "nonlocal", "not", "or", "pass", "raise", "return",
        "try", "while", "with", "yield");

    private static readonly HashSet<string> TyPython = Set(
        "int", "float", "complex", "bool", "str", "bytes", "bytearray", "list", "tuple", "dict",
        "set", "frozenset", "object", "type", "range");

    private static readonly HashSet<string> KwJava = Set(
        "abstract", "assert", "break", "case", "catch", "class", "const", "continue", "default",
        "do", "else", "enum", "extends", "final", "finally", "for", "goto", "if", "implements",
        "import", "instanceof", "interface", "native", "new", "package", "permits", "private",
        "protected", "public", "record", "return", "sealed", "static", "strictfp", "super",
        "switch", "synchronized", "this", "throw", "throws", "transient", "try", "var", "void",
        "volatile", "while", "yield");

    private static readonly HashSet<string> KwKotlin = Set(
        "abstract", "actual", "annotation", "as", "break", "by", "catch", "class", "companion",
        "const", "constructor", "continue", "crossinline", "data", "delegate", "do", "dynamic",
        "else", "enum", "expect", "external", "final", "finally", "for", "fun", "get", "if",
        "import", "in", "infix", "init", "inline", "inner", "interface", "internal", "is",
        "lateinit", "noinline", "object", "open", "operator", "out", "override", "package",
        "private", "protected", "public", "reified", "return", "sealed", "set", "super",
        "suspend", "tailrec", "this", "throw", "try", "typealias", "val", "var", "vararg",
        "when", "where", "while");

    private static readonly HashSet<string> KwGo = Set(
        "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough",
        "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range",
        "return", "select", "struct", "switch", "type", "var");

    private static readonly HashSet<string> TyGo = Set(
        "bool", "string", "int", "int8", "int16", "int32", "int64", "uint", "uint8", "uint16",
        "uint32", "uint64", "uintptr", "byte", "rune", "float32", "float64", "complex64",
        "complex128", "error", "any");

    private static readonly HashSet<string> KwRust = Set(
        "as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum",
        "extern", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod", "move", "mut",
        "pub", "ref", "return", "self", "Self", "static", "struct", "super", "trait", "type",
        "union", "unsafe", "use", "where", "while");

    private static readonly HashSet<string> TyRust = Set(
        "bool", "char", "str", "String", "i8", "i16", "i32", "i64", "i128", "isize", "u8", "u16",
        "u32", "u64", "u128", "usize", "f32", "f64", "Vec", "Box", "Option", "Result", "Rc", "Arc",
        "RefCell", "Cell", "HashMap", "HashSet", "BTreeMap", "Cow");

    private static readonly HashSet<string> KwRuby = Set(
        "alias", "and", "begin", "break", "case", "class", "def", "defined?", "do", "else",
        "elsif", "end", "ensure", "for", "if", "in", "module", "next", "nil", "not", "or",
        "redo", "rescue", "retry", "return", "self", "super", "then", "undef", "unless", "until",
        "when", "while", "yield", "require", "require_relative", "attr_accessor", "attr_reader",
        "attr_writer", "lambda", "proc");

    private static readonly HashSet<string> KwPhp = Set(
        "abstract", "and", "array", "as", "break", "callable", "case", "catch", "class", "clone",
        "const", "continue", "declare", "default", "do", "echo", "else", "elseif", "enum",
        "extends", "final", "finally", "fn", "for", "foreach", "function", "global", "goto", "if",
        "implements", "include", "include_once", "instanceof", "insteadof", "interface", "isset",
        "list", "match", "namespace", "new", "or", "print", "private", "protected", "public",
        "readonly", "require", "require_once", "return", "static", "switch", "throw", "trait",
        "try", "unset", "use", "var", "while", "xor", "yield");

    private static readonly HashSet<string> KwShell = Set(
        "if", "then", "else", "elif", "fi", "case", "esac", "for", "while", "until", "do", "done",
        "in", "function", "select", "time", "coproc", "return", "break", "continue", "local",
        "export", "readonly", "declare", "typeset", "unset", "shift", "eval", "exec", "trap",
        "set", "source", "alias");

    private static readonly HashSet<string> KwSwift = Set(
        "actor", "as", "associatedtype", "async", "await", "break", "case", "catch", "class",
        "continue", "default", "defer", "deinit", "do", "else", "enum", "extension", "fallthrough",
        "fileprivate", "final", "for", "func", "guard", "if", "import", "in", "init", "inout",
        "internal", "is", "let", "open", "operator", "private", "protocol", "public", "repeat",
        "rethrows", "return", "self", "Self", "static", "struct", "subscript", "switch", "throw",
        "throws", "try", "typealias", "var", "where", "while");

    // SQL keywords and types are matched case-insensitively.
    private static readonly HashSet<string> KwSql = SetCI(
        "select", "from", "where", "insert", "into", "values", "update", "set", "delete", "create",
        "alter", "drop", "truncate", "table", "index", "view", "sequence", "database", "schema",
        "join", "inner", "left", "right", "full", "outer", "cross", "on", "using", "group", "by",
        "order", "having", "limit", "offset", "fetch", "union", "intersect", "except", "all",
        "distinct", "as", "and", "or", "not", "is", "in", "like", "ilike", "between", "exists",
        "case", "when", "then", "else", "end", "primary", "key", "foreign", "references", "default",
        "constraint", "unique", "check", "cascade", "begin", "commit", "rollback", "transaction",
        "with", "returning", "asc", "desc", "null", "into", "grant", "revoke", "add", "column",
        "if", "replace", "over", "partition", "recursive");

    private static readonly HashSet<string> TySql = SetCI(
        "int", "integer", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real",
        "double", "precision", "bit", "money", "varchar", "nvarchar", "char", "nchar", "text",
        "ntext", "clob", "blob", "date", "time", "timestamp", "datetime", "datetime2",
        "smalldatetime", "boolean", "bool", "uuid", "json", "jsonb", "serial", "bigserial", "bytea");

    private sealed record Lang(
        bool LineSlash,
        bool LineHash,
        bool LineDash,
        bool Block,
        bool HashPreprocessor,
        bool BacktickString,
        IReadOnlySet<string> Keywords,
        IReadOnlySet<string> Types);

    private static Lang Resolve(string? language)
    {
        var l = (language ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

        switch (l)
        {
            case "c":
            case "h":
                return new Lang(true, false, false, true, true, false, KwC, CommonTypes);

            case "cpp":
            case "cc":
            case "cxx":
            case "hpp":
            case "hxx":
            case "c++":
            case "objc":
            case "objective-c":
            case "objcpp":
            case "cuda":
            case "metal":
                return new Lang(true, false, false, true, true, false, KwCpp, TyCpp);

            case "cs":
            case "csharp":
            case "c#":
                return new Lang(true, false, false, true, true, false, KwCSharp, TyCSharp);

            case "js":
            case "jsx":
            case "mjs":
            case "cjs":
            case "javascript":
            case "node":
            case "ts":
            case "tsx":
            case "mts":
            case "typescript":
                return new Lang(true, false, false, true, false, true, KwJs, TyJs);

            case "java":
                return new Lang(true, false, false, true, false, false, KwJava, CommonTypes);

            case "kt":
            case "kts":
            case "kotlin":
                return new Lang(true, false, false, true, false, true, KwKotlin, CommonTypes);

            case "swift":
                return new Lang(true, false, false, true, false, false, KwSwift, CommonTypes);

            case "go":
            case "golang":
                return new Lang(true, false, false, true, false, true, KwGo, TyGo);

            case "rust":
            case "rs":
                return new Lang(true, false, false, true, false, false, KwRust, TyRust);

            case "php":
                return new Lang(true, true, false, true, false, false, KwPhp, CommonTypes);

            case "rb":
            case "ruby":
                return new Lang(false, true, false, false, false, false, KwRuby, CommonTypes);

            case "py":
            case "python":
            case "pyi":
                return new Lang(false, true, false, false, false, false, KwPython, TyPython);

            case "sh":
            case "bash":
            case "shell":
            case "zsh":
            case "ksh":
            case "fish":
            case "dockerfile":
                return new Lang(false, true, false, false, false, false, KwShell, CommonTypes);

            case "yaml":
            case "yml":
            case "toml":
            case "ini":
            case "cfg":
            case "conf":
            case "properties":
            case "makefile":
            case "make":
            case "cmake":
            case "r":
            case "perl":
            case "pl":
            case "elixir":
            case "ex":
            case "exs":
            case "nim":
            case "julia":
            case "jl":
            case "coffee":
                return new Lang(false, true, false, false, false, false, CommonKeywords, CommonTypes);

            case "ps1":
            case "powershell":
            case "psm1":
                return new Lang(false, true, false, false, false, false, CommonKeywords, CommonTypes);

            case "scala":
            case "dart":
            case "groovy":
            case "proto":
            case "protobuf":
                return new Lang(true, false, false, true, false, false, CommonKeywords, CommonTypes);

            case "sql":
            case "mysql":
            case "postgres":
            case "postgresql":
            case "pgsql":
            case "plsql":
            case "tsql":
            case "sqlite":
                return new Lang(false, false, true, true, false, false, KwSql, TySql);

            case "json":
            case "json5":
            case "jsonc":
                return new Lang(false, false, false, l != "json", false, false,
                    new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

            default:
                // Unknown: assume C-family line/block comments, broad keyword union.
                return new Lang(true, false, false, true, false, false, CommonKeywords, CommonTypes);
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

                // Numbers (not when glued to the end of an identifier). A leading
                // '.' counts only when a digit follows (".5").
                bool numberStart = char.IsDigit(c)
                    || (c == '.' && i + 1 < n && char.IsDigit(code[i + 1]));
                if (numberStart && (plain.Length == 0 || !IsIdentChar(plain[plain.Length - 1])))
                {
                    int e = NumberEnd(code, i);
                    Emit(CodeTokenKind.Number, code.Substring(i, e - i));
                    i = e;
                    atLineStart = false;
                    continue;
                }

                // Identifiers / keywords / types / constants / call names
                if (IsIdentStart(c))
                {
                    int e = i + 1;
                    while (e < n && IsIdentChar(code[e])) e++;
                    var word = code.Substring(i, e - i);

                    CodeTokenKind kind =
                        Constants.Contains(word) ? CodeTokenKind.Constant :
                        lang.Keywords.Contains(word) ? CodeTokenKind.Keyword :
                        lang.Types.Contains(word) ? CodeTokenKind.Type :
                        IsCallName(code, e, n) ? CodeTokenKind.Function :
                        CodeTokenKind.Plain;

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

    // True when the identifier that ends at <paramref name="from"/> is immediately
    // followed (past spaces/tabs, but not newlines) by '(' - i.e. a call or a
    // function/method definition.
    private static bool IsCallName(string s, int from, int n)
    {
        int i = from;
        while (i < n && (s[i] == ' ' || s[i] == '\t')) i++;
        return i < n && s[i] == '(';
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
        else if (s[i] == '0' && i + 1 < n && (s[i + 1] == 'b' || s[i + 1] == 'B'))
        {
            i += 2;
            while (i < n && (s[i] == '0' || s[i] == '1' || s[i] == '_')) i++;
        }
        else if (s[i] == '0' && i + 1 < n && (s[i + 1] == 'o' || s[i + 1] == 'O'))
        {
            i += 2;
            while (i < n && ((s[i] >= '0' && s[i] <= '7') || s[i] == '_')) i++;
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

        // numeric suffixes: 10ULL, 3.0f, 100L, 5u, 2n (JS BigInt)
        while (i < n && "uUlLfFdDnN".IndexOf(s[i]) >= 0) i++;
        return i;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';
    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';
}

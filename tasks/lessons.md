# Lessons Learned

## C# Raw String Literals with JSON
**Rule:** In `$"""..."""` interpolated raw string literals, `{{` is a literal `{`. However, with .NET 10 compiler (CS9006), this can cause issues when the JSON template has `{{key}}` patterns that look ambiguous. Use regular string concatenation for prompts that contain JSON schema examples.

**Why:** CS9006 "does not start with enough '$' characters" error thrown by compiler when JSON template braces in `$"""` strings are ambiguous.

**How to apply:** For AI prompt strings containing JSON examples, use string concatenation or `$$"""..."""` syntax with `{{expr}}` for interpolations.

## HtmlAgilityPack Null Coalescing with Collection Expressions
**Rule:** `SelectNodes()` returns `HtmlNodeCollection`, which cannot be null-coalesced with a collection expression `[]`. Use `Enumerable.Empty<HtmlNode>()` instead.

**Why:** C# compiler error CS0019: `??` cannot be applied to `HtmlNodeCollection` and collection expression.

**How to apply:** `var nodes = doc.SelectNodes("//x"); foreach (var n in nodes ?? Enumerable.Empty<HtmlNode>())`

## ASP.NET Core Framework-Included Packages
**Rule:** Do NOT explicitly add `Microsoft.AspNetCore.DataProtection` or `Microsoft.AspNetCore.SignalR` as PackageReferences in a `Microsoft.NET.Sdk.Web` project. They are included in the framework.

**Why:** NU1510 warning and potential version conflicts. These packages are part of the ASP.NET Core shared framework.

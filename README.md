# `Blokyk.CA2254CodeFix`

The CA2254 diagnostic (`Template should be a static expression`) helps you
detect logger messages that could be problematic for structured logging, by
flagging non-constant message templates, i.e. messages that contain expressions
themselves.

Most notably, this includes interpolated strings (`$"hello {user.name}!"`),
which can be pretty annoying to "un-interpolate." This small roslyn codefix aims
to address that! Here's an example:

```cs
// Before codefix, marked with CA2254
logger.LogDebug($"User {user.Username} uploaded {documents.Count} documents!");

// After codefix
logger.LogDebug("User {Username} uploaded {Count} documents!", user.Username, documents.Count);
```

## Installation

This codefix can be installed just like any package using the dotnet CLI:
```sh
dotnet add package Blokyk.CA2254CodeFix
```

You can also reference it in your project:
```xml
<PackageReference Include="Blokyk.CA2254CodeFix" Version="*" />
```

## Details about the expression names

The expressions in the interpolated string need to be replaced with *something*,
so obviously this codefix is quite opinionated. Here are some examples of
transformations used to derive the names:

| expression            | resulting name     |
|-----------------------|--------------------|
| `foo.bar`             | `Bar`              |
| `foo.bar()`           | `Bar`              |
| `foo.bar(alice, bob)` | `BarOfALiceAndBob` |
| `foo is not null`     | `FooIsNotNull`     |
| `DateTime.UtcNow`     | `DateTime`         |

The actual derivation is mostly done in [`ExpressionNamer.cs`](./ExpressionNamer.cs),
with a little bit more in [`CSharpLoggerMessageFixer.cs`](./CSharpLoggerMessageFixer.cs)
right now.

If you'd like to suggest a tweak or a new naming rule, don't hesitate to open a
new issue or PR, I'm completely open to ideas on this!
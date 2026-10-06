# Contributing

Thanks for your interest in Image Grid Fusion. Issues and pull requests are welcome.

## Build

- Requires the .NET 10 SDK, on Windows 10 version 2004 or later.
- Run: `dotnet run --project src/ImageGridFusion`
- Publish: `dotnet publish src/ImageGridFusion -c Release`

There is no test project: check a change by hand in the running app.

## Before changing the code

- [RULES.md](RULES.md) holds the rules every change follows — effects, toolbars, output format,
  undo history, settings. A change that breaks one of them updates the rule in the same pull request.
- [GLOSSARY.md](GLOSSARY.md) gives each word its one meaning: use its terms in the code, the
  comments and the documentation.
- [`workfiles/`](workfiles) keeps the design of each feature, the origin of the rules.

## Pull requests

- One feature or fix per pull request, in English: code, comments, commit messages.
- Match the surrounding code: naming, comment density, idioms. C# / WinForms, no third-party
  library — Windows' own components only.
- A change the user can see updates [README.md](README.md) and [README.fr.md](README.fr.md).

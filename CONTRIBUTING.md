# How to contribute
All contributions are welcome:
- Documentation/typos: XML comments, README, Wiki 
- Feedback: bug reports, feature requests
- Code/CI: pull requests

## Questions about WOPI
- [Submit a question](https://stackoverflow.com/questions/ask?tags=ms-wopi) under the [`ms-wopi`](https://stackoverflow.com/questions/tagged/ms-wopi) on StackOverflow

## Bug reports
- [Submit a new bug](https://github.com/petrsvihlik/WopiHost/issues/new/choose)

## Feature requests
- [Request a feature](https://github.com/petrsvihlik/WopiHost/issues/new/choose)

## Pull requests
- Feel free to submit a PR, just make sure your code adheres the existing codestyle, and to [.NET's Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)

## Toolchain
The SDK is pinned in [`global.json`](global.json): any .NET 10 SDK satisfies it, and `allowPrerelease: false` keeps preview SDKs out of ordinary builds. `LangVersion` is pinned in `Directory.Build.props` for the same reason — left at `latest` it would track whatever compiler happens to be installed.

Together that means **.NET 11 previews can be installed side by side without affecting this repo**. Builds keep selecting the .NET 10 SDK and C# 14 no matter what else is on the machine.

Preview SDKs and runtimes are exercised nightly by [`.github/workflows/net11-preview.yml`](.github/workflows/net11-preview.yml). It is informational, gates nothing, and changes no target framework.

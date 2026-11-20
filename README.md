# Rim Chronicles

A RimWorld 1.7 mod that adds a story mode: a major plot (the Awakened Empire) and minor plots grown from each colony's own history, narrated by a local LLM, with template fallbacks so it plays fine with the model switched off.

Status: Stage 0 (model evaluation harness), milestone M0 scaffold done.

- [Design spec](docs/Rim%20Chronicles%20—%20Design%20Spec.md)
- [Technical spec](https://claude.ai/artifact/AMTEFZMpdn9ELPEJd5biXB)

## Build

Requires the .NET 10 SDK.

```bash
dotnet build RimChronicles.slnx
dotnet test RimChronicles.slnx
```

See `CLAUDE.md` for the layout and coding rules.

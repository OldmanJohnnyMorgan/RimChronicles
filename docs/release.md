# Releasing Rim Chronicles

The steps for a Steam Workshop release, run on the PC in **PowerShell** from the repository folder
(`C:\RimworldMods\RimChronicles\RimChronicles`). Nothing here runs in the codespace except the packaging test.

## 1. Before a release

- [ ] Every in-game checkpoint for the release has passed (`docs/test-plan.md`), and its branch is merged into `main`.
- [ ] A Stage 0 run covers every prompt that changed since the last release (`docs/stage0-runbook.md`, no `--judge`).
- [ ] `dotnet test RimChronicles.slnx` passes, and `training` tests pass (`cd training; python3 -m unittest discover -s tests`).
- [ ] `About/About.xml`: `modVersion` bumped, and the description lists what's new.
- [ ] `README.md`: the status line and the feature list match the release.
- [ ] `About/Preview.png` exists (the Workshop's preview image: 640×360 or larger, under 1 MB).
- [ ] The director models the presets name are on the Ollama registry (§3).

## 2. Package the mod

The repository holds the source, the harness, training and docs, none of which belongs on the Workshop. Package the
folder the game loads:

```powershell
dotnet build src/RimChronicles.Game -c Release -t:PackageRelease
```

This writes `release\RimChronicles\` (git-ignored) with only `About\`, `1.7\Assemblies\` (the three mod DLLs, never
Harmony or game DLLs), `Defs\`, `prompts\`, `LICENSE` and `README.md`. It stops if `Directory.Build.props.user` doesn't
point at RimWorld, and warns if `About\Preview.png` is missing.

Test the package, not the repository: move the repository's own copy out of RimWorld's `Mods` folder (or rename its
`About\About.xml`), copy `release\RimChronicles` into `Mods`, start a new colony and press **Test connection**.

## 3. Publish the director models (Ollama registry)

Gemma 4 is released under Apache 2.0, so the fine-tuned directors can be shared, as long as the licence goes with
them. Players get them with one `ollama pull`, or the mod's **Download model** button; the game's presets name them
`oldmanjohnnymorgan/rimchron-director-e2b` and `-e4b` (`TierPresets.RegistryNamespace`).

One-time setup:

1. Make an account on [ollama.com](https://ollama.com) named `oldmanjohnnymorgan` (or change `RegistryNamespace` in
   `src/RimChronicles.Llm/TierPresets.cs` and `NAMESPACE` in `training/rimchron_train/registry.py` to yours; a test
   keeps them equal).
2. Add this PC's Ollama key to the account (ollama.com → Settings → Ollama keys):
   ```powershell
   Get-Content $env:USERPROFILE\.ollama\id_ed25519.pub
   ```

For each director that passed its report (only `rimchron-director-e4b` knows the War in Heaven so far), in WSL2:

```bash
cd /mnt/c/RimworldMods/RimChronicles/RimChronicles/training
uv run python -m rimchron_train publish --config config/director-e4b-war.yaml --model rimchron-director-e4b
```

It writes `Modelfile.publish` (the local model plus Gemma's `LICENSE`) and prints the two PowerShell commands to run,
`ollama create oldmanjohnnymorgan/rimchron-director-e4b -f Modelfile.publish` and
`ollama push oldmanjohnnymorgan/rimchron-director-e4b`. On ollama.com, give the model a short description: fine-tuned
from Gemma 4 E4B for Rim Chronicles' plot director, Apache 2.0, a link to the repository.

Check it as a player would: `ollama rm oldmanjohnnymorgan/rimchron-director-e4b`, then pick the preset in the mod
settings and press **Download model**.

A backup copy on Hugging Face is optional: upload the GGUF from `rc-models\director-e4b-war\` with the Apache licence
and a model card; Ollama can pull it as `hf.co/<user>/<repo>:Q4_K_M`.

## 4. Upload to the Workshop

1. Start RimWorld with only Harmony and the packaged Rim Chronicles (`release\RimChronicles` copied into `Mods`).
2. **Mods** → select Rim Chronicles → **Upload to Steam Workshop**. Accept the agreement.
3. The upload writes `About\PublishedFileId.txt` into the copy in `Mods`. **Copy it back into the repository's
   `About\` and commit it**: every later upload must carry it, or Steam makes a second Workshop item.
4. On the Workshop page: the description (from `About.xml`), the requirements (Harmony; Royalty, and Biotech for the
   Hollow Choir; Ollama for narration), a link to the repository, and set visibility to public.
5. Tag the commit: `git tag v<modVersion>`, `git push origin v<modVersion>`.

## What players install

The mod can't bundle Ollama (a separate app that manages the GPU) or the models (several GB). The README's
[Install](../README.md#install) section is the player's guide: Ollama from [ollama.com](https://ollama.com/download),
then **Download model** in the mod settings.

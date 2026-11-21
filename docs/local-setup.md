# Local machine setup

This sets up a Windows PC to run the Rim Chronicles model evaluation: the GPU driver, the tools, the code, Ollama with its models, and the API key. It ends with a check that tells you everything is ready. After that, `docs/stage0-runbook.md` covers running the evaluation itself.

Allow about an hour, plus download time. Every command here is for **PowerShell** (Start menu → "Terminal" or "PowerShell"). When a step says *open a new terminal*, close the window and open a fresh one, so it picks up newly installed programs and settings.

## What you need

| | Needed |
| --- | --- |
| Windows | 10 or 11, 64-bit |
| GPU | NVIDIA RTX 5060 Ti, 16 GB (the reference card) |
| Memory | 16 GB RAM at least; 32 GB is more comfortable |
| Disk | About 25 GB free, mostly for the models |
| Accounts | Access to the `OldmanJohnnyMorgan/RimChronicles` repo on GitHub, and the Anthropic API key |

## 1. GPU driver

1. Update the NVIDIA driver with the NVIDIA app (Drivers → install the latest). Any recent driver that supports the RTX 50 series works, and the newest is best.
2. Check that Windows sees the card and all 16 GB:
   ```powershell
   nvidia-smi
   ```
   The table should show the RTX 5060 Ti with about 16,300 MiB of memory.

## 2. Tools

Install Git, the .NET 10 SDK and Ollama with winget:

```powershell
winget install Git.Git
winget install Microsoft.DotNet.SDK.10
winget install Ollama.Ollama
```

If `winget` isn't recognised, install **App Installer** from the Microsoft Store first. If an install fails, each tool also has an ordinary installer: git-scm.com, dotnet.microsoft.com and ollama.com.

Open a new terminal and check that all three are there:

```powershell
git --version
dotnet --version    # should start with 10.
ollama --version
```

## 3. The code

Put the repo in a folder outside OneDrive, because OneDrive syncing slows builds and can lock files mid-run. `C:\dev` works well:

```powershell
mkdir C:\dev -Force
cd C:\dev
git clone https://github.com/OldmanJohnnyMorgan/RimChronicles.git
cd RimChronicles
```

If Git asks you to sign in, a browser window opens; sign in with your GitHub account.

Build it and run the tests. Neither needs the GPU or any model:

```powershell
dotnet build RimChronicles.slnx
dotnet test RimChronicles.slnx
dotnet run --project harness/RimChronicles.Eval -- validate
```

The tests should all pass, and `validate` should end with `OK`. If either fails, stop here and send me the output.

## 4. Ollama

Ollama runs the three local models (all Gemma 4). The fourth, the Claude reference, runs in the cloud.

**Settings.** Two settings matter for fair results:

- **One model in memory at a time**, so each model's VRAM reading is its own and models don't compete for the GPU.
- **A context window of 8,192 tokens.** Our largest prompts (the High tier's world summary plus two earlier bulletins) are about 3,500 tokens with the answer. If Ollama's default window is smaller, it cuts prompts off without saying so, and the results would be quietly wrong.

```powershell
[Environment]::SetEnvironmentVariable("OLLAMA_MAX_LOADED_MODELS", "1", "User")
[Environment]::SetEnvironmentVariable("OLLAMA_CONTEXT_LENGTH", "8192", "User")
```

To keep the models on another drive, also set `OLLAMA_MODELS` to a folder there, for example `D:\ollama-models`.

Now restart Ollama so it reads the settings: right-click the llama icon in the system tray → **Quit Ollama**, then start **Ollama** from the Start menu. Check that it's running:

```powershell
curl.exe http://localhost:11434/api/version
```

That prints a version number. An error means Ollama isn't running.

**Models.** Pull the three models in `harness/models.json`. This is the big download:

```powershell
ollama pull gemma4:e2b
ollama pull gemma4:e4b
ollama pull gemma4:12b
```

If a pull says the model doesn't exist, look the model up on ollama.com/library, pull it under the name shown there, and change that model's `"model"` value in `harness/models.json` to match.

The Qwen models were dropped from the evaluation after the trial rounds. If you pulled them earlier, `ollama rm qwen3.5:2b qwen3.5:4b qwen3:14b` frees their disk space.

**Check the GPU is used.** Run one model briefly, then list what's loaded:

```powershell
ollama run gemma4:e4b "Say hello in five words."
ollama ps
```

The `PROCESSOR` column should say `100% GPU`. If it shows any CPU share, see Troubleshooting.

## 5. API key

The judge and the Claude reference model use the Anthropic API key. Store it as a user environment variable named `RIMCHRONICLES_ANTHROPIC_API_KEY`:

```powershell
[Environment]::SetEnvironmentVariable("RIMCHRONICLES_ANTHROPIC_API_KEY", (Read-Host "Paste the key"), "User")
```

Don't use the name `ANTHROPIC_API_KEY`. Claude Code reads that name too, and it would stop using your claude.ai sign-in.

Open a new terminal and check it's set, without printing it:

```powershell
if ($env:RIMCHRONICLES_ANTHROPIC_API_KEY) { "set" } else { "missing" }
```

## 6. Final check

With Ollama running:

```powershell
cd C:\dev\RimChronicles
dotnet run --project harness/RimChronicles.Eval -- check
```

`check` makes two small calls per model and prints a table with each model's server, quantization, VRAM and test calls. It costs a few cents for the two Claude calls.

- It ends with **Ready** when every model answered. Read any `WARNING` lines (`docs/stage0-runbook.md` explains each one), then go on to the runbook.
- Every `PROBLEM` line needs fixing first. Troubleshooting below covers the usual ones.

Send me the full output of `check` the first time you run it. It's the quickest way to catch a wrong model name or a thinking setting the server ignores.

## Troubleshooting

| What you see | What to do |
| --- | --- |
| `dotnet`, `git` or `ollama` is not recognised | Open a new terminal. If it still fails, rerun that tool's install. |
| `No answer from http://localhost:11434` | Ollama isn't running: start it from the Start menu. |
| `model '…' is not on the server` | Pull it (step 4), or set `"model"` in `harness/models.json` to the name `check` lists as similar. |
| `ollama ps` shows CPU, or `check` warns *runs on the CPU* | Close other programs using the GPU (games, browsers with many tabs), make sure the driver is current, and restart Ollama. If one model still doesn't fit, pull a smaller quantization of it. |
| `Output cut off by the token limit` with *spent the limit thinking* | The model reasoned until it ran out of tokens. Make sure you have the latest code (`git pull`): every local model has `"thinking": "Off"` with `"reasoning_effort": "none"` in `harness/models.json`. If it still happens, send me the `check` output. |
| `check` says the key variable is not set | Redo step 5, then open a new terminal. |

## Later: Raon-1 nano

Raon-1 nano is not part of the current evaluation. These steps are kept for when we test it. It only runs on Krafton's fork of llama.cpp, which you build yourself (until llama.cpp PR #32200 merges and Ollama can run it).

**Build tools** (about 10 GB):

1. **Visual Studio 2022 Build Tools**: from visualstudio.microsoft.com → Tools for Visual Studio → Build Tools. In the installer, tick the **Desktop development with C++** workload.
2. **CMake**: `winget install Kitware.CMake`
3. **CUDA Toolkit 12.8 or newer**, from developer.nvidia.com/cuda-downloads. The RTX 50 series needs 12.8 at least; older toolkits can't build for it.

Open a new terminal after installing these.

**Build the server.** Clone the fork from the link in Krafton's Raon-1 release (not the main llama.cpp repo), then:

```powershell
cd C:\dev
git clone <Krafton's llama.cpp fork URL> llama.cpp-krafton
cd llama.cpp-krafton
cmake -B build -DGGML_CUDA=ON
cmake --build build --config Release -j
```

This takes a while. The server ends up at `build\bin\Release\llama-server.exe`. If the fork's README gives different build steps, follow the README.

**Get the model.** Download Raon-1 nano as a `.gguf` file from Krafton's release. It's small, so take the `Q8_0` version if there's a choice, for the best quality.

**Start the server.** `--alias` makes it report the name that `harness/models.json` expects:

```powershell
.\build\bin\Release\llama-server.exe -m <path to the .gguf file> --port 8081 -ngl 99 -c 8192 --alias raon-1-nano
```

Leave this window open whenever you run the evaluation. In another terminal, check it answers:

```powershell
curl.exe http://localhost:8081/v1/models
```

**Add it to the evaluation.** Put its entry back into `"models"` in `harness/models.json` (the comment at the top of that file has it), then run `check` with the server running.

| What you see | What to do |
| --- | --- |
| `No answer from http://localhost:8081` | The Raon-1 nano server isn't running: start it as above. |
| Port 8081 is already in use | Start the server with another `--port`, and change its `"endpoint"` in `harness/models.json` to match. |
| The CUDA build fails | Check the CUDA Toolkit is 12.8 or newer and that the C++ workload is installed, then build again from a new terminal. |

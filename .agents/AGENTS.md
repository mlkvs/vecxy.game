# Vecxy Game Agent Guide

## Purpose

This repository contains the Vecxy C# game engine and the HardCore.Cultivation game.
Keep AI-specific working material under `.agents/`. The root `AGENTS.md` is only a
discovery pointer for tools that load instructions from the repository root.

## Repository Layout

- `Engine/Vecxy/Code/`: engine projects. Make engine changes here only when the
  requested behavior belongs in the reusable engine rather than the game.
- `HardCore.Cultivation/`: the game application, assets, gameplay code, and build
  configuration.
- `HardCore.Cultivation.Tests/`, `Engine/Vecxy/Code/Vecxy.UI.Tests/`, and
  `Sandbox/Tests/`: executable test projects.
- `Docs/`: project documentation. Read the relevant document before changing a
  documented subsystem.
- `build`: Bash entry point for Android and desktop publishing. See
  `Docs/android-build.md` before packaging.

## Platform and Tooling

- C# targets .NET 10 with nullable reference types and implicit usings enabled.
- `global.json` pins SDK `10.0.110` and sets `rollForward` to `disable`. Verify that
  exact SDK is installed before running `dotnet` commands. Do not change the pin
  merely to make a local command run unless the task explicitly requests an SDK
  update.
- The game supports Android and desktop builds. Do not run release packaging,
  publish commands, or commands that alter signing material unless requested.

## Working Conventions

- Inspect the nearest relevant code, tests, and documentation before editing.
- Keep changes focused; do not reformat or refactor unrelated code.
- Preserve the existing code style and project boundaries. Add a reusable engine
  API only when the behavior is not game-specific.
- Prefer updating or adding focused tests alongside behavioral changes. Test the
  narrowest affected project first.
- Treat XML, CSS, YAML, shaders, and other assets as source. Preserve their
  formatting and validate paths/casing used by runtime asset loading.
- For Vecxy UI, consult `Engine/Vecxy/Code/Vecxy.UI/README.md`. XML reload can
  replace UI elements, so bind event handlers again through `UiDocument.Reloaded`.
- Avoid storing generated output, temporary investigation files, agent notes, or
  plans outside `.agents/`.

## Verification

After the required SDK is available, use the smallest applicable command:

```powershell
dotnet build <project-path>
dotnet run --project HardCore.Cultivation.Tests/HardCore.Cultivation.Tests.csproj
dotnet run --project Engine/Vecxy/Code/Vecxy.UI.Tests/Vecxy.UI.Tests.csproj
dotnet run --project Sandbox/Tests/Tests.csproj
```

For cross-project changes, build the solution:

```powershell
dotnet build vecxy.game.sln
```

Report verification that could not run, including a missing pinned SDK or platform
dependency. Do not claim a test passed if it was not executed.

## Sensitive Material

- Do not print, copy, commit, rotate, or expose credentials, API keys, signing
  passwords, or keystores.
- `HardCore.Cultivation/Assets/Configs/Build.yaml` and `Analytics.yaml` may hold
  sensitive values. Read only the minimum necessary to complete the task and do
  not include their values in output.
- `Signing/Keystores/` and release artifacts require explicit user intent before
  modification or packaging.

## AI Workspace

- Keep shared agent guidance in `.agents/`.
- Put task plans in `.agents/plans/` and durable research notes in `.agents/notes/`
  when they are useful to future work.
- Keep machine-local or disposable agent state under `.agents/local/`; it is
  ignored by Git.
- Do not overwrite user-authored files or revert unrelated working-tree changes.

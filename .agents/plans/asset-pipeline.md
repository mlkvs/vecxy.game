# Vecxy Asset Pipeline

1. Extend `Vecxy.Assets` with typed ID handles, manifest loading, handle-based loading,
   missing-asset reporting, and watcher-driven reload while preserving path APIs.
2. Add a reusable `Vecxy.AssetPipeline` tool library for deterministic manifest scan,
   content-based rename reconciliation, C# generation, reference analysis, and validation.
3. Add the submodule-local `tools/Vecxy.Cli` entry point with `assets scan`,
   `assets generate`, `assets validate`, and the ordered `build` pipeline.
4. Add a Roslyn analyzer project which records generated asset-property references in
   `obj/vecxy.asset.references.json`, and make CLI analysis use the same reference model.
5. Add focused executable tests for manifest creation, missing files, rename stability,
   generated code, and run the narrow projects plus solution validation where practical.
6. Add portable `vecxy.cmd` / `vecxy.sh` wrappers and concise integration documentation.

Compatibility decisions:

- IDs are GUID-backed because the engine already exposes `AssetId(Guid)`; generated
  symbolic property names replace handwritten paths.
- Rename stability is achieved without `.meta` files by persisting a SHA-256 content
  fingerprint in `Assets.manifest` and reconciling unmatched old/new entries by hash.
- Existing `Load<T>(string)` remains supported; typed handles are additive.

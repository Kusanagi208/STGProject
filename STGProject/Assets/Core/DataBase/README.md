# GenjitsuLAB.Data

Unity 6000.3.11f1 authoring module. Runtime stays in GenjitsuLAB.Core; Editor tools live in GenjitsuLAB.Data.Editor.
The module uses Unity's native Inspector and EditorWindow APIs and has no Odin dependency.

## Quick start

1. Open Samples/Weapons.asset or Enemies.asset. Select a row to edit its draft inline, then Apply Record.
2. EnemyData.m_weapon is a WeaponData key dropdown. The label and stored key are independent.
3. Select both sample database assets and use GenjitsuLAB > Data > Import Selected Databases.
4. Load Weapon.csv for Weapons and Enemy.csv for Enemies; review ADD / UPDATE / KEEP, then Apply Preview.
5. Undo/Redo applies to the whole batch. Discard Preview destroys only temporary drafts.
6. Runtime code references Samples/Catalog.asset (or a project-specific catalog), calls Initialize once and Shutdown when the owner exits.

```csharp
catalog.Initialize();
if (Database<EnemyData>.TryGet("enemy_001", out EnemyData enemy) &&
    enemy.Weapon.TryResolve(out WeaponData weapon))
{
    // Use immutable authored settings; keep current health / cooldown in gameplay state.
}
catalog.Shutdown();
```

Catalog initialization replaces the active database set. Reinitializing does not accumulate records.
Shutdown from an older catalog does not clear indexes owned by a newer catalog.
Standalone Database<T>.Initialize(records) replaces only that type.
Runtime indexes use GenjitsuLAB.Core.Repository<string, T>. Reflection/CSV/networking are editor-only.

## Add a new schema

Create a concrete Data subclass, a Repository<T> subclass with CreateAssetMenu, and a concrete
[Serializable] DataReference<T> subclass for typed reference fields.
Use public fields or [SerializeField] private m_ fields. Include the repository in your catalog.
The inherited key is case-sensitive and unique across all persisted repositories of that concrete type.
Repository assets contain their records as ScriptableObject sub-assets. Empty repositories are valid.

## Google Sheets

Each repository accepts a public Spreadsheet ID and numeric worksheet gid.
Use anonymous-read test data; the sample assets intentionally contain no live ID.
Open Google Sheet opens its worksheet. Download and Preview fetches CSV with a 30-second timeout.
For multiple selected repositories every download finishes before combined reference validation.
HTTP failures, HTML/login pages and cancellation never apply partial data.
The window releases its requests and callbacks on close/reload.
Cloud upload, OAuth and Sheet-to-C# generation are not part of this version.

Set key and reference-key columns to plain text in Sheets to preserve values such as 001.
First row: exact C# serialized field names (including m_ prefixes).
Use key, m_titlePath and m_comment for identity and labels.
Supported: strings, bool, integer types, float/double, enums, Vector2/3/4, concrete DataReference,
and one-dimensional arrays of those types. Vectors use comma-separated invariant components.
Arrays use semicolons within a single CSV cell; elements containing semicolons and a sole empty-string
element cannot round-trip and export reports them instead of silently corrupting them.
CSV quotes, doubled quotes, embedded newlines, LF/CRLF, UTF-8 BOM and empty cells are supported.

Missing columns preserve old values; new records keep C# defaults.
Present empty string/reference/array cells clear the value; empty numbers/enums are errors.
Unity Object fields are Inspector-owned and omitted on export; including one as a CSV column is rejected.
Other unsupported supplied columns are rejected.
Absent rows are retained and listed as KEEP.
Updates preserve the original sub-asset local file ID and any fields not supplied by the CSV.
A stale preview is rejected if the destination or an original record changed before Apply.
Renaming/deleting a key with inbound database references is blocked and lists the referring records.
The module validates database references; arbitrary string keys held outside database records are not tracked.

## Verification

Editor tests: GenjitsuLAB.Data.Editor.Tests.
Includes CSV edge cases, combined forward references, duplicate/invalid keys, no-mutation failures,
sub-asset identity, Undo/Redo, catalog restart and 10,000 allocation-free lookups.
The Play Mode test opens an empty scene and uses the sample catalog.
BuildValidationPlayer is an executeMethod entry point intended for an isolated project copy; it builds
the existing Main scene to DatabaseValidationBuild/STG.exe.
DatabaseBuildValidation rejects invalid keys/references in player builds.
No STG tuning or gameplay has been migrated into the samples.

Official references:
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.AddObjectToAsset.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Undo.html
- https://developers.google.com/chart/interactive/docs/spreadsheets

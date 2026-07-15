# ClassicUO.UltimaSdk

An **Ultima Online data file reader library**, extracted from the [ClassicUO](https://github.com/ClassicUO/ClassicUO) project. It provides two API surfaces under one roof:

| API | Namespace | Purpose |
|-----|-----------|---------|
| **ClassicUO** | `ClassicUO.*` | The modern, low-level file-reading API (loaders, I/O, utilities) |
| **UltimaSDK** | `Ultima.*` | A legacy-compatible adapter layer matching the old UltimaSDK conventions |

Originally developed as part of the [ClassicUO](https://github.com/ClassicUO/ClassicUO) game client — a C# implementation of the Ultima Online client — this library was extracted so it can be reused across UO tooling projects without depending on the full game client.

---

## Credits

This library is adapted from **ClassicUO**, created and maintained by the ClassicUO team. The vast majority of the file-reading code (IO layer, loaders, decompression, utility helpers) originates from their project.

- **ClassicUO Repository**: [github.com/ClassicUO/ClassicUO](https://github.com/ClassicUO/ClassicUO)
- **License**: MIT (same as the upstream project)

We are grateful for their excellent work. Without ClassicUO, this library would not exist.

---

## Why Two APIs?

### `ClassicUO.*` — The Primary API

The `ClassicUO.Assets`, `ClassicUO.IO`, and `ClassicUO.Utility` namespaces contain the direct code from ClassicUO. This is the recommended API for new code:

- **Modern, type-safe** — exposes `UOFileManager`, individual loaders (`ArtLoader`, `MapLoader`, `TileDataLoader`, etc.), and efficient I/O primitives
- **Full fidelity** — access to all file formats, compression types, and metadata that ClassicUO supports
- **Self-documenting** — the code matches the ClassicUO codebase, so any ClassicUO developer or documentation applies here too

### `Ultima.*` — The Compatibility Adapter

The `Ultima.*` namespace provides a higher-level convenience API modeled after the venerable **UltimaSDK** (the de facto standard UO library used by Razor, UOSteam, and many other tools).

This adapter exists because:

1. **Backward compatibility** — existing code written against `Ultima.Map`, `Ultima.Files`, `Ultima.TileData`, etc. works with minimal changes
2. **Simpler surface** — one-liners like `TileData.LandTable[id]` or `Map.Felucca.Tiles.GetLandTile(x, y)` replace multi-step loader setup
3. **Migration path** — projects can start with the familiar `Ultima.*` API and gradually adopt `ClassicUO.*` where they need more control

Neither API is deprecated. Use whichever fits your needs, or mix both in the same project.

---

## Usage

Both APIs are served from the same assembly: `ClassicUO.UltimaSdk.dll`.

### Initialization (required)

```csharp
using Ultima;

// Point to your UO client files directory
Ultima.Files.Initialize(@"C:\Program Files\Ultima Online", ClassicUO.Utility.ClientVersion.CV_7000);

// Load all data files
Ultima.Files.Load();
```

### ClassicUO API

```csharp
using ClassicUO.Assets;

// Access the file manager
var manager = UOFileManager.Instance;

// Load art directly
var artInfo = manager.Arts.GetArt(0x0F00);
// artInfo.Pixels, artInfo.Width, artInfo.Height

// Load tile data
var landData = manager.TileData.LandData;
var staticData = manager.TileData.StaticData;

// Load map data
manager.Maps.LoadMap(0); // Felucca
// Access block indices, read raw cells
```

### UltimaSDK API

```csharp
using Ultima;

// Map data
var map = Map.Felucca;
var landTile = map.Tiles.GetLandTile(1450, 1670);
// landTile.Id, landTile.Z

var statics = map.Tiles.GetStaticTiles(1450, 1670);
// foreach (var st in statics) { st.Id, st.Hue, st.Z }

// Tile data lookup
var itemData = TileData.ItemTable[0x0F00];
bool isImpassable = (itemData.Flags & TileFlag.Impassable) != 0;
bool isSurface = itemData.Surface;
int height = itemData.Height;

var landData = TileData.LandTable[0x01];
bool isWet = (landData.Flags & TileFlag.Wet) != 0;

// Art
var bmp = Art.GetStatic(0x0F00);

// Hues
var hue = Hues.GetHue(1150);
uint color = hue.GetColorRgba(10);

// Multi components
var multi = Multis.GetComponents(0x4000);
// multi.SortedTiles, multi.Min, multi.Max

// Skills
var skill = Skills.GetSkill(12); // Anatomy
// skill.Name, skill.Index, skill.IsAction

// Cliloc strings
var cliloc = new StringList("enu", false);
string text = cliloc.GetString(1044060); // "Alchemy"
```

---

## Target Frameworks

- **.NET Framework 4.7.2** (`net472`)
- **.NET 10.0** (`net10.0`)

Windows-only APIs (System.Drawing interop) in the `Ultima.*` adapter are suppressed on non-Windows runtimes.

---

## Building

```bash
# Build for .NET 10
dotnet build -c Release -p:TargetFramework=net10.0

# Build for .NET Framework 4.7.2
dotnet build -c Release -p:TargetFramework=net472

# Build all targets
dotnet build -c Release
```

---

## Project Structure

```
ClassicUO.UltimaSdk/
├── IO/                # Low-level file I/O (UOFile, UOFileIndex, readers)
├── Loaders/           # Asset loaders (Art, Map, TileData, Hues, Gumps, etc.)
├── Utility/           # Zip, BWT decompress, client version, platform helpers, logging
├── UltimaAdapter/     # Legacy UltimaSDK-compatible wrapper API
│   ├── UltimaFiles.cs     # File management (entry point for both APIs)
│   ├── UltimaMap.cs       # Map.Felucca, Map.Trammel, TileMatrix
│   ├── UltimaTileData.cs  # TileData.LandTable, TileData.ItemTable
│   ├── UltimaArt.cs       # Art.GetStatic (bitmap decoding)
│   ├── UltimaHues.cs      # Hues.GetHue
│   ├── UltimaMultis.cs    # Multis.GetComponents
│   ├── UltimaSkills.cs    # Skills.GetSkill
│   ├── UltimaStringList.cs # Cliloc string loading
│   ├── UltimaGumps.cs     # Gumps index validation
│   ├── UltimaTileFlag.cs  # TileFlag enum
│   ├── UltimaTileTypes.cs # Tile / HuedTile structs
│   └── UltimaZLibManaged.cs
├── ClassicUO.UltimaSdk.csproj
├── ClassicUO.UltimaSdk.sln
└── Directory.Packages.props
```

---

## Updating from Upstream

The IO, Loaders, and Utility directories track the upstream [ClassicUO/ClassicUO](https://github.com/ClassicUO/ClassicUO) source closely. When syncing, cherry-pick relevant changes and apply the dual-targeting guards documented in `AGENTS.md`.

### Sync History

| Date | Upstream Commit | Key Changes |
|------|----------------|-------------|
| 2026-07-15 | `5a5cfd350` (HEAD) | **Initial sync after extraction from NeoUO fork** — synced IO, Loaders, and Utility files to align with upstream. Cherry-picked: `c34abad08` (multi reading fix), `ffc3d509e` (BWT decompress rewrite), upstream removal of `NETFRAMEWORK` conditionals and `SetForceManagedZlib`, UOFileManager constructor overload with `UOFilesOverrideMap`, `Verdata.Load()` call, case-insensitive file path for non-Windows, style alignment (`var`, block bodies). Added net472 dual-targeting guards for `[InlineArray]`, `Encoding.GetString(Span)`, `Unsafe.SkipInit`, `Span<T>.Sort()`. |

### Next Sync Steps

1. Clone the upstream: `git clone git@github.com:ClassicUO/ClassicUO.git`
2. Generate diffs: `git diff --no-index ClassicUO.UltimaSdk/IO upstream/ClassicUO.IO/` (repeat for Loaders, Utility)
3. Apply substantive changes (skip pure style changes unless aligning)
4. Add `#if NETFRAMEWORK` guards for any new uses of: `[InlineArray]`, `Encoding.GetString(Span)`, `Unsafe.SkipInit`, `Span<T>.Sort()`, `BinaryReader.Read(Span)`
5. Update this table with the new sync date and upstream commit hash
6. Update `AGENTS.md` if new compatibility patterns are needed

---

## License

MIT. See [LICENSE](LICENSE) (if applicable) or refer to the ClassicUO project's license for the portions derived from their codebase.

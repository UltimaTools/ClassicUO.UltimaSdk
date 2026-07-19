# AGENTS.md — ClassicUO.UltimaSdk

## Project Overview

Standalone Ultima Online data file reader library extracted from [ClassicUO](https://github.com/ClassicUO/ClassicUO). Dual-targets `net472` (.NET Framework 4.7.2) and `net10.0`. Provides two API surfaces: `ClassicUO.*` (modern loaders/IO) and `Ultima.*` (legacy compatibility adapter).

## Build Commands

```bash
dotnet build -c Release -p:TargetFramework=net472   # net472 Release
dotnet build -c Release -p:TargetFramework=net10.0  # net10 Release
dotnet build -c Release                              # all TFMs
```

## Upstream Tracking

This repo tracks `src/ClassicUO.IO/`, `src/ClassicUO.Assets/`, and `src/ClassicUO.Utility/` from the upstream ClassicUO repo. The upstream targets modern .NET only and uses C# 12+ features not available on net472. The `UltimaAdapter/` files are NeoUO-specific and have no upstream counterpart.

## Dual-Targeting Strategy (net472 ↔ net10)

The upstream ClassicUO code uses modern .NET APIs and C# 12 features that don't exist on .NET Framework 4.7.2. We deal with this through several patterns:

### 1. Polyfills (`Polyfills.cs`)

When the upstream uses compiler-recognized attributes that don't exist on net472, we provide them under `#if NETFRAMEWORK`:

| Attribute | Needed For | Notes |
|-----------|-----------|-------|
| `System.Runtime.CompilerServices.InlineArrayAttribute` | `[InlineArray(N)]` | The compiler recognizes this attribute but the class doesn't exist on net472. Polyfill is sufficient for compilation—but `[InlineArray]` still generates IL that net472 can't execute (CS9171). See pattern #2. |
| `System.Diagnostics.CodeAnalysis.NotNullAttribute` | `[return: NotNull]` | Nullable analysis attribute. Since the project has `<Nullable>disable</Nullable>`, this attribute is decoration only, but the type must exist. |

### 2. Inline Array Structs (`#if NETFRAMEWORK` with `StructLayout(Size=...)`)

`[InlineArray(N)]` generates IL that requires .NET 8+ runtime support. On net472, the attribute class exists (from polyfill) but the runtime rejects the IL (CS9171).

**Pattern used in**: `HuesLoader.cs`, `MapLoader.cs`, `TileDataLoader.cs`

```csharp
#if NETFRAMEWORK
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct ColorTableArray
    {
        private ushort _a0;

        public unsafe ref ushort this[int index]
        {
            get
            {
                fixed (ColorTableArray* p = &this)
                    return ref ((ushort*)p)[index];
            }
        }
    }
#else
    [InlineArray(32)]
    public struct ColorTableArray
    {
        private ushort _a0;
    }
#endif
```

**Size calculation**: `N * sizeof(elementType)`. For struct elements (e.g., `HuesBlock` in `HuesBlockArray`), compute the total packed size using `StructLayout(LayoutKind.Sequential, Pack = 1)`.

Known sizes (for reference):
- `ColorTableArray`: 32 × ushort(2) = **64**
- `HuesBlockArray`: 8 × HuesBlock(88) = **704** (HuesBlock = ColorTableArray(64) + TableStart(2) + TableEnd(2) + Name(20))
- `MapCellsArray`: 64 × MapCells(3) = **192**
- `LandTilesOldArray32`: 32 × LandTilesOld(26) = **832**
- `LandTilesNewArray32`: 32 × LandTilesNew(30) = **960**
- `StaticTilesOldArray32`: 32 × StaticTilesOld(37) = **1184**
- `StaticTilesNewArray32`: 32 × StaticTilesNew(41) = **1312**
- `BufferArray20`: 20 × byte(1) = **20**

**Important**: The `net472` fallback **must** also add an indexer (`public unsafe ref T this[int index]`) because `StructLayout(Size=N)` doesn't provide automatic element access. Without it, `hues.Entries[i]` fails with CS0021.

### 3. `Encoding.GetString(Span<byte>)` vs `byte[]`

.NET Framework's `Encoding.GetString()` does not accept `ReadOnlySpan<byte>`. The upstream uses span overloads exclusively.

**Pattern used in**: `UOFileManager.cs`, `SkillsLoader.cs`, `TileDataLoader.cs`

```csharp
#if NETFRAMEWORK
var name = Encoding.ASCII.GetString(buf, 0, entry.Length - 1).TrimEnd('\0');
#else
var name = Encoding.ASCII.GetString(buf.AsSpan(0, entry.Length - 1)).TrimEnd('\0');
#endif
```

For `Encoding.UTF8.GetString(Span<byte>)`:
```csharp
#if NETFRAMEWORK
var name = Encoding.UTF8.GetString(buf.ToArray()).Trim('\0');
#else
var name = Encoding.UTF8.GetString(buf).Trim('\0');
#endif
```

### 4. `Unsafe.SkipInit<T>(out T)` → `default(T)`

`System.Runtime.CompilerServices.Unsafe.SkipInit` is available on net472 via the `System.Runtime.CompilerServices.Unsafe` NuGet package, but the package version we use may predate the `SkipInit` API. The fallback is `T v = default;`.

**Pattern used in**: `FileReader.cs`, `StackDataReader.cs`

```csharp
#if NETFRAMEWORK
T v = default;
#else
Unsafe.SkipInit(out T v);
#endif
```

Note: The `System.Runtime.CompilerServices.Unsafe` package (`Unsafe.SizeOf`, `Unsafe.As`, etc.) IS available and works on net472. Only `SkipInit` may be missing.

### 5. `Span<T>.Sort()` → Manual Sort

`MemoryExtensions.Sort(Span<T>)` was added in .NET 5+. On net472, we fall back to a simple bubble sort.

**Pattern used in**: `BwtDecompress.cs`

```csharp
#if NETFRAMEWORK
SpanSort(table);
#else
table.Sort();
#endif
```

The `SpanSort` method is defined inside the same `#if NETFRAMEWORK` block:
```csharp
#if NETFRAMEWORK
static void SpanSort(Span<ushort> span)
{
    for (int i = 0; i < span.Length - 1; i++)
        for (int j = i + 1; j < span.Length; j++)
            if (span[j] < span[i])
            {
                ushort tmp = span[i];
                span[i] = span[j];
                span[j] = tmp;
            }
}
#endif
```

### 6. `Read(Span<byte>)` overload on `BinaryReader`

Upstream `FileReader.cs` replaced the `#if NETFRAMEWORK` branching with a single expression-bodied `Read(Span<byte>)` using `Reader.Read(buffer)`, which only compiles on .NET Core/.NET 5+ because `BinaryReader.Read(Span<byte>)` was added in .NET Core 2.1.

**Pattern used in**: `FileReader.cs`

```csharp
#if NETFRAMEWORK
public int Read(Span<byte> buffer)
{
    byte[] tmp = new byte[buffer.Length];
    int read = Reader.Read(tmp, 0, buffer.Length);
    tmp.AsSpan(0, read).CopyTo(buffer);
    _position += read;
    return read;
}
#else
public int Read(Span<byte> buffer) { _position += buffer.Length; return Reader.Read(buffer); }
#endif
```

### 7. Cherry-Picked Changes from Upstream (Non-Dependency)

Some upstream changes were intentionally NOT synced because they introduce external dependencies inappropriate for a standalone SDK:

| Change | Skipped | Reason |
|--------|---------|--------|
| `HuesHelper.cs` MonoGame `Color` parameter | Kept `(byte r, byte g, byte b)` | Avoids `Microsoft.Xna.Framework` dependency |
| `StringHelper.cs` SDL3 clipboard | Not applied | Avoids `SDL3` dependency |
| `UOFileManager.cs` new loaders | Not synced | Types don't exist in this repo (AnimationsLoader, etc.) |

## Files with `#if NETFRAMEWORK` Guards

| File | What's Guarded |
|------|---------------|
| `Polyfills.cs` | `InlineArrayAttribute`, `NotNullAttribute` |
| `IO/FileReader.cs` | `Read(Span<byte>)`, `Unsafe.SkipInit` |
| `IO/StackDataReader.cs` | `Unsafe.SkipInit` |
| `Loaders/HuesLoader.cs` | `ColorTableArray`, `HuesBlockArray` (inline array structs) |
| `Loaders/MapLoader.cs` | `MapCellsArray` (inline array struct) |
| `Loaders/TileDataLoader.cs` | 5 inline array structs + `Encoding.UTF8.GetString(Span)` |
| `Loaders/SkillsLoader.cs` | `Encoding.ASCII.GetString(Span)` |
| `Loaders/UOFileManager.cs` | `Encoding.ASCII.GetString(Span)` (3 locations) |
| `Utility/BwtDecompress.cs` | `Span<ushort>.Sort()` → `SpanSort()` |
| `Utility/StringHelper.cs` | `IntToAbbreviatedString` negative number fix |

## Known Gotchas

- `<Nullable>disable</Nullable>` — do NOT enable nullable. `NotNullAttribute` polyfill works as decoration only.
- `LangVersion` must be `preview` because the upstream uses C# 12 features (collection expressions, `[InlineArray]`) that the compiler needs to parse even when producing net472 IL.
- `GenerateAssemblyInfo` and `GenerateTargetFrameworkAttribute` are `false` — hand-maintained `AssemblyInfo.cs` in `Properties/` (if it exists).
- The `UltimaAdapter/` files use a custom lightweight bitmap implementation in `Ultima.Drawing` instead of `System.Drawing.Common`, so the library works on `net472`, `net8.0-windows`, and `net10.0` without requiring GDI+ or `libgdiplus`.
- When syncing from upstream, check for **new** `[InlineArray]`, `Span<>.Sort()`, `Encoding.GetString(Span)`, and `Unsafe.SkipInit` usages. These are the most common compatibility breakers.
- The upstream occasionally changes struct sizes (e.g., adding a field). If you add an `[InlineArray]` struct, compute the Size value carefully and verify with `Unsafe.SizeOf<T>()` (or check the struct layout manually).
- `System.Buffers`, `System.Memory`, `System.Runtime.CompilerServices.Unsafe`, and `System.Numerics.Vectors` are managed via `Directory.Packages.props` for net472. `System.Drawing.Common` is only for net10.0 (adapter).

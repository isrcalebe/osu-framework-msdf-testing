# Msdf -- osu!framework MSDF Font Rendering Demo

## Project Context

Msdf is a solo demo/sample application built on `ppy.osu.Framework`, showcasing multi-channel signed distance field (MSDF) font rendering inside an osu!framework game. It follows the same project layout and coding conventions as osu!framework itself (`Directory.Build.props`, banned-API analyzer, `.editorconfig`), since it's built by/for that ecosystem.

The `tools/` directory vendors the native `msdfgen` and `msdf-atlas-gen` executables used to generate MSDF font atlases as part of the asset pipeline -- they are not referenced by the build, just invoked manually to (re)generate resources under `source/resources`.

## Tech Stack

- **.NET 10** / C# (`LangVersion: latest`)
- **ppy.osu.Framework** 2026.914.0 -- game framework (rendering, input, screens, dependency injection, config)
- **NUnit3TestAdapter** + osu.Framework's visual test browser -- testing (not xUnit; this is the osu!framework-standard approach)
- **Microsoft.CodeAnalysis.BannedApiAnalyzers** -- enforced via `source/analysis/banned-symbols.txt`
- **msdfgen** / **msdf-atlas-gen** (native tools in `tools/`) -- offline MSDF atlas/font generation

## Architecture

```
source/
  Msdf.sln
  Directory.Build.props        # shared C#/analyzer settings for every project
  analysis/
    banned-symbols.txt         # BannedApiAnalyzer symbol list
    gamekit.globalconfig       # analyzer severities (osu!framework style)
  game/                        # Msdf.Game -- the actual game logic (SDK: Microsoft.NET.Sdk)
    MsdfGameBase.cs            # bootstraps resources, fonts, config, storage
    MsdfGame.cs                # picks entry screen from LocalConfig, pushes onto ScreenStack
    Configuration/
      MsdfConfigManager.cs     # IniConfigManager<MsdfSetting>, backs msdf.ini
      Settings/                # MsdfSetting enum, ScreenEntryPoint enum
    Graphics/
      MsdfFont.cs              # font accessors used by screens
    Extensions/
      FontExtensions/          # extension helpers over font/text types
    Screens/
      MsdfScreen.cs            # base class for all screens (osu.Framework.Screens.Screen)
      EntryPointScreenA.cs     # sample screen
      EntryPointScreenB.cs     # sample screen
  resources/                   # Msdf.Game.Resources (netstandard2.1, embedded resources only)
    Textures/ Shaders/ Fonts/ Samples/ Tracks/
  platforms/
    desktop/                   # Msdf.Desktop -- Exe host, references game project only
  tests/
    game/                      # Msdf.Game.Tests -- WinExe, NUnit adapter, visual test browser
      Program.cs
      MsdfTestBrowser.cs
      Visual/
        MsdfTestScene.cs       # base class for visual test scenes
        Screens/
tools/
  msdfgen/                    # native msdfgen.exe (vendored, not built)
  msdf-atlas-gen/             # native msdf-atlas-gen.exe (vendored, not built)
```

### Screens, not pages or endpoints

Navigation is done via `ScreenStack.Push(...)` with `osu.Framework.Screens.Screen` subclasses, not routes/controllers. New user-facing content is a new `MsdfScreen` subclass under `Screens/`, added to the stack from `MsdfGame.load()` or pushed from another screen.

```csharp
// GOOD -- new screen following existing convention
public partial class MyNewScreen : MsdfScreen
{
    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = "...",
        });
    }
}
```

### Dependency loading

Use `[BackgroundDependencyLoader]` load methods and `DependencyContainer`/`CreateChildDependencies` overrides for DI, matching `MsdfGameBase`/`MsdfGame` -- not constructor injection, not `Microsoft.Extensions.DependencyInjection`.

### Config

New persisted settings go through `MsdfSetting` (enum) + `MsdfConfigManager.InitialiseDefaults()`, not raw file I/O or `IConfiguration`.

### Resources

Fonts/textures/shaders/samples/tracks are embedded resources in `Msdf.Game.Resources` and exposed to the game via `Resources.AddStore(new DllResourceStore(MsdfResourceAssemblyProvider.Assembly))` in `MsdfGameBase`. Add new asset files under the matching `resources/<Category>/` folder -- they're picked up by the existing globs in `Msdf.Game.Resources.csproj`.

## Coding Standards

These come from `source/Directory.Build.props`, `.editorconfig`, and `source/analysis/gamekit.globalconfig` -- don't deviate without checking those files first.

- **`Nullable: enable`**, but **`ImplicitUsings: disable`** -- every file needs explicit `using` statements
- **File-scoped namespaces** -- always (`namespace Msdf.Game.Screens;`)
- **PascalCase for fields too** -- `public`/`internal`/`protected` fields, properties, methods, and events are all PascalCase (osu!framework convention, not standard .NET); private members are `camelCase`
- **`partial` on framework `Drawable`/`Screen`/`Game` subclasses** -- matches existing `MsdfGame`, `MsdfGameBase`, `EntryPointScreenA`
- **No regions**
- **Comments explain "why", not "what"**
- **`IDE0055` (formatting), `IDE0005` (unused usings), `IDE1006` (naming) are build warnings** -- run `dotnet format` before considering work done

### Banned APIs (`source/analysis/banned-symbols.txt`)

Do not use:

- `object.Equals(...)` / `ValueType.Equals(...)` -- use `IEquatable<T>` or `EqualityComparer<T>.Default`
- Non-generic `IComparable` -- use the generic version
- `Enum.HasFlag` -- use `osu.Framework.Extensions.EnumExtensions.HasFlagFast<T>()`
- `Task.Wait()` / `Task<T>.Result` -- use `Task.WaitSafely()` / `Task.GetResultSafely()`
- `new Guid()` -- use `Guid.NewGuid()` or `Guid.Empty` explicitly
- `Assembly.GetEntryAssembly()` -- use `osu.Framework.RuntimeInfo.EntryAssembly`
- `ManualResetEventSlim.Wait()` without a timeout
- `UriKind.RelativeOrAbsolute` -- use `Validation.TryParseUri(string, out Uri?)`

## Testing

Tests live in `source/tests/game` and run through osu!framework's **visual test browser**, not plain unit tests:

- `MsdfTestBrowser` (in `Program.cs`) is the entry point that launches the browser UI
- New test scenes subclass `MsdfTestScene` under `Visual/Screens/`, following the pattern in `Visual/MsdfTestScene.cs`
- The project references `Microsoft.NET.Test.Sdk` + `NUnit3TestAdapter`, but most verification happens by visually inspecting a running scene, not asserting in code -- this is the standard osu!framework testing style and should stay that way

## Commands

```bash
# Build the desktop host (from source/)
dotnet build platforms/desktop -p:GenerateFullPaths=true -m -verbosity:m

# Run the desktop app
dotnet run --project source/platforms/desktop

# Launch the visual test browser
dotnet run --project source/tests/game

# Run NUnit tests
dotnet test source/tests/game

# Format check
dotnet format source/Msdf.sln --verify-no-changes

# Regenerate an MSDF atlas (example, see tools/msdf-atlas-gen/example.bat)
tools/msdf-atlas-gen/msdf-atlas-gen.exe -font <path-to-font> -type mtsdf -imageout atlas.png -json atlas.json
```

VS Code tasks (`.vscode/tasks.json`) and launch configs (`.vscode/launch.json`) already wrap the build/run/test commands above, including `OSU_SDL3=0`/`1` toggles for SDL2 vs SDL3 windowing.

## Workflow

- **Plan first** for anything touching more than one screen/project -- osu!framework's DI and lifecycle (`SetHost`, `CreateChildDependencies`, `[BackgroundDependencyLoader]`) has ordering pitfalls that are easy to get wrong.
- **Verify visually** -- for rendering/screen changes, run the visual test browser or the desktop app; `dotnet build`/`dotnet test` alone won't catch a broken layout or a missing glyph.
- **Match existing files before inventing new patterns** -- when adding a screen, config setting, or resource, copy the shape of the nearest existing example (`EntryPointScreenA`, `MsdfSetting`, `resources/Fonts`) rather than introducing a new convention.
- **Keep it solo-scale** -- no PR templates, no CI pipeline, no package publishing concerns unless asked for.

## Anti-patterns

Do NOT generate code that:

- Uses standard .NET DI (`IServiceCollection`, constructor injection) instead of osu!framework's `[BackgroundDependencyLoader]`/`DependencyContainer` pattern
- Uses `ASP.NET`, `IConfiguration`, or web-hosting abstractions -- this is a desktop game, not a web app
- Reads/writes settings via raw file I/O instead of `MsdfConfigManager`/`MsdfSetting`
- Adds `ImplicitUsings: enable` or removes explicit `using`s relied on elsewhere
- Introduces any of the banned APIs listed above
- Adds xUnit or converts the visual test browser to a plain assertion-only test suite
- Adds NuGet dependencies without checking whether osu!framework already exposes the needed functionality

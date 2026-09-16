using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using Msdf.Game.Configuration;
using Msdf.Game.Resources;

namespace Msdf.Game;

public abstract partial class MsdfGameBase : osu.Framework.Game
{
    private DependencyContainer? dependencies;

    protected MsdfConfigManager? LocalConfig { get; private set; }

    protected Storage? Storage { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        Resources.AddStore(new DllResourceStore(MsdfResourceAssemblyProvider.Assembly));

        addFonts();

        dependencies?.CacheAs(Storage);
        dependencies?.CacheAs(LocalConfig);
    }

    private void addFonts()
    {
        foreach (var weight in new[] { "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "Black" })
        {
            AddFont(Resources, $@"Fonts/Inter/Inter-{weight}");
            AddFont(Resources, $@"Fonts/Inter/Inter-{weight}Italic");
        }
    }

    public override void SetHost(GameHost host)
    {
        base.SetHost(host);

        Storage ??= host.Storage;
        LocalConfig = new MsdfConfigManager(Storage);
    }

    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
}

using Msdf.Game.Configuration;
using Msdf.Game.Graphics;
using Msdf.Game.Resources;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace Msdf.Game;

public abstract partial class MsdfGameBase : osu.Framework.Game
{
    private DependencyContainer? dependencies;

    private MsdfFontStoreCache? msdfFontStoreCache;

    protected MsdfConfigManager? LocalConfig { get; private set; }

    protected Storage? Storage { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        Resources.AddStore(new DllResourceStore(MsdfResourceAssemblyProvider.Assembly));

        addFonts();

        dependencies?.CacheAs(Storage);
        dependencies?.CacheAs(LocalConfig);
        dependencies?.CacheAs(msdfFontStoreCache = new MsdfFontStoreCache());
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        msdfFontStoreCache?.Dispose();
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

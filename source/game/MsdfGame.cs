using osu.Framework.Screens;
using Msdf.Game.Configuration.Settings;
using Msdf.Game.Screens;

namespace Msdf.Game;

public partial class MsdfGame : MsdfGameBase
{
    private DependencyContainer? dependencies;

    private ScreenStack? screens;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(screens = new ScreenStack());

        var entryPoint = LocalConfig?.Get<ScreenEntryPoint>(MsdfSetting.ScreenEntryPoint);

        switch (entryPoint)
        {
            case ScreenEntryPoint.ScreenA:
                screens.Push(new EntryPointScreenA());
                break;

            case ScreenEntryPoint.ScreenB:
                screens.Push(new EntryPointScreenB());
                break;

            default:
                screens.Push(new EntryPointScreenA());
                break;
        }
    }

    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
}

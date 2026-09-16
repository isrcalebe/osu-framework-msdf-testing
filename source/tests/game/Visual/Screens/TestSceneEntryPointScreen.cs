using osu.Framework.Screens;
using Msdf.Game.Screens;

namespace Msdf.Game.Tests.Visual.Screens;

public partial class TestSceneEntryPointScreen : MsdfTestScene
{
    private ScreenStack? screens;

    [BackgroundDependencyLoader]
    private void load()
    {
        Child = screens = new ScreenStack();

        AddStep("push screen a", () => screens.Push(new EntryPointScreenA()));
        AddStep("push screen b", () => screens.Push(new EntryPointScreenB()));
    }
}

using osu.Framework.Graphics.Sprites;
using Msdf.Game.Graphics;

namespace Msdf.Game.Screens;

public partial class EntryPointScreenB : MsdfScreen
{
    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = "Screen B",
            Font = MsdfFont.Roboto.With(size: 48.0f),
        });
    }
}

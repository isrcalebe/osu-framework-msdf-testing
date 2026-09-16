using System.Reflection;
using Msdf.Game;
using osu.Framework.Platform;

namespace Msdf.Desktop;

internal sealed partial class MsdfDesktopGame : MsdfGame
{
    public override void SetHost(GameHost host)
    {
        base.SetHost(host);

        var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(GetType(), "Msdf.ico");
        if (iconStream != null)
            host.Window?.SetIconFromStream(iconStream);
    }
}

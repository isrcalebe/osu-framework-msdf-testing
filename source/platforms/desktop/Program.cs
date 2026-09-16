using Msdf.Desktop;
using osu.Framework;

using var host = Host.GetSuitableDesktopHost("msdf", new HostOptions
{
    FriendlyGameName = "Msdf"
});
using var game = new MsdfDesktopGame();

host.Run(game);

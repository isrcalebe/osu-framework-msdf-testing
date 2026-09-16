using Msdf.Game.Tests;
using osu.Framework;

using var host = Host.GetSuitableDesktopHost("msdf-visual-tests", new HostOptions
{
    PortableInstallation = true
});
using var game = new MsdfTestBrowser();

host.Run(game);

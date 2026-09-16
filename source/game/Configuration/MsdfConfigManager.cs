using System.Collections.Generic;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Msdf.Game.Configuration.Settings;

namespace Msdf.Game.Configuration;

public class MsdfConfigManager : IniConfigManager<MsdfSetting>
{
    protected override string Filename => "msdf.ini";

    public MsdfConfigManager(Storage storage, IDictionary<MsdfSetting, object>? defaultOverrides = null)
        : base(storage, defaultOverrides)
    {
    }

    protected override void InitialiseDefaults()
    {
        SetDefault(MsdfSetting.ScreenEntryPoint, ScreenEntryPoint.ScreenA);
    }
}

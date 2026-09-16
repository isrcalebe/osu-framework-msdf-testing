using System.Reflection;

namespace Msdf.Game.Resources;

public static class MsdfResourceAssemblyProvider
{
    public static Assembly Assembly => typeof(MsdfResourceAssemblyProvider).Assembly;
}

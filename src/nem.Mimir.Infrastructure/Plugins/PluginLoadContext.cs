using System.Reflection;
using System.Runtime.Loader;

namespace nem.Mimir.Infrastructure.Plugins;

/// <summary>
/// Custom AssemblyLoadContext that provides isolation for each loaded plugin.
/// Disposing this context unloads the plugin assembly and frees its resources.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "System.",
        "nem.Contracts.",
        "nem.Plugins.Sdk.",
    ];

    private static readonly HashSet<string> SharedAssemblyNames = new(StringComparer.Ordinal)
    {
        "System",
        "mscorlib",
        "netstandard",
        "nem.Contracts",
        "nem.Plugins.Sdk",
    };

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsSharedAssembly(assemblyName.Name))
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    private static bool IsSharedAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        if (SharedAssemblyNames.Contains(assemblyName))
        {
            return true;
        }

        return SharedAssemblyPrefixes.Any(assemblyName.StartsWith);
    }
}

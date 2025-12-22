using System.Reflection;
using System.Runtime.Loader;
using KeyAsio.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace KeyAsio.Shared.Plugins;

public class PluginManager : IPluginManager, IDisposable
{
    private readonly ILogger<PluginManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<PluginWrapper> _plugins = new();
    private readonly Dictionary<string, AssemblyLoadContext> _loadContexts = new();

    public PluginManager(ILogger<PluginManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public IEnumerable<IPlugin> GetAllPlugins()
    {
        return _plugins.Select(p => p.Instance);
    }

    public T? GetPlugin<T>() where T : class, IPlugin
    {
        return _plugins.Select(p => p.Instance).OfType<T>().FirstOrDefault();
    }

    public IEnumerable<IGameStateHandler> GetActiveHandlers(SyncOsuStatus status)
    {
        var handlers = new List<IGameStateHandler>();
        foreach (var wrapper in _plugins)
        {
            if (wrapper.Context is PluginContext ctx)
            {
                handlers.AddRange(ctx.GetHandlers(status));
            }
        }

        // Sort by Priority Descending (Higher priority first)
        handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return handlers;
    }

    public void LoadPlugins(string pluginDirectory, string searchPattern = "*.dll",
        SearchOption searchOption = SearchOption.AllDirectories)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            if (pluginDirectory == AppDomain.CurrentDomain.BaseDirectory)
            {
                // The root directory must exist, this is a safety check.
                return;
            }

            try
            {
                Directory.CreateDirectory(pluginDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create plugin directory: {PluginDirectory}", pluginDirectory);
                return;
            }
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, searchPattern, searchOption);
        foreach (var dllPath in dllFiles)
        {
            // Exclude Abstractions lib
            if (Path.GetFileName(dllPath)
                .Equals("KeyAsio.Plugins.Abstractions.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LoadPlugin(dllPath);
        }
    }

    private bool ValidatePluginAssembly(Assembly assembly)
    {
        // 1. Compatibility Check
        var abstractionsRef = assembly.GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name == "KeyAsio.Plugins.Abstractions");

        if (abstractionsRef != null)
        {
            var currentVersion = typeof(IPlugin).Assembly.GetName().Version;
            // Assuming major version change breaks compatibility
            if (abstractionsRef.Version != null && currentVersion != null &&
                abstractionsRef.Version.Major != currentVersion.Major)
            {
                _logger.LogWarning("Plugin {PluginAssembly} references an incompatible version of Abstractions: {RefVersion} (Current: {CurrentVersion})",
                    assembly.GetName().Name, abstractionsRef.Version, currentVersion);
                return false;
            }
        }

        // 2. Security Check (Signature)
        // Note: This is a basic check.
        var publicKey = assembly.GetName().GetPublicKey();
        if (publicKey == null || publicKey.Length == 0)
        {
            _logger.LogWarning("Plugin {PluginAssembly} is unsigned. Loading anyway (Loose Mode).", assembly.GetName().Name);
            // In Strict Mode, return false here.
            // return false;
        }
        else
        {
            // Verify if the public key is trusted
            // if (!IsTrusted(publicKey)) return false;
            _logger.LogInformation("Plugin {PluginAssembly} is signed.", assembly.GetName().Name);
        }

        return true;
    }

    private void LoadPlugin(string dllPath)
    {
        try
        {
            var loadContext = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dllPath), true);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);

            if (!ValidatePluginAssembly(assembly))
            {
                loadContext.Unload();
                return;
            }

            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IPlugin plugin)
                {
                    var wrapper = new PluginWrapper(plugin, loadContext,
                        Path.GetDirectoryName(dllPath) ?? string.Empty);
                    _plugins.Add(wrapper);
                    _loadContexts[plugin.Id] = loadContext;
                    _logger.LogInformation("Loaded plugin: {PluginName} ({PluginId})", plugin.Name, plugin.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {DllPath}", dllPath);
        }
    }

    public void InitializePlugins(IAudioEngine audioEngine)
    {
        foreach (var wrapper in _plugins)
        {
            try
            {
                var context = new PluginContext(_serviceProvider, audioEngine, wrapper.PluginDirectory);
                wrapper.Context = context;
                wrapper.Instance.Initialize(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing plugin {PluginName}", wrapper.Instance.Name);
            }
        }
    }

    public void StartupPlugins()
    {
        foreach (var wrapper in _plugins)
        {
            try
            {
                wrapper.Instance.Startup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting plugin {PluginName}", wrapper.Instance.Name);
            }
        }
    }

    public void UnloadPlugins()
    {
        foreach (var wrapper in _plugins)
        {
            try
            {
                wrapper.Instance.Shutdown();
                wrapper.Instance.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading plugin {PluginName}", wrapper.Instance.Name);
            }
        }

        _plugins.Clear();
        foreach (var context in _loadContexts.Values)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading AssemblyLoadContext");
            }
        }

        _loadContexts.Clear();
    }

    public void Dispose()
    {
        UnloadPlugins();
    }

    private class PluginWrapper
    {
        public IPlugin Instance { get; }
        public AssemblyLoadContext LoadContext { get; }
        public string PluginDirectory { get; }
        public PluginContext? Context { get; set; }

        public PluginWrapper(IPlugin instance, AssemblyLoadContext loadContext, string pluginDirectory)
        {
            Instance = instance;
            LoadContext = loadContext;
            PluginDirectory = pluginDirectory;
        }
    }
}
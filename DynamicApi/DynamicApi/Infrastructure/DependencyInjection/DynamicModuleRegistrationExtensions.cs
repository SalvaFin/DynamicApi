using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace DynamicApi.Infrastructure.DependencyInjection;

public static class DynamicModuleRegistrationExtensions
{
    private const string ModulePrefix = "Dynamic.";

    public static IMvcBuilder AddDynamicModules(this IServiceCollection services, IConfiguration configuration)
    {
        IMvcBuilder mvcBuilder = services.AddControllers();

        foreach (Assembly assembly in DiscoverModuleAssemblies())
        {
            foreach (Type registrarType in assembly.GetTypes().Where(IsModuleRegistrar))
            {
                object? registrar = Activator.CreateInstance(registrarType);
                MethodInfo? registerMethod = registrarType.GetMethod(
                    "RegisterModule",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: [typeof(IServiceCollection), typeof(IConfiguration), typeof(IMvcBuilder)],
                    modifiers: null);

                registerMethod?.Invoke(registrar, [services, configuration, mvcBuilder]);
            }
        }

        return mvcBuilder;
    }

    private static bool IsModuleRegistrar(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
        {
            return false;
        }

        return type.GetMethod(
            "RegisterModule",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(IServiceCollection), typeof(IConfiguration), typeof(IMvcBuilder)],
            modifiers: null) is not null;
    }

    private static IReadOnlyCollection<Assembly> DiscoverModuleAssemblies()
    {
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is null)
        {
            return [];
        }

        HashSet<string> visited = [];
        Queue<AssemblyName> pending = new(entryAssembly.GetReferencedAssemblies());
        List<Assembly> assemblies = [];

        while (pending.Count > 0)
        {
            AssemblyName next = pending.Dequeue();
            if (string.IsNullOrWhiteSpace(next.Name) || !next.Name.StartsWith(ModulePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!visited.Add(next.FullName ?? next.Name))
            {
                continue;
            }

            Assembly assembly = Assembly.Load(next);
            assemblies.Add(assembly);

            foreach (AssemblyName childReference in assembly.GetReferencedAssemblies())
            {
                if (!string.IsNullOrWhiteSpace(childReference.Name) &&
                    childReference.Name.StartsWith(ModulePrefix, StringComparison.Ordinal))
                {
                    pending.Enqueue(childReference);
                }
            }
        }

        return assemblies;
    }
}

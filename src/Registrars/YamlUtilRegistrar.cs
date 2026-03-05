using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.Yaml.Abstract;

namespace Soenneker.Utils.Yaml.Registrars;

/// <summary>
/// A utility library handling useful YAML functionalities
/// </summary>
public static class YamlUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IYamlUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddYamlUtilAsSingleton(this IServiceCollection services)
    {
        services.AddFileUtilAsSingleton()
                .TryAddSingleton<IYamlUtil, YamlUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IYamlUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddYamlUtilAsScoped(this IServiceCollection services)
    {
        services.AddFileUtilAsScoped()
                .TryAddScoped<IYamlUtil, YamlUtil>();

        return services;
    }
}
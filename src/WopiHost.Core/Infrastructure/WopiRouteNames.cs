using WopiHost.Core.Controllers;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Endpoint names.
/// </summary>
public static class WopiRouteNames
{
    /// <summary>
    /// Endpoint name for <see cref="EcosystemController.CheckEcosystem"/>
    /// </summary>
    public const string CheckEcosystem = nameof(CheckEcosystem);

    /// <summary>
    /// Endpoint name for <see cref="FilesController.CheckFileInfo(string, CancellationToken)"/>
    /// </summary>
    public const string CheckFileInfo = nameof(CheckFileInfo);

    /// <summary>
    /// Endpoint name for <see cref="ContainersController.CheckContainerInfo(string, CancellationToken)"/>
    /// </summary>
    public const string CheckContainerInfo = nameof(CheckContainerInfo);
}

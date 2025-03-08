﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WopiHost.Abstractions;

/// <summary>
/// Implementation of writable operations for an external storage provider.
/// </summary>
public interface IWopiWritableStorageProvider
{
    /// <summary>
    /// Creates a new container in the specified container.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="name">the new Container's name</param>
    /// <param name="isExactName">whether the container's name is a suggestion or must be left as-is</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>identifier of new container</returns>
    Task<string?> CreateWopiChildContainer(
        string identifier,
        string name,
        bool isExactName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified container.
    /// </summary>
    /// <param name="identifier">Generic string identifier of a container (typically some kind of a path).</param>
    /// <param name="cancellationToken">cancellation token</param>
    /// <returns>true for success</returns>
    Task<bool> DeleteWopiContainer(string identifier, CancellationToken cancellationToken = default);
}
// -----------------------------------------------------------------------
// <copyright file="TemporaryImplementationAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Maintenances;

/// <summary>
/// Marks a deliberately incomplete implementation.
/// </summary>
/// <param name="reason">The reason this implementation is temporary.</param>
public class TemporaryImplementationAttribute(string reason)
    : MaintenanceAttribute(reason);
// -----------------------------------------------------------------------
// <copyright file="WorkaroundAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Maintenances;

/// <summary>
/// Marks a workaround for an external defect or limitation.
/// </summary>
/// <param name="reason">The external defect or limitation being worked around.</param>
public class WorkaroundAttribute(string reason)
    : MaintenanceAttribute(reason);
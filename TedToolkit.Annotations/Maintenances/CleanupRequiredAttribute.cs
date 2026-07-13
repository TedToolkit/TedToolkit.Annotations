// -----------------------------------------------------------------------
// <copyright file="CleanupRequiredAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Maintenances;

/// <summary>
/// Marks code that is correct but should be simplified, removed, or reorganized.
/// </summary>
/// <param name="reason">The cleanup work that is required.</param>
public sealed class CleanupRequiredAttribute(string reason) : MaintenanceAttribute(reason);
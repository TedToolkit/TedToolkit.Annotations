// -----------------------------------------------------------------------
// <copyright file="IdempotentAttribute.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Annotations.Documentations;

/// <summary>
/// Documents that repeating the annotated operation has no additional observable effect.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class IdempotentAttribute : OperationalAttribute;
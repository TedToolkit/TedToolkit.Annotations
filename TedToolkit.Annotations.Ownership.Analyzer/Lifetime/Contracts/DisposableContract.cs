// -----------------------------------------------------------------------
// <copyright file="DisposableContract.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Describes the synchronous and asynchronous disposal contracts available in a compilation.
/// </summary>
internal sealed class DisposableContract
{
    private const string DISPOSABLE_TYPE_NAME = "System.IDisposable";

    private const string ASYNC_DISPOSABLE_TYPE_NAME = "System.IAsyncDisposable";

    private DisposableContract(INamedTypeSymbol? disposableType, INamedTypeSymbol? asyncDisposableType)
    {
        DisposableType = disposableType;
        AsyncDisposableType = asyncDisposableType;
        DisposeMethod = disposableType?.GetMembers("Dispose").OfType<IMethodSymbol>().SingleOrDefault();
        DisposeAsyncMethod = asyncDisposableType?.GetMembers("DisposeAsync").OfType<IMethodSymbol>().SingleOrDefault();
    }

    /// <summary>
    /// Gets the IDisposable type.
    /// </summary>
    internal INamedTypeSymbol? DisposableType { get; }

    /// <summary>
    /// Gets the IAsyncDisposable type.
    /// </summary>
    internal INamedTypeSymbol? AsyncDisposableType { get; }

    /// <summary>
    /// Gets the IDisposable.Dispose interface method.
    /// </summary>
    internal IMethodSymbol? DisposeMethod { get; }

    /// <summary>
    /// Gets the IAsyncDisposable.DisposeAsync interface method.
    /// </summary>
    internal IMethodSymbol? DisposeAsyncMethod { get; }

    /// <summary>
    /// Creates the disposal contract for a compilation.
    /// </summary>
    /// <param name="compilation">The compilation that supplies framework type symbols.</param>
    /// <returns>A contract bound to the disposal interfaces available in the compilation.</returns>
    internal static DisposableContract Create(Compilation compilation)
    {
        return new(
            compilation.GetTypeByMetadataName(DISPOSABLE_TYPE_NAME),
            compilation.GetTypeByMetadataName(ASYNC_DISPOSABLE_TYPE_NAME));
    }

    /// <summary>
    /// Determines whether the compilation exposes either disposal interface.
    /// </summary>
    internal bool IsAvailable
    {
        get
        {
            return DisposableType is not null || AsyncDisposableType is not null;
        }
    }

    /// <summary>
    /// Determines whether a type supports synchronous or asynchronous disposal.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements either disposal interface.</returns>
    internal bool IsDisposable(ITypeSymbol type)
    {
        return Implements(type, DisposableType) || Implements(type, AsyncDisposableType);
    }

    /// <summary>
    /// Determines whether a type supports synchronous disposal.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements <see cref="IDisposable"/>.</returns>
    internal bool IsSynchronouslyDisposable(ITypeSymbol type)
    {
        return Implements(type, DisposableType);
    }

    /// <summary>
    /// Determines whether a type supports asynchronous disposal.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type implements <see cref="IAsyncDisposable"/>.</returns>
    internal bool IsAsynchronouslyDisposable(ITypeSymbol type)
    {
        return Implements(type, AsyncDisposableType);
    }

    /// <summary>
    /// Determines whether an invocation calls the IDisposable contract.
    /// </summary>
    /// <param name="method">The invoked method.</param>
    /// <returns><see langword="true"/> for `IDisposable.Dispose` or its implementation.</returns>
    internal bool IsSynchronousRelease(IMethodSymbol method)
    {
        return IsInterfaceContractMethod(method, DisposableType, DisposeMethod);
    }

    /// <summary>
    /// Determines whether an invocation calls the IAsyncDisposable contract.
    /// </summary>
    /// <param name="method">The invoked method.</param>
    /// <returns><see langword="true"/> for `IAsyncDisposable.DisposeAsync` or its implementation.</returns>
    internal bool IsAsynchronousRelease(IMethodSymbol method)
    {
        return IsInterfaceContractMethod(method, AsyncDisposableType, DisposeAsyncMethod);
    }

    /// <summary>
    /// Finds a type's implementation of IDisposable.Dispose.
    /// </summary>
    /// <param name="type">The implementing type.</param>
    /// <returns>The method that implements `IDisposable.Dispose`, or <see langword="null"/>.</returns>
    internal IMethodSymbol? GetSynchronousReleaseMethod(INamedTypeSymbol type)
    {
        return DisposeMethod is null ? null : type.FindImplementationForInterfaceMember(DisposeMethod) as IMethodSymbol;
    }

    /// <summary>
    /// Finds a type's implementation of IAsyncDisposable.DisposeAsync.
    /// </summary>
    /// <param name="type">The implementing type.</param>
    /// <returns>The method that implements `IAsyncDisposable.DisposeAsync`, or <see langword="null"/>.</returns>
    internal IMethodSymbol? GetAsynchronousReleaseMethod(INamedTypeSymbol type)
    {
        return DisposeAsyncMethod is null ? null : type.FindImplementationForInterfaceMember(DisposeAsyncMethod) as IMethodSymbol;
    }

    private static bool Implements(ITypeSymbol type, INamedTypeSymbol? interfaceType)
    {
        return interfaceType is not null
            && (SymbolEqualityComparer.Default.Equals(type, interfaceType)
                || type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, interfaceType)));
    }

    private static bool IsInterfaceContractMethod(
        IMethodSymbol method,
        INamedTypeSymbol? interfaceType,
        IMethodSymbol? interfaceMethod)
    {
        if (interfaceType is null || interfaceMethod is null || method.Parameters.Length != 0)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, interfaceMethod.OriginalDefinition))
        {
            return true;
        }

        var implementation = method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;
        return implementation is not null
            && SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, implementation.OriginalDefinition);
    }
}
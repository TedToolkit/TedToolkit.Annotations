// -----------------------------------------------------------------------
// <copyright file="LifetimeOwnershipSemantics.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

namespace TedToolkit.Annotations.Analyzer.Lifetime.Contracts;

/// <summary>
/// Interprets ownership annotations and Roslyn operations for lifetime analysis.
/// </summary>
internal static class LifetimeOwnershipSemantics
{
    private const string OWNERSHIP_ATTRIBUTE_NAME =
        "TedToolkit.Annotations.Documentations.OwnershipAttribute";

    private const string CALLBACK_LIFETIME_ATTRIBUTE_NAME =
        "TedToolkit.Annotations.Documentations.CallbackLifetimeAttribute";

    /// <summary>
    /// Determines whether a type implements IDisposable.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="disposableType">The disposal interface resolved from the compilation.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or implements the interface.</returns>
    internal static bool IsDisposable(ITypeSymbol type, INamedTypeSymbol disposableType)
    {
        return SymbolEqualityComparer.Default.Equals(type, disposableType)
            || type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, disposableType));
    }

    /// <summary>
    /// Determines whether a type implements a supported disposal contract.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="contract">The synchronous and asynchronous disposal interfaces.</param>
    /// <returns><see langword="true"/> when the type supports either disposal contract.</returns>
    internal static bool IsDisposable(ITypeSymbol type, DisposableContract contract)
    {
        return contract.IsDisposable(type);
    }

    /// <summary>
    /// Gets resource ownership supplied by an operation.
    /// </summary>
    /// <param name="operation">The expression that supplies a resource.</param>
    /// <param name="disposableType">The disposal interface used to recognize resource types.</param>
    /// <returns>The supplied ownership, or <see langword="null"/> when the expression is not a recognized resource source.</returns>
    internal static LifetimeResourceOwnershipType? GetResourceOwnership(IOperation? operation, INamedTypeSymbol disposableType)
    {
        return operation switch
        {
            IObjectCreationOperation => LifetimeResourceOwnershipType.OWNED,
            IInvocationOperation invocation when invocation.Type is not null && IsDisposable(invocation.Type, disposableType) =>
                GetReturnOwnership(invocation.TargetMethod),
            IPropertyReferenceOperation property when IsDisposable(property.Property.Type, disposableType) =>
                GetOwnership(property.Property, LifetimeOwnershipFlowType.OUTPUT, LifetimeOwnershipTransferType.UNCHANGED),
            IFieldReferenceOperation field when IsDisposable(field.Field.Type, disposableType) => GetFieldOwnership(field.Field),
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetResourceOwnership(GetConditionalAccessReceiver(conditionalAccess), disposableType),
            IConversionOperation conversion => GetResourceOwnership(conversion.Operand, disposableType),
            _ => null,
        };
    }

    /// <summary>
    /// Gets resource ownership supplied by an operation.
    /// </summary>
    /// <param name="operation">The expression that supplies a resource.</param>
    /// <param name="contract">The disposal interfaces used to recognize resource types.</param>
    /// <returns>The supplied ownership, or <see langword="null"/> when the expression is not a recognized resource source.</returns>
    internal static LifetimeResourceOwnershipType? GetResourceOwnership(IOperation? operation, DisposableContract contract)
    {
        return operation switch
        {
            IObjectCreationOperation creation when creation.Type is not null && contract.IsDisposable(creation.Type) =>
                LifetimeResourceOwnershipType.OWNED,
            IInvocationOperation invocation when invocation.Type is not null && contract.IsDisposable(invocation.Type) =>
                GetReturnOwnership(invocation.TargetMethod),
            IPropertyReferenceOperation property when contract.IsDisposable(property.Property.Type) =>
                GetOwnership(property.Property, LifetimeOwnershipFlowType.OUTPUT, LifetimeOwnershipTransferType.UNCHANGED),
            IFieldReferenceOperation field when contract.IsDisposable(field.Field.Type) => GetFieldOwnership(field.Field),
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetResourceOwnership(GetConditionalAccessReceiver(conditionalAccess), contract),
            IConversionOperation conversion => GetResourceOwnership(conversion.Operand, contract),
            _ => null,
        };
    }

    /// <summary>
    /// Gets ownership supplied through an output parameter.
    /// </summary>
    /// <param name="parameter">An <see langword="out"/> or <see langword="ref"/> parameter.</param>
    /// <returns>The ownership supplied to the caller, or <see langword="null"/> when no output contract applies.</returns>
    internal static LifetimeResourceOwnershipType? GetOutputOwnership(IParameterSymbol parameter)
    {
        if (parameter.RefKind == RefKind.Out)
        {
            return GetOwnership(parameter, LifetimeOwnershipFlowType.OUTPUT, LifetimeOwnershipTransferType.TRANSFERRED);
        }

        return parameter.RefKind == RefKind.Ref
            && TryGetOwnershipTransfer(parameter, LifetimeOwnershipFlowType.OUTPUT, allowDefaultFlow: false, out var transfer)
                ? ToResourceOwnership(transfer)
                : null;
    }

    /// <summary>
    /// Determines whether a parameter receives transferred ownership.
    /// </summary>
    /// <param name="parameter">The receiving parameter.</param>
    /// <returns><see langword="true"/> when ownership moves from the caller into the member.</returns>
    internal static bool IsTransferredInput(IParameterSymbol parameter)
    {
        return TryGetOwnershipTransfer(
            parameter,
            LifetimeOwnershipFlowType.INPUT,
            allowDefaultFlow: parameter.RefKind != RefKind.Ref,
            out var transfer)
            && transfer == LifetimeOwnershipTransferType.TRANSFERRED;
    }

    /// <summary>
    /// Determines whether a property setter receives transferred ownership.
    /// </summary>
    /// <param name="property">The property whose setter receives the value.</param>
    /// <returns><see langword="true"/> when the setter takes ownership.</returns>
    internal static bool IsTransferredInput(IPropertySymbol property)
    {
        return TryGetOwnershipTransfer(property, LifetimeOwnershipFlowType.INPUT, allowDefaultFlow: false, out var transfer)
            && transfer == LifetimeOwnershipTransferType.TRANSFERRED;
    }

    /// <summary>
    /// Determines whether a symbol explicitly owns disposal.
    /// </summary>
    /// <param name="symbol">A disposable field or other storage symbol.</param>
    /// <returns><see langword="true"/> when the symbol has an explicit transferred-ownership annotation.</returns>
    internal static bool IsExplicitlyOwned(ISymbol symbol)
    {
        return TryGetOwnershipTransfer(symbol, LifetimeOwnershipFlowType.DEFAULT, allowDefaultFlow: false, out var transfer)
            && transfer == LifetimeOwnershipTransferType.TRANSFERRED;
    }

    /// <summary>
    /// Determines whether a symbol explicitly borrows disposal.
    /// </summary>
    /// <param name="symbol">A disposable field or other storage symbol.</param>
    /// <returns><see langword="true"/> when the symbol has an explicit unchanged-ownership annotation.</returns>
    internal static bool IsExplicitlyBorrowed(ISymbol symbol)
    {
        return TryGetOwnershipTransfer(symbol, LifetimeOwnershipFlowType.DEFAULT, allowDefaultFlow: false, out var transfer)
            && transfer == LifetimeOwnershipTransferType.UNCHANGED;
    }

    /// <summary>
    /// Gets the ownership annotation applied to a symbol.
    /// </summary>
    /// <param name="symbol">The symbol whose effective contract is queried.</param>
    /// <returns>The first ownership annotation, or <see langword="null"/> when none is declared.</returns>
    internal static AttributeData? GetOwnershipAnnotation(ISymbol symbol)
    {
        var attributes = GetOwnershipAnnotations(symbol);
        return attributes.FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == OWNERSHIP_ATTRIBUTE_NAME);
    }

    /// <summary>
    /// Gets all ownership annotations participating in a symbol's effective contract.
    /// </summary>
    /// <param name="symbol">The symbol whose direct and inherited contracts are queried.</param>
    /// <returns>Ownership annotations from the symbol, its overrides, and implemented interface members.</returns>
    internal static IEnumerable<AttributeData> GetOwnershipAnnotations(ISymbol symbol)
    {
        return symbol is IMethodSymbol method
            ? GetReturnAttributes(method)
            : GetContractAttributes(symbol);
    }

    /// <summary>
    /// Gets the ownership promised by a method or property return boundary.
    /// </summary>
    /// <param name="method">The method or property accessor that supplies the return value.</param>
    /// <returns>The effective output ownership; unannotated disposable returns are owned by default.</returns>
    internal static LifetimeResourceOwnershipType GetReturnOwnership(IMethodSymbol method)
    {
        return method.AssociatedSymbol is IPropertySymbol property
            ? GetOwnership(property, LifetimeOwnershipFlowType.OUTPUT, LifetimeOwnershipTransferType.TRANSFERRED)
            : GetOwnership(
                GetReturnAttributes(method),
                LifetimeOwnershipFlowType.OUTPUT,
                LifetimeOwnershipTransferType.TRANSFERRED);
    }

    /// <summary>
    /// Gets a callback lifetime annotation.
    /// </summary>
    /// <param name="parameter">The callback parameter.</param>
    /// <param name="callbackLifetime">Receives the declared callback lifetime enum value.</param>
    /// <returns><see langword="true"/> when a valid callback-lifetime annotation is present.</returns>
    internal static bool TryGetCallbackLifetime(IParameterSymbol parameter, out int callbackLifetime)
    {
        var attribute = GetContractAttributes(parameter).FirstOrDefault(candidate =>
            candidate.AttributeClass?.ToDisplayString() == CALLBACK_LIFETIME_ATTRIBUTE_NAME);
        if (attribute is null || attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not int value)
        {
            callbackLifetime = default;
            return false;
        }

        callbackLifetime = value;
        return true;
    }

    /// <summary>
    /// Gets a local referenced by an operation.
    /// </summary>
    /// <param name="operation">The expression to unwrap.</param>
    /// <returns>The referenced local, or <see langword="null"/>.</returns>
    internal static ILocalSymbol? GetReferencedLocal(IOperation? operation)
    {
        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IDeclarationExpressionOperation declaration => GetReferencedLocal(declaration.Expression),
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetReferencedLocal(GetConditionalAccessReceiver(conditionalAccess)),
            IConversionOperation conversion => GetReferencedLocal(conversion.Operand),
            _ => null,
        };
    }

    /// <summary>
    /// Gets a tracked local or parameter referenced by an operation.
    /// </summary>
    /// <param name="operation">The expression to unwrap.</param>
    /// <returns>The referenced local or parameter, or <see langword="null"/>.</returns>
    internal static ISymbol? GetReferencedSymbol(IOperation? operation)
    {
        return operation switch
        {
            ILocalReferenceOperation localReference => localReference.Local,
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            IDeclarationExpressionOperation declaration => GetReferencedSymbol(declaration.Expression),
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetReferencedSymbol(GetConditionalAccessReceiver(conditionalAccess)),
            IConversionOperation conversion => GetReferencedSymbol(conversion.Operand),
            _ => null,
        };
    }

    /// <summary>
    /// Gets a field referenced by an operation.
    /// </summary>
    /// <param name="operation">The expression to unwrap.</param>
    /// <returns>The referenced field, or <see langword="null"/>.</returns>
    internal static IFieldSymbol? GetReferencedField(IOperation? operation)
    {
        return operation switch
        {
            IFieldReferenceOperation fieldReference => fieldReference.Field,
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetReferencedField(GetConditionalAccessReceiver(conditionalAccess)),
            IConversionOperation conversion => GetReferencedField(conversion.Operand),
            _ => null,
        };
    }

    /// <summary>
    /// Gets a property referenced by an operation.
    /// </summary>
    /// <param name="operation">The expression to unwrap.</param>
    /// <returns>The referenced property, or <see langword="null"/>.</returns>
    internal static IPropertySymbol? GetReferencedProperty(IOperation? operation)
    {
        return operation switch
        {
            IPropertyReferenceOperation propertyReference => propertyReference.Property,
            IConditionalAccessInstanceOperation conditionalAccess =>
                GetReferencedProperty(GetConditionalAccessReceiver(conditionalAccess)),
            IConversionOperation conversion => GetReferencedProperty(conversion.Operand),
            _ => null,
        };
    }

    /// <summary>
    /// Gets a parameter referenced by an operation.
    /// </summary>
    /// <param name="operation">The expression to unwrap.</param>
    /// <returns>The referenced parameter, or <see langword="null"/>.</returns>
    internal static IParameterSymbol? GetReferencedParameter(IOperation? operation)
    {
        return operation switch
        {
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            IConversionOperation conversion => GetReferencedParameter(conversion.Operand),
            _ => null,
        };
    }

    /// <summary>
    /// Finds the receiver represented by a conditional-access placeholder operation.
    /// </summary>
    /// <param name="operation">The placeholder produced inside a conditional-access operation.</param>
    /// <returns>The expression to the left of <c>?.</c>, or <see langword="null"/>.</returns>
    internal static IOperation? GetConditionalAccessReceiver(IConditionalAccessInstanceOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IConditionalAccessOperation conditionalAccess)
            {
                return conditionalAccess.Operation;
            }
        }

        return null;
    }

    private static LifetimeResourceOwnershipType? GetFieldOwnership(IFieldSymbol field)
    {
        return TryGetOwnershipTransfer(field, LifetimeOwnershipFlowType.DEFAULT, allowDefaultFlow: false, out var transfer)
            ? ToResourceOwnership(transfer)
            : null;
    }

    private static LifetimeResourceOwnershipType GetOwnership(
        ISymbol symbol,
        LifetimeOwnershipFlowType flow,
        LifetimeOwnershipTransferType defaultTransfer)
    {
        return GetOwnership(GetContractAttributes(symbol), flow, defaultTransfer);
    }

    private static LifetimeResourceOwnershipType GetOwnership(
        IEnumerable<AttributeData> attributes,
        LifetimeOwnershipFlowType flow,
        LifetimeOwnershipTransferType defaultTransfer)
    {
        return TryGetOwnershipTransfer(attributes, flow, allowDefaultFlow: true, out var transfer)
            ? ToResourceOwnership(transfer)
            : ToResourceOwnership(defaultTransfer);
    }

    private static LifetimeResourceOwnershipType ToResourceOwnership(LifetimeOwnershipTransferType transfer)
    {
        return transfer == LifetimeOwnershipTransferType.TRANSFERRED
            ? LifetimeResourceOwnershipType.OWNED
            : LifetimeResourceOwnershipType.BORROWED;
    }

    private static bool TryGetOwnershipTransfer(
        ISymbol symbol,
        LifetimeOwnershipFlowType flow,
        bool allowDefaultFlow,
        out LifetimeOwnershipTransferType transfer)
    {
        return TryGetOwnershipTransfer(GetContractAttributes(symbol), flow, allowDefaultFlow, out transfer);
    }

    private static bool TryGetOwnershipTransfer(
        IEnumerable<AttributeData> attributes,
        LifetimeOwnershipFlowType flow,
        bool allowDefaultFlow,
        out LifetimeOwnershipTransferType transfer)
    {
        // Multiple inherited contracts are valid only when they agree for the requested flow.
        var found = false;
        transfer = default;
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString() != OWNERSHIP_ATTRIBUTE_NAME
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not int transferValue)
            {
                continue;
            }

            var attributeFlow = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is int flowValue
                ? (LifetimeOwnershipFlowType)flowValue
                : LifetimeOwnershipFlowType.DEFAULT;
            if (attributeFlow != flow && (!allowDefaultFlow || attributeFlow != LifetimeOwnershipFlowType.DEFAULT))
            {
                continue;
            }

            var candidate = (LifetimeOwnershipTransferType)transferValue;
            if (found && transfer != candidate)
            {
                transfer = default;
                return false;
            }

            transfer = candidate;
            found = true;
        }

        return found;
    }

    private static IEnumerable<AttributeData> GetContractAttributes(ISymbol symbol)
    {
        // Contracts on overrides and interface declarations remain part of the implementation's obligations.
        foreach (var attribute in symbol.GetAttributes())
        {
            yield return attribute;
        }

        if (symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol method, } parameter)
        {
            foreach (var relatedMethod in GetRelatedMethods(method).Skip(1))
            {
                if (parameter.Ordinal < relatedMethod.Parameters.Length)
                {
                    foreach (var attribute in relatedMethod.Parameters[parameter.Ordinal].GetAttributes())
                    {
                        yield return attribute;
                    }
                }
            }
        }

        if (symbol is IPropertySymbol property)
        {
            foreach (var relatedProperty in GetRelatedProperties(property).Skip(1))
            {
                foreach (var attribute in relatedProperty.GetAttributes())
                {
                    yield return attribute;
                }
            }
        }
    }

    private static IEnumerable<AttributeData> GetReturnAttributes(IMethodSymbol method)
    {
        foreach (var relatedMethod in GetRelatedMethods(method))
        {
            foreach (var attribute in relatedMethod.GetReturnTypeAttributes())
            {
                yield return attribute;
            }
        }
    }

    private static IEnumerable<IMethodSymbol> GetRelatedMethods(IMethodSymbol method)
    {
        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (seen.Add(current))
            {
                yield return current;
            }
        }

        foreach (var @interface in method.ContainingType.AllInterfaces)
        {
            foreach (var interfaceMethod in @interface.GetMembers().OfType<IMethodSymbol>())
            {
                var implementation = method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;
                if (implementation is not null
                    && IsInOverrideChain(method, implementation)
                    && seen.Add(interfaceMethod))
                {
                    yield return interfaceMethod;
                }
            }
        }
    }

    private static IEnumerable<IPropertySymbol> GetRelatedProperties(IPropertySymbol property)
    {
        var seen = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        for (var current = property; current is not null; current = current.OverriddenProperty)
        {
            if (seen.Add(current))
            {
                yield return current;
            }
        }

        foreach (var @interface in property.ContainingType.AllInterfaces)
        {
            foreach (var interfaceProperty in @interface.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.ContainingType.FindImplementationForInterfaceMember(interfaceProperty) is IPropertySymbol implementation
                    && SymbolEqualityComparer.Default.Equals(implementation, property)
                    && seen.Add(interfaceProperty))
                {
                    yield return interfaceProperty;
                }
            }
        }
    }

    private static bool IsInOverrideChain(IMethodSymbol method, IMethodSymbol candidate)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
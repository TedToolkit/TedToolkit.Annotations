// -----------------------------------------------------------------------
// <copyright file="ConstContractInference.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using TedToolkit.Annotations.Analyzer.Const.Contracts;

namespace TedToolkit.Annotations.Analyzer.Const;

/// <summary>
/// Infers the const contract required by directly invoked, annotated source members.
/// </summary>
internal static class ConstContractInference
{
    /// <summary>
    /// Tries to infer the const contract that a method can publish from its invocation sites.
    /// </summary>
    /// <param name="declaration">The method declaration to inspect.</param>
    /// <param name="semanticModel">The semantic model for <paramref name="declaration"/>.</param>
    /// <param name="cancellationToken">The token that cancels the analysis.</param>
    /// <param name="methodDepths">The inferred depths for the method receiver.</param>
    /// <param name="parameterDepths">The inferred depths for each method parameter.</param>
    /// <returns><see langword="true"/> when every invocation has a const contract and at least one depth is inferred.</returns>
    internal static bool TryInfer(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out uint methodDepths,
        out IReadOnlyDictionary<IParameterSymbol, uint> parameterDepths)
    {
        methodDepths = 0;
        parameterDepths = new Dictionary<IParameterSymbol, uint>(SymbolEqualityComparer.Default);
        if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method
            || ConstContractResolver.TryGetConstDepths(method, out _)
            || method.Parameters.Any(parameter => ConstContractResolver.TryGetConstDepths(parameter, out _)))
        {
            return false;
        }

        var hasContract = false;
        var depthsByParameter = (Dictionary<IParameterSymbol, uint>)parameterDepths;
        foreach (var invocationSyntax in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocationSyntax.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not null
                || invocationSyntax.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() is not null
                || semanticModel.GetOperation(invocationSyntax, cancellationToken) is not IInvocationOperation invocation
                || !TryInferInvocation(invocation, method, ref methodDepths, depthsByParameter))
            {
                methodDepths = 0;
                parameterDepths = new Dictionary<IParameterSymbol, uint>(SymbolEqualityComparer.Default);
                return false;
            }

            hasContract = true;
        }

        return hasContract && (methodDepths != 0 || parameterDepths.Count != 0);
    }

    private static bool TryInferInvocation(
        IInvocationOperation invocation,
        IMethodSymbol containingMethod,
        ref uint methodDepths,
        Dictionary<IParameterSymbol, uint> parameterDepths)
    {
        var methodHasContract = ConstContractResolver.TryGetConstDepths(invocation.TargetMethod, out var calledMethodDepths);
        var parameterContracts = invocation.Arguments
            .Select(GetParameterContract)
            .ToArray();
        if ((!methodHasContract && !parameterContracts.Any(contract => contract.HasContract))
            || invocation.Arguments.Any(argument => argument.Parameter?.RefKind != RefKind.None))
        {
            return false;
        }

        if (methodHasContract && invocation.Instance is not null
            && !TryApplyDepths(invocation.Instance, calledMethodDepths, containingMethod, ref methodDepths, parameterDepths))
        {
            return false;
        }

        foreach (var contract in parameterContracts.Where(contract => contract.HasContract))
        {
            if (!TryApplyDepths(contract.Argument.Value, contract.Depths, containingMethod, ref methodDepths, parameterDepths))
            {
                return false;
            }
        }

        return true;
    }

    private static (IArgumentOperation Argument, bool HasContract, uint Depths) GetParameterContract(IArgumentOperation argument)
    {
        if (argument.Parameter is not { } parameter
            || !ConstContractResolver.TryGetConstDepths(parameter, out var depths))
        {
            return (argument, false, 0);
        }

        return (argument, true, depths);
    }

    private static bool TryApplyDepths(
        IOperation value,
        uint requiredDepths,
        IMethodSymbol containingMethod,
        ref uint methodDepths,
        Dictionary<IParameterSymbol, uint> parameterDepths)
    {
        if (!TryGetRoot(value, out var root, out var offset))
        {
            return true;
        }

        if (requiredDepths != uint.MaxValue
            && offset != 0
            && ((requiredDepths >> (32 - offset)) != 0))
        {
            return false;
        }

        var shiftedDepths = requiredDepths == uint.MaxValue ? uint.MaxValue : requiredDepths << offset;
        switch (root)
        {
            case IInstanceReferenceOperation:
                methodDepths |= shiftedDepths;
                return true;

            case IParameterReferenceOperation parameterReference
                when SymbolEqualityComparer.Default.Equals(parameterReference.Parameter.ContainingSymbol, containingMethod):
                parameterDepths.TryGetValue(parameterReference.Parameter, out var existingDepths);
                parameterDepths[parameterReference.Parameter] = existingDepths | shiftedDepths;
                return true;

            default:
                return true;
        }
    }

    private static bool TryGetRoot(IOperation operation, out IOperation root, out int offset)
    {
        offset = 0;
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;

                case IFieldReferenceOperation field when field.Instance is not null:
                    offset++;
                    operation = field.Instance;
                    continue;

                case IPropertyReferenceOperation property when property.Instance is not null:
                    offset++;
                    operation = property.Instance;
                    continue;

                case IInstanceReferenceOperation or IParameterReferenceOperation:
                    root = operation;
                    return true;

                default:
                    root = operation;
                    return false;
            }
        }
    }
}
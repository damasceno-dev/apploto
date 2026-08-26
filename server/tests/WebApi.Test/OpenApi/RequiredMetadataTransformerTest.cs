using System.Diagnostics.CodeAnalysis;
using Microsoft.OpenApi;
using server.OpenApi;
using Shouldly;
using Xunit;

namespace WebApi.Test.OpenApi;

public sealed class RequiredMetadataTransformerTest
{
    [Fact]
    public void MarkRequired_ShouldResolveAndMutateReferencedParameter()
    {
        var target = new OpenApiParameter
        {
            Name = "Date",
            In = ParameterLocation.Query
        };
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Parameters = new Dictionary<string, IOpenApiParameter>
                {
                    ["SharedDate"] = target
                }
            }
        };
        document.RegisterComponents();
        var reference = new OpenApiParameterReference("SharedDate", document, null!);

        var marked = RequiredQueryParametersOpenApiOperationTransformer.MarkRequired(
            [reference],
            "date");

        marked.ShouldBeTrue();
        target.Required.ShouldBeTrue();
    }

    [Fact]
    public void FindQueryParameter_ShouldUseFirstCaseInsensitiveMatch()
    {
        var first = new OpenApiParameter { Name = "Mine", In = ParameterLocation.Query };
        var second = new OpenApiParameter { Name = "mine", In = ParameterLocation.Query };

        var found = RequiredQueryParametersOpenApiOperationTransformer.FindQueryParameter(
            [first, second],
            "MINE");

        found.ShouldBeSameAs(first);
    }

    [Fact]
    public void FindProperty_ShouldUseFirstCaseInsensitiveMatchForCaseCollisions()
    {
        var found = RequiredQueryParametersOpenApiOperationTransformer.FindProperty(
            typeof(CaseCollidingQuery),
            "VALUE");

        found.ShouldNotBeNull();
        string.Equals(found.Name, "Value", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void IsRequired_ShouldUseWriteStateForInputsAndReadStateForOutputs()
    {
        var property = typeof(AsymmetricContract).GetProperty(nameof(AsymmetricContract.Value))!;

        ContractNullability.IsRequired(property, forInput: true).ShouldBeTrue();
        ContractNullability.IsRequired(property, forInput: false).ShouldBeFalse();
    }

    [Fact]
    public void FlattenedQueryDefaults_ShouldMatchRequestBodyRulesForNonNullableEnums()
    {
        var definedZero = typeof(EnumQuery).GetProperty(nameof(EnumQuery.DefinedZero))!;
        var undefinedZero = typeof(EnumQuery).GetProperty(nameof(EnumQuery.UndefinedZero))!;

        FlattenedQueryParameterDefaults.HasOmissionDefault(definedZero).ShouldBeTrue();
        FlattenedQueryParameterDefaults.HasOmissionDefault(undefinedZero).ShouldBeFalse();
    }

    private class BaseQuery
    {
        public string? Value { get; init; }
    }

    private sealed class CaseCollidingQuery : BaseQuery
    {
        public string? value { get; init; }
    }

    private sealed class AsymmetricContract
    {
        [MaybeNull]
        [DisallowNull]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class EnumQuery
    {
        public DefinedZeroEnum DefinedZero { get; init; }
        public UndefinedZeroEnum UndefinedZero { get; init; }
    }

    private enum DefinedZeroEnum
    {
        Unspecified = 0,
        Active = 1
    }

    private enum UndefinedZeroEnum
    {
        Active = 1
    }
}

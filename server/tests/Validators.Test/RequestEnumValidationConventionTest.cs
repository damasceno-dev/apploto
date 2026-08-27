using FluentValidation;
using FluentValidation.Validators;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using Shouldly;
using Xunit;

namespace Validators.Test;

public sealed class RequestEnumValidationConventionTest
{
    [Fact]
    public void EveryRequestEnumProperty_ShouldHaveMatchingEnumValidator()
    {
        var requestAssembly = typeof(RequestUpsertTimeEntryJson).Assembly;
        var applicationAssembly = typeof(UpsertTimeEntryFluentValidation).Assembly;
        var validatorTypesByRequest = applicationAssembly.GetTypes()
            .Where(static type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(static contract =>
                    contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IValidator<>))
                .Select(contract => (RequestType: contract.GetGenericArguments()[0], ValidatorType: type)))
            .ToLookup(pair => pair.RequestType, pair => pair.ValidatorType);

        var uncoveredProperties = requestAssembly.GetTypes()
            .Where(static type => type.IsClass && type.Namespace == typeof(RequestUpsertTimeEntryJson).Namespace)
            .SelectMany(type => type.GetProperties().Select(property => (RequestType: type, Property: property)))
            .Select(pair =>
            {
                var enumType = Nullable.GetUnderlyingType(pair.Property.PropertyType) ?? pair.Property.PropertyType;
                return (pair.RequestType, pair.Property, EnumType: enumType);
            })
            .Where(static pair => pair.EnumType.IsEnum)
            .Where(pair => validatorTypesByRequest[pair.RequestType].Any(
                validatorType => HasMatchingEnumValidator(validatorType, pair.Property.Name, pair.EnumType)) is false)
            .Select(pair => $"{pair.RequestType.Name}.{pair.Property.Name} ({pair.EnumType.Name})")
            .OrderBy(static description => description, StringComparer.Ordinal)
            .ToList();

        uncoveredProperties.ShouldBeEmpty(
            "undefined-but-representable request enum integers must be rejected by the owning feature validator");
    }

    private static bool HasMatchingEnumValidator(Type validatorType, string propertyName, Type enumType)
    {
        var validator = Activator.CreateInstance(validatorType, nonPublic: true) as IValidator;
        validator.ShouldNotBeNull($"{validatorType.FullName} must have a parameterless constructor");

        return validator.CreateDescriptor()
            .GetValidatorsForMember(propertyName)
            .Any(component =>
                component.Validator is IEnumValidator enumValidator &&
                (Nullable.GetUnderlyingType(enumValidator.EnumType) ?? enumValidator.EnumType) == enumType);
    }
}

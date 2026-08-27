using Microsoft.AspNetCore.Mvc.ModelBinding;
using server.Exceptions;
using server.Serialization;
using Shouldly;
using Xunit;

namespace WebApi.Test.Serialization;

public sealed class ContractModelStateMessagesTest
{
    [Fact]
    public void Describe_ShouldExposeOnlyDistinctBackendAuthoredMessages()
    {
        var modelState = new ModelStateDictionary();
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string));
        modelState.AddModelError(
            "enumName",
            new ContractJsonException(ResourcesErrorMessages.ENUM_NAME_INVALID),
            metadata);
        modelState.AddModelError("duplicate", ResourcesErrorMessages.ENUM_NAME_INVALID);
        modelState.AddModelError(
            "unapprovedBackendMessage",
            new ContractJsonException("Internal detail"),
            metadata);
        modelState.AddModelError("unsafe", "The JSON value could not be converted.");

        var messages = ContractModelStateMessages.Describe(modelState);

        messages.ShouldBe([ResourcesErrorMessages.ENUM_NAME_INVALID]);
    }

    [Fact]
    public void Describe_ShouldFallBackToRequestInvalid_WhenNoSafeMessageExists()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("unsafe", "The value '999' is invalid.");

        var messages = ContractModelStateMessages.Describe(modelState);

        messages.ShouldBe([ResourcesErrorMessages.REQUEST_INVALID]);
    }
}

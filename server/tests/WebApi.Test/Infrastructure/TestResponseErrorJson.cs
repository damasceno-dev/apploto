namespace WebApi.Test.Infrastructure;

/// <summary>
/// Test-side mirror of <c>server.Communication.Responses.ResponseErrorJson</c>. The production
/// type only exposes parameterized constructors because the API never deserializes its own error
/// contract back from the wire; <see cref="System.Text.Json.JsonSerializer"/> therefore cannot
/// round-trip it. Integration tests that assert on the error payload deserialize into this local
/// DTO instead, which matches the exact wire shape (<c>errorMessages</c>) without changing the
/// stable production contract.
/// </summary>
internal class TestResponseErrorJson
{
    public List<string> ErrorMessages { get; set; } = [];
}

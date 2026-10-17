using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.OpenApi;
using server.OpenApi;
using Shouldly;
using Xunit;

namespace WebApi.Test.OpenApi;

/// <summary>
/// M7.6 follow-up Phase 4.2 (Shared M2 H3) on synthetic documents, so each rule is exercised even where today's
/// contract has no instance of it. Every expectation is checked against what the serializer actually writes for the
/// same CLR type, so the tests pin wire fidelity rather than a rule restated.
/// </summary>
public sealed class ResponseNumericFidelityTransformerTest
{
    private static readonly JsonSerializerOptions WebDefaults =
        new(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    [Fact]
    public void Narrow_ShouldFollowEachMembersSerializedForm_NestedInlineObjectsAndCollectionsIncluded()
    {
        var document = new OpenApiDocument { Paths = [] };
        var original = Object(
            ("count", Widened(JsonSchemaType.Integer, "int32")),
            ("amount", Widened(JsonSchemaType.Number, "double", nullable: true)),
            ("values", Array(Widened(JsonSchemaType.Integer, "int32"))),
            ("map", new OpenApiSchema { Type = JsonSchemaType.Object, AdditionalProperties = Widened(JsonSchemaType.Integer, "int32") }),
            ("asText", Widened(JsonSchemaType.Integer, "int32")),
            ("asTextValues", Array(Widened(JsonSchemaType.Integer, "int32"))),
            ("child", Object(("plain", Widened(JsonSchemaType.Integer, "int32")), ("asText", Widened(JsonSchemaType.Integer, "int32")))),
            ("typedChild", Object(("count", Widened(JsonSchemaType.Integer, "int32")))));
        AddOperation(document, "/inline", null, original, typeof(InlineResponse));

        ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults);

        // Inline response roots are narrowed on a copy, so read the published schema back from the document.
        var response = (OpenApiSchema)document.Paths["/inline"].Operations![HttpMethod.Post].Responses!["200"]
            .Content!["application/json"].Schema!;
        var count = Property(response, "count");
        var amount = Property(response, "amount");
        var element = (OpenApiSchema)Property(response, "values").Items!;
        var mapValue = (OpenApiSchema)Property(response, "map").AdditionalProperties!;
        var asText = Property(response, "asText");
        var asTextElement = (OpenApiSchema)Property(response, "asTextValues").Items!;
        var nestedPlain = Property(Property(response, "child"), "plain");
        var nestedAsText = Property(Property(response, "child"), "asText");
        var typedChildCount = Property(Property(response, "typedChild"), "count");
        var wire = Serialize(new InlineResponse());
        wire.GetProperty("count").ValueKind.ShouldBe(JsonValueKind.Number);
        count.Type.ShouldBe(JsonSchemaType.Integer);
        count.Pattern.ShouldBeNull();
        count.Format.ShouldBe("int32");
        amount.Type.ShouldBe(JsonSchemaType.Null | JsonSchemaType.Number);
        element.Type.ShouldBe(JsonSchemaType.Integer);
        mapValue.Type.ShouldBe(JsonSchemaType.Integer);
        nestedPlain.Type.ShouldBe(JsonSchemaType.Integer);

        // Members System.Text.Json writes as strings keep admitting a string, wherever they sit.
        wire.GetProperty("asText").ValueKind.ShouldBe(JsonValueKind.String);
        asText.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
        asText.Pattern.ShouldNotBeNull();
        wire.GetProperty("asTextValues")[0].ValueKind.ShouldBe(JsonValueKind.String);
        asTextElement.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
        wire.GetProperty("child").GetProperty("asText").ValueKind.ShouldBe(JsonValueKind.String);
        nestedAsText.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
        wire.GetProperty("typedChild").GetProperty("count").ValueKind.ShouldBe(JsonValueKind.String);
        typedChildCount.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
    }

    [Fact]
    public void Narrow_ShouldSplitASharedComponent_KeepingTheInputAndNarrowingANewResponseRepresentation()
    {
        var document = new OpenApiDocument { Paths = [] };
        var sharedValue = Widened(JsonSchemaType.Number, "double");
        var childValue = Widened(JsonSchemaType.Integer, "int32");
        var shared = Object(("value", sharedValue), ("child", new OpenApiSchemaReference(nameof(SharedChild), document)));
        shared.Required = new HashSet<string> { "value", "child" };
        var child = Object(("count", childValue));
        AddComponents(document, (nameof(SharedBody), shared), (nameof(SharedChild), child));
        var query = Widened(JsonSchemaType.Integer, "int32");
        AddOperation(document, "/shared", new OpenApiSchemaReference(nameof(SharedBody), document),
            new OpenApiSchemaReference(nameof(SharedBody), document), typeof(SharedBody), query);

        ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults);

        var components = document.Components!.Schemas!;
        components.Keys.ShouldBe(
            [nameof(SharedBody), nameof(SharedChild), nameof(SharedBody) + "Output", nameof(SharedChild) + "Output"],
            ignoreOrder: true);

        // The input representation is untouched: binding still accepts numeric strings.
        sharedValue.Type.ShouldBe(JsonSchemaType.Number | JsonSchemaType.String);
        childValue.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
        query.Type.ShouldBe(JsonSchemaType.Integer | JsonSchemaType.String);
        var operation = document.Paths["/shared"].Operations![HttpMethod.Post];
        ((OpenApiSchemaReference)operation.RequestBody!.Content!["application/json"].Schema!).Reference.Id.ShouldBe(nameof(SharedBody));

        // The response points at its own representation, narrowed, with requiredness and formats kept.
        ((OpenApiSchemaReference)operation.Responses!["200"].Content!["application/json"].Schema!).Reference.Id
            .ShouldBe(nameof(SharedBody) + "Output");
        var output = (OpenApiSchema)components[nameof(SharedBody) + "Output"];
        output.Required.ShouldBe(shared.Required, ignoreOrder: true);
        Property(output, "value").Type.ShouldBe(JsonSchemaType.Number);
        Property(output, "value").Format.ShouldBe("double");
        ((OpenApiSchemaReference)output.Properties!["child"]).Reference.Id.ShouldBe(nameof(SharedChild) + "Output");
        Property((OpenApiSchema)components[nameof(SharedChild) + "Output"], "count").Type.ShouldBe(JsonSchemaType.Integer);
    }

    [Fact]
    public void Narrow_ShouldRefuseAnOutputIdThatAlreadyExists()
    {
        var document = new OpenApiDocument { Paths = [] };
        AddComponents(document,
            (nameof(SharedChild), Object(("count", Widened(JsonSchemaType.Integer, "int32")))),
            (nameof(SharedChild) + "Output", Object()));
        AddOperation(document, "/shared", new OpenApiSchemaReference(nameof(SharedChild), document),
            new OpenApiSchemaReference(nameof(SharedChild), document), typeof(SharedChild));

        Should.Throw<InvalidOperationException>(() => ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults))
            .Message.ShouldContain("already exists");
    }

    [Fact]
    public void Narrow_ShouldRefuseAResponseWithoutADeclaredClrType()
    {
        var document = new OpenApiDocument { Paths = [] };
        AddOperation(document, "/untyped", null, Object(("count", Widened(JsonSchemaType.Integer, "int32"))), type: null);

        Should.Throw<InvalidOperationException>(() => ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults))
            .Message.ShouldContain("no declared CLR type");
    }

    [Fact]
    public void Narrow_ShouldRefuseOneComponentReachedAsTwoClrTypes()
    {
        var document = new OpenApiDocument { Paths = [] };
        AddComponents(document, ("Ambiguous", Object(("count", Widened(JsonSchemaType.Integer, "int32")))));
        AddOperation(document, "/a", null, new OpenApiSchemaReference("Ambiguous", document), typeof(SharedChild));
        AddOperation(document, "/b", null, new OpenApiSchemaReference("Ambiguous", document), typeof(TypedChild));

        Should.Throw<InvalidOperationException>(() => ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults))
            .Message.ShouldContain("reached as both");
    }

    [Fact]
    public void Narrow_ShouldRefuseANumberWrittenByACustomConverter()
    {
        var document = new OpenApiDocument { Paths = [] };
        AddOperation(document, "/converted", null, Object(("count", Widened(JsonSchemaType.Integer, "int32"))), typeof(ConvertedResponse));

        Should.Throw<InvalidOperationException>(() => ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults))
            .Message.ShouldContain("custom converter");
    }

    [Fact]
    public void Narrow_ShouldRefuseASchemaPropertyTheSerializerDoesNotWrite()
    {
        var document = new OpenApiDocument { Paths = [] };
        AddOperation(document, "/drift", null, Object(("missing", Widened(JsonSchemaType.Integer, "int32"))), typeof(SharedChild));

        Should.Throw<InvalidOperationException>(() => ResponseNumericFidelityOpenApiDocumentTransformer.Narrow(document, WebDefaults))
            .Message.ShouldContain("no matching serialized member");
    }

    private static void AddComponents(OpenApiDocument document, params (string Id, OpenApiSchema Schema)[] components)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        foreach (var (id, schema) in components)
            document.Components.Schemas[id] = schema;
        document.RegisterComponents();
    }

    private static void AddOperation(
        OpenApiDocument document,
        string path,
        IOpenApiSchema? body,
        IOpenApiSchema response,
        Type? type,
        OpenApiSchema? query = null)
    {
        var operation = new OpenApiOperation
        {
            Parameters = query is null ? [] : [new OpenApiParameter { Name = "page", In = ParameterLocation.Query, Schema = query }],
            RequestBody = body is null ? null : new OpenApiRequestBody { Content = Json(body) },
            Responses = new OpenApiResponses { ["200"] = new OpenApiResponse { Content = Json(response) } }
        };
        ResponseNumericFidelityOpenApiDocumentTransformer.StampResponseTypes(
            operation,
            type is null ? new Dictionary<string, Type>() : new Dictionary<string, Type> { ["200"] = type });
        document.Paths[path] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Post] = operation }
        };
    }

    private static JsonElement Serialize<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, WebDefaults)).RootElement;

    private static Dictionary<string, OpenApiMediaType> Json(IOpenApiSchema schema) =>
        new() { ["application/json"] = new OpenApiMediaType { Schema = schema } };

    private static OpenApiSchema Object(params (string Name, IOpenApiSchema Schema)[] properties) => new()
    {
        Type = JsonSchemaType.Object,
        Properties = properties.ToDictionary(property => property.Name, property => property.Schema)
    };

    private static OpenApiSchema Array(IOpenApiSchema items) => new() { Type = JsonSchemaType.Array, Items = items };

    private static OpenApiSchema Widened(JsonSchemaType numeric, string format, bool nullable = false) => new()
    {
        Type = numeric | JsonSchemaType.String | (nullable ? JsonSchemaType.Null : 0),
        Format = format,
        Pattern = "^-?(?:0|[1-9]\\d*)$"
    };

    private static OpenApiSchema Property(OpenApiSchema schema, string name) => (OpenApiSchema)schema.Properties![name];

    private sealed class InlineResponse
    {
        public int Count { get; init; } = 1;
        public double? Amount { get; init; } = 2.5;
        public List<int> Values { get; init; } = [3];
        public Dictionary<string, int> Map { get; init; } = new() { ["a"] = 4 };

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public int AsText { get; init; } = 5;

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public List<int> AsTextValues { get; init; } = [6];

        public NestedChild Child { get; init; } = new();
        public TypedChild TypedChild { get; init; } = new();
    }

    private sealed class NestedChild
    {
        public int Plain { get; init; } = 7;

        [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
        public int AsText { get; init; } = 8;
    }

    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    private sealed class TypedChild
    {
        public int Count { get; init; } = 9;
    }

    private sealed class SharedBody
    {
        public double Value { get; init; }
        public SharedChild Child { get; init; } = new();
    }

    private sealed class SharedChild
    {
        public int Count { get; init; }
    }

    private sealed class ConvertedResponse
    {
        [JsonConverter(typeof(TextInt32Converter))]
        public int Count { get; init; }
    }

    private sealed class TextInt32Converter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            int.Parse(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}

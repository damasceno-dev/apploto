using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace server.OpenApi;

/// <summary>
/// Response numeric fidelity (M7.6 follow-up Phase 4.2, Shared M2 handoff H3). The generator reads numbers with the
/// web defaults' <c>AllowReadingFromString</c>, so it publishes every numeric member as <c>number | string</c> (with a
/// numeric <c>pattern</c>). That is true for requests — binding accepts numeric strings — but false for responses,
/// which the MVC serializer writes as JSON numbers unless a member's number handling says otherwise.
/// <list type="number">
/// <item>As an operation transformer it records, on each operation, the CLR type ApiExplorer declares for every
/// response status, so the response side starts from real metadata rather than schema names.</item>
/// <item>As a document transformer it first gives every widened component that both a request and a response reach
/// a separate response representation (<c>{id}Output</c>) and points response references at it, so the input schema
/// keeps accepting numeric strings.</item>
/// <item>It then walks every response schema alongside the MVC serializer's <see cref="JsonTypeInfo"/> for the declared
/// type — properties, collection elements and dictionary values, nested inline objects included — and narrows each
/// widened number to <c>number</c>/<c>integer</c> only where its effective number handling does not write strings.
/// That handling follows System.Text.Json's verified precedence: a member's property attribute, then its declaring
/// type's attribute, then — for a collection or dictionary — the collection type's own attribute, then the options;
/// elements take their collection's handling, and a nested collection starts again from its own type. Nullability and
/// formats are kept. Request-only schemas and parameters are never touched.</item>
/// </list>
/// Anything the walk cannot tie to serializer metadata — a custom converter on a number or collection, an unmodeled
/// schema shape, one component reached as two CLR types, or a response without a declared type — stops generation
/// instead of being guessed.
/// </summary>
internal sealed class ResponseNumericFidelityOpenApiDocumentTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>Operation metadata key (never serialized) holding the response CLR type per response key.</summary>
    internal const string ResponseTypesMetadataKey = "server.openapi.response-clr-types";

    /// <summary>Suffix of the response representation split from a shared request/response component.</summary>
    internal const string OutputSuffix = "Output";

    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(Half), typeof(Int128), typeof(UInt128)
    ];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var response in context.Description.SupportedResponseTypes)
        {
            if (response.Type is null || response.Type == typeof(void))
                continue;

            var key = response.IsDefaultResponse ? "default" : response.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (types.TryGetValue(key, out var existing) && existing != response.Type)
            {
                throw new InvalidOperationException(
                    $"{context.Description.ActionDescriptor.DisplayName} declares response {key} as both " +
                    $"{existing.FullName} and {response.Type.FullName}.");
            }

            types[key] = response.Type;
        }

        StampResponseTypes(operation, types);
        return Task.CompletedTask;
    }

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var serializer = new JsonSerializerOptions(
            context.ApplicationServices.GetRequiredService<IOptions<MvcJsonOptions>>().Value.JsonSerializerOptions);
        Narrow(document, serializer);
        return Task.CompletedTask;
    }

    internal static void StampResponseTypes(OpenApiOperation operation, IReadOnlyDictionary<string, Type> types)
    {
        operation.Metadata ??= new Dictionary<string, object>();
        operation.Metadata[ResponseTypesMetadataKey] = types;
    }

    /// <summary>
    /// Splits shared widened components and narrows response numbers in <paramref name="document"/>, using
    /// <paramref name="serializer"/> as the response writer and the response types stamped on each operation.
    /// </summary>
    internal static void Narrow(OpenApiDocument document, JsonSerializerOptions serializer)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        var components = document.Components.Schemas;
        var responses = ResponseRoots(document);

        var requestReachable = Reachable(RequestRoots(document), components);
        var responseReachable = Reachable(responses.Select(root => root.Media.Schema!), components);

        var outputIds = SplitSharedComponents(document, requestReachable, responseReachable);
        foreach (var root in responses)
            root.Media.Schema = Rewire(root.Media.Schema!, outputIds, document, copy: true);
        foreach (var id in responseReachable.Except(requestReachable).Order(StringComparer.Ordinal))
            components[id] = Rewire(components[id], outputIds, document, copy: false);

        responseReachable = Reachable(responses.Select(root => root.Media.Schema!), components);
        foreach (var id in responseReachable.Intersect(requestReachable).Order(StringComparer.Ordinal))
        {
            if (ContainsWidenedNumber(components[id]))
            {
                throw new InvalidOperationException(
                    $"OpenAPI schema '{id}' is still reachable from both requests and responses after the response split.");
            }
        }

        var walk = new TypedWalk(serializer, components);
        foreach (var root in responses)
            walk.Visit(root.Media.Schema!, root.Type, ValueContext.Root, root.Location);

        foreach (var node in Nodes(responses.Select(root => root.Media.Schema!), components).Where(IsWidenedNumber))
        {
            if (walk.Decisions.TryGetValue(node, out var narrow) is false)
            {
                throw new InvalidOperationException(
                    "A response number is not reached by the serializer metadata walk, so its wire form cannot be verified.");
            }

            if (narrow)
            {
                node.Type = node.Type!.Value & ~JsonSchemaType.String;
                node.Pattern = null;
            }
        }
    }

    private sealed record ResponseRoot(OpenApiMediaType Media, Type Type, string Location);

    private static List<IOpenApiSchema> RequestRoots(OpenApiDocument document)
    {
        var roots = new List<IOpenApiSchema>();
        foreach (var (_, operation) in Operations(document))
        {
            foreach (var parameter in operation.Parameters ?? [])
            {
                if (parameter.Schema is not null)
                    roots.Add(parameter.Schema);
            }

            foreach (var media in operation.RequestBody?.Content?.Values ?? [])
            {
                if (media.Schema is not null)
                    roots.Add(media.Schema);
            }
        }

        return roots;
    }

    private static List<ResponseRoot> ResponseRoots(OpenApiDocument document)
    {
        var roots = new List<ResponseRoot>();
        foreach (var (location, operation) in Operations(document))
        {
            var types = operation.Metadata?.TryGetValue(ResponseTypesMetadataKey, out var stamped) is true
                ? (IReadOnlyDictionary<string, Type>)stamped
                : null;
            foreach (var (key, response) in operation.Responses ?? [])
            {
                foreach (var (mediaType, media) in response.Content ?? new Dictionary<string, OpenApiMediaType>())
                {
                    if (media.Schema is null)
                        continue;
                    if (types is null || types.TryGetValue(key, out var type) is false)
                    {
                        throw new InvalidOperationException(
                            $"{location} response {key} ({mediaType}) has no declared CLR type, so its serialized numbers cannot be verified.");
                    }

                    roots.Add(new ResponseRoot(media, type, $"{location} {key}"));
                }
            }
        }

        return roots;
    }

    private static IEnumerable<(string Location, OpenApiOperation Operation)> Operations(OpenApiDocument document)
    {
        foreach (var (path, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
                yield return ($"{method.Method} {path}", operation);
        }
    }

    /// <summary>
    /// Adds <c>{id}Output</c> for every component reachable from both directions whose response representation can
    /// differ — it holds a widened number, or references a component that is split — and returns id → output id.
    /// </summary>
    private static Dictionary<string, string> SplitSharedComponents(
        OpenApiDocument document,
        HashSet<string> requestReachable,
        HashSet<string> responseReachable)
    {
        var components = document.Components!.Schemas!;
        var shared = requestReachable.Intersect(responseReachable).ToHashSet(StringComparer.Ordinal);
        var split = shared.Where(id => ContainsWidenedNumber(components[id])).ToHashSet(StringComparer.Ordinal);
        while (true)
        {
            var next = shared
                .Where(id => split.Contains(id) is false && DirectReferences(components[id]).Any(split.Contains))
                .ToList();
            if (next.Count == 0)
                break;
            split.UnionWith(next);
        }

        var outputIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in split.Order(StringComparer.Ordinal))
        {
            var outputId = id + OutputSuffix;
            if (components.ContainsKey(outputId))
            {
                throw new InvalidOperationException(
                    $"OpenAPI schema '{id}' needs a separate response representation, but '{outputId}' already exists.");
            }

            outputIds[id] = outputId;
        }

        foreach (var (id, outputId) in outputIds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            components[outputId] = Rewire(components[id], outputIds, document, copy: true);

        return outputIds;
    }

    /// <summary>
    /// Returns <paramref name="schema"/> with every reference to a split component pointed at its output id. With
    /// <paramref name="copy"/> every inline node is a new object, so later narrowing never touches the original tree.
    /// </summary>
    private static IOpenApiSchema Rewire(
        IOpenApiSchema schema,
        IReadOnlyDictionary<string, string> outputIds,
        OpenApiDocument document,
        bool copy)
    {
        if (schema is OpenApiSchemaReference reference)
        {
            if (reference.Reference.Id is not { } id || outputIds.TryGetValue(id, out var outputId) is false)
                return reference;

            return new OpenApiSchemaReference(outputId, document)
            {
                Description = reference.Description,
                Title = reference.Title,
                Default = reference.Default,
                ReadOnly = reference.ReadOnly,
                WriteOnly = reference.WriteOnly,
                Examples = reference.Examples,
                Deprecated = reference.Deprecated,
                Extensions = reference.Extensions
            };
        }

        if (schema is not OpenApiSchema node)
            throw new InvalidOperationException($"Unsupported OpenAPI schema implementation {schema.GetType().FullName}.");

        var target = copy ? (OpenApiSchema)node.CreateShallowCopy() : node;
        target.Properties = Map(node.Properties);
        target.PatternProperties = Map(node.PatternProperties);
        target.Definitions = Map(node.Definitions);
        target.Items = Each(node.Items);
        target.AdditionalProperties = Each(node.AdditionalProperties);
        target.UnevaluatedPropertiesSchema = Each(node.UnevaluatedPropertiesSchema);
        target.Not = Each(node.Not);
        target.AllOf = List(node.AllOf);
        target.AnyOf = List(node.AnyOf);
        target.OneOf = List(node.OneOf);
        return target;

        IOpenApiSchema? Each(IOpenApiSchema? child) => child is null ? null : Rewire(child, outputIds, document, copy);

        IDictionary<string, IOpenApiSchema>? Map(IDictionary<string, IOpenApiSchema>? children) =>
            children?.ToDictionary(pair => pair.Key, pair => Rewire(pair.Value, outputIds, document, copy), StringComparer.Ordinal);

        IList<IOpenApiSchema>? List(IList<IOpenApiSchema>? children) =>
            children?.Select(child => Rewire(child, outputIds, document, copy)).ToList();
    }

    private static HashSet<string> Reachable(IEnumerable<IOpenApiSchema> roots, IDictionary<string, IOpenApiSchema> components)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<IOpenApiSchema>(roots);
        var visited = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance);
        while (pending.Count > 0)
        {
            var schema = pending.Pop();
            if (visited.Add(schema) is false)
                continue;

            if (schema is OpenApiSchemaReference reference)
            {
                if (reference.Reference.Id is { } id && reached.Add(id) && components.TryGetValue(id, out var target))
                    pending.Push(target);
                continue;
            }

            foreach (var child in Children(schema))
                pending.Push(child);
        }

        return reached;
    }

    /// <summary>Every inline node reachable from <paramref name="roots"/>, following references into components.</summary>
    private static IEnumerable<OpenApiSchema> Nodes(IEnumerable<IOpenApiSchema> roots, IDictionary<string, IOpenApiSchema> components)
    {
        var pending = new Stack<IOpenApiSchema>(roots);
        var visited = new HashSet<IOpenApiSchema>(ReferenceEqualityComparer.Instance);
        while (pending.Count > 0)
        {
            var schema = pending.Pop();
            if (visited.Add(schema) is false)
                continue;

            if (schema is OpenApiSchemaReference reference)
            {
                if (reference.Reference.Id is { } id && components.TryGetValue(id, out var target))
                    pending.Push(target);
                continue;
            }

            if (schema is OpenApiSchema node)
                yield return node;
            foreach (var child in Children(schema))
                pending.Push(child);
        }
    }

    private static IEnumerable<IOpenApiSchema> Children(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema node)
            yield break;

        foreach (var child in (node.Properties?.Values ?? [])
                     .Concat(node.PatternProperties?.Values ?? [])
                     .Concat(node.Definitions?.Values ?? [])
                     .Concat(node.AllOf ?? [])
                     .Concat(node.AnyOf ?? [])
                     .Concat(node.OneOf ?? []))
        {
            yield return child;
        }

        foreach (var child in new[] { node.Items, node.AdditionalProperties, node.UnevaluatedPropertiesSchema, node.Not })
        {
            if (child is not null)
                yield return child;
        }
    }

    /// <summary>References directly inside <paramref name="schema"/>'s inline tree (not followed).</summary>
    private static IEnumerable<string> DirectReferences(IOpenApiSchema schema)
    {
        foreach (var child in Children(schema))
        {
            if (child is OpenApiSchemaReference { Reference.Id: { } id })
            {
                yield return id;
                continue;
            }

            foreach (var nested in DirectReferences(child))
                yield return nested;
        }
    }

    /// <summary>A widened number is a schema typed as a number/integer that also admits a string.</summary>
    private static bool IsWidenedNumber(OpenApiSchema schema) =>
        schema.Type is { } type &&
        type.HasFlag(JsonSchemaType.String) &&
        (type.HasFlag(JsonSchemaType.Number) || type.HasFlag(JsonSchemaType.Integer));

    /// <summary>Whether <paramref name="schema"/>'s inline tree (references not followed) holds a widened number.</summary>
    private static bool ContainsWidenedNumber(IOpenApiSchema schema) =>
        schema is OpenApiSchema node && (IsWidenedNumber(node) || Children(node).Any(ContainsWidenedNumber));

    private static bool ContainsReferenceOrWidenedNumber(IOpenApiSchema schema) =>
        schema is OpenApiSchemaReference ||
        (schema is OpenApiSchema node && (IsWidenedNumber(node) || Children(node).Any(ContainsReferenceOrWidenedNumber)));

    /// <summary>
    /// The number handling a value inherits from where it sits, as System.Text.Json resolves it (verified against real
    /// serialization): a member value gets its property attribute, else its declaring type's attribute
    /// (<paramref name="Explicit"/> with <paramref name="FromMember"/>); a collection element gets its collection's
    /// element handling (<paramref name="Explicit"/> without <paramref name="FromMember"/>); a root value gets neither.
    /// </summary>
    private readonly record struct ValueContext(JsonNumberHandling? Explicit, bool FromMember, JsonPropertyInfo? Member)
    {
        public static ValueContext Root => new(null, false, null);
    }

    /// <summary>Walks response schemas alongside the MVC serializer's type metadata and records a decision per number.</summary>
    private sealed class TypedWalk(JsonSerializerOptions serializer, IDictionary<string, IOpenApiSchema> components)
    {
        private readonly Dictionary<string, Type> _componentTypes = new(StringComparer.Ordinal);
        private readonly HashSet<(string Id, ValueContext Context)> _visited = [];

        public Dictionary<OpenApiSchema, bool> Decisions { get; } = new(ReferenceEqualityComparer.Instance);

        public void Visit(IOpenApiSchema schema, Type type, ValueContext context, string location)
        {
            if (schema is OpenApiSchemaReference reference)
            {
                var id = reference.Reference.Id
                         ?? throw new InvalidOperationException($"{location} holds a schema reference without an id.");
                if (_componentTypes.TryGetValue(id, out var known) && known != type)
                {
                    throw new InvalidOperationException(
                        $"OpenAPI schema '{id}' is reached as both {known.FullName} and {type.FullName}; one schema " +
                        "cannot describe two serialized types.");
                }

                _componentTypes[id] = type;
                if (_visited.Add((id, context)))
                    Visit(components[id], type, context, $"#/components/schemas/{id}");
                return;
            }

            if (schema is not OpenApiSchema node)
                throw new InvalidOperationException($"{location} uses unsupported schema implementation {schema.GetType().FullName}.");

            type = Nullable.GetUnderlyingType(type) ?? type;
            foreach (var branch in (node.AllOf ?? []).Concat(node.AnyOf ?? []).Concat(node.OneOf ?? []))
                Visit(branch, type, context, location);

            var unmodeled = new[] { node.Not, node.UnevaluatedPropertiesSchema }
                .Concat(node.PatternProperties?.Values ?? [])
                .Concat(node.Definitions?.Values ?? []);
            if (unmodeled.Any(child => child is not null && ContainsReferenceOrWidenedNumber(child)))
                throw new InvalidOperationException($"{location} uses a schema keyword the numeric walk does not model.");

            if (IsWidenedNumber(node))
                Decide(node, type, context, location);

            var hasProperties = node.Properties is { Count: > 0 };
            if (hasProperties is false && node.Items is null && node.AdditionalProperties is null)
                return;

            var info = serializer.GetTypeInfo(type);
            switch (info.Kind)
            {
                case JsonTypeInfoKind.Object when node.Items is null && node.AdditionalProperties is null:
                    foreach (var (name, propertySchema) in node.Properties ?? new Dictionary<string, IOpenApiSchema>())
                    {
                        var property = info.Properties.FirstOrDefault(candidate => candidate.Name == name)
                            ?? throw new InvalidOperationException(
                                $"{location}.{name} has no matching serialized member on {type.FullName}.");
                        var memberContext = new ValueContext(property.NumberHandling ?? info.NumberHandling, FromMember: true, property);
                        Visit(propertySchema, property.PropertyType, memberContext, $"{location}.{name}");
                    }

                    break;
                case JsonTypeInfoKind.Enumerable when node.Items is not null && hasProperties is false:
                    Visit(node.Items, info.ElementType!, ElementContext(type, info, context, location), $"{location}[]");
                    break;
                case JsonTypeInfoKind.Dictionary when node.AdditionalProperties is not null && hasProperties is false:
                    Visit(node.AdditionalProperties, info.ElementType!, ElementContext(type, info, context, location), $"{location}{{}}");
                    break;
                default:
                    if (Children(node).Except((node.AllOf ?? []).Concat(node.AnyOf ?? []).Concat(node.OneOf ?? []))
                        .Any(ContainsReferenceOrWidenedNumber))
                    {
                        throw new InvalidOperationException(
                            $"{location} is published as a structured schema, but {type.FullName} serializes as " +
                            $"{info.Kind}; its numbers cannot be verified.");
                    }

                    break;
            }
        }

        /// <summary>
        /// A collection's element (or dictionary value) handling: the member's own handling when the collection is a
        /// member value, else the collection type's attribute, else the options. A collection that is itself an
        /// element does not inherit its parent collection's handling.
        /// </summary>
        private ValueContext ElementContext(Type type, JsonTypeInfo info, ValueContext context, string location)
        {
            if (context.Member?.CustomConverter is not null || serializer.Converters.Any(converter => converter.CanConvert(type)))
                throw new InvalidOperationException($"{location} is written by a custom converter; its wire form is not modeled.");

            var inherited = context.FromMember ? context.Explicit : null;
            return new ValueContext(inherited ?? info.NumberHandling ?? serializer.NumberHandling, FromMember: false, Member: null);
        }

        private void Decide(OpenApiSchema node, Type type, ValueContext context, string location)
        {
            if (NumericTypes.Contains(type) is false)
                throw new InvalidOperationException($"{location} is published as a number but serializes {type.FullName}.");
            if (context.Member?.CustomConverter is not null || serializer.Converters.Any(converter => converter.CanConvert(type)))
                throw new InvalidOperationException($"{location} is written by a custom converter; its wire form is not modeled.");

            var effective = context.Explicit ?? serializer.NumberHandling;
            var narrow = (effective & JsonNumberHandling.WriteAsString) == 0;
            if (Decisions.TryGetValue(node, out var previous) && previous != narrow)
                throw new InvalidOperationException($"{location} is serialized both as a number and as a string.");

            Decisions[node] = narrow;
        }
    }
}

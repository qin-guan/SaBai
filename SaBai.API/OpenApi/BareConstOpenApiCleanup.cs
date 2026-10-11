using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SaBai.API.OpenApi;

/// <summary>
/// Workaround for a Microsoft.OpenApi 2.x defect.
/// </summary>
/// <remarks>
/// <para>
/// <c>OpenApiSchema(IOpenApiSchema)</c> unconditionally assigns <c>Const</c>
/// (<c>Const = schema.Const ?? Const;</c>). Because the <c>Const</c> setter sets the
/// internal <c>WasConstExplicitlySet</c> flag, cloning any schema - even one with no
/// const - marks it as having an explicit const. The serializer then emits
/// <c>"const": null</c> for that schema.
/// </para>
/// <para>
/// FastEndpoints clones request DTO schemas to apply validation metadata, so every
/// request body schema ended up serialized as <c>{"const": null, ...}</c>. Downstream
/// code generators (Kubb) read that as the whole schema being <c>null</c>, which is why
/// generated request bodies were typed <c>null</c>.
/// </para>
/// <para>
/// Response schemas are referenced rather than cloned, so they were never affected.
/// This document transformer runs after FastEndpoints has built the document and clears
/// the flag wherever no const was actually provided, restoring valid request schemas.
/// </para>
/// </remarks>
internal static class BareConstOpenApiCleanup
{
    private const string FlagPropertyName = "WasConstExplicitlySet";
    private const string FlagBackingFieldName = "<" + FlagPropertyName + ">k__BackingField";

    private static readonly FieldInfo? FlagField =
        typeof(OpenApiSchema).GetField(FlagBackingFieldName, BindingFlags.Instance | BindingFlags.NonPublic) ??
        typeof(OpenApiSchema).GetField(FlagPropertyName, BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Registers the cleanup transformer on the OpenAPI options.</summary>
    public static void Register(OpenApiOptions options)
    {
        // If Microsoft.OpenApi changes its internals we silently skip rather than crash
        // the whole API at startup over a documentation-only concern.
        if (FlagField is null)
            return;

        options.AddDocumentTransformer((document, _, _) =>
        {
            if (document.Components?.Schemas is { } schemas)
            {
                var visited = new HashSet<IOpenApiSchema>(ReferenceComparer.Instance);

                foreach (var schema in schemas.Values)
                    ClearBareConst(schema, visited);
            }

            return Task.CompletedTask;
        });
    }

    private static void ClearBareConst(IOpenApiSchema? schema, HashSet<IOpenApiSchema> visited)
    {
        // References point back at components which are visited directly, so there is
        // nothing nested to walk here.
        if (schema is null || !visited.Add(schema) || schema is not OpenApiSchema concrete)
            return;

        if (concrete.Const is null)
            FlagField!.SetValue(concrete, false);

        if (concrete.Properties is { } properties)
        {
            foreach (var property in properties.Values)
                ClearBareConst(property, visited);
        }

        ClearBareConst(concrete.Items, visited);
        ClearBareConst(concrete.AdditionalProperties, visited);
        ClearBareConst(concrete.Not, visited);
        ClearBareConst(concrete.Contains, visited);
        ClearBareConst(concrete.PropertyNames, visited);

        ClearAll(concrete.AllOf, visited);
        ClearAll(concrete.OneOf, visited);
        ClearAll(concrete.AnyOf, visited);
    }

    private static void ClearAll(IList<IOpenApiSchema>? schemas, HashSet<IOpenApiSchema> visited)
    {
        if (schemas is null)
            return;

        foreach (var schema in schemas)
            ClearBareConst(schema, visited);
    }

    private sealed class ReferenceComparer : IEqualityComparer<IOpenApiSchema>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(IOpenApiSchema? x, IOpenApiSchema? y) => ReferenceEquals(x, y);

        public int GetHashCode(IOpenApiSchema obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

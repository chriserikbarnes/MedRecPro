using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

[assembly: MedRecPro.Api.Controllers.SwaggerTagDocumentationAttribute(
    "Label",
    "Structured Product Labeling operations grouped by documents, sections, products, ingredients, classification, imports, comparisons, and search.",
    "Label ")]
[assembly: MedRecPro.Api.Controllers.SwaggerTagDocumentationAttribute(
    "Settings",
    "Application configuration, diagnostics, cache, and administrative log operations.",
    "Settings ")]
[assembly: MedRecPro.Api.Controllers.SwaggerTagDocumentationAttribute(
    "Users",
    "User authentication, directory, activity, profile, MCP integration, and account-administration operations.",
    "User ")]
#if !DEBUG
[assembly: MedRecPro.Api.Controllers.SwaggerTagDocumentationAttribute(
    "MedRecPro",
    "MedRecPro application status and API entry-point information.")]
#endif

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Declares the Swagger UI group for a controller or action.
    /// </summary>
    /// <remarks>
    /// Group metadata affects OpenAPI presentation only and does not participate in routing, model binding, authorization,
    /// or response generation. Method metadata takes precedence over controller metadata.
    /// </remarks>
    /// <seealso cref="SwaggerGroupOperationFilter"/>
    /// <seealso cref="SwaggerGroupDocumentFilter"/>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SwaggerGroupAttribute : Attribute
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the normalized Swagger group name.
        /// </summary>
        /// <seealso cref="OpenApiTag.Name"/>
        public string Name { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the optional normalized document-level group description.
        /// </summary>
        /// <remarks>
        /// Blank descriptions are normalized to <see langword="null"/> and do not create document-level tags.
        /// </remarks>
        /// <seealso cref="OpenApiTag.Description"/>
        public string? Description { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="SwaggerGroupAttribute"/> class.
        /// </summary>
        /// <param name="name">Swagger UI group name.</param>
        /// <param name="description">Optional document-level group description.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is blank.</exception>
        /// <seealso cref="SwaggerGroupMetadataResolver"/>
        public SwaggerGroupAttribute(string name, string? description = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Swagger group name cannot be blank.", nameof(name));
            }

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Declares stable documentation for a Swagger tag and, optionally, the prefix shared by child tags.
    /// </summary>
    /// <remarks>
    /// Assembly-level declarations keep tag presentation metadata independent from controller routing. An optional child
    /// prefix lets the Swagger UI hierarchy script discover parent-child relationships from the OpenAPI document.
    /// </remarks>
    /// <seealso cref="SwaggerTagDocumentationDocumentFilter"/>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    internal sealed class SwaggerTagDocumentationAttribute : Attribute
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the normalized document-level tag name.
        /// </summary>
        /// <seealso cref="OpenApiTag.Name"/>
        public string Name { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the optional normalized prefix used by child tags in a UI family.
        /// </summary>
        /// <remarks>
        /// Standalone documented tags return <see langword="null"/> and are not treated as collapsible parents.
        /// </remarks>
        /// <seealso cref="SwaggerTagDocumentationDocumentFilter.ChildTagPrefixExtensionName"/>
        public string? ChildTagPrefix { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the normalized sentence displayed beside the tag title.
        /// </summary>
        /// <seealso cref="OpenApiTag.Description"/>
        public string Description { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a Swagger tag-documentation declaration.
        /// </summary>
        /// <param name="name">Document-level tag name.</param>
        /// <param name="description">Tag documentation sentence.</param>
        /// <param name="childTagPrefix">Optional exact prefix shared by child operation tags.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="description"/> is blank.</exception>
        /// <seealso cref="SwaggerTagDocumentationDocumentFilter"/>
        public SwaggerTagDocumentationAttribute(string name, string description, string? childTagPrefix = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Swagger tag name cannot be blank.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Swagger tag description cannot be blank.", nameof(description));
            }

            Name = name.Trim();
            Description = description.Trim();
            ChildTagPrefix = string.IsNullOrWhiteSpace(childTagPrefix) ? null : childTagPrefix.TrimStart();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves nearest-wins Swagger group metadata for reflected API actions.
    /// </summary>
    /// <remarks>
    /// Both Swagger filters use this resolver so method-level and controller-level grouping cannot drift.
    /// </remarks>
    /// <seealso cref="SwaggerGroupAttribute"/>
    internal static class SwaggerGroupMetadataResolver
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Resolves method-level metadata before controller-level metadata.
        /// </summary>
        /// <param name="methodInfo">Rendered API action method.</param>
        /// <returns>The nearest Swagger group declaration, or <see langword="null"/> when none exists.</returns>
        /// <seealso cref="SwaggerGroupAttribute"/>
        internal static SwaggerGroupAttribute? Resolve(MethodInfo methodInfo)
        {
            #region implementation

            return methodInfo.GetCustomAttribute<SwaggerGroupAttribute>(inherit: true)
                ?? methodInfo.DeclaringType?.GetCustomAttribute<SwaggerGroupAttribute>(inherit: true);

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies resolved Swagger group metadata to generated OpenAPI operations.
    /// </summary>
    /// <remarks>
    /// The filter replaces the generated tag list with exactly one declared group and leaves every wire-contract field
    /// unchanged.
    /// </remarks>
    /// <seealso cref="SwaggerGroupAttribute"/>
    /// <seealso cref="SwaggerGroupMetadataResolver"/>
    internal sealed class SwaggerGroupOperationFilter : IOperationFilter
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Copies the nearest Swagger group name into the operation tag list.
        /// </summary>
        /// <param name="operation">OpenAPI operation being generated.</param>
        /// <param name="context">Operation filter context containing the reflected action method.</param>
        /// <seealso cref="IOperationFilter"/>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            #region implementation

            var metadata = SwaggerGroupMetadataResolver.Resolve(context.MethodInfo);
            if (metadata != null)
            {
                operation.Tags = new List<OpenApiTag>
                {
                    new() { Name = metadata.Name }
                };
            }

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Merges rendered Swagger group descriptions into document-level tags.
    /// </summary>
    /// <remarks>
    /// Only actions present in <see cref="DocumentFilterContext.ApiDescriptions"/> contribute metadata, so ignored or
    /// non-rendered actions cannot create orphan tags. Existing tag order and nonblank descriptions are preserved; new
    /// described groups are appended in ordinal name order.
    /// </remarks>
    /// <seealso cref="SwaggerGroupAttribute"/>
    /// <seealso cref="SwaggerGroupMetadataResolver"/>
    internal sealed class SwaggerGroupDocumentFilter : IDocumentFilter
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Merges unique rendered group descriptions into the OpenAPI document.
        /// </summary>
        /// <param name="swaggerDoc">Generated OpenAPI document.</param>
        /// <param name="context">Document filter context containing rendered API descriptions.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when rendered actions declare different nonblank descriptions for the same group name.
        /// </exception>
        /// <seealso cref="IDocumentFilter"/>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            #region implementation

            var describedGroups = context.ApiDescriptions
                .Select(description => (description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo)
                .Where(methodInfo => methodInfo != null)
                .Select(methodInfo => SwaggerGroupMetadataResolver.Resolve(methodInfo!))
                .Where(metadata => metadata?.Description != null)
                .Select(metadata => metadata!)
                .GroupBy(metadata => metadata.Name, StringComparer.Ordinal)
                .Select(group =>
                {
                    var descriptions = group
                        .Select(metadata => metadata.Description!)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();

                    if (descriptions.Length > 1)
                    {
                        throw new InvalidOperationException(
                            $"Swagger group '{group.Key}' declares conflicting descriptions: {string.Join(" | ", descriptions)}");
                    }

                    return new OpenApiTag
                    {
                        Name = group.Key,
                        Description = descriptions[0]
                    };
                })
                .OrderBy(tag => tag.Name, StringComparer.Ordinal)
                .ToArray();

            swaggerDoc.Tags ??= new List<OpenApiTag>();

            foreach (var describedGroup in describedGroups)
            {
                var existingTag = swaggerDoc.Tags.FirstOrDefault(tag =>
                    string.Equals(tag.Name, describedGroup.Name, StringComparison.Ordinal));

                if (existingTag == null)
                {
                    swaggerDoc.Tags.Add(describedGroup);
                }
                else if (string.IsNullOrWhiteSpace(existingTag.Description))
                {
                    existingTag.Description = describedGroup.Description;
                }
            }

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Publishes stable tag descriptions and optional child-prefix metadata for Swagger UI families.
    /// </summary>
    /// <remarks>
    /// Tag definitions come from assembly metadata, keeping this filter generic. Duplicate declared tags contributed by
    /// split controllers are consolidated, and explicit descriptions replace arbitrary controller summaries.
    /// </remarks>
    /// <seealso cref="SwaggerTagDocumentationAttribute"/>
    /// <seealso cref="OpenApiTag.Extensions"/>
    internal sealed class SwaggerTagDocumentationDocumentFilter : IDocumentFilter
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the OpenAPI extension name that carries a documented tag's exact child-tag prefix.
        /// </summary>
        /// <seealso cref="OpenApiString"/>
        internal const string ChildTagPrefixExtensionName = "x-medrecpro-child-tag-prefix";

        /**************************************************************/
        /// <summary>
        /// Adds or normalizes all declared tags in the generated OpenAPI document.
        /// </summary>
        /// <param name="swaggerDoc">Generated OpenAPI document.</param>
        /// <param name="context">Document filter context for the current generation pass.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the assembly declares conflicting definitions for one tag.
        /// </exception>
        /// <seealso cref="IDocumentFilter"/>
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            #region implementation

            var documentedTags = typeof(SwaggerTagDocumentationDocumentFilter).Assembly
                .GetCustomAttributes<SwaggerTagDocumentationAttribute>()
                .GroupBy(documentation => documentation.Name, StringComparer.Ordinal)
                .Select(group =>
                {
                    var definitions = group.ToArray();
                    var first = definitions[0];

                    if (definitions.Any(definition =>
                            !string.Equals(definition.ChildTagPrefix, first.ChildTagPrefix, StringComparison.Ordinal) ||
                            !string.Equals(definition.Description, first.Description, StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            $"Swagger tag '{group.Key}' declares conflicting prefixes or descriptions.");
                    }

                    return first;
                })
                .OrderBy(documentation => documentation.Name, StringComparer.Ordinal)
                .ToArray();

            swaggerDoc.Tags ??= new List<OpenApiTag>();

            foreach (var documentation in documentedTags)
            {
                var matchingTags = swaggerDoc.Tags
                    .Where(tag => string.Equals(tag.Name, documentation.Name, StringComparison.Ordinal))
                    .ToArray();
                var documentedTag = matchingTags.FirstOrDefault();

                if (documentedTag == null)
                {
                    documentedTag = new OpenApiTag { Name = documentation.Name };
                    swaggerDoc.Tags.Add(documentedTag);
                }

                foreach (var duplicateTag in matchingTags.Skip(1))
                {
                    swaggerDoc.Tags.Remove(duplicateTag);
                }

                // Assembly metadata is authoritative and must not depend on which split controller XML summary ran first.
                documentedTag.Description = documentation.Description;

                if (documentation.ChildTagPrefix != null)
                {
                    documentedTag.Extensions[ChildTagPrefixExtensionName] = new OpenApiString(documentation.ChildTagPrefix);
                }
            }

            #endregion
        }

        #endregion
    }
}

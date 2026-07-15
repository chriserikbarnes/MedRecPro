using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

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
}

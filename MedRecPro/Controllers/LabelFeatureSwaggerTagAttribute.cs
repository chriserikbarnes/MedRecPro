using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Provides a Swagger UI tag name for a Label feature controller or action.
    /// </summary>
    /// <remarks>
    /// Tags affect only OpenAPI documentation grouping; they do not participate in ASP.NET Core routing or model binding.
    /// </remarks>
    /// <seealso cref="LabelFeatureSwaggerTagOperationFilter"/>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class LabelFeatureSwaggerTagAttribute : Attribute
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the Swagger UI tag name used for the annotated Label feature surface.
        /// </summary>
        /// <remarks>
        /// The value is copied into <see cref="OpenApiOperation.Tags"/> by <see cref="LabelFeatureSwaggerTagOperationFilter"/>.
        /// </remarks>
        public string Tag { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelFeatureSwaggerTagAttribute"/> class.
        /// </summary>
        /// <param name="tag">Swagger UI tag name used to group matching operations.</param>
        /// <exception cref="ArgumentException">Thrown when the tag is blank.</exception>
        public LabelFeatureSwaggerTagAttribute(string tag)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Swagger tag cannot be blank.", nameof(tag));
            }

            Tag = tag;

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies Label feature Swagger tags to generated OpenAPI operations.
    /// </summary>
    /// <remarks>
    /// The filter is intentionally documentation-only. Route templates, action names, parameters, response types, and authorization metadata are left unchanged.
    /// </remarks>
    /// <seealso cref="LabelFeatureSwaggerTagAttribute"/>
    internal sealed class LabelFeatureSwaggerTagOperationFilter : IOperationFilter
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Copies the nearest Label feature tag attribute into the OpenAPI operation tag list.
        /// </summary>
        /// <param name="operation">OpenAPI operation being generated.</param>
        /// <param name="context">Operation filter context containing reflected action metadata.</param>
        /// <seealso cref="IOperationFilter"/>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            #region implementation

            var tagAttribute = context.MethodInfo.GetCustomAttribute<LabelFeatureSwaggerTagAttribute>(inherit: true)
                ?? context.MethodInfo.DeclaringType?.GetCustomAttribute<LabelFeatureSwaggerTagAttribute>(inherit: true);

            if (tagAttribute != null)
            {
                operation.Tags = new List<OpenApiTag>
                {
                    new() { Name = tagAttribute.Tag }
                };
            }

            #endregion
        }

        #endregion
    }
}
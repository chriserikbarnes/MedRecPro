using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Pins a feature controller to an explicit public MVC controller name.
    /// </summary>
    /// <remarks>
    /// The name is consumed by <see cref="FeatureControllerNameConvention"/> so a physically split controller can inherit
    /// the route template from <see cref="MedRecPro.Controllers.ApiControllerBase"/> without exposing its implementation
    /// class name in the public route table.
    /// </remarks>
    /// <seealso cref="FeatureControllerNameConvention"/>
    /// <seealso cref="MedRecPro.Controllers.ApiControllerBase"/>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class FeatureControllerNameAttribute : Attribute
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets the normalized public MVC controller name.
        /// </summary>
        /// <remarks>
        /// The value replaces the MVC <c>[controller]</c> route token for the annotated controller.
        /// </remarks>
        /// <seealso cref="ControllerModel.ControllerName"/>
        public string ControllerName { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureControllerNameAttribute"/> class.
        /// </summary>
        /// <param name="controllerName">Public MVC controller name used for route-token resolution.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="controllerName"/> is blank.</exception>
        /// <seealso cref="FeatureControllerNameConvention"/>
        public FeatureControllerNameAttribute(string controllerName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(controllerName))
            {
                throw new ArgumentException("Controller name cannot be blank.", nameof(controllerName));
            }

            ControllerName = controllerName.Trim();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies attribute-supplied public controller names to MVC controller models.
    /// </summary>
    /// <remarks>
    /// The convention is family-agnostic: adding another split controller family requires only an attribute value and no
    /// convention changes. Swagger grouping remains independent through <see cref="SwaggerGroupAttribute"/>.
    /// </remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    /// <seealso cref="SwaggerGroupAttribute"/>
    /// <seealso cref="ControllerModel"/>
    public sealed class FeatureControllerNameConvention : IApplicationModelConvention, IControllerModelConvention
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Applies the controller-name convention to every controller in an application model.
        /// </summary>
        /// <param name="application">MVC application model being configured.</param>
        /// <remarks>
        /// Implementing <see cref="IApplicationModelConvention"/> keeps the convention directly visible in
        /// <c>MvcOptions.Conventions</c> while sharing the controller-level implementation.
        /// </remarks>
        /// <seealso cref="Apply(ControllerModel)"/>
        public void Apply(ApplicationModel application)
        {
            #region implementation

            foreach (var controller in application.Controllers)
            {
                Apply(controller);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies the explicit public name to a controller carrying <see cref="FeatureControllerNameAttribute"/>.
        /// </summary>
        /// <param name="controller">MVC controller model being configured.</param>
        /// <remarks>
        /// Unmarked controllers remain unchanged and therefore continue to resolve from their implementation class names.
        /// </remarks>
        /// <seealso cref="FeatureControllerNameAttribute"/>
        public void Apply(ControllerModel controller)
        {
            #region implementation

            var attribute = controller.Attributes
                .OfType<FeatureControllerNameAttribute>()
                .FirstOrDefault();

            if (attribute != null)
            {
                controller.ControllerName = attribute.ControllerName;
            }

            #endregion
        }

        #endregion
    }
}

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Marks a split Label feature controller whose public route surface must remain under the original Label controller name.
    /// </summary>
    /// <remarks>
    /// The marker is consumed by <see cref="LabelFeatureControllerModelConvention"/> so feature-owned controllers can inherit
    /// the build-conditional route prefix from <see cref="MedRecPro.Controllers.ApiControllerBase"/> without exposing their
    /// implementation class names in the route table.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerModelConvention"/>
    /// <seealso cref="MedRecPro.Controllers.ApiControllerBase"/>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class LabelFeatureControllerAttribute : Attribute
    {
    }

    /**************************************************************/
    /// <summary>
    /// Pins marked Label feature controllers to the public <c>Label</c> controller name.
    /// </summary>
    /// <remarks>
    /// This convention preserves the existing Debug route <c>api/Label</c> and Release route <c>Label</c> while allowing
    /// internal controller classes such as <see cref="LabelMarkdownController"/> to own smaller action clusters. Swagger
    /// grouping is handled separately by <see cref="LabelFeatureSwaggerTagAttribute"/>.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelFeatureSwaggerTagAttribute"/>
    /// <seealso cref="ControllerModel"/>
    public sealed class LabelFeatureControllerModelConvention : IApplicationModelConvention, IControllerModelConvention
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Public controller name used by all split Label feature controllers.
        /// </summary>
        /// <remarks>
        /// The value must remain <c>Label</c> to preserve existing clients and the inherited <c>[controller]</c> token route.
        /// </remarks>
        public const string LabelControllerName = "Label";

        /**************************************************************/
        /// <summary>
        /// Applies the Label controller name convention to all marked controllers in the application model.
        /// </summary>
        /// <param name="application">The MVC application model being configured.</param>
        /// <remarks>
        /// Implementing <see cref="IApplicationModelConvention"/> keeps the startup registration directly visible in
        /// <c>MvcOptions.Conventions</c> while reusing the controller-level implementation.
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
        /// Applies the Label controller name to controller models carrying <see cref="LabelFeatureControllerAttribute"/>.
        /// </summary>
        /// <param name="controller">The MVC controller model being configured.</param>
        /// <remarks>
        /// Unmarked controllers are left unchanged so existing controller route names continue to follow their class names.
        /// </remarks>
        /// <seealso cref="LabelFeatureControllerAttribute"/>
        public void Apply(ControllerModel controller)
        {
            #region implementation

            if (controller.Attributes.OfType<LabelFeatureControllerAttribute>().Any())
            {
                controller.ControllerName = LabelControllerName;
            }

            #endregion
        }

        #endregion
    }
}

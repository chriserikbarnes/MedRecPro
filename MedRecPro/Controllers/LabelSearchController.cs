using MedRecPro.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Preserves the legacy Label search-controller feature marker after action decomposition.
    /// </summary>
    /// <remarks>
    /// Search actions now reside in cohesive feature controllers while the controller-name convention
    /// keeps their complete public surface pinned to the original Label route.
    /// </remarks>
    /// <seealso cref="LabelApplicationController"/>
    /// <seealso cref="LabelClassificationController"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Search")]
    public sealed class LabelSearchController : ApiControllerBase
    {
    }
}

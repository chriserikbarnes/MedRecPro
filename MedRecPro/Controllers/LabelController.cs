using MedRecPro.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Compatibility controller shell for the original Label route surface.
    /// </summary>
    /// <remarks>
    /// Feature-owned Label controllers now own the public actions while this type preserves the original controller name for documentation and compatibility references.
    /// </remarks>
    /// <seealso cref="LabelSearchController"/>
    /// <seealso cref="LabelSectionController"/>
    /// <seealso cref="LabelImportController"/>
    /// <seealso cref="LabelComparisonController"/>
    /// <seealso cref="LabelDocumentController"/>
    [ApiController]
    public class LabelController : ApiControllerBase
    {
    }
}
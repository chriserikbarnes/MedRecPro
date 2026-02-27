using Microsoft.AspNetCore.Mvc;

/**************************************************************/
/// <summary>
/// Base class for API controllers in the MedRecPro application. This
/// changes the routing based on the build configuration to facilitate
/// path consistency between local development and production environments.
///</summary >
namespace MedRecPro.Controllers
{
    [ApiController]
#if DEBUG
    [Route("api/[controller]")]  // Local development: /api/Settings
#else
    [Route("[controller]")]       // Production with virtual app: /Settings (virtual app adds /api)
#endif
    public abstract class ApiControllerBase : ControllerBase
    {
        #region Protected Constants

        /**************************************************************/
        /// <summary>
        /// Default page number used when pagination is applied but no page number is specified.
        /// </summary>
        protected const int DefaultPageNumber = 1;

        /**************************************************************/
        /// <summary>
        /// Default page size used when pagination is applied but no page size is specified.
        /// </summary>
        protected const int DefaultPageSize = 10;

        #endregion

        #region Protected Methods

        /**************************************************************/
        /// <summary>
        /// Validates pagination parameters and returns a BadRequest result if invalid.
        /// </summary>
        /// <param name="pageNumber">The page number to validate.</param>
        /// <param name="pageSize">The page size to validate.</param>
        /// <returns>BadRequest result if validation fails, null if validation passes.</returns>
        /// <remarks>
        /// Validates that:
        /// - If pageNumber is provided, it must be greater than 0
        /// - If pageSize is provided, it must be greater than 0
        /// - If one paging parameter is provided, the other defaults automatically
        ///   (pageNumber defaults to 1, pageSize defaults to 10)
        /// </remarks>
        /// <example>
        /// <code>
        /// var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
        /// if (pagingValidation != null) return pagingValidation;
        /// </code>
        /// </example>
        protected BadRequestObjectResult? validatePagingParameters(ref int? pageNumber, ref int? pageSize)
        {
            #region implementation

            // Validate pageNumber if provided
            if (pageNumber.HasValue && pageNumber.Value <= 0)
            {
                return BadRequest($"Invalid page number: {pageNumber.Value}. Page number must be greater than 0 if provided.");
            }

            // Validate pageSize if provided
            if (pageSize.HasValue && pageSize.Value <= 0)
            {
                return BadRequest($"Invalid page size: {pageSize.Value}. Page size must be greater than 0 if provided.");
            }

            // Default pageNumber to 1 when pageSize is provided but pageNumber is not
            if (pageSize.HasValue && !pageNumber.HasValue)
            {
                pageNumber = 1;
            }

            // Default pageSize to 10 when pageNumber is provided but pageSize is not
            if (pageNumber.HasValue && !pageSize.HasValue)
            {
                pageSize = 10;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds pagination response headers to the HTTP response.
        /// </summary>
        /// <param name="pageNumber">The current page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="totalCount">The total count of records in the current response.</param>
        /// <remarks>
        /// Adds the following headers when pagination is applied:
        /// - X-Page-Number: The current page number
        /// - X-Page-Size: The number of records per page
        /// - X-Total-Count: The total count of records returned
        /// </remarks>
        /// <example>
        /// <code>
        /// addPaginationHeaders(pageNumber, pageSize, results.Count);
        /// </code>
        /// </example>
        protected void addPaginationHeaders(int? pageNumber, int? pageSize, int totalCount)
        {
            #region implementation

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                Response.Headers.Append("X-Page-Number", pageNumber.Value.ToString());
                Response.Headers.Append("X-Page-Size", pageSize.Value.ToString());
                Response.Headers.Append("X-Total-Count", totalCount.ToString());
            }

            #endregion
        }

        #endregion
    }
}
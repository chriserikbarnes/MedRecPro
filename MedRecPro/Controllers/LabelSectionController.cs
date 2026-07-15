using MedRecPro.Controllers;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedRecPro.Models.UserRole;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles dynamic Label section metadata and CRUD endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// This controller retains the legacy dynamic section repository seam while separating section CRUD from search, import, comparison, and document endpoints.
    /// </remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    /// <seealso cref="LabelController"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup("Label Sections", "Dynamic label-section metadata, content retrieval, and CRUD operations.")]
    public class LabelSectionController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Service that owns dynamic section repository and encrypted-ID operations.
        /// </summary>
        /// <seealso cref="ILabelSectionCrudService"/>
        private readonly ILabelSectionCrudService _sectionCrudService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSectionController"/> class.
        /// </summary>
        /// <param name="sectionCrudService">Service for dynamic repository and encrypted-ID operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelSectionController(ILabelSectionCrudService sectionCrudService)
        {
            #region implementation

            _sectionCrudService = sectionCrudService ?? throw new ArgumentNullException(nameof(sectionCrudService));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Provides a list of available label sections (data tables) that can be interacted with.
        /// Each section name corresponds to a class within MedRecPro.DataModels.Label.
        /// </summary>
        /// <returns>A sorted list of section names.</returns>
        /// <response code="200">Returns the list of section names.</response>
        /// <response code="500">If an error occurs while generating the menu.</response>
        /// <remarks>
        /// GET /api/Label/SectionMenu
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   "ActiveMoiety",
        ///   "Address",
        ///   "Document",
        ///   "Organization",
        ///   ...
        /// ]
        /// ```
        ///
        /// Uses DtoTransformer.ToEntityMenu to generate the list of available sections.
        /// Returns an empty list if an error occurs during menu generation.
        /// </remarks>
        [HttpGet("sectionMenu")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetLabelSectionMenu()
        {
            #region Implementation

            // Generate menu using DtoTransformer helper, fallback to empty list if null
                List<string> menu = _sectionCrudService.GetMenu();
                return Ok(menu);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves documentation for a specified label section (data model class).
        /// This includes the class summary and details for its public properties
        /// (name, type, nullability, and XML documentation summary).
        /// </summary>
        /// <param name="menuSelection">The name of the label section (e.g., "Document", "Organization")
        /// for which to retrieve documentation. This name must match a nested class within
        /// MedRecPro.DataModels.Label.</param>
        /// <returns>Detailed documentation for the specified class.</returns>
        /// <response code="200">Returns the class documentation.</response>
        /// <response code="400">If the menuSelection is invalid or not found.</response>
        /// <response code="500">If an internal server error occurs while retrieving documentation.</response>
        /// <remarks>
        /// GET /api/label/document/Documentation
        ///
        /// Response (200):
        /// ```json
        /// {
        ///   "name": "Document",
        ///   "fullName": "MedRecPro.DataModels.Label.Document",
        ///   "summary": "Stores the main metadata for each SPL document version. Based on Section 2.1.3.",
        ///   "properties": [
        ///     {
        ///       "name": "DocumentID",
        ///       "typeName": "System.Nullable`1[[System.Int32, System.Private.CoreLib, Version=...]]",
        ///       "isNullable": true,
        ///       "summary": "Primary key for the Document table."
        ///     },
        ///     {
        ///       "name": "DocumentGUID",
        ///       "typeName": "System.Nullable`1[[System.Guid, System.Private.CoreLib, Version=...]]",
        ///       "isNullable": true,
        ///       "summary": "Globally Unique Identifier for this specific document version."
        ///     },
        ///     // ... other properties
        ///   ]
        /// }
        /// ```
        /// </remarks>
        [HttpGet("{menuSelection}/documentation")]
        [ProducesResponseType(typeof(ClassDocumentation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public ActionResult<ClassDocumentation> GetSectionDocumentation(string menuSelection)
        {
            #region Implementation
            var outcome = _sectionCrudService.GetDocumentation(menuSelection);
            return outcome.Status == SectionCrudStatus.InvalidInput
                ? BadRequest(outcome.Error)
                : Ok((ClassDocumentation)outcome.Payload!);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves records for a specified label section, with optional paging.
        /// If paging parameters (pageNumber, pageSize) are not provided or are
        /// null, all records for the section are returned. Each record is
        /// transformed to include an encrypted primary key and omit the original numeric PK.
        /// </summary>
        /// <param name="menuSelection">
        /// The name of the label section (table) to query (e.g., "Document", "Organization").
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// If provided, pageSize must also be provided for paging to apply.
        /// If omitted or null (and pageSize is also null/omitted), all records are returned.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// If provided, pageNumber must also be provided for paging to apply.
        /// If omitted or null (and pageNumber is also null/omitted), all records are returned.
        /// </param>
        /// <returns>
        /// A list of records from the specified section, with encrypted IDs.
        /// If both pageNumber and pageSize are provided, returns a specific page of records. Otherwise, returns all records.
        /// </returns>
        /// <response code="200">Returns the list of records (all or paged).</response>
        /// <response code="400">
        /// If menuSelection is invalid, or if provided paging parameters are invalid (e.g., pageNumber &lt;= 0, pageSize &lt;= 0).
        /// </response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// To get all records:
        /// GET /api/Label/Section
        ///
        /// To get paged records (e.g., page 2, 20 items per page):
        /// GET /api/label/section?pageNumber=2&amp;pageSize=20
        ///
        /// Response (200 for paged request):
        ///
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "some_encrypted_string_page2_item1"
        ///     // other properties
        ///   }
        ///   // up to pageSize items
        /// ]
        /// ```
        ///
        /// Uses reflection to invoke ReadAllAsync(int? pageNumber, int? pageSize) on the appropriate repository.
        /// The repository is expected to handle null for pageNumber or pageSize as a request for all records.
        /// If pageNumber is provided for paging, it's 1-based from the client and converted to 0-based for the repository.
        /// All numeric primary keys are replaced with encrypted equivalents for security.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/{menuSelection}")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object?>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object?>>>> GetSection(
            string menuSelection,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region implementation

            var outcome = await _sectionCrudService.GetAsync(menuSelection, pageNumber, pageSize, HttpContext.RequestAborted);
            if (outcome.Status == SectionCrudStatus.Success && outcome.Payload is IReadOnlyCollection<Dictionary<string, object?>> payload && pageNumber.HasValue && pageSize.HasValue)
            {
                Response.Headers.Append("X-Page-Number", pageNumber.Value.ToString());
                Response.Headers.Append("X-Page-Size", pageSize.Value.ToString());
                Response.Headers.Append("X-Total-Count", payload.Count.ToString());
            }

            return outcome.Status switch
            {
                SectionCrudStatus.InvalidInput => BadRequest(outcome.Error),
                SectionCrudStatus.NotFound => NotFound(outcome.Error),
                _ => Ok((IEnumerable<Dictionary<string, object?>>)outcome.Payload!)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a specific record from a label section by its encrypted primary key ID.
        /// The record is transformed to include an encrypted primary key and omit the original numeric PK.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to retrieve.</param>
        /// <returns>The requested record with an encrypted ID, or NotFound.</returns>
        /// <response code="200">Returns the requested record.</response>
        /// <response code="400">If the menuSelection is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/label/{Document}/some_encrypted_string
        ///
        /// Response (200):
        /// ```json
        /// {
        ///   "EncryptedDocumentID": "some_encrypted_string",
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// ```
        ///
        /// Response (404): Not Found Uses the repository's ReadByIdAsync
        /// method which handles encrypted ID decryption internally.
        /// The returned entity has its numeric primary key replaced with
        /// the encrypted equivalent.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("{menuSelection}/{encryptedId}")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object?>>> GetByIdAsync(string menuSelection, string encryptedId)
        {
            #region Implementation

            var outcome = await _sectionCrudService.GetByIdAsync(menuSelection, encryptedId, HttpContext.RequestAborted);
            return outcome.Status switch
            {
                SectionCrudStatus.InvalidInput => BadRequest(outcome.Error),
                SectionCrudStatus.NotFound => NotFound(outcome.Error),
                _ => Ok((Dictionary<string, object?>)outcome.Payload!)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new record in the specified label section.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) where the record will be created (e.g., "Document", "Organization").</param>
        /// <param name="jsonData">A Json string representing the record to create. Primary key fields (e.g., "DocumentID") should be omitted or null as they are auto-generated.</param>
        /// <returns>The encrypted ID of the newly created record.</returns>
        /// <response code="201">Returns an object containing the encrypted ID of the new record and a Location header pointing to the new resource.</response>
        /// <response code="400">If menuSelection is invalid or input data is invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/label/document
        ///
        /// Request Body:
        /// ```json
        /// {
        ///   "DocumentID": null, (Omit or set to null)
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// ```
        ///
        /// Response (201):
        ///
        /// ```json
        /// {
        ///   "encryptedId": "newly_generated_encrypted_string"
        /// }
        /// ```
        /// Header: Location: /api/Label/Document/newly_generated_encrypted_string
        ///
        /// Primary key fields should be omitted from the request body as they are auto-generated.
        /// The response includes a Location header pointing to the newly created resource.
        /// Uses reflection to invoke CreateAsync on the appropriate repository.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("{menuSelection}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateAsync(string menuSelection, [FromBody] object? jsonData)
        {
            #region Implementation

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var outcome = await _sectionCrudService.CreateAsync(menuSelection, Convert.ToString(jsonData), HttpContext.RequestAborted);
            return outcome.Status == SectionCrudStatus.InvalidInput
                ? BadRequest(outcome.Error)
                : Ok(outcome.Payload);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates an existing record in the specified label section.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to update.</param>
        /// <param name="jsonData">Json representing the updated record data. The primary key identified by `encryptedId` will be used; any PK in the body is ignored for identification but should match if present for data integrity.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the update was successful.</response>
        /// <response code="400">If menuSelection is invalid, ID is invalid, or input data is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// PUT /api/label/{Document}/some_encrypted_string
        ///
        /// Request Body:
        /// ```json
        /// {
        ///   "DocumentID": (value matching decrypted 'some_encrypted_string', or can be omitted),
        ///   "DocumentGUID": "updated_guid_value",
        ///   "Title": "Updated Title",
        ///   // ... other properties to update
        /// }
        /// ```
        ///
        /// Response (204): No Content
        ///
        /// The primary key from the route parameter takes precedence over any PK in the request body.
        /// Validates that the record exists before attempting the update operation.
        /// Uses reflection to invoke repository methods for existence check and update.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPut("{menuSelection}/{encryptedId}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAsync(string menuSelection, string encryptedId, [FromBody] object? jsonData)
        {
            #region Implementation

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var outcome = await _sectionCrudService.UpdateAsync(menuSelection, encryptedId, Convert.ToString(jsonData), HttpContext.RequestAborted);
            return outcome.Status switch
            {
                SectionCrudStatus.InvalidInput => BadRequest(outcome.Error),
                SectionCrudStatus.NotFound => NotFound(outcome.Error),
                _ => NoContent()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes a record from the specified label section by its encrypted primary key ID.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to delete.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the deletion was successful.</response>
        /// <response code="400">If the menuSelection is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// DELETE /api/label/{Document}/some_encrypted_string
        ///
        /// Response (204): No Content
        /// Response (404): Not Found
        ///
        /// Uses the repository's DeleteAsync method which handles encrypted ID decryption internally.
        /// The Repository throws KeyNotFoundException for non-existent records.
        /// Handles different exception types to provide appropriate HTTP status codes.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpDelete("{menuSelection}/{encryptedId}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAsync(string menuSelection, string encryptedId)
        {
            #region Implementation

            var outcome = await _sectionCrudService.DeleteAsync(menuSelection, encryptedId, HttpContext.RequestAborted);
            return outcome.Status switch
            {
                SectionCrudStatus.InvalidInput => BadRequest(outcome.Error),
                SectionCrudStatus.NotFound => NotFound(outcome.Error),
                _ => NoContent()
            };

            #endregion
        }

        #endregion
    }
}

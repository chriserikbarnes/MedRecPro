using Microsoft.AspNetCore.Mvc;
using MedRecPro.DataModels; // From LabelClasses.cs
using MedRecPro.DataAccess; // From LabelDataAccess.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Required for StatusCodes

namespace MedRecPro.Api.Controllers
{
    /// <summary>
    /// API controller for managing Document entities based on SPL metadata.
    /// Provides endpoints for CRUD operations on the Document table.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly GenericRepository<Document> _documentRepository;

        /// /***********************************************/
        /// <summary>
        /// Initializes a new instance of the DocumentsController.
        /// </summary>
        /// <param name="documentRepository">The repository for accessing Document data.</param>
        public DocumentsController(GenericRepository<Document> documentRepository)
        {
            _documentRepository = documentRepository;
        }

        /// /***********************************************/
        /// <summary>
        /// Retrieves all Document records.
        /// </summary>
        /// <returns>A list of all documents.</returns>
        /// <response code="200">Returns the list of documents.</response>
        /// <example>
        /// GET /api/Documents
        /// Response:
        /// [
        ///   {
        ///     "documentID": 1, //
        ///     "documentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef", //
        ///     "documentCode": "34391-9", //
        ///     "documentCodeSystem": "2.16.840.1.113883.6.1", //
        ///     "documentDisplayName": "HUMAN PRESCRIPTION DRUG LABEL", //
        ///     "title": "Example Drug Label", //
        ///     "effectiveTime": "2025-05-02T15:11:17", //
        ///     "setGUID": "f0e9d8c7-b6a5-4321-fedc-ba9876543210", //
        ///     "versionNumber": 1, //
        ///     "submissionFileName": "a1b2c3d4-e5f6-7890-1234-567890abcdef.xml" //
        ///   },
        ///   ...
        /// ]
        /// </example>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Document>>> GetAllDocuments()
        {
            var documents = await _documentRepository.ReadAllAsync(); //
            return Ok(documents);
        }

        /// /***********************************************/
        /// <summary>
        /// Retrieves a specific Document record by its ID.
        /// </summary>
        /// <param name="id">The primary key (DocumentID) of the document to retrieve.</param>
        /// <returns>The requested document.</returns>
        /// <response code="200">Returns the requested document.</response>
        /// <response code="404">If the document with the specified ID is not found.</response>
        /// <example>
        /// GET /api/Documents/5
        /// Response (200):
        /// {
        ///   "documentID": 5, //
        ///   "documentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef", //
        ///   "documentCode": "34391-9", //
        ///   "documentCodeSystem": "2.16.840.1.113883.6.1", //
        ///   "documentDisplayName": "HUMAN PRESCRIPTION DRUG LABEL", //
        ///   "title": "Example Drug Label", //
        ///   "effectiveTime": "2025-05-02T15:11:17", //
        ///   "setGUID": "f0e9d8c7-b6a5-4321-fedc-ba9876543210", //
        ///   "versionNumber": 1, //
        ///   "submissionFileName": "a1b2c3d4-e5f6-7890-1234-567890abcdef.xml" //
        /// }
        /// Response (404): Not Found
        /// </example>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Document>> GetDocumentById(int id)
        {
            var document = await _documentRepository.ReadByIdAsync(id); //
            if (document == null)
            {
                return NotFound();
            }
            return Ok(document);
        }

        /// /***********************************************/
        /// <summary>
        /// Creates a new Document record.
        /// </summary>
        /// <param name="document">The document object to create. The DocumentID should be null or 0 as it's auto-generated.</param>
        /// <returns>The newly created document, including its assigned DocumentID.</returns>
        /// <response code="201">Returns the newly created document.</response>
        /// <response code="400">If the input document data is invalid.</response>
        /// <example>
        /// POST /api/Documents
        /// Request Body:
        /// {
        ///   // "documentID": 0, (Omit or set to 0/null)
        ///   "documentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef", //
        ///   "documentCode": "34391-9", //
        ///   "documentCodeSystem": "2.16.840.1.113883.6.1", //
        ///   "documentDisplayName": "HUMAN PRESCRIPTION DRUG LABEL", //
        ///   "title": "Example Drug Label", //
        ///   "effectiveTime": "2025-05-02T15:11:17", //
        ///   "setGUID": "f0e9d8c7-b6a5-4321-fedc-ba9876543210", //
        ///   "versionNumber": 1, //
        ///   "submissionFileName": "a1b2c3d4-e5f6-7890-1234-567890abcdef.xml" //
        /// }
        /// Response (201):
        /// {
        ///   "documentID": 123, // ID assigned by the database
        ///   "documentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef", //
        ///   "documentCode": "34391-9", //
        ///   "documentCodeSystem": "2.16.840.1.113883.6.1", //
        ///   "documentDisplayName": "HUMAN PRESCRIPTION DRUG LABEL", //
        ///   "title": "Example Drug Label", //
        ///   "effectiveTime": "2025-05-02T15:11:17", //
        ///   "setGUID": "f0e9d8c7-b6a5-4321-fedc-ba9876543210", //
        ///   "versionNumber": 1, //
        ///   "submissionFileName": "a1b2c3d4-e5f6-7890-1234-567890abcdef.xml" //
        /// }
        /// </example>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Document>> CreateDocument([FromBody] Document document)
        {
            if (document == null || !ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // CreateAsync sets the ID on the entity after creation
            await _documentRepository.CreateAsync(document); //

            // Return the created document with the generated ID
            return CreatedAtAction(nameof(GetDocumentById), new { id = document.DocumentID }, document);
        }

        /// /***********************************************/
        /// <summary>
        /// Updates an existing Document record.
        /// </summary>
        /// <param name="id">The primary key (DocumentID) of the document to update.</param>
        /// <param name="document">The updated document object. The DocumentID in the body should match the ID in the route.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the update was successful.</response>
        /// <response code="400">If the ID in the route doesn't match the ID in the body, or if the data is invalid.</response>
        /// <response code="404">If the document with the specified ID is not found.</response>
        /// <example>
        /// PUT /api/Documents/123
        /// Request Body:
        /// {
        ///   "documentID": 123, // Must match the ID in the route
        ///   "documentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef", //
        ///   "documentCode": "34391-9", //
        ///   "documentCodeSystem": "2.16.840.1.113883.6.1", //
        ///   "documentDisplayName": "HUMAN PRESCRIPTION DRUG LABEL", //
        ///   "title": "Example Drug Label - Updated", // Updated field
        ///   "effectiveTime": "2025-05-03T10:00:00", // Updated field
        ///   "setGUID": "f0e9d8c7-b6a5-4321-fedc-ba9876543210", //
        ///   "versionNumber": 2, // Updated field
        ///   "submissionFileName": "a1b2c3d4-e5f6-7890-1234-567890abcdef_v2.xml" // Updated field
        /// }
        /// Response (204): No Content
        /// Response (400): Bad Request (e.g., ID mismatch)
        /// Response (404): Not Found
        /// </example>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDocument(int id, [FromBody] Document document)
        {
            if (document == null || id != document.DocumentID) //
            {
                return BadRequest("Document ID mismatch or invalid document data.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingDocument = await _documentRepository.ReadByIdAsync(id); //
            if (existingDocument == null)
            {
                return NotFound($"Document with ID {id} not found.");
            }

            await _documentRepository.UpdateAsync(document); //

            return NoContent();
        }

        /// /***********************************************/
        /// <summary>
        /// Deletes a Document record by its ID.
        /// </summary>
        /// <param name="id">The primary key (DocumentID) of the document to delete.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the deletion was successful.</response>
        /// <response code="404">If the document with the specified ID is not found.</response>
        /// <example>
        /// DELETE /api/Documents/123
        /// Response (204): No Content
        /// Response (404): Not Found
        /// </example>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var existingDocument = await _documentRepository.ReadByIdAsync(id); //
            if (existingDocument == null)
            {
                return NotFound($"Document with ID {id} not found.");
            }

            await _documentRepository.DeleteAsync(id); //

            return NoContent();
        }
    }
}

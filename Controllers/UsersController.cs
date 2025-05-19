using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.DataAccess; // Added for UserDataAccess
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography; // Required for CryptographicException
using System; // Added for ArgumentNullException, DateTime, etc.
using System.Threading.Tasks; // Added for Task
using System.Collections.Generic; // Added for IEnumerable
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations; // Added for StatusCodes


namespace MedRecPro.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly StringCipher _stringCipher;
        private readonly IConfiguration _configuration;
        private readonly UserDataAccess _userDataAccess; // Added UserDataAccess
        private readonly string _pkSecret;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="stringCipher">The string cipher utility.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="userDataAccess">The data access layer for users.</param>
        /// <remarks>
        /// Dependencies are injected via the constructor.
        /// The PKSecret for encryption is retrieved from configuration.
        /// </remarks>
        public UsersController(StringCipher stringCipher, IConfiguration configuration, UserDataAccess userDataAccess)
        {
            #region implementation
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _userDataAccess = userDataAccess ?? throw new ArgumentNullException(nameof(userDataAccess)); // Initialize UserDataAccess

            _pkSecret = _configuration["Security:DB:PKSecret"] ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing.");
            if (string.IsNullOrWhiteSpace(_pkSecret))
            {
                throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' cannot be empty.");
            }
            #endregion
        }

        // Private helper method (example, if needed for getting current user ID)
        // Ensure it follows naming convention: private string getAuthenticatedUserId() { ... }

        #region User Retrieval

        /**************************************************************/
        /// <summary>
        /// Retrieves a specific user by their encrypted ID.
        /// </summary>
        /// <param name="encryptedUserId">The encrypted unique identifier of the user.</param>
        /// <returns>The user object if found; otherwise, a NotFound or BadRequest response.</returns>
        /// <remarks>
        /// This endpoint fetches a single user. The ID in the path must be the encrypted user ID.
        /// Example: `GET /api/users/AbCdEf12345GhIjK`
        /// </remarks>
        /// <response code="200">Returns the requested user.</response>
        /// <response code="400">If the encryptedUserId is invalid or malformed.</response>
        /// <response code="404">If the user with the specified ID is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet("{encryptedUserId}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUser(string encryptedUserId)
        {
            #region implementation
            // Input validation for encryptedUserId can be added here if desired (e.g., length, pattern)
            if (string.IsNullOrWhiteSpace(encryptedUserId))
            {
                return BadRequest("Encrypted User ID cannot be empty.");
            }

            try
            {
                // UserDataAccess.GetByIdAsync handles decryption and fetching
                var user = await _userDataAccess.GetByIdAsync(encryptedUserId);

                if (user == null)
                {
                    return NotFound($"User with ID '{encryptedUserId}' not found.");
                }

                var dto = new UserDto(user);

                return Ok(dto);
            }
            catch (FormatException ex) // Catch specific format errors (e.g. from base64 decoding if StringCipher throws it)
            {
                // Log ex appropriately
                return BadRequest("Invalid encrypted User ID structure.");
            }
            catch (CryptographicException ex) // Catch decryption errors
            {
                // Log ex (securely, avoid leaking sensitive info)
                return BadRequest("Invalid User ID. Decryption failed."); // Generic error for security
            }
            catch (Exception ex) // Catch other potential errors from data access or unexpected issues
            {
                // Log ex
                // Consider a more specific logging and error handling strategy
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a paginated list of users.
        /// </summary>
        /// <param name="includeDeleted">Optional. Whether to include soft-deleted users. Defaults to false.</param>
        /// <param name="skip">Optional. Number of records to skip for pagination. Defaults to 0.</param>
        /// <param name="take">Optional. Number of records to take for pagination. Defaults to 100, max 1000.</param>
        /// <returns>A list of user objects.</returns>
        /// <remarks>
        /// Example: `GET /api/users?skip=0&amp;take=50`
        /// Example: `GET /api/users?includeDeleted=true&amp;take=10`
        /// </remarks>
        /// <response code="200">Returns a list of users.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllUsers([FromQuery] bool includeDeleted = false, [FromQuery] int skip = 0, [FromQuery] int take = 100)
        {
            #region implementation
            try
            {
                // UserDataAccess.GetAllAsync handles fetching and pagination.
                // It also ensures EncryptedUserId is populated for each user.
                var users = await _userDataAccess.GetAllAsync(includeDeleted, skip, take);
                return Ok(users);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving users.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address of the user to retrieve.</param>
        /// <returns>The user object if found; otherwise, a NotFound response.</returns>
        /// <remarks>
        /// Example: `GET /api/users/byemail?email=test@example.com`
        /// </remarks>
        /// <response code="200">Returns the requested user.</response>
        /// <response code="400">If the email is not provided.</response>
        /// <response code="404">If the user with the specified email is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet("byemail")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserByEmail([FromQuery] string email)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email address cannot be empty.");
            }

            try
            {
                var user = await _userDataAccess.GetByEmailAsync(email);
                if (user == null)
                {
                    return NotFound($"User with email '{email}' not found.");
                }
                // UserDataAccess populates EncryptedUserId
                return Ok(user);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
            #endregion
        }
        #endregion

        #region User Creation & Authentication

        /**************************************************************/
        /// <summary>
        /// Registers (creates) a new user account.
        /// </summary>
        /// <param name="signUpRequest">The user sign-up information.</param>
        /// <returns>The encrypted ID of the newly created user or an error if creation failed.</returns>
        /// <remarks>
        /// This endpoint allows new users to register.
        /// Example Request Body:
        /// ```json
        /// {
        ///   "username": "newuser",
        ///   "displayName": "New User Display",
        ///   "email": "newuser@example.com",
        ///   "password": "Password123!",
        ///   "confirmPassword": "Password123!",
        ///   "phoneNumber": "123-456-7890",
        ///   "timezone": "America/New_York",
        ///   "locale": "en-US"
        /// }
        /// ```
        /// </remarks>
        /// <response code="201">User created successfully. Returns the encrypted User ID in the response body or Location header.</response>
        /// <response code="400">If the request is invalid (e.g., missing fields, passwords don't match, email already exists).</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("signup")]
        [ProducesResponseType(typeof(string), StatusCodes.Status201Created)] // Assuming encrypted ID is returned
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SignUpUser([FromBody] UserDataAccess.UserSignUpRequest signUpRequest)
        {
            #region implementation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // UserDataAccess.SignUpAsync handles creation and password hashing.
                var encryptedUserId = await _userDataAccess.SignUpAsync(signUpRequest);

                if (encryptedUserId == null)
                {
                    // This could be due to various reasons, including database errors.
                    // SignUpAsync logs errors, so a generic message here is okay.
                    return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during user sign-up.");
                }

                if (encryptedUserId == "Duplicate")
                {
                    // Specific error for duplicate email.
                    return BadRequest($"A user with email '{signUpRequest.Email}' already exists.");
                }

                // User created successfully. Return 201 Created with the encrypted ID.
                // Optionally, return the full user object by fetching it:
                // var newUser = await _userDataAccess.GetByIdAsync(encryptedUserId);
                // return CreatedAtAction(nameof(GetUser), new { encryptedUserId = encryptedUserId }, newUser);
                return CreatedAtAction(nameof(GetUser), new { encryptedUserId = encryptedUserId }, new { encryptedUserId = encryptedUserId });

            }
            catch (ArgumentException ex) // Catch validation errors from UserDataAccess if it throws them directly
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during user sign-up.");
            }
            #endregion
        }

        // Placeholder for LoginRequestDto
        public class LoginRequestDto
        {
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.EmailAddress]
            public string Email { get; set; }

            [System.ComponentModel.DataAnnotations.Required]
            public string Password { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Authenticates a user and returns user details upon success.
        /// </summary>
        /// <param name="loginRequest">The user's login credentials (email and password).</param>
        /// <returns>The authenticated user's details or an unauthorized/error response.</returns>
        /// <remarks>
        /// Example Request Body:
        /// ```json
        /// {
        ///   "email": "user@example.com",
        ///   "password": "Password123!"
        /// }
        /// ```
        /// In a real application, this endpoint would typically return a JWT token.
        /// </remarks>
        /// <response code="200">Authentication successful. Returns user details.</response>
        /// <response code="400">If the request is invalid (e.g., missing email or password).</response>
        /// <response code="401">Authentication failed (e.g., invalid credentials, account locked).</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("authenticate")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AuthenticateUser([FromBody] LoginRequestDto loginRequest)
        {
            #region implementation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _userDataAccess.AuthenticateAsync(loginRequest.Email, loginRequest.Password);

                if (user == null)
                {
                    // AuthenticationAsync handles logging of failed attempts, locked accounts, etc.
                    return Unauthorized("Authentication failed. Invalid email or password, or account locked.");
                }

                // UserDataAccess.AuthenticateAsync populates EncryptedUserId and updates last login.
                // In a real app, generate and return a JWT token here.
                return Ok(user);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during authentication.");
            }
            #endregion
        }
        #endregion

        #region User Update & Deletion

        // Define a DTO for profile updates if 'User' model is too broad or contains sensitive fields
        // For this example, we'll assume 'User' can be used, and UserDataAccess.UpdateProfileAsync handles field mapping.
        // public class UserProfileUpdateDto { /* relevant fields like DisplayName, PhoneNumber, Timezone, Locale */ }


        /**************************************************************/
        /// <summary>
        /// Updates a user's own profile information.
        /// </summary>
        /// <param name="encryptedUserId">The encrypted ID of the user whose profile is to be updated (must match authenticated user).</param>
        /// <param name="profileUpdate">A User object containing the fields to update (e.g., DisplayName, PhoneNumber).</param>
        /// <returns>A success response if the update is successful; otherwise, an error response.</returns>
        /// <remarks>
        /// The `encryptedUserId` in the path must correspond to the authenticated user.
        /// The `profileUpdate.EncryptedUserId` should also be set to this `encryptedUserId`.
        /// Example: `PUT /api/users/AbCdEf12345GhIjK/profile`
        /// Example Request Body (subset of User model):
        /// ```json
        /// {
        ///   "encryptedUserId": "AbCdEf12345GhIjK", // Should match path and be the ID of user being updated
        ///   "displayName": "Updated Name",
        ///   "phoneNumber": "987-654-3210",
        ///   "timezone": "America/Los_Angeles",
        ///   "locale": "en-CA"
        /// }
        /// ```
        /// An `encryptedUpdaterUserId` would typically be derived from the authentication context (e.g., JWT claims).
        /// </remarks>
        /// <response code="204">Profile updated successfully.</response>
        /// <response code="400">If the request is invalid or IDs don't match.</response>
        /// <response code="401">If the user is not authorized to update this profile.</response>
        /// <response code="404">If the user to update is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPut("{encryptedUserId}/profile")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateUserProfile(string encryptedUserId, [FromBody] User profileUpdate) // Using User model as DTO
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(encryptedUserId))
            {
                return BadRequest("Encrypted User ID in path cannot be empty.");
            }
            if (profileUpdate == null)
            {
                return BadRequest("Profile update data cannot be empty.");
            }
            if (!ModelState.IsValid) // Validate DTO
            {
                return BadRequest(ModelState);
            }

            // Ensure the EncryptedUserId in the DTO matches the one from the path.
            // UserDataAccess.UpdateProfileAsync also uses profile.EncryptedUserId internally to find the user.
            if (profileUpdate.EncryptedUserId != encryptedUserId)
            {
                // For clarity, ensure DTO's EncryptedUserId is aligned with the path parameter.
                // Or, you could rely on the path parameter and set it on the DTO:
                // profileUpdate.EncryptedUserId = encryptedUserId;
                // However, UserDataAccess.UpdateProfileAsync uses profile.EncryptedUserId, so it must be set.
                return BadRequest("Encrypted User ID in path does not match Encrypted User ID in request body.");
            }


            // IMPORTANT: In a real application, get the authenticated user's ID from claims.
            // This is a placeholder for how you might get the updater's ID.
            // var encryptedUpdaterUserIdFromAuth = User.Claims.FirstOrDefault(c => c.Type == "EncryptedUserId")?.Value;
            // For demonstration, if you require self-update:
            var encryptedUpdaterUserIdFromAuth = encryptedUserId; // Simplistic assumption: user updates their own profile.
                                                                  // More robustly: check if authenticated user IS encryptedUserId or is an admin.

            if (string.IsNullOrWhiteSpace(encryptedUpdaterUserIdFromAuth))
            {
                return Unauthorized("Unable to determine updater user ID from authentication context.");
            }

            try
            {
                bool success = await _userDataAccess.UpdateProfileAsync(profileUpdate, encryptedUpdaterUserIdFromAuth);

                if (!success)
                {
                    // UpdateProfileAsync logs specifics. Could be not found, or other update issue.
                    // Check if user exists first to return a more specific 404 if that's the case.
                    var userExists = await _userDataAccess.GetByIdAsync(encryptedUserId);
                    if (userExists == null) return NotFound($"User with ID '{encryptedUserId}' not found for update.");

                    return BadRequest("Failed to update user profile. The user may not exist or an error occurred.");
                }

                return NoContent(); // Standard for successful PUT update with no content to return.
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the user profile.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Soft-deletes a user (marks them as deleted).
        /// </summary>
        /// <param name="encryptedUserId">The encrypted ID of the user to be deleted.</param>
        /// <returns>A success response if deletion is successful; otherwise, an error response.</returns>
        /// <remarks>
        /// Example: `DELETE /api/users/AbCdEf12345GhIjK`
        /// An `encryptedDeleterUserId` would typically be derived from the authentication context (e.g., admin's JWT claims).
        /// For self-deletion, it would be the user's own ID.
        /// </remarks>
        /// <response code="204">User deleted successfully.</response>
        /// <response code="400">If the encryptedUserId is invalid.</response>
        /// <response code="401">If the user is not authorized to delete this account.</response>
        /// <response code="404">If the user to delete is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpDelete("{encryptedUserId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteUser(string encryptedUserId)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(encryptedUserId))
            {
                return BadRequest("Encrypted User ID cannot be empty.");
            }

            // IMPORTANT: Get the authenticated deleter's ID from claims.
            // This is a placeholder. Admins or users deleting their own accounts.
            // var encryptedDeleterUserIdFromAuth = User.Claims.FirstOrDefault(c => c.Type == "EncryptedUserId")?.Value;
            // For example, if an admin is deleting:
            // string encryptedDeleterUserIdFromAuth = "ENCRYPTED_ADMIN_USER_ID_FROM_CLAIMS";
            // If allowing self-deletion (careful with this):
            string encryptedDeleterUserIdFromAuth = encryptedUserId; // Placeholder: Assumes self-deletion or admin with this ID

            if (string.IsNullOrWhiteSpace(encryptedDeleterUserIdFromAuth))
            {
                // This check might be different based on policy (e.g. admin must be present)
                return Unauthorized("Unable to determine deleter user ID from authentication context.");
            }


            try
            {
                bool success = await _userDataAccess.DeleteAsync(encryptedUserId, encryptedDeleterUserIdFromAuth);

                if (!success)
                {
                    // DeleteAsync logs specifics. Could be not found or other issue.
                    // Check if user exists first for a better 404.
                    var userExists = await _userDataAccess.GetByIdAsync(encryptedUserId); // Checks if user *was* there
                    if (userExists == null) return NotFound($"User with ID '{encryptedUserId}' not found for deletion.");

                    return BadRequest("Failed to delete user. The user may not exist or an error occurred.");
                }

                return NoContent(); // Standard for successful DELETE.
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the user.");
            }
            #endregion
        }


        // TODO: Implement other endpoints from UserDataAccess as needed:
        // - UpdateAdminAsync -> PUT /api/users/admin-update (takes AdminUserUpdate DTO)
        // - RotatePasswordAsync -> POST /api/users/rotate-password (takes targetUserId, newPassword)
        // - CreateAsync (general purpose create, if different from SignUpAsync)

        // Example of a more general UpdateUser that might use UserDataAccess.UpdateAsync
        // This would require a DTO that reflects the fields updatable by UpdateAsync
        // and careful consideration of authorization (who can update what).

        /**************************************************************/
        /// <summary>
        /// (Admin) Updates comprehensive details for a specified user.
        /// </summary>
        /// <param name="adminUpdateData">The admin update DTO containing the target user's encrypted ID and fields to update.</param>
        /// <returns>A success response if the update is successful; otherwise, an error response.</returns>
        /// <remarks>
        /// This endpoint is typically restricted to administrators.
        /// The `adminUpdateData.EncryptedUserId` identifies the user to be modified.
        /// The updater's ID (admin) would be derived from the authentication context.
        /// Example: `PUT /api/users/admin-update`
        /// Example Request Body (AdminUserUpdate DTO):
        /// ```json
        /// {
        ///   "encryptedUserId": "TARGET_USER_ENCRYPTED_ID",
        ///   "userRole": "Editor",
        ///   "failedLoginCount": 0,
        ///   "lockoutUntil": null,
        ///   "mfaEnabled": true,
        ///   // ... other admin-updatable fields
        /// }
        /// ```
        /// </remarks>
        /// <response code="204">User updated successfully by admin.</response>
        /// <response code="400">If the request is invalid.</response>
        /// <response code="401">If the current user is not authorized for this action.</response>
        /// <response code="404">If the target user is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPut("admin-update")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AdminUpdateUser([FromBody] UserDataAccess.AdminUserUpdate adminUpdateData)
        {
            #region implementation
            if (adminUpdateData == null || string.IsNullOrWhiteSpace(adminUpdateData.EncryptedUserId))
            {
                return BadRequest("Admin update data and target Encrypted User ID are required.");
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // IMPORTANT: Get the authenticated ADMIN user's ID from claims.
            // This is a critical security step.
            // var encryptedAdminUpdaterIdFromAuth = User.Claims.FirstOrDefault(c => c.Type == "EncryptedAdminId" && User.IsInRole("Admin"))?.Value;
            // For demonstration, using a placeholder. THIS IS NOT SECURE FOR PRODUCTION.
            string encryptedAdminUpdaterIdFromAuth = "ENCRYPTED_ADMIN_ID_PLACEHOLDER"; // Replace with actual claim retrieval and validation.

            if (string.IsNullOrWhiteSpace(encryptedAdminUpdaterIdFromAuth))
            {
                return Unauthorized("Admin privileges required and admin ID not found in authentication context.");
            }

            try
            {
                bool success = await _userDataAccess.UpdateAdminAsync(adminUpdateData, encryptedAdminUpdaterIdFromAuth);

                if (!success)
                {
                    // UserDataAccess.UpdateAdminAsync logs details.
                    // Check if target user exists for a better 404
                    var targetUserExists = await _userDataAccess.GetByIdAsync(adminUpdateData.EncryptedUserId);
                    if (targetUserExists == null) return NotFound($"Target user with ID '{adminUpdateData.EncryptedUserId}' not found for admin update.");

                    return BadRequest("Failed to perform admin update on user. The user may not exist or an error occurred.");
                }
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred during the admin update process.");
            }
            #endregion
        }


        // Define DTO for password rotation
        public class RotatePasswordRequestDto
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string EncryptedTargetUserId { get; set; }
            [System.ComponentModel.DataAnnotations.Required]
            [MinLength(8)] // Example: Add password complexity rules as needed
            public string NewPlainPassword { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Rotates (changes) the password for a specified user.
        /// </summary>
        /// <param name="rotatePasswordRequest">Request containing the target user's encrypted ID and the new password.</param>
        /// <returns>A success response if the password rotation is successful; otherwise, an error response.</returns>
        /// <remarks>
        /// This endpoint can be used by a user to change their own password or by an admin to reset a user's password.
        /// The updater's ID would be derived from the authentication context.
        /// Example: `POST /api/users/rotate-password`
        /// Example Request Body:
        /// ```json
        /// {
        ///   "encryptedTargetUserId": "USER_TO_UPDATE_ENCRYPTED_ID",
        ///   "newPlainPassword": "NewSecurePassword123!"
        /// }
        /// ```
        /// </remarks>
        /// <response code="204">Password rotated successfully.</response>
        /// <response code="400">If the request is invalid (e.g., missing fields, weak password).</response>
        /// <response code="401">If the current user is not authorized for this action.</response>
        /// <response code="404">If the target user is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("rotate-password")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RotatePassword([FromBody] RotatePasswordRequestDto rotatePasswordRequest)
        {
            #region implementation
            if (rotatePasswordRequest == null ||
                string.IsNullOrWhiteSpace(rotatePasswordRequest.EncryptedTargetUserId) ||
                string.IsNullOrWhiteSpace(rotatePasswordRequest.NewPlainPassword))
            {
                return BadRequest("Target user ID and new password are required.");
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // IMPORTANT: Get the authenticated updater's ID from claims.
            // This could be the user themselves or an admin.
            var encryptedUpdaterUserIdFromAuth = User.Claims.FirstOrDefault(c => c.Type == "EncryptedUserId")?.Value;
            // For demonstration:
           // string encryptedUpdaterUserIdFromAuth = "CURRENT_USER_OR_ADMIN_ENCRYPTED_ID_PLACEHOLDER"; // Replace with actual logic

            // Authorization: Check if encryptedUpdaterUserIdFromAuth is the same as EncryptedTargetUserId (self-change)
            // OR if encryptedUpdaterUserIdFromAuth belongs to an admin.
           //if (encryptedUpdaterUserIdFromAuth != rotatePasswordRequest.EncryptedTargetUserId && !User.IsInRole("Admin")) 
            //          return Unauthorized("Not authorized to change this user's password.");


            if (string.IsNullOrWhiteSpace(encryptedUpdaterUserIdFromAuth))
            {
                return Unauthorized("Unable to determine updater user ID from authentication context for password rotation.");
            }


            try
            {
                bool success = await _userDataAccess.RotatePasswordAsync(
                    rotatePasswordRequest.EncryptedTargetUserId,
                    rotatePasswordRequest.NewPlainPassword,
                    encryptedUpdaterUserIdFromAuth);

                if (!success)
                {
                    // UserDataAccess.RotatePasswordAsync logs details.
                    var targetUserExists = await _userDataAccess.GetByIdAsync(rotatePasswordRequest.EncryptedTargetUserId);
                    if (targetUserExists == null) return NotFound($"Target user with ID '{rotatePasswordRequest.EncryptedTargetUserId}' not found for password rotation.");

                    return BadRequest("Failed to rotate password. The user may not exist or an error occurred.");
                }
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message); // e.g. password empty
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred during password rotation. {ex.Message}");
            }
            #endregion
        }

        #endregion
    }
}
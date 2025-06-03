
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography; // Required for CryptographicException
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity; // Added for StatusCodes
using Microsoft.IdentityModel.JsonWebTokens;

using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.DataAccess;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Newtonsoft.Json; // Added for UserDataAccess


namespace MedRecPro.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly StringCipher _stringCipher;
        private readonly IConfiguration _configuration;
        private readonly UserDataAccess _userDataAccess; // Added UserDataAccess
        private readonly ILogger<UsersController> _logger; // Added for logging
        private readonly string _pkSecret;

        #region Properties
        // Username and password for LoginRequestDto
        public class LoginRequestDto
        {
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.EmailAddress]
            public string Email { get; set; }

            [System.ComponentModel.DataAnnotations.Required]
            public string Password { get; set; }
        }

        // Define DTO for password rotation
        public class RotatePasswordRequestDto
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string EncryptedTargetUserId { get; set; }

            [System.ComponentModel.DataAnnotations.Required]
            [MinLength(12)]
            public string NewPlainPassword { get; set; }

            [System.ComponentModel.DataAnnotations.Required]
            [MinLength(12)]
            public string NewConfirmationPassword { get; set; }
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="stringCipher">The string cipher utility.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="userDataAccess">The data access layer for users.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        /// <remarks>
        /// Dependencies are injected via the constructor.
        /// The PKSecret for encryption is retrieved from configuration.
        /// </remarks>
        public UsersController(StringCipher stringCipher, IConfiguration configuration, UserDataAccess userDataAccess, ILogger<UsersController> logger)
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

            _logger = logger;
            #endregion
        }

        // Private helper method (example, if needed for getting current user ID)
        // Ensure it follows naming convention: private string getAuthenticatedUserId() { ... }

        #region Private
        /**************************************************************/
        /// <summary>
        /// Retrieves the encrypted user ID from the claims of the authenticated user.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        private string? getEncryptedIdFromClaim()
        {
            #region Implementation

            string? ret = null;

            // Encrypt to for the call to get the user from the db           
            string encryptedAuthUserId;

            try
            {
                long id = 0;

                // IMPORTANT: Get the authenticated ID from claims.
                var idClaim = User.Claims.FirstOrDefault(c => c.Type
                    .Contains("NameIdentifier", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrEmpty(idClaim) || !Int64.TryParse(idClaim, out id) || id <= 0)
                {
                    throw new UnauthorizedAccessException("Unable to determine user ID from authentication context.");
                }

                try
                {
                    encryptedAuthUserId = StringCipher.Encrypt(id.ToString(), _pkSecret);

                    ret = encryptedAuthUserId;
                }
                catch (Exception ex) // Catch potential encryption errors
                {
                    _logger.LogError(ex, "Encryption failed for user ID.");
                    throw;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get encrypted id from claims");
                throw;
            }

            return ret; 

            #endregion
        }

        /**************************************************************/
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)] // 
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        private async Task<ActionResult> hasUserAdminStatus()
        {
            #region Implementation

            string? encryptedAuthUserId = null;

            // IMPORTANT: Get the authenticated ID from claims.
            try
            {
                encryptedAuthUserId = getEncryptedIdFromClaim();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Unable to determine user ID from authentication context.");
            }
            catch (Exception e)
            {
                return Problem($"{e.Message}");
            }

            if (string.IsNullOrEmpty(encryptedAuthUserId))
            {
                return Unauthorized("Unable to acquire encrypted ID.");
            }

            // Authenticated user from claims
            User? claimsUser = await _userDataAccess
                .GetByIdAsync(encryptedAuthUserId);

            // This should not happen, but if it does, return unauthorized.
            if (claimsUser == null)
            {
                // User ID from claim was valid but user not found in DB.
                // Could be 401 (token valid, user doesn't exist) or 403 (user exists but shouldn't be here)                
                return Unauthorized("Unable to identify the acting user from the provided token.");
            }

            // Authorization Check: user must be an Admin/UserAdmin
            bool isAuthorized = claimsUser.IsUserAdmin();

            if (!isAuthorized)
            {
                // Caller cannot perform function.
                return StatusCode(StatusCodes.Status403Forbidden, "You are not authorized.");
            }

            return Ok(); // Return OK if the user is an admin or authorized 
            #endregion
        }
        #endregion

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
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
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

                var dto = new UserManagementDto(user);

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
        [ProducesResponseType(typeof(IEnumerable<UserManagementDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllUsers([FromQuery] bool includeDeleted = false, [FromQuery] int skip = 0, [FromQuery] int take = 100)
        {
            #region implementation
            List<UserManagementDto> userDtos = new List<UserManagementDto>(take);

            try
            {
                var adminCheck = await hasUserAdminStatus();

                if (adminCheck is not OkResult)
                {
                    return adminCheck; // Return if the user is not an admin
                }

                // UserDataAccess.GetAllAsync handles fetching and pagination.
                // It also ensures EncryptedUserId is populated for each user.
                var users = await _userDataAccess.GetAllAsync(includeDeleted, skip, take);

                if (users == null || users.Count() == 0)
                {
                    return Ok(userDtos); // Return empty list if no users found
                }
                else
                {
                    // Convert User to UserDto for response
                    userDtos = users.Select(u => new UserManagementDto(u)).ToList();
                }

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                // Log ex
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while retrieving users. {ex.Message}");
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
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
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
                var adminCheck = await hasUserAdminStatus();

                if (adminCheck is not OkResult)
                {
                    return adminCheck; // Return if the user is not an admin
                }

                var user = await _userDataAccess.GetByEmailAsync(email);

                if (user == null)
                {
                    return NotFound($"User with email '{email}' not found.");
                }

                var dto = new UserManagementDto(user);

                // UserDataAccess populates EncryptedUserId
                return Ok(dto);
            }
            catch (Exception ex)
            {
                // Log ex
                _logger.LogError(ex, "An error occurred while retrieving user by email.");

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request. {ex.Message}");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a user by their authentication claim.
        /// </summary>
        /// <returns>The user object if found; otherwise, a NotFound response.</returns>
        /// <remarks>
        /// Example: `GET /api/users/me`
        /// </remarks>
        /// <response code="200">Returns the requested user.</response>
        /// <response code="404">If the user with the specified email is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserFacingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMe()
        {
            #region implementation

            try
            {
                string? encryptedAuthUserId = null;

                // IMPORTANT: Get the authenticated ID from claims.
                try
                {
                    encryptedAuthUserId = getEncryptedIdFromClaim();
                }
                catch (UnauthorizedAccessException)
                {
                    return Unauthorized("Unable to determine user ID from authentication context.");
                }
                catch (Exception e)
                {
                    return Problem($"{e.Message}");
                }

                if (string.IsNullOrEmpty(encryptedAuthUserId))
                {
                    return Unauthorized("Unable to acquire encrypted ID.");
                }

                var user = await _userDataAccess.GetByIdAsync(encryptedAuthUserId);

                if (user == null)
                {
                    return NotFound($"User not found.");
                }

                var dto = new UserFacingDto(user);

                // UserDataAccess populates EncryptedUserId
                return Ok(dto);
            }
            catch (Exception ex)
            {
                // Log ex
                _logger.LogError(ex, "An error occurred while retrieving user.");

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request. {ex.Message}");
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
        public async Task<IActionResult> SignUpUser([FromBody] UserSignUpRequestDto signUpRequest)
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
        /// </remarks>
        /// <response code="200">Authentication successful. Returns user details.</response>
        /// <response code="400">If the request is invalid (e.g., missing email or password).</response>
        /// <response code="401">Authentication failed (e.g., invalid credentials, account locked).</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("authenticate")]
        [ProducesResponseType(typeof(UserFacingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AuthenticateUser([FromBody] LoginRequestDto loginRequest)
        {
            #region implementation

            int timeOut;

            // Initialize to null, will be set if authentication is successful
            UserFacingDto? userDto
                = null;

            string? cookieName = _configuration["UserCookie"];

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

                // Set timeout for the user session
                if (!Int32.TryParse(_configuration["AuthenticationTimeout"], out timeOut))
                {
                    _logger.LogWarning("Authentication timeout not set in configuration, using default of 60 minutes.");

                    // Default to 60 minutes if not set
                    timeOut = 60;
                }

                // User authenticated successfully, now create claims and sign in.
                var claims = new List<Claim>
                {
                    // THIS IS THE CRUCIAL CLAIM for getEncryptedIdFromClaim()
                    // It uses the UNENCRYPTED user.Id
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

                    // Using ClaimTypes for standard claims
                    new Claim(ClaimTypes.Role, user.UserRole),

                    new Claim(ClaimTypes.Email, user.Email ?? loginRequest.Email ?? string.Empty),

                    new Claim(ClaimTypes.Name, user.UserName ?? loginRequest.Email ?? string.Empty), 

                    // Unique Token ID, good practice             
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                // Security: Token claim to mask the NameIdentifier value
                var jwtClaim = new List<Claim>
                {
                    // Using ClaimTypes for standard claims
                    new Claim(ClaimTypes.Role, user.UserRole),

                    // It uses the ENCRYPTED user.Id
                    new Claim(JwtRegisteredClaimNames.NameId, StringCipher.Encrypt(_pkSecret, user.Id.ToString())),

                    // Using JwtRegisteredClaimNames
                    new Claim(JwtRegisteredClaimNames.Email, user.Email ?? loginRequest.Email ?? string.Empty), 

                    // Using JwtRegisteredClaimNames for 'name'
                    new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? loginRequest.Email ?? string.Empty), 

                    // Unique Token ID, good practice             
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                    // Custom claim for user permissions
                    new Claim("Permissions", user.UserPermissions ?? string.Empty)
                };

                // Retrieve JWT settings from configuration
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];
                var jwtAudience = _configuration["Jwt:Audience"];

                if (string.IsNullOrEmpty(jwtKey)
                    || string.IsNullOrEmpty(jwtIssuer)
                    || string.IsNullOrEmpty(jwtAudience))
                {
                    _logger.LogCritical("JWT configuration (Key, Issuer, or Audience) is missing.");

                    return StatusCode(StatusCodes.Status500InternalServerError, "Authentication service configuration error.");
                }

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                // SecurityTokenDescriptor to describe the token to be created.
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(jwtClaim),
                    Expires = DateTime.UtcNow.AddMinutes(timeOut),
                    Issuer = jwtIssuer,
                    Audience = jwtAudience,
                    SigningCredentials = credentials
                };

                // JsonWebTokenHandler from Microsoft.IdentityModel.JsonWebTokens
                var tokenHandler = new JsonWebTokenHandler();
                var jwtTokenString = tokenHandler.CreateToken(tokenDescriptor);

                // Prepare Response DTO
                userDto = new UserFacingDto(user)
                {
                    Token = jwtTokenString // Assign the generated token
                };

                // Ensure userDto is not null before proceeding
                if (userDto == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create user DTO.");
                }

                // Create the identity and principal
                var claimsIdentity = new ClaimsIdentity(
                    claims, IdentityConstants.ApplicationScheme);

                // Create authentication properties
                var authProperties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(timeOut)
                };

                // Sign in the user
                await HttpContext.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                try
                {
                    // Set user cookie
                    if (!string.IsNullOrEmpty(cookieName))
                    {
                        // Serialize
                        string json = JsonConvert.SerializeObject(userDto);

                        // Encrypt
                        string eJson = StringCipher.Encrypt(json, _pkSecret);

                        // Write
                        Response.Cookies.Append(cookieName, eJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to write encrypted user cookie {ex.Message}");
                }

                // return user data
                return Ok(userDto);
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
        public async Task<IActionResult> UpdateUserProfile(string encryptedUserId, [FromBody] UserFacingUpdateDto profileUpdate) // Using UserFacingUpdateDto model as DTO
        {
            #region implementation

            string? encryptedIdFromClaim = getEncryptedIdFromClaim();

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
            if (string.IsNullOrEmpty(encryptedIdFromClaim))
            {
                return Unauthorized("Unable to acquire encrypted ID from authentication context.");
            }

            bool isSelf = String.Equals(encryptedUserId.Decrypt(_pkSecret), encryptedIdFromClaim.Decrypt(_pkSecret), StringComparison.Ordinal);

            // Encrypted User ID values are never the same; comparing the decrypted values is necessary.
            if (!isSelf)
            {
                return BadRequest("Encrypted User ID in path does not match Encrypted User ID in request body.");
            }

            try
            {
                // UpateProfileAsync handles the update logic.
                bool success = await _userDataAccess
                    .UpdateProfileAsync(profileUpdate.ToUser(), encryptedIdFromClaim);

                if (!success)
                {
                    // UpdateProfileAsync logs specifics. Could be not found, or other update issue.
                    // Check if user exists first to return a more specific 404 if that's the case.
                    var userExists = await _userDataAccess.GetByIdAsync(encryptedUserId);

                    if (userExists == null) return NotFound($"User with ID '{encryptedUserId}' not found for update.");

                    return BadRequest("Failed to update user profile. The user may not exist or an error occurred.");
                }

                // Standard for successful PUT update with no content to return.
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Log ex
                _logger.LogError(ex, $"An error occurred while updating user profile. {ex}");

                // Return a generic error message to avoid leaking sensitive information
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while updating the user profile. {ex.Message}");
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
        /// <response code ="403">If the user is not authorized to delete this account (e.g., not an admin or self).</response>
        /// <response code="404">If the user to delete is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpDelete("{encryptedUserId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteUser(string encryptedUserId)
        {
            #region implementation

            string? encryptedDeleterUserIdFromAuth = null;

            #region Validation and Authorizations

            if (string.IsNullOrWhiteSpace(encryptedUserId))
            {
                return BadRequest("Encrypted User ID cannot be empty.");
            }

            // Find the user to delete
            User? targetUser = await _userDataAccess.GetByIdAsync(encryptedUserId);

            // Check if the user exists
            if (targetUser == null)
            {
                return NotFound($"User with ID '{encryptedUserId}' not found.");
            }

            // IMPORTANT: Get the authenticated ID from claims.
            try
            {
                encryptedDeleterUserIdFromAuth = getEncryptedIdFromClaim();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Unable to determine user ID from authentication context.");
            }
            catch (Exception e)
            {
                return Problem($"{e.Message}");
            }

            if (string.IsNullOrEmpty(encryptedDeleterUserIdFromAuth))
            {
                return Unauthorized("Unable to acquire encrypted ID.");
            }

            // Authenticated user from claims
            User? claimsUser = await _userDataAccess
                .GetByIdAsync(encryptedDeleterUserIdFromAuth);

            // This should not happen, but if it does, return unauthorized.
            if (claimsUser == null)
            {
                return Unauthorized("Unable to identify the acting user.");
            }

            // Authorization Check: Deleter must be an Admin/UserAdmin OR be the target user.
            bool isAuthorized = claimsUser.IsUserAdmin() || (claimsUser.Id == targetUser.Id);

            if (!isAuthorized)
            {
                // Caller cannot delete this user.
                StatusCode(StatusCodes.Status403Forbidden, "You are not authorized.");
            }

            #endregion

            try
            {
                bool success = await _userDataAccess.DeleteAsync(encryptedUserId, encryptedDeleterUserIdFromAuth);

                if (!success)
                {
                    // Failed to delete user.
                    _logger.LogWarning($"Failed to delete user with ID '{encryptedUserId}'.");
                    return BadRequest("Failed to delete user. The user may no longer exist or an error occurred.");
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
        public async Task<IActionResult> AdminUpdateUser([FromBody] AdminUserUpdateDto adminUpdateData)
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

            string? encryptedAdminUpdaterIdFromAuth = getEncryptedIdFromClaim();

            if (string.IsNullOrWhiteSpace(encryptedAdminUpdaterIdFromAuth))
            {
                return Unauthorized("ID not found in authentication context.");
            }

            var adminCheck = await hasUserAdminStatus();

            if (adminCheck is not OkResult)
            {
                return adminCheck; // Return if the user is not an admin
            }

            try
            {
                bool success = await _userDataAccess.UpdateAdminAsync(adminUpdateData, encryptedAdminUpdaterIdFromAuth);

                if (!success)
                {
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
        ///   "newPlainPassword": "NewSecurePassword123!",
        ///   "newConfirmationPassword": "NewSecurePassword123!"
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
                string.IsNullOrWhiteSpace(rotatePasswordRequest.NewPlainPassword) ||
                string.IsNullOrWhiteSpace(rotatePasswordRequest.NewConfirmationPassword))
            {
                return BadRequest("Target user ID and new password are required.");
            }

            if (rotatePasswordRequest.NewPlainPassword != rotatePasswordRequest.NewConfirmationPassword)
            {
                return BadRequest("New password and confirmation password do not match.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // IMPORTANT: Get the authenticated updater's ID from claims.
            var encryptedUpdaterUserIdFromAuth = getEncryptedIdFromClaim();

            if (string.IsNullOrWhiteSpace(encryptedUpdaterUserIdFromAuth))
            {
                return Unauthorized("Unable to determine updater user ID from authentication context for password rotation.");
            }

            // Get the claim user
            var claimsUser = await _userDataAccess
                .GetByIdAsync(encryptedUpdaterUserIdFromAuth);

            // This should not happen, but if it does, return unauthorized.
            if (claimsUser == null)
            {
                _logger.LogWarning("Unable to identify the acting user from the provided token.");

                return Unauthorized("Unable to identify the acting user from the provided token.");
            }

            // Encrypted User ID values are never the same; comparing the decrypted values is necessary.
            bool isSelf = String.Equals(
                rotatePasswordRequest.EncryptedTargetUserId.Decrypt(_pkSecret),
                encryptedUpdaterUserIdFromAuth.Decrypt(_pkSecret),
                StringComparison.Ordinal);

            try
            {
                // Authorization Check: user must be the target user or an Admin/UserAdmin
                if (isSelf || claimsUser.IsUserAdmin())
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
                }
                else
                {
                    return Unauthorized("You are not authorized to rotate this user's password.");
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
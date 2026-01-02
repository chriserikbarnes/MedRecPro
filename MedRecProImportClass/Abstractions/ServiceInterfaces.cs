namespace MedRecProImportClass.Abstractions;

/**************************************************************/
/// <summary>
/// Interface for encryption operations.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Decrypts the specified encrypted value.
    /// </summary>
    string? Decrypt(string? encryptedValue);

    /// <summary>
    /// Encrypts the specified value.
    /// </summary>
    string? Encrypt(string? value);

    /// <summary>
    /// Decrypts the specified encrypted value and parses to int.
    /// </summary>
    int? DecryptToInt(object? encryptedValue);

    /// <summary>
    /// Decrypts the specified encrypted value to string.
    /// </summary>
    string? DecryptToString(object? encryptedValue);
}

/**************************************************************/
/// <summary>
/// Interface for dictionary utility operations.
/// </summary>
public interface IDictionaryUtilityService
{
    /// <summary>
    /// Safely retrieves a value from a dictionary.
    /// </summary>
    object? SafeGet(IDictionary<string, object?> dictionary, string key);
}

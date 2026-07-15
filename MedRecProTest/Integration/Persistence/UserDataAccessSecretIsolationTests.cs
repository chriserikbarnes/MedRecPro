using MedRecPro.Configuration;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence;

/**************************************************************/
/// <summary>
/// Verifies that user identifier encryption remains isolated to each root service provider.
/// </summary>
/// <remarks>
/// Each provider owns an independent database-security configuration and must resolve only identifiers
/// encrypted by its own <see cref="IPrimaryKeyCipher"/> singleton.
/// </remarks>
/// <seealso cref="UserDataAccess"/>
/// <seealso cref="PrimaryKeyCipher"/>
[TestClass]
[TestCategory("Integration")]
public class UserDataAccessSecretIsolationTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies two providers retain independent user-ID ciphers across interleaved resolutions.
    /// </summary>
    /// <remarks>
    /// The A-to-B-to-A order detects both first-writer and last-writer process-wide secret caches, while
    /// bidirectional rejection proves foreign ciphertext never reaches another provider's EF query path.
    /// </remarks>
    /// <returns>A task representing the asynchronous isolation assertions.</returns>
    /// <seealso cref="UserDataAccess.GetByIdAsync"/>
    [TestMethod]
    public async Task GetByIdAsync_TwoProviders_IsolatesPrimaryKeySecrets()
    {
        #region implementation

        await using var providerA = createProvider(
            $"UserDataAccessSecretIsolation-A-{Guid.NewGuid():N}",
            "provider-a-primary-key-secret");
        await using var providerB = createProvider(
            $"UserDataAccessSecretIsolation-B-{Guid.NewGuid():N}",
            "provider-b-primary-key-secret");
        await using var scopeA = providerA.CreateAsyncScope();
        await using var scopeB = providerB.CreateAsyncScope();

        const long userAId = 101;
        const long userBId = 202;
        await seedUserAsync(scopeA.ServiceProvider, userAId, "provider-a@example.com");
        await seedUserAsync(scopeB.ServiceProvider, userBId, "provider-b@example.com");

        var cipherA = scopeA.ServiceProvider.GetRequiredService<IPrimaryKeyCipher>();
        var cipherB = scopeB.ServiceProvider.GetRequiredService<IPrimaryKeyCipher>();
        var usersA = scopeA.ServiceProvider.GetRequiredService<UserDataAccess>();
        var usersB = scopeB.ServiceProvider.GetRequiredService<UserDataAccess>();
        var encryptedA = cipherA.Encrypt(userAId);
        var encryptedB = cipherB.Encrypt(userBId);

        var resolvedAFirst = await usersA.GetByIdAsync(encryptedA);
        var resolvedB = await usersB.GetByIdAsync(encryptedB);
        var resolvedASecond = await usersA.GetByIdAsync(encryptedA);
        var foreignForA = await usersA.GetByIdAsync(encryptedB);
        var foreignForB = await usersB.GetByIdAsync(encryptedA);

        Assert.AreNotSame(cipherA, cipherB);
        Assert.IsNotNull(resolvedAFirst);
        Assert.IsNotNull(resolvedB);
        Assert.IsNotNull(resolvedASecond);
        Assert.AreEqual(userAId, resolvedAFirst.Id);
        Assert.AreEqual(userBId, resolvedB.Id);
        Assert.AreEqual(userAId, resolvedASecond.Id);
        Assert.IsNull(foreignForA);
        Assert.IsNull(foreignForB);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a validated provider with an independent database and primary-key secret.
    /// </summary>
    /// <param name="databaseName">Unique EF InMemory database name.</param>
    /// <param name="pkSecret">Provider-owned primary-key encryption secret.</param>
    /// <returns>A service provider configured with production cipher and user data-access lifetimes.</returns>
    /// <seealso cref="PrimaryKeyCipher"/>
    /// <seealso cref="UserDataAccess"/>
    private static ServiceProvider createProvider(string databaseName, string pkSecret)
    {
        #region implementation

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<ILogger<UserDataAccess>>(NullLogger<UserDataAccess>.Instance);
        services.Configure<DatabaseSecurityOptions>(options => options.PKSecret = pkSecret);
        services.AddSingleton<IPrimaryKeyCipher, PrimaryKeyCipher>();
        services.AddScoped<UserDataAccess>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Seeds one active user into a provider-owned database.
    /// </summary>
    /// <param name="serviceProvider">Scoped provider that owns the database context.</param>
    /// <param name="userId">Deterministic numeric user identifier.</param>
    /// <param name="email">Unique user email address.</param>
    /// <returns>A task representing the asynchronous database insert.</returns>
    /// <seealso cref="User"/>
    private static async Task seedUserAsync(
        IServiceProvider serviceProvider,
        long userId,
        string email)
    {
        #region implementation

        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        context.AppUsers.Add(new User
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PrimaryEmail = email,
            DisplayName = email,
            CanonicalUsername = email,
            UserRole = "User",
            Timezone = "UTC",
            Locale = "en-US",
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await context.SaveChangesAsync();

        #endregion
    }

    #endregion
}

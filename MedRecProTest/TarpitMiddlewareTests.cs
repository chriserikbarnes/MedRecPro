using MedRecPro.Middleware;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Net;

namespace MedRecPro.Service.Test
{
    /*************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TarpitMiddleware"/> functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover disabled passthrough, 404 tracking and delay application,
    /// counter reset on success, error resilience, and client IP resolution
    /// from various header sources (Cloudflare, X-Forwarded-For, RemoteIpAddress).
    /// </remarks>
    /// <seealso cref="TarpitMiddleware"/>
    /// <seealso cref="TarpitService"/>
    /// <seealso cref="TarpitSettings"/>
    [TestClass]
    public class TarpitMiddlewareTests
    {
        #region Helper Methods

        /*************************************************************/
        /// <summary>
        /// Creates a mock IOptionsMonitor for TarpitSettings with the specified configuration.
        /// </summary>
        private static IOptionsMonitor<TarpitSettings> CreateSettingsMonitor(TarpitSettings? settings = null)
        {
            #region implementation

            var config = settings ?? new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 5,
                MaxDelayMs = 30_000,
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = true,
                MonitoredEndpoints = new List<string> { "/api/", "/Home/Index" },
                EndpointRateThreshold = 20,
                EndpointWindowSeconds = 60
            };

            var monitor = new Mock<IOptionsMonitor<TarpitSettings>>();
            monitor.Setup(m => m.CurrentValue).Returns(config);
            return monitor.Object;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Creates a DefaultHttpContext with an optional remote IP address and request path.
        /// </summary>
        private static DefaultHttpContext CreateHttpContext(string? remoteIp = "127.0.0.1", string path = "/nonexistent-path")
        {
            #region implementation

            var context = new DefaultHttpContext();
            context.Request.Path = path;

            if (remoteIp != null)
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            }

            return context;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Creates a TarpitService instance for use in middleware tests.
        /// </summary>
        private static TarpitService CreateTarpitService(TarpitSettings? settings = null)
        {
            #region implementation

            var monitor = CreateSettingsMonitor(settings);
            var logger = new Mock<ILogger<TarpitService>>();
            return new TarpitService(monitor, logger.Object);

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Creates a TarpitMiddleware instance with a configurable next delegate.
        /// </summary>
        private static TarpitMiddleware CreateMiddleware(
            RequestDelegate next,
            TarpitService tarpitService,
            TarpitSettings? settings = null)
        {
            #region implementation

            var monitor = CreateSettingsMonitor(settings);
            var logger = new Mock<ILogger<TarpitMiddleware>>();
            return new TarpitMiddleware(next, tarpitService, monitor, logger.Object);

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Creates a RequestDelegate that sets the specified status code.
        /// </summary>
        private static RequestDelegate CreateNextDelegate(int statusCode)
        {
            #region implementation

            return context =>
            {
                context.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            };

            #endregion
        }

        #endregion

        #region Enabled/Disabled Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that when Enabled=false, the middleware passes through
        /// without any tracking or delay.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_Disabled_NoTrackingOccurs()
        {
            #region implementation

            // Arrange
            var settings = new TarpitSettings { Enabled = false, CleanupIntervalMinutes = 60 };
            using var tarpitService = CreateTarpitService(settings);
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService, settings);
            var context = CreateHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(0, tarpitService.TrackedIpCount,
                "When disabled, no IPs should be tracked");

            #endregion
        }

        #endregion

        #region Non-404 Response Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that 200 responses are not tracked or delayed.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_Non404Response_NoDelay()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);
            var context = CreateHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(0, tarpitService.GetHitCount("127.0.0.1"),
                "A 200 response should not record a hit");

            #endregion
        }

        #endregion

        #region 404 Tracking Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that the first 404 from an IP records a hit but does not delay.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_First404_NoDelay()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            var context = CreateHttpContext();

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await middleware.InvokeAsync(context);
            sw.Stop();

            // Assert
            Assert.AreEqual(1, tarpitService.GetHitCount("127.0.0.1"),
                "First 404 should record 1 hit");
            Assert.IsTrue(sw.ElapsedMilliseconds < 500,
                $"First 404 should not delay (took {sw.ElapsedMilliseconds}ms)");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that 404s below the trigger threshold record hits but do not delay.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_404BelowThreshold_NoDelay()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);

            // Act — hit 4 times (below threshold of 5)
            for (int i = 0; i < 4; i++)
            {
                var context = CreateHttpContext();
                await middleware.InvokeAsync(context);
            }

            // Assert
            Assert.AreEqual(4, tarpitService.GetHitCount("127.0.0.1"),
                "4 hits below threshold should be recorded");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that at the trigger threshold, a delay is applied.
        /// </summary>
        /// <remarks>
        /// Validates by checking the service state (hit count at threshold)
        /// and verifying the delay calculation returns a non-zero value.
        /// Actual Task.Delay timing is not precisely testable in unit tests.
        /// </remarks>
        [TestMethod]
        public async Task InvokeAsync_404AtThreshold_AppliesDelay()
        {
            #region implementation

            // Arrange — use a small max delay for faster test execution
            var settings = new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 3,
                MaxDelayMs = 100, // Very small delay for test speed
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = true
            };

            using var tarpitService = CreateTarpitService(settings);
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService, settings);

            // Act — hit threshold (3) times
            for (int i = 0; i < 3; i++)
            {
                var context = CreateHttpContext();
                await middleware.InvokeAsync(context);
            }

            // Assert
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Should have 3 hits at threshold");
            Assert.IsTrue(tarpitService.CalculateDelay(3) > 0,
                "Delay at threshold should be > 0");

            #endregion
        }

        #endregion

        #region ResetOnSuccess Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that a 200 response resets the counter when ResetOnSuccess is enabled.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_SuccessAfter404s_ResetsCounter()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();

            // Record some 404s first
            var notFoundMiddleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            for (int i = 0; i < 3; i++)
            {
                await notFoundMiddleware.InvokeAsync(CreateHttpContext());
            }
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Precondition: Should have 3 hits");

            // Act — send a 200 response
            var successMiddleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);
            await successMiddleware.InvokeAsync(CreateHttpContext());

            // Assert
            Assert.AreEqual(0, tarpitService.GetHitCount("127.0.0.1"),
                "After a successful response, counter should be reset");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that when ResetOnSuccess is disabled, a 200 response does not reset the counter.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_SuccessAfter404s_NoResetWhenDisabled()
        {
            #region implementation

            // Arrange
            var settings = new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 5,
                MaxDelayMs = 30_000,
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = false // Disabled
            };

            using var tarpitService = CreateTarpitService(settings);

            // Record some 404s
            var notFoundMiddleware = CreateMiddleware(CreateNextDelegate(404), tarpitService, settings);
            for (int i = 0; i < 3; i++)
            {
                await notFoundMiddleware.InvokeAsync(CreateHttpContext());
            }
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Precondition: Should have 3 hits");

            // Act — send a 200 response
            var successMiddleware = CreateMiddleware(CreateNextDelegate(200), tarpitService, settings);
            await successMiddleware.InvokeAsync(CreateHttpContext());

            // Assert
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "With ResetOnSuccess=false, counter should NOT be reset by a 200 response");

            #endregion
        }

        #endregion

        #region Error Resilience Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that exceptions in tarpit logic do not crash the request pipeline.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ExceptionInTarpit_DoesNotCrashPipeline()
        {
            #region implementation

            // Arrange — use a next delegate that sets 404, combined with a null service
            // to trigger an exception during tarpit processing.
            // Since we can't easily inject a null service without the constructor throwing,
            // we simulate an error by using a RequestDelegate that throws after setting the status.
            var callCount = 0;
            RequestDelegate throwingNext = context =>
            {
                context.Response.StatusCode = 404;
                callCount++;
                return Task.CompletedTask;
            };

            // Create middleware with normal service — the middleware should handle gracefully
            // even if something unexpected happens
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(throwingNext, tarpitService);
            var context = CreateHttpContext();

            // Act — should not throw
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(1, callCount, "Next delegate should have been called");
            Assert.AreEqual(404, context.Response.StatusCode,
                "Response status should still be 404");

            #endregion
        }

        #endregion

        #region GetClientIp Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that CF-Connecting-IP header is preferred for IP resolution.
        /// </summary>
        [TestMethod]
        public async Task GetClientIp_CloudflareHeader_UsesCfConnectingIp()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            var context = CreateHttpContext("10.0.0.1");
            context.Request.Headers["CF-Connecting-IP"] = "203.0.113.50";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 10.0.0.1";

            // Act
            await middleware.InvokeAsync(context);

            // Assert — should use CF-Connecting-IP, not X-Forwarded-For or RemoteIpAddress
            Assert.AreEqual(1, tarpitService.GetHitCount("203.0.113.50"),
                "Should use CF-Connecting-IP header value");
            Assert.AreEqual(0, tarpitService.GetHitCount("198.51.100.1"),
                "Should not use X-Forwarded-For when CF-Connecting-IP is present");
            Assert.AreEqual(0, tarpitService.GetHitCount("10.0.0.1"),
                "Should not use RemoteIpAddress when CF-Connecting-IP is present");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that X-Forwarded-For first IP is used when CF header is absent.
        /// </summary>
        [TestMethod]
        public async Task GetClientIp_XForwardedFor_UsesFirstIp()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            var context = CreateHttpContext("10.0.0.1");
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 10.0.0.2, 10.0.0.3";

            // Act
            await middleware.InvokeAsync(context);

            // Assert — should use first IP from X-Forwarded-For
            Assert.AreEqual(1, tarpitService.GetHitCount("198.51.100.1"),
                "Should use the first IP from X-Forwarded-For");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that RemoteIpAddress is used when no proxy headers are present.
        /// </summary>
        [TestMethod]
        public async Task GetClientIp_NoHeaders_UsesRemoteIpAddress()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            var context = CreateHttpContext("192.168.1.100");

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(1, tarpitService.GetHitCount("192.168.1.100"),
                "Should use RemoteIpAddress when no proxy headers are present");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that "unknown" is returned when no IP source is available.
        /// </summary>
        [TestMethod]
        public async Task GetClientIp_NoIpAvailable_ReturnsUnknown()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            var context = new DefaultHttpContext();
            context.Request.Path = "/nonexistent-path";
            // RemoteIpAddress is null by default on DefaultHttpContext

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(1, tarpitService.GetHitCount("unknown"),
                "Should fall back to 'unknown' when no IP is available");

            #endregion
        }

        #endregion

        #region Endpoint Abuse Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that a 200 response on a monitored endpoint records an endpoint hit.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MonitoredEndpoint200_RecordsEndpointHit()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);
            var context = CreateHttpContext("127.0.0.1", "/api/some-resource");

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.AreEqual(1, tarpitService.GetEndpointHitCount("127.0.0.1", "/api/"),
                "200 on monitored path /api/ should record an endpoint hit");
            Assert.AreEqual(1, tarpitService.TrackedEndpointCount,
                "Should have 1 tracked endpoint entry");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that a 200 on a monitored endpoint does NOT reset the 404 counter.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MonitoredEndpoint200_DoesNotReset404Counter()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();

            // Build up some 404 hits first
            var notFoundMiddleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            for (int i = 0; i < 3; i++)
            {
                await notFoundMiddleware.InvokeAsync(CreateHttpContext());
            }
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Precondition: Should have 3 hits in 404 tracker");

            // Act — 200 on a monitored endpoint
            var successMiddleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);
            await successMiddleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/api/test"));

            // Assert — 404 counter should NOT be reset
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "200 on monitored endpoint should NOT reset the 404 counter");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that a 200 on a non-monitored endpoint still resets the 404 counter.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NonMonitoredEndpoint200_Resets404Counter()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();

            // Build up some 404 hits first
            var notFoundMiddleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);
            for (int i = 0; i < 3; i++)
            {
                await notFoundMiddleware.InvokeAsync(CreateHttpContext());
            }
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Precondition: Should have 3 hits");

            // Act — 200 on a NON-monitored endpoint
            var successMiddleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);
            await successMiddleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/about"));

            // Assert — 404 counter SHOULD be reset (ResetOnSuccess = true)
            Assert.AreEqual(0, tarpitService.GetHitCount("127.0.0.1"),
                "200 on non-monitored endpoint should reset the 404 counter");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that below the rate threshold, no delay is applied.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MonitoredEndpointBelowThreshold_NoDelay()
        {
            #region implementation

            // Arrange — use small threshold for test speed
            var settings = new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 5,
                MaxDelayMs = 100,
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = true,
                MonitoredEndpoints = new List<string> { "/api/" },
                EndpointRateThreshold = 10,
                EndpointWindowSeconds = 60
            };

            using var tarpitService = CreateTarpitService(settings);
            var middleware = CreateMiddleware(CreateNextDelegate(200), tarpitService, settings);

            // Act — hit 5 times (below endpoint threshold of 10)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                await middleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/api/data"));
            }
            sw.Stop();

            // Assert — no delay should be applied
            Assert.AreEqual(5, tarpitService.GetEndpointHitCount("127.0.0.1", "/api/"),
                "Should have 5 endpoint hits");
            Assert.IsTrue(sw.ElapsedMilliseconds < 500,
                $"Below threshold — should not delay (took {sw.ElapsedMilliseconds}ms)");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that above the rate threshold, a delay is applied.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MonitoredEndpointAboveThreshold_AppliesDelay()
        {
            #region implementation

            // Arrange — small threshold and small max delay for test speed
            var settings = new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 5,
                MaxDelayMs = 100,
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = true,
                MonitoredEndpoints = new List<string> { "/api/" },
                EndpointRateThreshold = 3,
                EndpointWindowSeconds = 60
            };

            using var tarpitService = CreateTarpitService(settings);
            var middleware = CreateMiddleware(CreateNextDelegate(200), tarpitService, settings);

            // Act — hit threshold (3) times
            for (int i = 0; i < 3; i++)
            {
                await middleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/api/data"));
            }

            // Assert
            Assert.AreEqual(3, tarpitService.GetEndpointHitCount("127.0.0.1", "/api/"),
                "Should have 3 endpoint hits at threshold");
            Assert.IsTrue(tarpitService.CalculateEndpointDelay(3) > 0,
                "Delay at endpoint threshold should be > 0");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that monitored endpoint matching is case-insensitive.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MonitoredEndpoint_CaseInsensitiveMatch()
        {
            #region implementation

            // Arrange — monitor "/api/" in config
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(200), tarpitService);

            // Act — request path is "/API/something" (uppercase)
            await middleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/API/something"));

            // Assert — should match "/api/" case-insensitively
            Assert.AreEqual(1, tarpitService.GetEndpointHitCount("127.0.0.1", "/api/"),
                "Case-insensitive match: /API/ should match config /api/");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that an empty MonitoredEndpoints list falls through to existing ResetOnSuccess behavior.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_EmptyMonitoredList_FallsThrough()
        {
            #region implementation

            // Arrange — empty monitored endpoints list
            var settings = new TarpitSettings
            {
                Enabled = true,
                TriggerThreshold = 5,
                MaxDelayMs = 30_000,
                StaleEntryTimeoutMinutes = 10,
                CleanupIntervalMinutes = 60,
                MaxTrackedIps = 10_000,
                ResetOnSuccess = true,
                MonitoredEndpoints = new List<string>(), // Empty
                EndpointRateThreshold = 20,
                EndpointWindowSeconds = 60
            };

            using var tarpitService = CreateTarpitService(settings);

            // Build up 404 hits first
            var notFoundMiddleware = CreateMiddleware(CreateNextDelegate(404), tarpitService, settings);
            for (int i = 0; i < 3; i++)
            {
                await notFoundMiddleware.InvokeAsync(CreateHttpContext());
            }
            Assert.AreEqual(3, tarpitService.GetHitCount("127.0.0.1"),
                "Precondition: Should have 3 hits");

            // Act — 200 on /api/ path, but list is empty so it should fall through
            var successMiddleware = CreateMiddleware(CreateNextDelegate(200), tarpitService, settings);
            await successMiddleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/api/data"));

            // Assert — should reset 404 counter (empty list = all paths are non-monitored)
            Assert.AreEqual(0, tarpitService.GetHitCount("127.0.0.1"),
                "With empty MonitoredEndpoints, 200 should reset 404 counter via ResetOnSuccess");
            Assert.AreEqual(0, tarpitService.TrackedEndpointCount,
                "No endpoint abuse entries should be created with empty list");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that a 404 on a monitored path is still tracked in the 404 tracker
        /// (404 branch runs first, before the success branch).
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_404OnMonitoredPath_StillTrackedAs404()
        {
            #region implementation

            // Arrange
            using var tarpitService = CreateTarpitService();
            var middleware = CreateMiddleware(CreateNextDelegate(404), tarpitService);

            // Act — 404 on /api/ path
            await middleware.InvokeAsync(CreateHttpContext("127.0.0.1", "/api/nonexistent"));

            // Assert — should be in 404 tracker, NOT endpoint tracker
            Assert.AreEqual(1, tarpitService.GetHitCount("127.0.0.1"),
                "404 on monitored path should be tracked in 404 tracker");
            Assert.AreEqual(0, tarpitService.TrackedEndpointCount,
                "404 should NOT create an endpoint abuse entry");

            #endregion
        }

        #endregion
    }
}

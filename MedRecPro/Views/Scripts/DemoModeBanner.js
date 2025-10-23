/**
 * Demo Mode Banner Component for Single Page Applications
 * 
 * This component checks if demo mode is enabled via API endpoint
 * and displays a prominent warning banner.
 * 
 * Usage:
 * 1. Include this script in your HTML or bundle
 * 2. Call DemoModeBanner.init() on page load
 * 3. Optionally customize the styles
 */

const DemoModeBanner = (function () {
    'use strict';

    // Configuration
    const config = {
        // API endpoint that returns demo mode status
        // Example response: { enabled: true, refreshIntervalMinutes: 60, showBanner: true }
        apiEndpoint: '/api/settings/demomode',

        // CSS classes
        bannerClass: 'demo-mode-banner',
        containerClass: 'demo-mode-container',

        // Check interval (in milliseconds) - set to 0 to disable periodic checks
        checkInterval: 300000, // 5 minutes

        // Custom render function (optional)
        customRender: null
    };

    // State
    let bannerElement = null;
    let intervalId = null;

    /**
     * Initialize the demo mode banner
     */
    function init(options = {}) {
        // Merge custom options
        Object.assign(config, options);

        // Initial check
        checkDemoMode();

        // Setup periodic checks if enabled
        if (config.checkInterval > 0) {
            intervalId = setInterval(checkDemoMode, config.checkInterval);
        }
    }

    /**
     * Check demo mode status from API or configuration
     */
    async function checkDemoMode() {
        try {
            // Try to fetch from API
            const response = await fetch(config.apiEndpoint);

            if (response.ok) {
                const data = await response.json();

                if (data.enabled && data.showBanner) {
                    showBanner(data.refreshIntervalMinutes || 60);
                } else {
                    hideBanner();
                }
            } else {
                // If API endpoint doesn't exist, try to read from meta tag
                checkMetaTag();
            }
        } catch (error) {
            // Fallback to meta tag if API fails
            checkMetaTag();
        }
    }

    /**
     * Check for demo mode in HTML meta tag
     * Add to your HTML: <meta name="demo-mode" content="true" data-refresh-interval="60">
     */
    function checkMetaTag() {
        const metaTag = document.querySelector('meta[name="demo-mode"]');

        if (metaTag && metaTag.content === 'true') {
            const refreshInterval = parseInt(metaTag.dataset.refreshInterval) || 60;
            showBanner(refreshInterval);
        } else {
            hideBanner();
        }
    }

    /**
     * Show the demo mode banner
     */
    function showBanner(refreshIntervalMinutes) {
        // Don't recreate if already exists
        if (bannerElement) {
            updateBannerContent(refreshIntervalMinutes);
            return;
        }

        // Use custom render if provided
        if (config.customRender && typeof config.customRender === 'function') {
            bannerElement = config.customRender(refreshIntervalMinutes);
            document.body.insertBefore(bannerElement, document.body.firstChild);
            return;
        }

        // Default render
        bannerElement = createBannerElement(refreshIntervalMinutes);
        document.body.insertBefore(bannerElement, document.body.firstChild);

        // Add body padding to account for fixed banner
        document.body.style.paddingTop = bannerElement.offsetHeight + 'px';
    }

    /**
     * Create the banner DOM element
     */
    function createBannerElement(refreshIntervalMinutes) {
        const banner = document.createElement('div');
        banner.className = config.bannerClass;
        banner.setAttribute('role', 'alert');

        banner.innerHTML = `
            <div class="${config.containerClass}">
                <div class="demo-mode-icon">⚠️</div>
                <div class="demo-mode-content">
                    <strong class="demo-mode-title">DEMO MODE ACTIVE</strong>
                    <p class="demo-mode-text">
                        This system is running in demonstration mode. 
                        All data (except user accounts and activity logs) will be automatically reset every 
                        <strong>${refreshIntervalMinutes} minutes</strong>. 
                        Please do not store any important information.
                    </p>
                </div>
                <button class="demo-mode-close" aria-label="Dismiss" onclick="DemoModeBanner.dismiss()">×</button>
            </div>
        `;

        // Add styles
        addStyles();

        return banner;
    }

    /**
     * Update banner content (if refresh interval changes)
     */
    function updateBannerContent(refreshIntervalMinutes) {
        if (!bannerElement) return;

        const textElement = bannerElement.querySelector('.demo-mode-text strong');
        if (textElement) {
            textElement.textContent = `${refreshIntervalMinutes} minutes`;
        }
    }

    /**
     * Hide the demo mode banner
     */
    function hideBanner() {
        if (bannerElement) {
            bannerElement.remove();
            bannerElement = null;
            document.body.style.paddingTop = '';
        }
    }

    /**
     * Dismiss the banner (user action)
     */
    function dismiss() {
        hideBanner();
        // Stop periodic checks if user dismisses
        if (intervalId) {
            clearInterval(intervalId);
            intervalId = null;
        }
        // Store dismissal in session storage
        sessionStorage.setItem('demoBannerDismissed', 'true');
    }

    /**
     * Add CSS styles for the banner
     */
    function addStyles() {
        // Check if styles already added
        if (document.getElementById('demo-mode-styles')) return;

        const style = document.createElement('style');
        style.id = 'demo-mode-styles';
        style.textContent = `
            .${config.bannerClass} {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                z-index: 9999;
                background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
                border-bottom: 3px solid #ff6b6b;
                box-shadow: 0 2px 8px rgba(0,0,0,0.15);
                padding: 15px 20px;
                animation: demo-mode-slide-down 0.3s ease-out, demo-mode-pulse-border 2s ease-in-out infinite;
            }

            .${config.containerClass} {
                max-width: 1200px;
                margin: 0 auto;
                display: flex;
                align-items: center;
                gap: 15px;
            }

            .demo-mode-icon {
                font-size: 2rem;
                flex-shrink: 0;
            }

            .demo-mode-content {
                flex: 1;
            }

            .demo-mode-title {
                display: block;
                color: #d63031;
                font-size: 1.1rem;
                font-weight: 700;
                margin-bottom: 5px;
            }

            .demo-mode-text {
                color: #2d3436;
                font-size: 0.95rem;
                margin: 0;
                line-height: 1.4;
            }

            .demo-mode-text strong {
                color: #d63031;
            }

            .demo-mode-close {
                background: none;
                border: none;
                font-size: 2rem;
                line-height: 1;
                color: #636e72;
                cursor: pointer;
                padding: 0;
                width: 30px;
                height: 30px;
                display: flex;
                align-items: center;
                justify-content: center;
                border-radius: 50%;
                transition: all 0.2s;
                flex-shrink: 0;
            }

            .demo-mode-close:hover {
                background: rgba(0,0,0,0.1);
                color: #2d3436;
            }

            @keyframes demo-mode-slide-down {
                from {
                    transform: translateY(-100%);
                    opacity: 0;
                }
                to {
                    transform: translateY(0);
                    opacity: 1;
                }
            }

            @keyframes demo-mode-pulse-border {
                0%, 100% {
                    border-bottom-color: #ff6b6b;
                }
                50% {
                    border-bottom-color: #ffa502;
                }
            }

            /* Responsive */
            @media (max-width: 768px) {
                .${config.bannerClass} {
                    padding: 12px 15px;
                }

                .${config.containerClass} {
                    flex-direction: column;
                    gap: 10px;
                    text-align: center;
                }

                .demo-mode-icon {
                    font-size: 1.5rem;
                }

                .demo-mode-title {
                    font-size: 1rem;
                }

                .demo-mode-text {
                    font-size: 0.85rem;
                }

                .demo-mode-close {
                    position: absolute;
                    top: 10px;
                    right: 10px;
                }
            }
        `;

        document.head.appendChild(style);
    }

    /**
     * Cleanup on page unload
     */
    function cleanup() {
        if (intervalId) {
            clearInterval(intervalId);
        }
    }

    // Cleanup on page unload
    if (typeof window !== 'undefined') {
        window.addEventListener('beforeunload', cleanup);
    }

    // Public API
    return {
        init,
        dismiss,
        showBanner,
        hideBanner,
        checkDemoMode
    };
})();

// Auto-initialize if not in module environment
if (typeof module === 'undefined' && typeof document !== 'undefined') {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => DemoModeBanner.init());
    } else {
        DemoModeBanner.init();
    }
}

// Export for module environments
if (typeof module !== 'undefined' && module.exports) {
    module.exports = DemoModeBanner;
}
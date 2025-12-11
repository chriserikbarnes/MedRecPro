/* ============================================================
   MedRecPro Site JavaScript
   Scroll animations and interactive features
   ============================================================ */

/**
 * Initialize scroll-based animations using Intersection Observer
 * Elements with class 'animate-on-scroll' will fade in when visible
 */
function initScrollAnimations() {
    // Check for Intersection Observer support
    if (!('IntersectionObserver' in window)) {
        // Fallback: Show all elements immediately
        document.querySelectorAll('.animate-on-scroll').forEach(function(el) {
            el.classList.add('is-visible');
        });
        return;
    }

    // Configure the observer
    var observerOptions = {
        root: null,
        rootMargin: '0px 0px -50px 0px',
        threshold: 0.1
    };

    // Create observer instance
    var observer = new IntersectionObserver(function(entries, observer) {
        entries.forEach(function(entry) {
            if (entry.isIntersecting) {
                // Add staggered delay based on element index within parent
                var parent = entry.target.parentElement;
                if (parent) {
                    var siblings = Array.from(parent.querySelectorAll('.animate-on-scroll'));
                    var index = siblings.indexOf(entry.target);
                    if (index > -1) {
                        entry.target.style.transitionDelay = (index * 0.1) + 's';
                    }
                }
                
                entry.target.classList.add('is-visible');
                // Stop observing once animated
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    // Observe all target elements
    document.querySelectorAll('.animate-on-scroll').forEach(function(el) {
        observer.observe(el);
    });
}

/**
 * Add smooth scroll behavior for anchor links
 */
function initSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(function(anchor) {
        anchor.addEventListener('click', function(e) {
            var targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            var targetElement = document.querySelector(targetId);
            if (targetElement) {
                e.preventDefault();
                targetElement.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });
}

/**
 * Add hover effects for interactive cards
 */
function initCardInteractions() {
    // Feature cards
    document.querySelectorAll('.feature-card').forEach(function(card) {
        card.addEventListener('mouseenter', function() {
            this.style.transform = 'translateY(-4px)';
        });
        card.addEventListener('mouseleave', function() {
            this.style.transform = '';
        });
    });

    // Use case cards
    document.querySelectorAll('.use-case-card').forEach(function(card) {
        card.addEventListener('mouseenter', function() {
            this.style.transform = 'translateY(-4px) scale(1.02)';
        });
        card.addEventListener('mouseleave', function() {
            this.style.transform = '';
        });
    });
}

/**
 * Handle navbar scroll behavior (add shadow on scroll)
 */
function initNavbarScroll() {
    var navbar = document.querySelector('.navbar');
    if (!navbar) return;

    var lastScrollY = 0;
    var ticking = false;

    function updateNavbar() {
        if (window.scrollY > 10) {
            navbar.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.15)';
        } else {
            navbar.style.boxShadow = '';
        }
        ticking = false;
    }

    window.addEventListener('scroll', function() {
        lastScrollY = window.scrollY;
        if (!ticking) {
            window.requestAnimationFrame(updateNavbar);
            ticking = true;
        }
    }, { passive: true });
}

/**
 * Add loading animation for buttons
 */
function initButtonLoading() {
    document.querySelectorAll('.btn-auth').forEach(function(button) {
        button.addEventListener('click', function() {
            // Add loading state
            var originalContent = this.innerHTML;
            this.innerHTML = '<span class="loading-spinner"></span> Redirecting...';
            this.style.pointerEvents = 'none';
            this.style.opacity = '0.7';
        });
    });
}

/**
 * Initialize text shimmer animation reset
 * Ensures the gradient animation stays smooth
 */
function initTextShimmer() {
    var gradientText = document.querySelector('.text-gradient');
    if (!gradientText) return;

    // Reset animation periodically for smoother looping
    setInterval(function() {
        gradientText.style.animation = 'none';
        // Trigger reflow
        gradientText.offsetHeight;
        gradientText.style.animation = '';
    }, 16000); // Reset every 2 animation cycles
}

/**
 * Detect if user prefers reduced motion
 */
function prefersReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/**
 * Main initialization
 */
document.addEventListener('DOMContentLoaded', function() {
    // Respect reduced motion preferences
    if (prefersReducedMotion()) {
        document.querySelectorAll('.animate-on-scroll').forEach(function(el) {
            el.classList.add('is-visible');
            el.style.transition = 'none';
        });
        document.querySelectorAll('.animate-fade-in').forEach(function(el) {
            el.style.animation = 'none';
            el.style.opacity = '1';
        });
    } else {
        // Initialize animations
        initScrollAnimations();
        initTextShimmer();
    }

    // Initialize interactions (always)
    initSmoothScroll();
    initCardInteractions();
    initNavbarScroll();
    initButtonLoading();

    // Log initialization
    console.log('MedRecPro site initialized');
});

/**
 * Handle window resize
 */
var resizeTimeout;
window.addEventListener('resize', function() {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(function() {
        // Re-check for any layout-dependent features
    }, 250);
}, { passive: true });

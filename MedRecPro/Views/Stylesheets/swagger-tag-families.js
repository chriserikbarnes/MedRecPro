/**
 * Turns document-level Swagger family tags into controls for their prefixed child tags.
 */
(function configureSwaggerTagFamilies() {
    "use strict";

    const childTagPrefixExtension = "x-medrecpro-child-tag-prefix";
    const familyStates = new Map();
    let refreshScheduled = false;

    /**
     * Converts Swagger UI's immutable specification object into a plain object.
     * @returns {object|null} The current OpenAPI document, or null while Swagger UI is loading.
     */
    function getSpecification() {
        const specification = window.ui?.getSystem?.()?.specSelectors?.specJson?.();

        if (!specification) {
            return null;
        }

        return typeof specification.toJS === "function" ? specification.toJS() : specification;
    }

    /**
     * Reads parent names and exact child prefixes from document-level tag extensions.
     * @returns {Array<{name: string, childTagPrefix: string}>} Declared Swagger tag families.
     */
    function getFamilies() {
        const specification = getSpecification();
        const tags = Array.isArray(specification?.tags) ? specification.tags : [];
        const families = new Map();

        tags
            .filter(tag => typeof tag?.name === "string" &&
                typeof tag?.[childTagPrefixExtension] === "string")
            .forEach(tag => families.set(tag.name, {
                name: tag.name,
                childTagPrefix: tag[childTagPrefixExtension]
            }));

        return Array.from(families.values());
    }

    /**
     * Gets the exact title represented by a Swagger UI tag section.
     * @param {Element} section Swagger UI tag section.
     * @returns {string|null} Exact title text, or null when the section is incomplete.
     */
    function getSectionTitle(section) {
        const title = section.querySelector(":scope > .opblock-tag .nostyle span");
        const value = title?.textContent?.trim();
        return value || null;
    }

    /**
     * Synchronizes parent state, accessibility metadata, indentation, and child visibility.
     * @returns {void}
     */
    function refreshFamilies() {
        refreshScheduled = false;

        const families = getFamilies();
        if (families.length === 0) {
            return;
        }

        const sections = Array.from(document.querySelectorAll(".swagger-ui .opblock-tag-section"));
        const sectionsByTitle = new Map(
            sections
                .map(section => [getSectionTitle(section), section])
                .filter(([title]) => title !== null));

        for (const family of families) {
            const parentSection = sectionsByTitle.get(family.name);
            if (!parentSection) {
                continue;
            }

            const parentHeader = parentSection.querySelector(":scope > .opblock-tag");
            if (!parentHeader) {
                continue;
            }

            if (!familyStates.has(family.name)) {
                familyStates.set(family.name, true);
            }

            const expanded = familyStates.get(family.name) === true;
            parentSection.classList.add("medrecpro-swagger-family");
            parentSection.classList.toggle("is-open", expanded);
            parentSection.dataset.medrecproSwaggerFamily = family.name;
            parentHeader.setAttribute("data-is-open", expanded ? "true" : "false");
            parentHeader.setAttribute("aria-expanded", expanded ? "true" : "false");

            const childSections = [];
            for (const [title, childSection] of sectionsByTitle) {
                if (title === family.name || !title.startsWith(family.childTagPrefix)) {
                    continue;
                }

                childSection.classList.add("medrecpro-swagger-family-child");
                childSection.dataset.medrecproSwaggerParent = family.name;
                childSection.hidden = !expanded;
                childSections.push(childSection);
            }

            // Swagger's alphabetical sorter places "Users" after singular "User ..." child tags.
            // Reorder only the React wrapper spans so every declared parent consistently precedes its children.
            const parentWrapper = parentSection.parentElement;
            const commonContainer = parentWrapper?.parentElement;
            const childWrappers = childSections
                .map(childSection => childSection.parentElement)
                .filter(childWrapper => childWrapper?.parentElement === commonContainer);

            if (parentWrapper && commonContainer && childWrappers.length > 0) {
                if (parentWrapper.nextSibling !== childWrappers[0]) {
                    commonContainer.insertBefore(parentWrapper, childWrappers[0]);
                }

                let insertionPoint = parentWrapper.nextSibling;
                for (const childWrapper of childWrappers) {
                    if (childWrapper !== insertionPoint) {
                        commonContainer.insertBefore(childWrapper, insertionPoint);
                    }
                    insertionPoint = childWrapper.nextSibling;
                }
            }
        }
    }

    /**
     * Coalesces DOM and specification changes into one animation-frame refresh.
     * @returns {void}
     */
    function scheduleRefresh() {
        if (refreshScheduled) {
            return;
        }

        refreshScheduled = true;
        window.requestAnimationFrame(refreshFamilies);
    }

    /**
     * Toggles a family when its parent row is clicked while leaving child controls unchanged.
     * @param {MouseEvent} event Captured document click.
     * @returns {void}
     */
    function handleFamilyClick(event) {
        if (!(event.target instanceof Element)) {
            return;
        }

        const parentHeader = event.target.closest(".medrecpro-swagger-family > .opblock-tag");
        const parentSection = parentHeader?.parentElement;
        const familyName = parentSection?.dataset.medrecproSwaggerFamily;

        if (!parentHeader || !familyName) {
            return;
        }

        // Prevent Swagger UI from toggling only the empty parent section.
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();

        familyStates.set(familyName, familyStates.get(familyName) !== true);
        scheduleRefresh();
    }

    /**
     * Begins observing Swagger UI's asynchronous render cycle.
     * @returns {void}
     */
    function start() {
        document.addEventListener("click", handleFamilyClick, true);
        new MutationObserver(scheduleRefresh).observe(document.documentElement, {
            childList: true,
            subtree: true
        });
        scheduleRefresh();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", start, { once: true });
    } else {
        start();
    }
}());

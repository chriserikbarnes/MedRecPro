/**
 * Turns document-level Swagger family tags into controls for their prefixed child tags and
 * makes the information-description sections collapsible.
 */
(function configureSwaggerTagFamilies() {
    "use strict";

    const childTagPrefixExtension = "x-medrecpro-child-tag-prefix";
    const familyStates = new Map();
    const documentSectionStates = new Map();
    let documentSectionId = 0;
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
     * Finds the direct Markdown root used for the Swagger information description.
     * @returns {Element|null} The supported Markdown root, or null when Swagger UI has changed shape.
     */
    function getDescriptionMarkdownRoot() {
        const descriptions = document.querySelectorAll(".swagger-ui .information-container .info .description");

        for (const description of descriptions) {
            const markdownRoot = Array.from(description.children).find(child =>
                child.classList.contains("renderedMarkdown") || child.classList.contains("markdown"));
            if (markdownRoot) {
                return markdownRoot;
            }

            if (Array.from(description.children).some(child => child.tagName === "H2")) {
                return description;
            }
        }

        return null;
    }

    /**
     * Gets nonblank heading text without depending on the heading's original markup.
     * @param {Element} heading Description section heading.
     * @returns {string|null} Trimmed heading text, or null for an unusable heading.
     */
    function getDescriptionHeadingText(heading) {
        const value = heading.textContent?.trim();
        return value || null;
    }

    /**
     * Determines whether a heading belongs to the always-visible demo warning banner.
     * @param {Element} heading Candidate Markdown heading.
     * @returns {boolean} True when the heading must not be collapsed.
     */
    function isDemoHeading(heading) {
        const headingText = getDescriptionHeadingText(heading);
        return headingText?.toUpperCase().includes("DEMO MODE") === true;
    }

    /**
     * Applies the persisted visibility and accessibility state to one transformed description section.
     * @param {Element} section Transformed description section wrapper.
     * @returns {void}
     */
    function refreshDescriptionSection(section) {
        const sectionName = section.dataset.medrecproDocSection;
        const heading = section.querySelector(":scope > .medrecpro-doc-section-toggle");
        const body = section.querySelector(":scope > .medrecpro-doc-section-body");
        if (!sectionName || !heading || !body) {
            return;
        }

        const expanded = documentSectionStates.get(sectionName) === true;
        heading.setAttribute("aria-expanded", expanded ? "true" : "false");
        body.hidden = !expanded;
    }

    /**
     * Transforms eligible top-level Markdown sections into collapsed, keyboard-accessible regions.
     * @returns {void}
     */
    function refreshDescriptionSections() {
        const markdownRoot = getDescriptionMarkdownRoot();
        if (!markdownRoot) {
            return;
        }

        const existingSections = Array.from(markdownRoot.children)
            .filter(child => child.classList.contains("medrecpro-doc-section"));
        if (markdownRoot.dataset.medrecproDocSectionsInitialized === "true" && existingSections.length > 0) {
            existingSections.forEach(refreshDescriptionSection);
            return;
        }

        const children = Array.from(markdownRoot.children);
        const headings = children.filter(child => child.tagName === "H2" &&
            !isDemoHeading(child) && getDescriptionHeadingText(child) !== null);
        if (headings.length === 0) {
            return;
        }

        for (const heading of headings) {
            const sectionName = getDescriptionHeadingText(heading);
            if (!sectionName) {
                continue;
            }

            if (!documentSectionStates.has(sectionName)) {
                documentSectionStates.set(sectionName, false);
            }

            const headingIndex = children.indexOf(heading);
            const bodyChildren = [];
            for (let index = headingIndex + 1; index < children.length && children[index].tagName !== "H2"; index += 1) {
                bodyChildren.push(children[index]);
            }

            const section = document.createElement("section");
            const body = document.createElement("div");
            documentSectionId += 1;
            body.id = `medrecpro-doc-section-${documentSectionId}`;
            section.className = "medrecpro-doc-section";
            section.dataset.medrecproDocSection = sectionName;
            body.className = "medrecpro-doc-section-body";
            heading.classList.add("medrecpro-doc-section-toggle");
            heading.setAttribute("role", "button");
            heading.setAttribute("tabindex", "0");
            heading.setAttribute("aria-controls", body.id);

            markdownRoot.insertBefore(section, heading);
            section.appendChild(heading);
            section.appendChild(body);
            bodyChildren.forEach(child => body.appendChild(child));
            refreshDescriptionSection(section);
        }

        markdownRoot.dataset.medrecproDocSectionsInitialized = "true";
    }

    /**
     * Refreshes every MedRecPro Swagger enhancement from the shared render scheduler.
     * @returns {void}
     */
    function refreshSwaggerUi() {
        refreshScheduled = false;
        refreshFamilies();
        refreshDescriptionSections();
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
        window.requestAnimationFrame(refreshSwaggerUi);
    }

    /**
     * Toggles a family or information-description section from a captured click.
     * @param {MouseEvent} event Captured document click.
     * @returns {void}
     */
    function handleClick(event) {
        if (!(event.target instanceof Element)) {
            return;
        }

        const documentHeading = event.target.closest(".medrecpro-doc-section-toggle");
        const documentSection = documentHeading?.closest(".medrecpro-doc-section");
        const sectionName = documentSection?.dataset.medrecproDocSection;
        if (documentHeading && sectionName) {
            documentSectionStates.set(sectionName, documentSectionStates.get(sectionName) !== true);
            scheduleRefresh();
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
     * Toggles a description section from its keyboard button behavior.
     * @param {KeyboardEvent} event Captured document keydown.
     * @returns {void}
     */
    function handleDescriptionSectionKeydown(event) {
        if ((event.key !== "Enter" && event.key !== " ") || !(event.target instanceof Element)) {
            return;
        }

        const heading = event.target.closest(".medrecpro-doc-section-toggle");
        const section = heading?.closest(".medrecpro-doc-section");
        const sectionName = section?.dataset.medrecproDocSection;
        if (!heading || !sectionName) {
            return;
        }

        event.preventDefault();
        documentSectionStates.set(sectionName, documentSectionStates.get(sectionName) !== true);
        scheduleRefresh();
    }

    /**
     * Begins observing Swagger UI's asynchronous render cycle.
     * @returns {void}
     */
    function start() {
        document.addEventListener("click", handleClick, true);
        document.addEventListener("keydown", handleDescriptionSectionKeydown);
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

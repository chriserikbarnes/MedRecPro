/*
The contents of this file are subject to the Health Level-7 Public
License Version 1.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the
License at http://www.hl7.org/HPL/hpl.txt.

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
the License for the specific language governing rights and
limitations under the License.

The contents of this file are subject to the Apache License Version 2.0 
(the "License"); you may not use this file except in compliance with 
the License. You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0


Original Code portions subject to Health Level-7 Public License Version 1.0
Copyright (C) 2012-2016 Health Level Seven, Inc.
Copyright (C) 2025 Christopher Barnes

The Original Code has been depricated for MedRecPro.

The Initial Developer of the Original Code is Pragmatic Data LLC. 
Portions created by Initial Developer are
Copyright (C) 2012-2016 Health Level Seven, Inc. All Rights Reserved.
*/
(function () {
    /**************************************************************/
    /**
     * @private
     * @type {CSSStyleSheet|null}
     * @description Cached reference to the dynamically created stylesheet for mixin visibility control
     */
    let _mixinStyleSheet = null;

    /**************************************************************/
    /**
     * @function toggleMixin
     * @description Toggles the visibility of SPL mixin content by dynamically adding/toggling CSS rules.
     * Creates a stylesheet on first invocation that hides elements with class "spl .Mixin".
     * @public
     * @example
     * // Toggle mixin visibility
     * toggleMixin();
     * @remarks This function is exposed globally via window.toggleMixin
     * @see {@link https://www.hl7.org/implement/standards/product_brief.cfm?product_id=279|SPL Implementation Guide}
     */
    function toggleMixin() {
        // #region implementation
        if (!_mixinStyleSheet) {
            // Create stylesheet if it doesn't exist
            if (document.styleSheets && document.getElementsByTagName("head").length > 0) {
                var styleSheetElement = document.createElement("style");
                styleSheetElement.type = "text/css";
                styleSheetElement.title = "SPL Hide Mixin Content";
                document.getElementsByTagName("head")[0].appendChild(styleSheetElement);
                _mixinStyleSheet = document.styleSheets[document.styleSheets.length - 1];

                // Verify we got the correct stylesheet
                if (_mixinStyleSheet.title != "SPL Hide Mixin Content") {
                    _mixinStyleSheet = null;
                    return;
                }
            }

            // Add CSS rule to hide mixin content
            if (_mixinStyleSheet.insertRule)
                _mixinStyleSheet.insertRule(".spl .Mixin { display:none; }", 0);
            else if (_mixinStyleSheet.addRule)
                _mixinStyleSheet.addRule(".spl .Mixin", "display:none;", -1);
            else
                return;

            _mixinState = -1;
        } else {
            // Toggle existing stylesheet
            _mixinStyleSheet.disabled = !_mixinStyleSheet.disabled;
        }
        // #endregion
    }

    window.toggleMixin = toggleMixin;

    /**************************************************************/
    /**
     * @function columnize
     * @description Legacy columnization function - deprecated as of 10/21/2025 for MedRecPro.
     * @deprecated Use convertToTwoColumns or convertIndexToTwoColumns instead
     * @public
     * @remarks All functionality has been commented out. Retained for backwards compatibility.
     * @see {@link convertToTwoColumns}
     * @see {@link convertIndexToTwoColumns}
     */
    function columnize() {

        // 10/21/2025 Depricated for MedRecPro

        //[...document.querySelectorAll('.TwoColumnFormat')].forEach(rightColumn => {
        //    rightColumn.classList.remove('TwoColumnFormat');
        //    const twoColumnsContainer = document.createElementNS("http://www.w3.org/1999/xhtml", 'div');
        //    twoColumnsContainer.classList.add('two-columns');
        //    // FIXME: perhaps I should clone the current div? Attributes? Other classes?
        //    rightColumn.parentElement.insertBefore(twoColumnsContainer, rightColumn);
        //    twoColumnsContainer.insertBefore(rightColumn, null);
        //    const leftColumn = document.createElementNS("http://www.w3.org/1999/xhtml", 'div');
        //    twoColumnsContainer.insertBefore(leftColumn, rightColumn);
        //    const halfHeight = rightColumn.offsetHeight / 2;
        //    let leftHeight = leftColumn.offsetHeight;
        //    for (let item of [...rightColumn.children]) {
        //        const newLeftHeight = leftHeight + item.offsetHeight;
        //        if (newLeftHeight > halfHeight)
        //            break;
        //        leftColumn.insertBefore(item, null);
        //        leftHeight = newLeftHeight;
        //    }
        //    if (halfHeight - leftHeight > 20)
        //        columnizeFiner(leftColumn, rightColumn, halfHeight);
        //});
    }

    /**************************************************************/
    /**
     * @function columnizeFiner
     * @description Legacy fine-grained columnization function - deprecated as of 10/21/2025 for MedRecPro.
     * @param {HTMLDivElement} firstDivElement - Target left column element
     * @param {HTMLDivElement} secondDivElement - Source right column element
     * @param {number} halfHeight - Target height threshold in pixels
     * @param {HTMLElement} innerElement - Optional inner element to process
     * @param {Node} lastClonedNode - Last cloned node reference
     * @deprecated Use convertToTwoColumns or convertIndexToTwoColumns instead
     * @public
     * @remarks All functionality has been commented out. Retained for backwards compatibility.
     * @see {@link convertToTwoColumns}
     */
    function columnizeFiner(firstDivElement, secondDivElement, halfHeight, innerElement, lastClonedNode) {

        // 10/21/2025 Depricated for MedRecPro

        //let next = secondDivElement.firstChild;
        //let nextChild;

        //if (innerElement) {
        //    if (!innerElement.children)
        //        return;
        //    next = innerElement.firstChild;
        //}

        //while (next) {
        //    if (firstDivElement.done || secondDivElement.offsetHeight < halfHeight) {
        //        firstDivElement.done = true;
        //        return;
        //    }
        //    const child = next;
        //    next = child.nextElementSibling;
        //    let clonedNode;

        //    const childNodeName = child.nodeName.toLowerCase();

        //    const isListElement = childNodeName == "ul" || childNodeName == "ol";
        //    let copyCompleteElement = !child.children || child.children.length == 0 || childNodeName == "h1" || childNodeName == "li" || childNodeName == "p" || childNodeName == "table" || childNodeName == "h2" || childNodeName == "dt" || childNodeName == "dd";

        //    if (copyCompleteElement) {
        //        clonedNode = child;
        //        nextChild = child.nextSibling;
        //        if (lastClonedNode && (lastClonedNode.nodeName.toLowerCase() == "ul" || lastClonedNode.nodeName.toLowerCase() == "ol"))
        //            lastClonedNode = lastClonedNode.parentNode;

        //    } else if (isListElement) { /* Handling lists elements(ul,ol) separately - #1393 */
        //        const offsetHeightOfFirstDiv = firstDivElement.offsetHeight;
        //        if ((offsetHeightOfFirstDiv + child.offsetHeight) < halfHeight) {
        //            copyCompleteElement = true;
        //            lastClonedNode.appendChild(child);

        //        } else {
        //            const list = document.createElement(childNodeName);
        //            let newoffsetHeightOfFirstDiv = offsetHeightOfFirstDiv;
        //            for (let grandChild of [...child.children]) {
        //                if (newoffsetHeightOfFirstDiv < halfHeight || newoffsetHeightOfFirstDiv < secondDivElement.offsetHeight) {
        //                    list.appendChild(grandChild.cloneNode(true));
        //                    newoffsetHeightOfFirstDiv = newoffsetHeightOfFirstDiv + grandChild.offsetHeight;
        //                    child.removeChild(grandChild);
        //                } else
        //                    break;
        //            }
        //            lastClonedNode.appendChild(list);
        //            firstDivElement.done = true;
        //        }
        //        clonedNode = child;
        //        nextChild = child.nextSibling;
        //        lastClonedNode = child.parentNode;

        //    } else {
        //        clonedNode = child.cloneNode(false);
        //        if (child.attributes && child.attributes.getNamedItem("class") && child.attributes.getNamedItem("class").nodeValue == "HighlightSection")
        //            child.attributes.removeNamedItem("class");
        //    }

        //    if (lastClonedNode) {
        //        if (secondDivElement.offsetHeight > halfHeight) { // TODO Decide whether to move the last element to left
        //            if (!isListElement)
        //                // FIXME: lastClonedNode may be a text node, and then we can't append and an error is trhown.
        //                // the protection I am adding probably causes wrong behavior, but at least no exception
        //                if (lastClonedNode.nodeType == Node.ELEMENT_NODE)
        //                    lastClonedNode.appendChild(clonedNode);
        //                else
        //                    return;
        //        } else {
        //            firstDivElement.done = true;
        //            return;
        //        }
        //    } else
        //        firstDivElement.appendChild(clonedNode);

        //    if (!firstDivElement.done) {
        //        if (copyCompleteElement) {
        //            columnizeFiner(firstDivElement, secondDivElement, halfHeight, nextChild ? nextChild.parentNode : null, lastClonedNode ? lastClonedNode : clonedNode);
        //        } else {
        //            columnizeFiner(firstDivElement, secondDivElement, halfHeight, child, lastClonedNode ? lastClonedNode : clonedNode);
        //        }
        //    }
        //}
    }
    /**************************************************************/
    /**
     * @function convertContentToTwoColumns
     * @description Core implementation for converting single-column content into balanced two-column layout.
     * Distributes child elements between left and right columns attempting to achieve equal height distribution.
     * @private
     * @param {string} leftColumnId - DOM ID of the left column container
     * @param {string} rightColumnId - DOM ID of the right column container
     * @param {string} contentId - DOM ID of the source content container
     * @param {string} debugLabel - Label for console logging (e.g., "Highlights", "Index")
     * @returns {boolean} True if conversion succeeded, false otherwise
     * @example
     * // Internal usage
     * convertContentToTwoColumns('leftCol', 'rightCol', 'content', 'Main');
     * @remarks 
     * - Uses a 50px tolerance when determining optimal split point
     * - Retries after 100ms if content height is zero (waiting for render)
     * - Logs detailed information to console for debugging
     * @see {@link convertToTwoColumns}
     * @see {@link convertIndexToTwoColumns}
     */
    function convertContentToTwoColumns(leftColumnId, rightColumnId, contentId, debugLabel) {

        // Prevent duplicate execution
        var flagName = contentId + '_processed';
        if (window[flagName]) {
            console.log(debugLabel + ' already processed, skipping');
            return true;
        }

        // #region implementation
        var rightColumn = document.getElementById(rightColumnId);
        var leftColumn = document.getElementById(leftColumnId);
        var content = document.getElementById(contentId);

        // Validate all required elements exist
        if (!rightColumn || !leftColumn || !content) {
            console.error(debugLabel + ' two-column elements not found:', {
                rightColumn: !!rightColumn,
                leftColumn: !!leftColumn,
                content: !!content
            });
            return false;
        }

        // Get child elements to split
        var elements = Array.from(content.children);
        if (elements.length === 0) {
            console.error('No ' + debugLabel.toLowerCase() + ' content elements to split');
            return false;
        }

        // Wait for content to render if height is zero
        var totalHeight = content.offsetHeight;
        if (totalHeight === 0) {
            console.warn(debugLabel + ' content height is 0, retrying in 100ms...');
            setTimeout(function () {
                convertContentToTwoColumns(leftColumnId, rightColumnId, contentId, debugLabel);
            }, 100);
            return false;
        }

        // Mark as processed BEFORE doing the work (prevents re-entry during async operations)
        window[flagName] = true;

        // Calculate target height for balanced columns
        var targetHeight = totalHeight / 2;
        console.log(debugLabel + ' total height:', totalHeight, 'Target:', targetHeight);

        // Determine optimal split point - keep adding until we exceed target
        var currentHeight = 0;
        var elementsToMove = [];

        for (var i = 0; i < elements.length; i++) {
            var elem = elements[i];
            var elemHeight = elem.offsetHeight;
            var heightAfterAdding = currentHeight + elemHeight;

            // Always add first element
            if (i === 0) {
                elementsToMove.push(elem);
                currentHeight += elemHeight;
                continue;
            }

            // Decide whether to add this element
            if (currentHeight < targetHeight) {
                // Still under target - add if it doesn't overshoot by too much
                if (heightAfterAdding <= targetHeight * 1.25) {
                    elementsToMove.push(elem);
                    currentHeight += elemHeight;
                } else {
                    // This element would make column too tall, stop
                    break;
                }
            } else if (heightAfterAdding < currentHeight * 1.15) {
                // Already at/over target, but element is small relative to current height
                // Add it to avoid leaving very small element at top of right column
                elementsToMove.push(elem);
                currentHeight += elemHeight;
                break;
            } else {
                // Already at target and next element is substantial, stop
                break;
            }
        }

        // Move elements to left column
        elementsToMove.forEach(function (elem) {
            leftColumn.appendChild(elem);
        });

        var leftPercentage = ((currentHeight / totalHeight) * 100).toFixed(1);
        console.log('✓ ' + debugLabel + ' two-column layout applied: ' +
            elementsToMove.length + ' elements in left column (' + currentHeight + 'px, ' + leftPercentage + '%)');
        console.log('✓ Right column: ' + (elements.length - elementsToMove.length) + ' elements');

        return true;
        // #endregion
    }

    /**************************************************************/
    /**
     * @function convertToTwoColumns
     * @description Converts the highlights section content into a balanced two-column layout.
     * @public
     * @returns {boolean} True if conversion succeeded, false otherwise
     * @example
     * // Called automatically on page load or manually
     * convertToTwoColumns();
     * @remarks 
     * Requires DOM elements with IDs: highlightsLeftColumn, highlightsRightColumn, highlightsContent
     * @see {@link convertContentToTwoColumns}
     * @see {@link convertIndexToTwoColumns}
     */
    function convertToTwoColumns() {
        return convertContentToTwoColumns(
            'highlightsLeftColumn',
            'highlightsRightColumn',
            'highlightsContent',
            'Highlights'
        );
    }

    /**************************************************************/
    /**
     * @function convertIndexToTwoColumns
     * @description Converts the index section content into a balanced two-column layout.
     * @public
     * @returns {boolean} True if conversion succeeded, false otherwise
     * @example
     * // Called automatically on page load or manually
     * convertIndexToTwoColumns();
     * @remarks 
     * Requires DOM elements with IDs: indexLeftColumn, indexRightColumn, indexContent
     * @see {@link convertContentToTwoColumns}
     * @see {@link convertToTwoColumns}
     */
    function convertIndexToTwoColumns() {
        return convertContentToTwoColumns(
            'indexLeftColumn',
            'indexRightColumn',
            'indexContent',
            'Index'
        );
    }

    // #region global exports
    /**
     * @description Expose functions globally for external access and body onload compatibility
     */
    window.columnize = columnize; //deprication
    window.columnizeFiner = columnizeFiner;//deprication
    window.convertToTwoColumns = convertToTwoColumns;
    window.convertIndexToTwoColumns = convertIndexToTwoColumns;
    // #endregion

})();

// 10/21/2025 Depricated for MedRecPro
//document.addEventListener('DOMContentLoaded', columnize);

/**************************************************************/
/**
 * @description Page load event handler - executes column conversion after 200ms delay to ensure DOM rendering
 * @listens window#load
 * @see {@link convertToTwoColumns}
 * @see {@link convertIndexToTwoColumns}
 */
window.addEventListener('load', function () {
    // #region implementation
    setTimeout(function () {
        if (typeof convertToTwoColumns === 'function') {
            convertToTwoColumns();
        }
        if (typeof convertIndexToTwoColumns === 'function') {
            convertIndexToTwoColumns();
        }
    }, 200);
    // #endregion
});
/*
The contents of this file are subject to the Health Level-7 Public
License Version 1.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the
License at http://www.hl7.org/HPL/hpl.txt.

Software distributed under the License is distributed on an "AS IS"
basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
the License for the specific language governing rights and
limitations under the License.

The Original Code is all this file.

The Initial Developer of the Original Code is Pragmatic Data LLC. 
Portions created by Initial Developer are
Copyright (C) 2012-2016 Health Level Seven, Inc. All Rights Reserved.
*/
(function () {
    let _mixinStyleSheet = null;

    function toggleMixin() {
        if (!_mixinStyleSheet) {
            if (document.styleSheets && document.getElementsByTagName("head").length > 0) {
                var styleSheetElement = document.createElement("style");
                styleSheetElement.type = "text/css";
                styleSheetElement.title = "SPL Hide Mixin Content";
                document.getElementsByTagName("head")[0].appendChild(styleSheetElement);
                _mixinStyleSheet = document.styleSheets[document.styleSheets.length - 1];
                if (_mixinStyleSheet.title != "SPL Hide Mixin Content") {
                    _mixinStyleSheet = null;
                    return;
                }
            }

            if (_mixinStyleSheet.insertRule)
                _mixinStyleSheet.insertRule(".spl .Mixin { display:none; }", 0);
            else if (_mixinStyleSheet.addRule)
                _mixinStyleSheet.addRule(".spl .Mixin", "display:none;", -1);
            else
                return;
            _mixinState = -1;
        } else {
            _mixinStyleSheet.disabled = !_mixinStyleSheet.disabled;
        }
    }

    window.toggleMixin = toggleMixin;

    function columnize() {
        [...document.querySelectorAll('.TwoColumnFormat')].forEach(rightColumn => {
            rightColumn.classList.remove('TwoColumnFormat');
            const twoColumnsContainer = document.createElementNS("http://www.w3.org/1999/xhtml", 'div');
            twoColumnsContainer.classList.add('two-columns');
            // FIXME: perhaps I should clone the current div? Attributes? Other classes?
            rightColumn.parentElement.insertBefore(twoColumnsContainer, rightColumn);
            twoColumnsContainer.insertBefore(rightColumn, null);
            const leftColumn = document.createElementNS("http://www.w3.org/1999/xhtml", 'div');
            twoColumnsContainer.insertBefore(leftColumn, rightColumn);
            const halfHeight = rightColumn.offsetHeight / 2;
            let leftHeight = leftColumn.offsetHeight;
            for (let item of [...rightColumn.children]) {
                const newLeftHeight = leftHeight + item.offsetHeight;
                if (newLeftHeight > halfHeight)
                    break;
                leftColumn.insertBefore(item, null);
                leftHeight = newLeftHeight;
            }
            if (halfHeight - leftHeight > 20)
                columnizeFiner(leftColumn, rightColumn, halfHeight);
        });
    }

    window.columnize = columnize;

    function columnizeFiner(firstDivElement, secondDivElement, halfHeight, innerElement, lastClonedNode) {
        let next = secondDivElement.firstChild;
        let nextChild;

        if (innerElement) {
            if (!innerElement.children)
                return;
            next = innerElement.firstChild;
        }

        while (next) {
            if (firstDivElement.done || secondDivElement.offsetHeight < halfHeight) {
                firstDivElement.done = true;
                return;
            }
            const child = next;
            next = child.nextElementSibling;
            let clonedNode;

            const childNodeName = child.nodeName.toLowerCase();

            const isListElement = childNodeName == "ul" || childNodeName == "ol";
            let copyCompleteElement = !child.children || child.children.length == 0 || childNodeName == "h1" || childNodeName == "li" || childNodeName == "p" || childNodeName == "table" || childNodeName == "h2" || childNodeName == "dt" || childNodeName == "dd";

            if (copyCompleteElement) {
                clonedNode = child;
                nextChild = child.nextSibling;
                if (lastClonedNode && (lastClonedNode.nodeName.toLowerCase() == "ul" || lastClonedNode.nodeName.toLowerCase() == "ol"))
                    lastClonedNode = lastClonedNode.parentNode;

            } else if (isListElement) { /* Handling lists elements(ul,ol) separately - #1393 */
                const offsetHeightOfFirstDiv = firstDivElement.offsetHeight;
                if ((offsetHeightOfFirstDiv + child.offsetHeight) < halfHeight) {
                    copyCompleteElement = true;
                    lastClonedNode.appendChild(child);

                } else {
                    const list = document.createElement(childNodeName);
                    let newoffsetHeightOfFirstDiv = offsetHeightOfFirstDiv;
                    for (let grandChild of [...child.children]) {
                        if (newoffsetHeightOfFirstDiv < halfHeight || newoffsetHeightOfFirstDiv < secondDivElement.offsetHeight) {
                            list.appendChild(grandChild.cloneNode(true));
                            newoffsetHeightOfFirstDiv = newoffsetHeightOfFirstDiv + grandChild.offsetHeight;
                            child.removeChild(grandChild);
                        } else
                            break;
                    }
                    lastClonedNode.appendChild(list);
                    firstDivElement.done = true;
                }
                clonedNode = child;
                nextChild = child.nextSibling;
                lastClonedNode = child.parentNode;

            } else {
                clonedNode = child.cloneNode(false);
                if (child.attributes && child.attributes.getNamedItem("class") && child.attributes.getNamedItem("class").nodeValue == "HighlightSection")
                    child.attributes.removeNamedItem("class");
            }

            if (lastClonedNode) {
                if (secondDivElement.offsetHeight > halfHeight) { // TODO Decide whether to move the last element to left
                    if (!isListElement)
                        // FIXME: lastClonedNode may be a text node, and then we can't append and an error is trhown.
                        // the protection I am adding probably causes wrong behavior, but at least no exception
                        if (lastClonedNode.nodeType == Node.ELEMENT_NODE)
                            lastClonedNode.appendChild(clonedNode);
                        else
                            return;
                } else {
                    firstDivElement.done = true;
                    return;
                }
            } else
                firstDivElement.appendChild(clonedNode);

            if (!firstDivElement.done) {
                if (copyCompleteElement) {
                    columnizeFiner(firstDivElement, secondDivElement, halfHeight, nextChild ? nextChild.parentNode : null, lastClonedNode ? lastClonedNode : clonedNode);
                } else {
                    columnizeFiner(firstDivElement, secondDivElement, halfHeight, child, lastClonedNode ? lastClonedNode : clonedNode);
                }
            }
        }
    }

})();

document.addEventListener('DOMContentLoaded', columnize);

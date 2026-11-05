/**
 * @file core/i18n.js - translation markers and runtime string formatting.
 *
 * Purpose:
 *  - The permanent global translation markers _T, _TH, _TF and _TFH. The server side JS translator rewrites
 *    calls of these whose first argument is a string literal before the file is served (the literal is
 *    replaced by the translated text; a trailing quoted literal argument is the translation context and is
 *    removed). These definitions are the RUNTIME fallbacks (used when no translator is configured, and for
 *    rewritten calls that still have format arguments), identical to the old common.js:
 *      _T / _TH: format "{0}" style placeholders with the remaining arguments (no HTML escaping at runtime);
 *      _TF / _TFH: return the text unchanged.
 *  - `Sw.I18n`: the formatter (Format, FormatArgs), HTML arguments (ArgAsHtml) and the page language.
 *
 * Placeholder syntax: {n} is replaced by argument n; prefixes: {_n} lower-cases, {^n} upper-cases, {$n}
 * lower-cases unless the placeholder is at position 0 of the format. Undefined arguments leave the placeholder
 * as is; function arguments (see ArgAsHtml) are called with (n, caseFormatter, position).
 *
 * Load order: after core/kernel.js (core/base.js in sw/core.js). Dependencies: none at load time.
 * Globals defined: _T, _TH, _TF and _TFH as function declarations (as in the old common.js), Sw.I18n.
 * Rule: never write one of these marker names directly followed by a parenthesis anywhere else (not even in
 * comments or strings); only real translation calls with a literal first argument.
 */

/**
 * Translation marker with formatting (runtime fallback). Use with a string literal, optional format arguments
 * and an optional trailing context literal.
 * @param {String} format The text (must be a string literal at the call site), may contain {0}.. placeholders.
 * @returns {String} The formatted text.
 */
function _T(format) {
    const args = Array.prototype.slice.call(arguments, 1);
    return Sw.I18n.FormatArgs(format, args);
}

/**
 * Translation marker with formatting, the translated text is made HTML safe by the server (runtime fallback:
 * same as the formatting marker, no escaping).
 * @param {String} format The text (must be a string literal at the call site), may contain {0}.. placeholders.
 * @returns {String} The formatted text.
 */
function _TH(format) {
    const args = Array.prototype.slice.call(arguments, 1);
    return Sw.I18n.FormatArgs(format, args);
}

/**
 * Translation marker without formatting (runtime fallback: returns the text).
 * @param {String} text The text (must be a string literal at the call site).
 * @param {String} translationContext Optional context for the translator (removed by the server).
 * @returns {String} The text.
 */
function _TF(text, translationContext) {
    return text;
}

/**
 * Translation marker without formatting, HTML safe after server translation (runtime fallback: returns the text).
 * @param {String} text The text (must be a string literal at the call site).
 * @param {String} translationContext Optional context for the translator (removed by the server).
 * @returns {String} The text.
 */
function _TFH(text, translationContext) {
    return text;
}

(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    //  Once per page: a load once bundle (sw/session.js, sw/core.js ...) does not wrap this file (its global functions stay plain
    //  global functions), so a second bundle of the page runs it again
    if (Sw.I18n)
        return;

    /** @type {Map<string, function(string, number):string>} Placeholder prefix -> case formatter (value, position). */
    const formatMaps = new Map([
        ['_', (val, index) => val.toLowerCase()],
        ['^', (val, index) => val.toUpperCase()],
        ['$', (val, index) => index <= 0 ? val : val.toLowerCase()],
    ]);

    /** Placeholder expression: {n}, {_n}, {^n}, {$n}. */
    const placeholderRx = /{[_^$]?(\d+)}/g;

    /**
     * Format a template with an argument array (see the file header for the placeholder syntax).
     * @param {string} format - The template.
     * @param {Array<*>} args - Arguments.
     * @returns {string|undefined} The formatted string (undefined if format is falsy).
     */
    function formatArgs(format, args) {
        if (!format)
            return undefined;
        return format.replace(placeholderRx, function (match, number, index) {
            let val = args[number];
            const vt = typeof val;
            if (vt === 'undefined')
                return match;
            let fmt = formatMaps.get(match.charAt(1));
            if (vt === 'function') {
                val = val(number, fmt, index);
                fmt = null;
            }
            val = "" + val;
            if (fmt)
                val = fmt(val, index);
            return val;
        });
    }

    /**
     * @namespace Sw.I18n - runtime string formatting and language information.
     */
    Sw.I18n = {

        /** @type {Map<string, function(string, number):string>} The placeholder case formatters. */
        FormatMaps: formatMaps,

        /** Format a template with an argument array (format, args). */
        FormatArgs: formatArgs,

        /**
         * Format a template with variable arguments: Format("Hi {0}", name).
         * @param {string} format - The template.
         * @param {...*} args - Arguments.
         * @returns {string|undefined}
         */
        Format: (format, ...args) => formatArgs(format, args),

        /**
         * Wrap a format argument in an HTML element (default "b"), e.g. to emphasize a value in a translated
         * HTML text. The value is inserted as text (HTML safe, line breaks become br).
         * @param {*} argValue - The value.
         * @param {string} [elementClass] - Class(es) for the wrapping element.
         * @param {string} [elementType="b"] - Element name.
         * @returns {function(string, function(string,number):string, number):string|undefined} A formatter argument
         * (undefined if argValue is undefined).
         */
        ArgAsHtml(argValue, elementClass, elementType) {
            if (typeof argValue === "undefined")
                return undefined;
            const v = "" + argValue;
            return (number, fmt, index) => {
                const e = document.createElement(elementType ?? "b");
                if (elementClass)
                    e.setAttribute("class", elementClass);
                e.innerText = fmt ? fmt(v, index) : v;
                return e.outerHTML;
            };
        },

        /**
         * The language of the served page: the html lang attribute (set by the server translator), else the
         * browser language.
         * @returns {string} Language code, e.g. "en" or "sv-SE".
         */
        Language: () => document.documentElement.getAttribute("lang") || navigator.language || "en",
    };
})();

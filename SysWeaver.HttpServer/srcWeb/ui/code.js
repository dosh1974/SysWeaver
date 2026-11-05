/**
 * @file ui/code.js - SysWeaver UI library (Sw), part 7 of 7 (optional, not in the sw/ui.js bundle): the code editor Sw.CodeEditor.
 * Port of the EffectWeaver code editor (ew-code.js). Load it on demand after sw/ui.js, ex: await Sw.Net.IncludeJs(null, "../ui/code.js").
 * Styles: ui/code.css (part of sw/ui.css). Documentation: ui/README.md.
 * 
 * 
 * A textarea (the real editor: input, selection, native undo / redo, IME, clipboard) with transparent text over a highlighted copy of the
 * visible lines (virtualized: only the lines in view are rendered, fine for large files). Monospace, no line wrapping.
 * All edits go through document.execCommand("insertText") so they are part of the native undo history (Ctrl+Z / Ctrl+Y).
 * 
 * Languages: "glsl" (GLSL ES + ShaderToy uniforms, EffectWeaver snippet names Ew... / EW_...), "md" (markdown), "json" (keys, strings, numbers, keywords; no completion of members), "text".
 * Keys: Tab / Shift+Tab (indent / outdent lines), Enter (keeps the indentation, after "{" one more), "}" outdents, Home (smart),
 *       Ctrl+/ (toggle line comments), Ctrl+D (duplicate), Ctrl+Shift+K (delete line), Alt+Up / Alt+Down (move lines),
 *       Ctrl+F (find), Ctrl+H (replace), F3 / Shift+F3 (next / previous), Ctrl+G (go to line), Ctrl+Space (complete), Escape (close).
 */

(() => {
    "use strict";
    const { el, Icon, Button } = Sw;

    /** Escape text for html (&, <, >). */
    const esc = s => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    /** Split a white space separated word list into a Set. */
    const words = s => new Set(s.split(/\s+/).filter(x => x));

    // ------------------------------------------------------------------ languages

    const GlslKeywords = words(`attribute const uniform varying layout centroid flat smooth noperspective break continue do for while switch case default
        if else in out inout invariant precise discard return struct true false lowp mediump highp precision`);
    const GlslTypes = words(`void bool int uint float double vec2 vec3 vec4 bvec2 bvec3 bvec4 ivec2 ivec3 ivec4 uvec2 uvec3 uvec4 dvec2 dvec3 dvec4
        mat2 mat3 mat4 mat2x2 mat2x3 mat2x4 mat3x2 mat3x3 mat3x4 mat4x2 mat4x3 mat4x4 sampler2D sampler3D samplerCube sampler2DShadow
        sampler2DArray isampler2D usampler2D`);
    const GlslFunctions = words(`radians degrees sin cos tan asin acos atan sinh cosh tanh asinh acosh atanh pow exp log exp2 log2 sqrt inversesqrt
        abs sign floor trunc round roundEven ceil fract mod modf min max clamp mix step smoothstep isnan isinf floatBitsToInt floatBitsToUint
        intBitsToFloat uintBitsToFloat packSnorm2x16 unpackSnorm2x16 packUnorm2x16 unpackUnorm2x16 packHalf2x16 unpackHalf2x16 length distance
        dot cross normalize faceforward reflect refract matrixCompMult outerProduct transpose determinant inverse lessThan lessThanEqual
        greaterThan greaterThanEqual equal notEqual any all not texture textureSize textureProj textureLod textureOffset texelFetch
        texelFetchOffset textureProjOffset textureLodOffset textureProjLod textureGrad textureGradOffset texture2D texture2DLod textureCube
        dFdx dFdy fwidth`);
    const GlslVars = words(`gl_FragCoord gl_FragColor gl_FrontFacing gl_PointCoord gl_FragDepth iResolution iTime iTimeDelta iFrame iFrameRate
        iMouse iDate iChannel0 iChannel1 iChannel2 iChannel3 iChannelResolution iChannelTime iSampleRate fragColor fragCoord`);
    const GlslDirectives = words("define undef if ifdef ifndef else elif endif error pragma extension version line");

    /** Reference of the GLSL ES 3.00 built ins: name → [signature(s) ("\n" separated), description]. genType = float / vec2 / vec3 / vec4 */
    const GlslDocs = {
        radians: ["genType radians(genType degrees)", "Degrees to radians (× π / 180)."],
        degrees: ["genType degrees(genType radians)", "Radians to degrees (× 180 / π)."],
        sin: ["genType sin(genType angle)", "Sine, angle in radians."],
        cos: ["genType cos(genType angle)", "Cosine, angle in radians."],
        tan: ["genType tan(genType angle)", "Tangent, angle in radians."],
        asin: ["genType asin(genType x)", "Arc sine in [-π/2, π/2], undefined for |x| > 1."],
        acos: ["genType acos(genType x)", "Arc cosine in [0, π], undefined for |x| > 1."],
        atan: ["genType atan(genType y, genType x)\ngenType atan(genType y_over_x)", "Arc tangent. Two arguments: the angle of the vector (x, y) in [-π, π] (undefined for x = y = 0)."],
        sinh: ["genType sinh(genType x)", "Hyperbolic sine."],
        cosh: ["genType cosh(genType x)", "Hyperbolic cosine."],
        tanh: ["genType tanh(genType x)", "Hyperbolic tangent, in (-1, 1): a soft clamp."],
        asinh: ["genType asinh(genType x)", "Inverse hyperbolic sine."],
        acosh: ["genType acosh(genType x)", "Inverse hyperbolic cosine (x ≥ 1)."],
        atanh: ["genType atanh(genType x)", "Inverse hyperbolic tangent (|x| < 1)."],
        pow: ["genType pow(genType x, genType y)", "x raised to y. Undefined for x < 0 (use abs(x)) and for x = 0, y ≤ 0."],
        exp: ["genType exp(genType x)", "e to the power of x."],
        log: ["genType log(genType x)", "Natural logarithm, x > 0."],
        exp2: ["genType exp2(genType x)", "2 to the power of x."],
        log2: ["genType log2(genType x)", "Base 2 logarithm, x > 0."],
        sqrt: ["genType sqrt(genType x)", "Square root, x ≥ 0."],
        inversesqrt: ["genType inversesqrt(genType x)", "1 / sqrt(x), x > 0."],
        abs: ["genType abs(genType x)\ngenIType abs(genIType x)", "Absolute value."],
        sign: ["genType sign(genType x)", "-1, 0 or 1."],
        floor: ["genType floor(genType x)", "Nearest integer ≤ x."],
        trunc: ["genType trunc(genType x)", "Nearest integer towards 0."],
        round: ["genType round(genType x)", "Nearest integer (halves: implementation defined)."],
        roundEven: ["genType roundEven(genType x)", "Nearest integer, halves to the even one."],
        ceil: ["genType ceil(genType x)", "Nearest integer ≥ x."],
        fract: ["genType fract(genType x)", "x - floor(x), in [0, 1)."],
        mod: ["genType mod(genType x, genType y)\ngenType mod(genType x, float y)", "x - y × floor(x / y): the result has the sign of y (unlike %)."],
        modf: ["genType modf(genType x, out genType i)", "Fractional part, the integer part goes to i."],
        min: ["genType min(genType x, genType y)\ngenType min(genType x, float y)", "The smaller value (per component)."],
        max: ["genType max(genType x, genType y)\ngenType max(genType x, float y)", "The larger value (per component)."],
        clamp: ["genType clamp(genType x, genType minVal, genType maxVal)\ngenType clamp(genType x, float minVal, float maxVal)", "min(max(x, minVal), maxVal)."],
        mix: ["genType mix(genType x, genType y, genType a)\ngenType mix(genType x, genType y, float a)\ngenType mix(genType x, genType y, genBType a)", "Linear blend x × (1 - a) + y × a. With bools: picks y where a is true."],
        step: ["genType step(genType edge, genType x)\ngenType step(float edge, genType x)", "0.0 if x < edge, else 1.0."],
        smoothstep: ["genType smoothstep(genType edge0, genType edge1, genType x)\ngenType smoothstep(float edge0, float edge1, genType x)", "Hermite 0 → 1 between edge0 and edge1: t × t × (3 - 2t), t = clamp((x - edge0) / (edge1 - edge0), 0, 1)."],
        isnan: ["genBType isnan(genType x)", "True for NaN (may be optimized away)."],
        isinf: ["genBType isinf(genType x)", "True for ±infinity."],
        floatBitsToInt: ["genIType floatBitsToInt(genType value)", "The bits of a float as an int."],
        floatBitsToUint: ["genUType floatBitsToUint(genType value)", "The bits of a float as a uint (hashing)."],
        intBitsToFloat: ["genType intBitsToFloat(genIType value)", "An int's bits as a float."],
        uintBitsToFloat: ["genType uintBitsToFloat(genUType value)", "A uint's bits as a float."],
        packHalf2x16: ["uint packHalf2x16(vec2 v)", "Two 16 bit floats in a uint."],
        unpackHalf2x16: ["vec2 unpackHalf2x16(uint v)", "Two 16 bit floats from a uint."],
        packUnorm2x16: ["uint packUnorm2x16(vec2 v)", "Two [0, 1] values as 16 bit integers in a uint."],
        unpackUnorm2x16: ["vec2 unpackUnorm2x16(uint p)", "The inverse of packUnorm2x16."],
        packSnorm2x16: ["uint packSnorm2x16(vec2 v)", "Two [-1, 1] values as 16 bit integers in a uint."],
        unpackSnorm2x16: ["vec2 unpackSnorm2x16(uint p)", "The inverse of packSnorm2x16."],
        length: ["float length(genType x)", "Length of a vector, sqrt(dot(x, x))."],
        distance: ["float distance(genType p0, genType p1)", "length(p0 - p1)."],
        dot: ["float dot(genType x, genType y)", "Dot product: sum of the products of the components."],
        cross: ["vec3 cross(vec3 x, vec3 y)", "Cross product (perpendicular to both, right handed)."],
        normalize: ["genType normalize(genType x)", "The vector with length 1 (undefined for 0)."],
        faceforward: ["genType faceforward(genType N, genType I, genType Nref)", "N if dot(Nref, I) < 0, else -N."],
        reflect: ["genType reflect(genType I, genType N)", "Reflection of I at the surface with normal N (normalized): I - 2 × dot(N, I) × N."],
        refract: ["genType refract(genType I, genType N, float eta)", "Refraction direction (I, N normalized, eta = ratio of indices), 0 for total internal reflection."],
        matrixCompMult: ["mat matrixCompMult(mat x, mat y)", "Component wise product (not the matrix product)."],
        outerProduct: ["matNxM outerProduct(vecM c, vecN r)", "c × transpose(r)."],
        transpose: ["mat transpose(mat m)", "The transposed matrix."],
        determinant: ["float determinant(mat m)", "Determinant of a square matrix."],
        inverse: ["mat inverse(mat m)", "Inverse matrix (undefined if singular). Costly: for rotations use transpose."],
        lessThan: ["bvec lessThan(vec x, vec y)", "Component wise x < y."],
        lessThanEqual: ["bvec lessThanEqual(vec x, vec y)", "Component wise x ≤ y."],
        greaterThan: ["bvec greaterThan(vec x, vec y)", "Component wise x > y."],
        greaterThanEqual: ["bvec greaterThanEqual(vec x, vec y)", "Component wise x ≥ y."],
        equal: ["bvec equal(vec x, vec y)", "Component wise x == y."],
        notEqual: ["bvec notEqual(vec x, vec y)", "Component wise x != y."],
        any: ["bool any(bvec x)", "True if any component is true."],
        all: ["bool all(bvec x)", "True if all components are true."],
        not: ["bvec not(bvec x)", "Component wise logical not."],
        texture: ["gvec4 texture(gsampler2D sampler, vec2 P [, float bias])", "Sample a texture (filtered, mip mapped)."],
        textureLod: ["gvec4 textureLod(gsampler2D sampler, vec2 P, float lod)", "Sample a texture at an explicit mip level."],
        textureSize: ["ivec2 textureSize(gsampler2D sampler, int lod)", "Size of a mip level."],
        texelFetch: ["gvec4 texelFetch(gsampler2D sampler, ivec2 P, int lod)", "Read one texel (integer coordinates, no filtering)."],
        textureGrad: ["gvec4 textureGrad(gsampler2D sampler, vec2 P, vec2 dPdx, vec2 dPdy)", "Sample with explicit derivatives."],
        texture2D: ["vec4 texture2D(sampler2D sampler, vec2 coord)", "GLSL ES 1.00 texture sampling (texture() in 3.00)."],
        dFdx: ["genType dFdx(genType p)", "Screen space derivative in x (difference to the neighbor pixel)."],
        dFdy: ["genType dFdy(genType p)", "Screen space derivative in y."],
        fwidth: ["genType fwidth(genType p)", "abs(dFdx(p)) + abs(dFdy(p)): the change per pixel, for anti aliasing (smoothstep(-w, w, d))."],
        // Types
        void: ["void", "No value (function return type)."],
        bool: ["bool", "true / false."],
        int: ["int", "32 bit signed integer (highp)."],
        uint: ["uint", "32 bit unsigned integer (GLSL ES 3.00), literals with a u suffix."],
        float: ["float", "32 bit floating point (highp)."],
        vec2: ["vec2(x, y)", "2 floats. Components .xy / .rg / .st, swizzles like .yx."],
        vec3: ["vec3(x, y, z)", "3 floats (positions, directions, rgb colors). .xyz / .rgb."],
        vec4: ["vec4(x, y, z, w)", "4 floats (rgba colors, homogeneous positions). .xyzw / .rgba."],
        ivec2: ["ivec2", "2 ints."], ivec3: ["ivec3", "3 ints."], ivec4: ["ivec4", "4 ints."],
        uvec2: ["uvec2", "2 uints."], uvec3: ["uvec3", "3 uints (hashing)."], uvec4: ["uvec4", "4 uints."],
        bvec2: ["bvec2", "2 bools."], bvec3: ["bvec3", "3 bools."], bvec4: ["bvec4", "4 bools."],
        mat2: ["mat2(a, b, c, d)", "2×2 matrix, column major: mat2(c0.x, c0.y, c1.x, c1.y). Rotation: mat2(c, s, -s, c)."],
        mat3: ["mat3", "3×3 matrix (column major)."],
        mat4: ["mat4", "4×4 matrix (column major)."],
        sampler2D: ["sampler2D", "A 2D texture (iChannel0..3 on ShaderToy)."],
        // Keywords
        const: ["const", "A compile time constant (or a read only parameter)."],
        uniform: ["uniform", "A value set by the application, the same for all pixels."],
        in: ["in", "Parameter qualifier: input (the default). GLSL ES 3.00 globals: inputs from the previous stage."],
        out: ["out", "Parameter qualifier: written by the function (initial value undefined). Globals: the shader's outputs."],
        inout: ["inout", "Parameter qualifier: read and written by the function."],
        precision: ["precision highp float;", "Default precision of a type."],
        highp: ["highp", "High precision (32 bit float)."],
        mediump: ["mediump", "Medium precision (at least 16 bit float), faster on some mobile GPUs."],
        lowp: ["lowp", "Low precision (at least 10 bit, colors)."],
        discard: ["discard", "Throws the pixel away (no output)."],
        struct: ["struct Name { ... };", "A user defined type."],
        return: ["return", "Leave the function (with a value)."],
        break: ["break", "Leave a loop / switch."],
        continue: ["continue", "Next iteration of a loop."],
        for: ["for (init; condition; step)", "A loop. GLSL ES 1.00: constant bounds only."],
        while: ["while (condition)", "A loop (GLSL ES 3.00)."],
        if: ["if (condition)", "Branch. Divergent branches cost both sides on GPUs."],
        true: ["true", "bool true."], false: ["false", "bool false."],
        // ShaderToy uniforms
        iResolution: ["uniform vec3 iResolution", "Viewport size in pixels (z: pixel aspect, 1)."],
        iTime: ["uniform float iTime", "Effect time in seconds (speed / pause / seek: Controls tab)."],
        iTimeDelta: ["uniform float iTimeDelta", "Duration of the last frame in seconds (× speed)."],
        iFrame: ["uniform int iFrame", "Frame number since the effect started."],
        iFrameRate: ["uniform float iFrameRate", "Frames per second."],
        iMouse: ["uniform vec4 iMouse", "Effect Weaver: four controller values in [0, 1] (not pixels like ShaderToy), named by \"// iMouse.x = Name | Description | Default\" comments."],
        iDate: ["uniform vec4 iDate", "Year, month (0 based), day, seconds of the day."],
        iChannel0: ["uniform sampler2D iChannel0", "Input texture (not supplied here)."],
        fragCoord: ["in vec2 fragCoord", "mainImage: the pixel position (pixel centers: x + 0.5)."],
        fragColor: ["out vec4 fragColor", "mainImage: the output color."],
        gl_FragCoord: ["in vec4 gl_FragCoord", "Window position of the pixel."],
        gl_FragColor: ["vec4 gl_FragColor", "GLSL ES 1.00 output color."],
        mainImage: ["void mainImage(out vec4 fragColor, in vec2 fragCoord)", "The ShaderToy entry point, called for every pixel."],
    };

    const GlslTokenRx = /(\/\/.*)|(\/\*)|(\d+\.\d*(?:[eE][-+]?\d+)?[fF]?|\.\d+(?:[eE][-+]?\d+)?[fF]?|\d+[eE][-+]?\d+[fF]?|0[xX][0-9a-fA-F]+[uU]?|\d+[uU]?)|([A-Za-z_]\w*)|("(?:[^"\\]|\\.)*"?)|(\s+)|([^\w\s])/y;

    const Glsl = {
        Comment: "//",
        /** The snippet names (Set / Map of the known Ew… functions and EW_… macros), null = all count as known. Call Refresh() of the editors after changing it */
        Known: null,
        /** The reference of a built in: { Sigs: [signatures], Desc, Kind: "function" | "type" | "keyword" | "variable" }, null if unknown */
        Doc(word) {
            const d = Object.prototype.hasOwnProperty.call(GlslDocs, word) ? GlslDocs[word] : null;
            if (!d)
                return null;
            const kind = GlslFunctions.has(word) ? "function" : GlslTypes.has(word) ? "type" : GlslKeywords.has(word) ? "keyword" : "variable";
            return { Sigs: d[0].split("\n"), Desc: d[1], Kind: kind };
        },
        /** End state of a line (1 = inside a block comment) */
        Scan(line, state) {
            let i = 0;
            const n = line.length;
            while (i < n) {
                if (state === 1) {
                    const e = line.indexOf("*/", i);
                    if (e < 0)
                        return 1;
                    state = 0;
                    i = e + 2;
                    continue;
                }
                const c = line.indexOf("/*", i), l = line.indexOf("//", i);
                if (c < 0 || (l >= 0 && l < c))
                    return 0;
                state = 1;
                i = c + 2;
            }
            return state;
        },
        /** Highlight one GLSL line: returns the html of the tokens (comment state from the previous line). */
        Html(line, state) {
            let out = "", i = 0;
            const n = line.length;
            if (state === 1) {
                const e = line.indexOf("*/");
                if (e < 0)
                    return '<span class="c">' + esc(line) + "</span>";
                out += '<span class="c">' + esc(line.slice(0, e + 2)) + "</span>";
                i = e + 2;
            }
            //  Preprocessor directive name
            const pm = /^(\s*)(#\s*\w*)/.exec(line);
            if (pm && i === 0) {
                out += pm[1] + '<span class="p">' + esc(pm[2]) + "</span>";
                i = pm[0].length;
            }
            const rx = GlslTokenRx;
            while (i < n) {
                rx.lastIndex = i;
                const m = rx.exec(line);
                if (!m) {
                    out += esc(line[i]);
                    ++i;
                    continue;
                }
                const s = m[0];
                if (m[1])
                    out += '<span class="c">' + esc(s) + "</span>";
                else if (m[2]) {
                    const e = line.indexOf("*/", i + 2);
                    const end = e < 0 ? n : e + 2;
                    out += '<span class="c">' + esc(line.slice(i, end)) + "</span>";
                    i = end;
                    continue;
                }
                else if (m[3])
                    out += '<span class="n">' + s + "</span>";
                else if (m[4]) {
                    let cls = GlslKeywords.has(s) ? "k" : GlslTypes.has(s) ? "t" : GlslFunctions.has(s) ? "f" : GlslVars.has(s) ? "v" : null;
                    if (!cls && /^Ew[A-Z]|^EW_[A-Z0-9_]/.test(s)) {
                        //  Snippets: functions "s", EW_ constants "sc", names the library doesn't know "sx" (Glsl.Known, set by the page)
                        const known = Glsl.Known;
                        cls = known && !known.has(s) ? "sx" : s.startsWith("EW_") ? "sc" : "s";
                    }
                    else if (!cls && /^\s*\(/.test(line.slice(i + s.length)))
                        cls = "u";
                    else if (!cls && /^[A-Z][A-Z0-9_]+$/.test(s))
                        cls = "m";
                    out += cls ? '<span class="' + cls + '">' + s + "</span>" : s;
                }
                else if (m[5])
                    out += '<span class="q">' + esc(s) + "</span>";
                else if (m[6])
                    out += s;
                else
                    out += '<span class="o">' + esc(s) + "</span>";
                i += s.length;
            }
            return out;
        },
        Words: [...GlslKeywords, ...GlslTypes, ...GlslFunctions, ...GlslVars].map(x => ({
            Text: x, Kind: GlslKeywords.has(x) ? "keyword" : GlslTypes.has(x) ? "type" : GlslFunctions.has(x) ? "function" : "variable",
        })).concat([...GlslDirectives].map(x => ({ Text: x, Kind: "directive" }))),
    };

    /** Markdown: states 0 = text, 1 = inside a code fence, 2 = inside an html comment */
    const Markdown = {
        Comment: null,
        /** Markdown line scanner: the state after a line (code fence / html comment). */
        Scan(line, state) {
            if (state === 2)
                return line.includes("-->") ? Markdown.Scan(line.slice(line.indexOf("-->") + 3), 0) : 2;
            if (/^\s*(```|~~~)/.test(line))
                return state === 1 ? 0 : 1;
            if (state === 1)
                return 1;
            const c = line.lastIndexOf("<!--");
            return c >= 0 && line.indexOf("-->", c) < 0 ? 2 : 0;
        },
        /** Highlight the inline markdown of a text (emphasis, code, links). */
        Inline(s) {
            let out = "";
            const rx = /(`[^`]*`)|(\*\*[^*]+\*\*|__[^_]+__)|(\*[^*\s][^*]*\*|_[^_\s][^_]*_)|(!?\[[^\]]*\]\([^)]*\))|(<!--.*?(?:-->|$))|(\|)|([^`*_!\[<|]+|.)/g;
            let m;
            while ((m = rx.exec(s))) {
                const t = esc(m[0]);
                out += m[1] ? '<span class="q">' + t + "</span>" : m[2] ? '<span class="b">' + t + "</span>" : m[3] ? '<span class="i">' + t + "</span>" :
                    m[4] ? '<span class="l">' + t + "</span>" : m[5] ? '<span class="c">' + t + "</span>" : m[6] ? '<span class="o">' + t + "</span>" : t;
            }
            return out;
        },
        /** Highlight one markdown line: returns its html (headings, lists, fences, comments). */
        Html(line, state) {
            if (state === 2) {
                const e = line.indexOf("-->");
                if (e < 0)
                    return '<span class="c">' + esc(line) + "</span>";
                return '<span class="c">' + esc(line.slice(0, e + 3)) + "</span>" + Markdown.Inline(line.slice(e + 3));
            }
            if (/^\s*(```|~~~)/.test(line))
                return '<span class="p">' + esc(line) + "</span>";
            if (state === 1)
                return '<span class="q">' + esc(line) + "</span>";
            if (/^#{1,6}\s/.test(line))
                return '<span class="h">' + esc(line) + "</span>";
            const lm = /^(\s*(?:[-*+]|\d+\.|>)\s)/.exec(line);
            if (lm)
                return '<span class="k">' + esc(lm[1]) + "</span>" + Markdown.Inline(line.slice(lm[1].length));
            return Markdown.Inline(line);
        },
        Words: [],
    };

    const Text = { Comment: null, Scan: () => 0, Html: line => esc(line), Words: [] };

    /** JSON (no multi line state: json strings can't span lines): keys v, strings q, numbers n, true / false / null k, punctuation o. */
    const JsonTokenRx = /("(?:[^"\\]|\\.)*"?)(\s*:)?|(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)|(true|false|null)\b|(\s+)|([{}\[\],:])|([^\s{}\[\],:"]+)/y;
    const Json = {
        Comment: null,
        Scan: () => 0,
        /** Highlight one json line: returns its html. */
        Html(line) {
            let out = "", i = 0;
            const n = line.length;
            const rx = JsonTokenRx;
            while (i < n) {
                rx.lastIndex = i;
                const m = rx.exec(line);
                if (!m || !m[0]) {
                    out += esc(line[i]);
                    ++i;
                    continue;
                }
                const s = m[0];
                if (m[1])
                    out += '<span class="' + (m[2] ? "v" : "q") + '">' + esc(m[1]) + "</span>" + (m[2] ? '<span class="o">' + esc(m[2]) + "</span>" : "");
                else if (m[3])
                    out += '<span class="n">' + s + "</span>";
                else if (m[4])
                    out += '<span class="k">' + s + "</span>";
                else if (m[5])
                    out += s;
                else if (m[6])
                    out += '<span class="o">' + esc(s) + "</span>";
                else
                    out += '<span class="m">' + esc(s) + "</span>";
                i += s.length;
            }
            return out;
        },
        Words: [{ Text: "true", Kind: "keyword" }, { Text: "false", Kind: "keyword" }, { Text: "null", Kind: "keyword" }],
    };

    const Languages = { glsl: Glsl, md: Markdown, text: Text, json: Json };

    const IdentRx = /[A-Za-z0-9_]/;

    // ------------------------------------------------------------------ editor

    /**
     * new Sw.CodeEditor({ Value, Language: "glsl" | "md" | "json" | "text", ReadOnly, TabSize (4), AutoComplete (true: suggest while typing),
     *                     Completions() (extra items: [{ Text, Kind, Detail }], e.g. snippet functions), OnChange(editor), OnCursor(editor), Placeholder })
     * Get(), Set(text) (new document, clears the undo history), Edit(text) (replace everything as one undoable edit, the cursor stays on its line),
     * Insert(text, pos?) (undoable, at the cursor / a position), Replace(start, end, text, selStart?, selEnd?) (undoable),
     * Cursor() → { Line, Col (1 based), Pos, Sel (selected text) }, GoTo(line, col, select?), Focus(), SetReadOnly(bool),
     * SetMarkers([{ Line, Kind: "error" | "warning", Text }]) (gutter marks, line highlight, the message at the end of the line),
     * Find(replace?) (opens the find bar), GoToLine(), Refresh() (after font / size changes), Destroy() (removes the document listeners),
     * OnComplete(item, editor) (option: called after a completion was inserted).
     * Hover(info, editor) (option): the content of the hover popup for the identifier under the mouse (info: { Word, Start, End, Line, Col }),
     * an element, a promise of one, or null (no popup). The language reference: Sw.CodeLanguages.glsl.Doc(word).
     * SetDots([{ Line, Kind, Title }]) (clickable gutter dots, ex: breakpoints: option OnDot(line, dot, event)),
     * SetSpans([{ Line, Col (1 based), Len, Kind }]) (highlighted text ranges, class sw-code-span sw-<Kind>),
     * OnAltClick(info, editor) (option: Alt + click in the text, info = the word under the mouse { Word, Start, End, Line, Col }, or the
     * selection (Sel: true) when the click is inside it), Undoable() (false while the editor is hidden: edits of a hidden editor, ex: an
     * inactive tab, are applied directly, without undo, since a hidden textarea can't take the focus).
     */
    class CodeEditor {
        constructor(o) {
            const t = this;
            o = o || {};
            if (Sw.Work && typeof Sw.Work.CheckOptions === "function")
                Sw.Work.CheckOptions("Sw.CodeEditor", o);
            t.O = o;
            t.Lang = Languages[o.Language] || Glsl;
            t.TabSize = o.TabSize || 4;
            t.Lines = [""];
            t.Starts = [0];
            t.States = [0];
            t.Cache = new Map();
            t.Markers = new Map();
            t.Dots = new Map();
            t.Spans = [];
            t.FindState = null;
            t.Complete = null;
            t.View = { Top: 0, Left: 0, SelStart: 0, SelEnd: 0 };
            t.GutterInner = el("div.sw-code-gutter-inner");
            t.Gutter = el("div.sw-code-gutter", null, t.GutterInner);
            t.Layer = el("div.sw-code-layer");
            t.Input = el("textarea.sw-code-input", {
                spellcheck: false, readOnly: !!o.ReadOnly, placeholder: o.Placeholder || "",
                attrs: { wrap: "off", autocapitalize: "off", autocomplete: "off", autocorrect: "off", "aria-label": _TF("Code", "Accessible name of a code editor") },
            });
            t.Measure = el("span.sw-code-measure", { text: "x".repeat(100) });
            t.Main = el("div.sw-code-main", null, t.Layer, t.Input, t.Measure);
            t.E = el("div.sw-code", { class: "sw-code-" + (o.Language || "glsl") }, t.Gutter, t.Main);
            if (o.ReadOnly)
                t.E.classList.add("sw-code-readonly");
            t.Input.addEventListener("input", e => t.OnInput(e));
            t.Input.addEventListener("scroll", () => {
                t.HideHover();
                if (t.Input.clientHeight > 0) {
                    t.View.Top = t.Input.scrollTop;
                    t.View.Left = t.Input.scrollLeft;
                }
                if (t.Complete)
                    t.RenderComplete();
                t.Render();
            });
            t.Input.addEventListener("keydown", e => {
                t.HideHover();
                t.OnKey(e);
            });
            //  Hover information (option Hover)
            t.Input.addEventListener("mousemove", e => t.HoverMove(e));
            t.Input.addEventListener("mouseleave", () => t.HoverLeave());
            t.Input.addEventListener("mousedown", e => {
                t.HideHover();
                //  Alt + click: the identifier (or the selection) under the mouse (option OnAltClick)
                if (e.altKey && e.button === 0 && o.OnAltClick) {
                    const p = t.PosAt(e.clientX, e.clientY);
                    if (!p)
                        return;
                    e.preventDefault();
                    const s = t.Input.selectionStart, en = t.Input.selectionEnd;
                    const info = p.Pos >= s && p.Pos < en && en > s
                        ? { Word: t.Input.value.slice(s, en), Start: s, End: en, Line: t.LineOf(s) + 1, Col: s - t.Starts[t.LineOf(s)] + 1, Sel: true }
                        : t.WordAt(p.Pos);
                    if (info && !t.InComment(info.Start))
                        o.OnAltClick(info, t);
                }
            });
            const cursor = () => {
                if (document.activeElement !== t.Input)
                    return;
                t.View.SelStart = t.Input.selectionStart;
                t.View.SelEnd = t.Input.selectionEnd;
                t.Render();
                o.OnCursor && o.OnCursor(t);
            };
            t.OnSelection = cursor;
            document.addEventListener("selectionchange", cursor);
            t.Input.addEventListener("blur", () => setTimeout(() => {
                if (!t.E.contains(document.activeElement))
                    t.CloseComplete();
            }, 150));
            t.Gutter.addEventListener("pointerdown", e => {
                const line = t.LineAtY(e.clientY);
                if (line < 0)
                    return;
                e.preventDefault();
                const dot = t.Dots.get(line + 1);
                if (dot && o.OnDot) {
                    o.OnDot(line + 1, dot, e);
                    return;
                }
                const m = t.Markers.get(line + 1);
                t.Input.focus({ preventScroll: true });
                t.Input.setSelectionRange(t.Starts[line], line + 1 < t.Starts.length ? t.Starts[line + 1] : t.Input.value.length);
                if (m)
                    Sw.Toast(_T("Line {0}: {1}", line + 1, m.Text, "A message about a line of source code, {0} is the line number, {1} the message"), { Kind: m.Kind === "error" ? "error" : undefined });
            });
            //  Visible again (tab switch): restore the scroll position, sizes may have changed
            t.Resize = new ResizeObserver(() => {
                if (t.Input.clientHeight <= 0)
                    return;
                if (t.Input.scrollTop !== t.View.Top || t.Input.scrollLeft !== t.View.Left) {
                    t.Input.scrollTop = t.View.Top;
                    t.Input.scrollLeft = t.View.Left;
                }
                t.Refresh();
            });
            t.Resize.observe(t.Main);
            const themeOff = Sw.Theme && Sw.Theme.OnChange ? Sw.Theme.OnChange(() => t.Destroyed || setTimeout(() => t.Refresh(), 0)) : null;
            t.ThemeOff = typeof themeOff === "function" ? themeOff : null;
            t.Set(o.Value || "");
        }

        // -------------------------------------------------------------- document

        Get() {
            return this.Input.value;
        }

        /** A new document (clears the undo history) */
        Set(text) {
            const t = this;
            t.Input.value = (text || "").replace(/\r\n?/g, "\n");
            t.Input.setSelectionRange(0, 0);
            t.Input.scrollTop = t.Input.scrollLeft = 0;
            t.View = { Top: 0, Left: 0, SelStart: 0, SelEnd: 0 };
            t.Markers.clear();
            t.Update(true);
        }

        /** Remove the document listeners (when the editor is thrown away) */
        Destroy() {
            this.Destroyed = true;
            document.removeEventListener("selectionchange", this.OnSelection);
            this.Resize.disconnect();
            this.CloseComplete();
            this.HideHover && this.HideHover();
            if (this.ThemeOff)
                this.ThemeOff();
        }

        /** Make the editor read only (or editable). @param {boolean} ro Read only. */
        SetReadOnly(ro) {
            this.Input.readOnly = !!ro;
            this.E.classList.toggle("sw-code-readonly", !!ro);
        }

        /** Line starts / lines / comment states after a change (states from the first changed line on) */
        Update(all) {
            const t = this;
            const v = t.Input.value;
            const lines = v.split("\n");
            let first = 0;
            if (!all) {
                const old = t.Lines;
                const n = Math.min(old.length, lines.length);
                while (first < n && old[first] === lines[first])
                    ++first;
            }
            t.Lines = lines;
            const starts = new Array(lines.length);
            let p = 0;
            for (let i = 0; i < lines.length; ++i) {
                starts[i] = p;
                p += lines[i].length + 1;
            }
            t.Starts = starts;
            const states = t.States.slice(0, first + 1);
            states.length = lines.length;
            if (first === 0)
                states[0] = 0;
            for (let i = first; i < lines.length - 1; ++i)
                states[i + 1] = t.Lang.Scan(lines[i], states[i] || 0);
            t.States = states;
            if (t.Cache.size > 6000)
                t.Cache.clear();
            if (t.FindState)
                t.RunFind(false);
            t.Render();
        }

        /** Input handler: update the lines, notify OnChange, auto completion. */
        OnInput(e) {
            const t = this;
            t.HideHover();
            t.Update(false);
            t.View.SelStart = t.Input.selectionStart;
            t.View.SelEnd = t.Input.selectionEnd;
            t.O.OnChange && t.O.OnChange(t);
            //  Suggestions while typing an identifier
            if (t.O.AutoComplete !== false && e && e.inputType === "insertText" && e.data && IdentRx.test(e.data) && !t.Input.readOnly) {
                const w = t.WordBefore();
                if (w.Text.length >= 2 && !/^\d/.test(w.Text) && !t.InComment(t.Input.selectionStart))
                    t.ShowComplete(false);
                else
                    t.CloseComplete();
            }
            else if (t.Complete && e && e.inputType && e.inputType.startsWith("delete"))
                t.WordBefore().Text.length >= 1 ? t.ShowComplete(false) : t.CloseComplete();
            else
                t.CloseComplete();
        }

        /** Line index of a position (binary search) */
        LineOf(pos) {
            const s = this.Starts;
            let lo = 0, hi = s.length - 1;
            while (lo < hi) {
                const mid = (lo + hi + 1) >> 1;
                if (s[mid] <= pos)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }

        /** Visual column (tabs expanded) of a column in a line */
        VCol(line, col) {
            const s = this.Lines[line] || "";
            let v = 0;
            for (let i = 0; i < col && i < s.length; ++i)
                v = s[i] === "\t" ? v + this.TabSize - (v % this.TabSize) : v + 1;
            return v;
        }

        /** The cursor position. @returns {{Line: number, Col: number, Pos: number, Sel: string}} 1 based line / column, offset, selected text. */
        Cursor() {
            const t = this;
            const pos = t.Input.selectionStart;
            const line = t.LineOf(pos);
            return { Line: line + 1, Col: pos - t.Starts[line] + 1, Pos: pos, Sel: t.Input.value.slice(pos, t.Input.selectionEnd) };
        }

        /** Inside a comment (glsl)? */
        InComment(pos) {
            const t = this;
            const line = t.LineOf(pos);
            const before = t.Lines[line].slice(0, pos - t.Starts[line]);
            return t.Lang === Glsl && (t.Lang.Scan(before, t.States[line] || 0) === 1 || before.includes("//"));
        }

        // -------------------------------------------------------------- editing (undoable)

        /** Replace a range (undoable), then select selStart..selEnd (default: after the text) */
        Replace(start, end, text, selStart, selEnd) {
            const t = this;
            if (t.Input.readOnly)
                return;
            const ta = t.Input;
            let ok = false;
            //  execCommand edits the focused element: a hidden editor (ex: of an inactive tab) can't take the focus, it is changed directly (not undoable)
            if (t.Undoable()) {
                ta.focus({ preventScroll: true });
                ta.setSelectionRange(start, end);
                try {
                    ok = text.length > 0 ? document.execCommand("insertText", false, text) : (start === end || document.execCommand("delete", false));
                }
                catch {
                    ok = false;
                }
            }
            if (!ok) {
                ta.setRangeText(text, start, end, "end");
                t.OnInput(null);
            }
            if (selStart !== undefined)
                ta.setSelectionRange(selStart, selEnd ?? selStart);
            t.ScrollToPos(ta.selectionEnd);
        }

        /** True if edits are undoable now (the editor is displayed; a hidden one is changed directly). @returns {boolean} */
        Undoable() {
            return this.Input.getClientRects().length > 0;
        }

        /** Insert text (undoable) at a position or the cursor. @param {string} text The text. @param {number} [pos] Offset. */
        Insert(text, pos) {
            const t = this;
            const s = pos ?? t.Input.selectionStart, e = pos ?? t.Input.selectionEnd;
            t.Replace(s, e, text);
        }

        /** Replace everything (one undoable edit), the cursor stays on its line */
        Edit(text) {
            const t = this;
            text = (text || "").replace(/\r\n?/g, "\n");
            if (text === t.Input.value)
                return;
            const c = t.Cursor();
            const top = t.Input.scrollTop;
            t.Replace(0, t.Input.value.length, text);
            const line = Math.min(c.Line - 1, t.Lines.length - 1);
            const pos = t.Starts[line] + Math.min(c.Col - 1, t.Lines[line].length);
            t.Input.setSelectionRange(pos, pos);
            t.Input.scrollTop = top;
            t.Render();
        }

        /** Focus the editor. */
        Focus() {
            this.Input.focus({ preventScroll: true });
        }

        /** Select / place the cursor at a line and column (1 based) and scroll there */
        GoTo(line, col, select) {
            const t = this;
            const l = Math.max(0, Math.min(t.Lines.length - 1, (line || 1) - 1));
            const pos = t.Starts[l] + Math.max(0, Math.min(t.Lines[l].length, (col || 1) - 1));
            t.Input.focus({ preventScroll: true });
            if (select)
                t.Input.setSelectionRange(t.Starts[l], t.Starts[l] + t.Lines[l].length);
            else
                t.Input.setSelectionRange(pos, pos);
            t.ScrollToPos(pos, true);
        }

        /** Ask for a line number and move the cursor there. @returns {Promise<void>} */
        async GoToLine() {
            const t = this;
            const v = await Sw.Dialog.Prompt(_TF("Go to line", "Title of the dialog that moves the cursor of a code editor to a line"), String(t.Cursor().Line), "1.." + t.Lines.length);
            const n = parseInt(v, 10);
            if (n > 0)
                t.GoTo(n, 1);
            else
                t.Focus();
        }

        // -------------------------------------------------------------- keys

        LineRange() {
            const t = this;
            const s = t.Input.selectionStart, e = t.Input.selectionEnd;
            const first = t.LineOf(s);
            let last = t.LineOf(e);
            if (last > first && e === t.Starts[last])
                --last;
            return { First: first, Last: last, Start: t.Starts[first], End: t.Starts[last] + t.Lines[last].length };
        }

        /** Replace whole lines first..last with new lines, select them (or keep a caret at the same column if keepCaret) */
        SetLines(r, lines, selectAll) {
            const t = this;
            const text = lines.join("\n");
            if (selectAll)
                t.Replace(r.Start, r.End, text, r.Start, r.Start + text.length);
            else
                t.Replace(r.Start, r.End, text);
        }

        /** Key handler: indentation, comments, line moves, find, completion and the other editor keys. */
        OnKey(e) {
            const t = this;
            const ta = t.Input;
            const ctrl = e.ctrlKey || e.metaKey;
            //  Completion list open
            if (t.Complete) {
                const c = t.Complete;
                if (e.key === "ArrowDown" || e.key === "ArrowUp" || e.key === "PageDown" || e.key === "PageUp") {
                    e.preventDefault();
                    const step = e.key === "ArrowDown" ? 1 : e.key === "ArrowUp" ? -1 : e.key === "PageDown" ? 8 : -8;
                    c.Index = Math.max(0, Math.min(c.Items.length - 1, c.Index + step));
                    t.RenderComplete();
                    return;
                }
                if (e.key === "Enter" || e.key === "Tab") {
                    e.preventDefault();
                    t.AcceptComplete();
                    return;
                }
                if (e.key === "Escape") {
                    e.preventDefault();
                    e.stopPropagation();
                    t.CloseComplete();
                    return;
                }
            }
            if (ctrl && !e.altKey) {
                switch (e.key.toLowerCase()) {
                    case "f":
                        if (e.shiftKey)
                            return;
                        e.preventDefault();
                        t.Find(false);
                        return;
                    case "h":
                        e.preventDefault();
                        t.Find(true);
                        return;
                    case "g":
                        e.preventDefault();
                        t.GoToLine();
                        return;
                    case " ":
                        e.preventDefault();
                        t.ShowComplete(true);
                        return;
                }
                if (e.code === "Slash" && t.Lang.Comment && !ta.readOnly) {
                    e.preventDefault();
                    t.ToggleComment();
                    return;
                }
                if (e.key.toLowerCase() === "d" && !e.shiftKey && !ta.readOnly) {
                    e.preventDefault();
                    t.Duplicate();
                    return;
                }
                if (e.key.toLowerCase() === "k" && e.shiftKey && !ta.readOnly) {
                    e.preventDefault();
                    const r = t.LineRange();
                    const end = r.Last + 1 < t.Lines.length ? t.Starts[r.Last + 1] : t.Input.value.length;
                    const start = r.Last + 1 < t.Lines.length || r.First === 0 ? r.Start : r.Start - 1;
                    t.Replace(start, end, "", start, start);
                    return;
                }
            }
            if (e.key === "F3") {
                e.preventDefault();
                if (!t.FindState)
                    t.Find(false);
                else
                    t.FindNext(e.shiftKey ? -1 : 1);
                return;
            }
            if (e.key === "Escape" && t.FindBar && !t.FindBar.hidden) {
                e.preventDefault();
                t.CloseFind();
                return;
            }
            if (ta.readOnly)
                return;
            if (e.altKey && !ctrl && (e.key === "ArrowUp" || e.key === "ArrowDown")) {
                e.preventDefault();
                t.MoveLines(e.key === "ArrowUp" ? -1 : 1);
                return;
            }
            if (ctrl || e.altKey)
                return;
            const v = ta.value, s = ta.selectionStart, end = ta.selectionEnd;
            const line = t.LineOf(s);
            const ls = t.Starts[line];
            const before = v.slice(ls, s);
            switch (e.key) {
                case "Tab": {
                    e.preventDefault();
                    const r = t.LineRange();
                    if (!e.shiftKey && r.First === r.Last && !(s === r.Start && end === r.End && end > s)) {
                        const vc = t.VCol(line, s - ls);
                        t.Replace(s, end, " ".repeat(t.TabSize - (vc % t.TabSize)));
                        return;
                    }
                    const lines = t.Lines.slice(r.First, r.Last + 1).map(l => {
                        if (!e.shiftKey)
                            return l.length ? " ".repeat(t.TabSize) + l : l;
                        const m = /^( {1,4}|\t)/.exec(l);
                        return m ? l.slice(m[0].length) : l;
                    });
                    t.SetLines(r, lines, true);
                    return;
                }
                case "Enter": {
                    e.preventDefault();
                    const indent = /^[ \t]*/.exec(before)[0];
                    const opens = /[{([]\s*$/.test(before);
                    const extra = opens ? " ".repeat(t.TabSize) : "";
                    const after = v.slice(end, t.Starts[t.LineOf(end)] + t.Lines[t.LineOf(end)].length);
                    if (opens && /^[})\]]/.test(after)) {
                        //  Between brackets: the closing one goes to its own line
                        const first = "\n" + indent + extra;
                        t.Replace(s, end, first + "\n" + indent, s + first.length, s + first.length);
                        return;
                    }
                    t.Replace(s, end, "\n" + indent + extra);
                    return;
                }
                case "}": {
                    if (s === end && /^[ \t]+$/.test(before)) {
                        e.preventDefault();
                        const indent = before.replace(/\t/g, " ".repeat(t.TabSize));
                        const n = Math.max(0, indent.length - t.TabSize);
                        t.Replace(ls, s, " ".repeat(n) + "}");
                    }
                    return;
                }
                case "Home": {
                    if (e.shiftKey)
                        return;
                    e.preventDefault();
                    const ind = /^[ \t]*/.exec(t.Lines[line])[0].length;
                    const p = s - ls === ind ? ls : ls + ind;
                    ta.setSelectionRange(p, p);
                    return;
                }
            }
        }

        /** Toggle line comments of the selected lines. */
        ToggleComment() {
            const t = this;
            const c = t.Lang.Comment;
            const r = t.LineRange();
            const lines = t.Lines.slice(r.First, r.Last + 1);
            const code = lines.filter(l => l.trim().length > 0);
            const commented = code.length > 0 && code.every(l => l.trimStart().startsWith(c));
            const ind = Math.min(...code.map(l => /^[ \t]*/.exec(l)[0].length), 1e9);
            const out = lines.map(l => {
                if (l.trim().length === 0)
                    return l;
                if (commented)
                    return l.replace(new RegExp("^(\\s*)" + c.replace(/[/*]/g, "\\$&") + " ?"), "$1");
                return l.slice(0, ind) + c + " " + l.slice(ind);
            });
            t.SetLines(r, out, r.First !== r.Last);
        }

        /** Duplicate the current line or the selection. */
        Duplicate() {
            const t = this;
            const ta = t.Input;
            const s = ta.selectionStart, e = ta.selectionEnd;
            if (s !== e) {
                const sel = ta.value.slice(s, e);
                t.Replace(e, e, sel, e, e + sel.length);
                return;
            }
            const r = t.LineRange();
            const text = t.Lines.slice(r.First, r.Last + 1).join("\n");
            const col = s - r.Start;
            t.Replace(r.End, r.End, "\n" + text, r.End + 1 + col, r.End + 1 + col);
        }

        /** Move the selected lines up or down. @param {number} dir -1 up, 1 down. */
        MoveLines(dir) {
            const t = this;
            const r = t.LineRange();
            if ((dir < 0 && r.First === 0) || (dir > 0 && r.Last >= t.Lines.length - 1))
                return;
            const block = t.Lines.slice(r.First, r.Last + 1);
            const s = t.Input.selectionStart - r.Start, e = t.Input.selectionEnd - r.Start;
            if (dir < 0) {
                const prev = t.Lines[r.First - 1];
                const range = { Start: t.Starts[r.First - 1], End: r.End };
                const text = block.join("\n") + "\n" + prev;
                t.Replace(range.Start, range.End, text, range.Start + s, range.Start + e);
            }
            else {
                const next = t.Lines[r.Last + 1];
                const range = { Start: r.Start, End: t.Starts[r.Last + 1] + next.length };
                const text = next + "\n" + block.join("\n");
                const off = r.Start + next.length + 1;
                t.Replace(range.Start, range.End, text, off + s, off + e);
            }
        }

        // -------------------------------------------------------------- hover

        /** The text position under a point (null outside the text) */
        PosAt(clientX, clientY) {
            const t = this;
            if (!t.LH)
                t.Metrics();
            const r = t.Input.getBoundingClientRect();
            const line = Math.floor((clientY - r.top + t.Input.scrollTop - t.PadTop) / t.LH);
            const x = (clientX - r.left + t.Input.scrollLeft - t.PadLeft) / t.CW;
            if (line < 0 || line >= t.Lines.length || x < 0)
                return null;
            const s = t.Lines[line];
            let v = 0;
            for (let i = 0; i < s.length; ++i) {
                const w = s[i] === "\t" ? t.TabSize - (v % t.TabSize) : 1;
                if (x < v + w)
                    return { Line: line, Col: i, Pos: t.Starts[line] + i };
                v += w;
            }
            return null;
        }

        /** The identifier at a position: { Word, Start, End, Line (1 based), Col (1 based) }, null if none */
        WordAt(pos) {
            const t = this;
            const v = t.Input.value;
            if (!IdentRx.test(v[pos] || ""))
                return null;
            let s = pos, e = pos;
            while (s > 0 && IdentRx.test(v[s - 1]))
                --s;
            while (e < v.length && IdentRx.test(v[e]))
                ++e;
            const word = v.slice(s, e);
            if (/^\d/.test(word))
                return null;
            const line = t.LineOf(s);
            return { Word: word, Start: s, End: e, Line: line + 1, Col: s - t.Starts[line] + 1 };
        }

        /** Mouse move: show the hover popup for the identifier under the mouse (after a short delay). */
        HoverMove(e) {
            const t = this;
            if (!t.O.Hover || e.buttons)
                return;
            const p = t.PosAt(e.clientX, e.clientY);
            const w = p && !t.InComment(p.Pos) ? t.WordAt(p.Pos) : null;
            const key = w ? w.Start + ":" + w.Word : null;
            if (key === t.HoverKey)
                return;
            t.HoverKey = key;
            clearTimeout(t.HoverTimer);
            if (!w) {
                t.HoverLeave();
                return;
            }
            t.HoverTimer = setTimeout(() => t.ShowHover(w), 450);
        }

        /** Mouse leave: hide the hover popup unless the mouse moved onto it. */
        HoverLeave() {
            const t = this;
            clearTimeout(t.HoverTimer);
            t.HoverKey = null;
            clearTimeout(t.HoverHide);
            //  Some time to move into the popup (it has buttons)
            t.HoverHide = setTimeout(() => {
                if (!t.HoverBox || !t.HoverBox.matches(":hover"))
                    t.HideHover();
            }, 250);
        }

        /** Hide the hover popup. */
        HideHover() {
            const t = this;
            clearTimeout(t.HoverTimer);
            t.HoverKey = null;
            if (t.HoverBox)
                t.HoverBox.hidden = true;
        }

        /** Ask the page for the content (option Hover(word info, editor) → element / promise of an element / null) and show it next to the word */
        async ShowHover(w) {
            const t = this;
            const content = await t.O.Hover(w, t);
            if (!content || t.HoverKey !== w.Start + ":" + w.Word)
                return;
            if (!t.HoverBox) {
                t.HoverBox = el("div.sw-code-hover.sw-scroll");
                t.HoverBox.addEventListener("mouseleave", () => t.HoverLeave());
                t.HoverBox.addEventListener("mouseenter", () => clearTimeout(t.HoverHide));
                t.E.append(t.HoverBox);
            }
            const box = t.HoverBox;
            box.replaceChildren(content);
            box.hidden = false;
            const p = t.PosXY(w.Start);
            const mr = t.Main.getBoundingClientRect(), er = t.E.getBoundingClientRect();
            const x = mr.left - er.left + p.X, y = mr.top - er.top + p.Y;
            box.style.left = Math.max(0, Math.min(er.width - box.offsetWidth - 4, x)) + "px";
            const below = y + t.LH + box.offsetHeight + 4 < er.height || y < box.offsetHeight + 4;
            box.style.top = (below ? y + t.LH + 2 : y - box.offsetHeight - 2) + "px";
        }

        // -------------------------------------------------------------- completion

        WordBefore() {
            const t = this;
            const p = t.Input.selectionStart;
            const v = t.Input.value;
            let i = p;
            while (i > 0 && IdentRx.test(v[i - 1]))
                --i;
            return { Start: i, End: p, Text: v.slice(i, p) };
        }

        /** All candidates: the language words, the identifiers of the document, the page's extra items */
        Candidates() {
            const t = this;
            const map = new Map();
            for (const w of t.Lang.Words)
                map.set(w.Text, w);
            for (const w of (t.O.Completions ? t.O.Completions() || [] : []))
                map.set(w.Text, w);
            const ids = t.Input.value.match(/\b[A-Za-z_]\w{2,}\b/g) || [];
            for (const id of ids)
                if (!map.has(id))
                    map.set(id, { Text: id, Kind: "word" });
            return map;
        }

        /** Show the completion list for the word at the cursor. @param {boolean} explicit Requested with Ctrl+Space (also for short words). */
        ShowComplete(explicit) {
            const t = this;
            const w = t.WordBefore();
            if (!explicit && w.Text.length < 1) {
                t.CloseComplete();
                return;
            }
            const q = w.Text, ql = q.toLowerCase();
            const list = [];
            for (const it of t.Candidates().values()) {
                if (it.Text === q && it.Kind === "word")
                    continue;
                const tl = it.Text.toLowerCase();
                const rank = it.Text.startsWith(q) ? 0 : tl.startsWith(ql) ? 1 : ql.length >= 2 && tl.includes(ql) ? 2 : -1;
                if (rank >= 0)
                    list.push({ It: it, Rank: rank + (it.Kind === "word" ? 0.5 : 0) });
            }
            list.sort((a, b) => a.Rank - b.Rank || a.It.Text.length - b.It.Text.length || a.It.Text.localeCompare(b.It.Text));
            const items = list.slice(0, 60).map(x => x.It);
            if (items.length === 0 || (items.length === 1 && items[0].Text === q)) {
                t.CloseComplete();
                return;
            }
            if (!t.CompleteBox) {
                t.CompleteBox = el("div.sw-code-complete.sw-scroll");
                t.CompleteBox.addEventListener("pointerdown", e => e.preventDefault());
                t.E.append(t.CompleteBox);
            }
            t.Complete = { Word: w, Items: items, Index: 0 };
            t.RenderComplete();
        }

        /** Render the completion list. */
        RenderComplete() {
            const t = this;
            const c = t.Complete;
            const box = t.CompleteBox;
            box.replaceChildren();
            c.Items.forEach((it, i) => {
                const row = el("div.sw-code-complete-item" + (i === c.Index ? ".sw-active" : ""), { title: it.Detail || "" },
                    el("span.sw-code-ck.sw-ck-" + (it.Kind || "word"), { text: (it.Kind || "word")[0].toUpperCase() }),
                    el("span.sw-code-complete-text", { text: it.Text }),
                    it.Detail ? el("span.sw-code-complete-detail", { text: it.Detail }) : null);
                row.addEventListener("click", () => {
                    c.Index = i;
                    t.AcceptComplete();
                });
                box.append(row);
            });
            box.hidden = false;
            //  Below the word (above if there's no room)
            const p = t.PosXY(c.Word.Start);
            const mr = t.Main.getBoundingClientRect(), er = t.E.getBoundingClientRect();
            const x = mr.left - er.left + p.X, y = mr.top - er.top + p.Y;
            box.style.left = Math.max(0, Math.min(er.width - box.offsetWidth - 4, x)) + "px";
            const below = y + t.LH + box.offsetHeight < er.height;
            box.style.top = (below ? y + t.LH + 2 : Math.max(0, y - box.offsetHeight - 2)) + "px";
            const active = box.children[c.Index];
            active && active.scrollIntoView({ block: "nearest" });
        }

        /** Insert the selected completion. */
        AcceptComplete() {
            const t = this;
            const c = t.Complete;
            if (!c)
                return;
            const it = c.Items[c.Index];
            t.CloseComplete();
            const w = t.WordBefore();
            t.Replace(w.Start, w.End, it.Text);
            t.O.OnComplete && t.O.OnComplete(it, t);
        }

        /** Close the completion list. */
        CloseComplete() {
            const t = this;
            t.Complete = null;
            if (t.CompleteBox)
                t.CompleteBox.hidden = true;
        }

        // -------------------------------------------------------------- find / replace

        Find(replace) {
            const t = this;
            if (!t.FindBar)
                t.BuildFind();
            t.FindBar.hidden = false;
            t.ReplaceRow.hidden = !replace;
            const sel = t.Input.value.slice(t.Input.selectionStart, t.Input.selectionEnd);
            if (sel && !sel.includes("\n"))
                t.FindInput.value = sel;
            t.FindInput.focus();
            t.FindInput.select();
            t.RunFind(false);
        }

        /** Create the find / replace bar. */
        BuildFind() {
            const t = this;
            const opt = (text, title) => {
                const b = new Button({ Text: text, Title: title, Small: true, Kind: "ghost", Pressable: true, OnClick: () => t.RunFind(true) });
                b.E.classList.add("sw-code-find-opt");
                return b;
            };
            t.FindInput = el("input.sw-input.sw-code-find-input", { type: "text", placeholder: _TF("Find", "Placeholder of the search box of a code editor"), attrs: { spellcheck: "false" } });
            t.ReplaceInput = el("input.sw-input.sw-code-find-input", { type: "text", placeholder: _TF("Replace", "Placeholder of the replace box of a code editor"), attrs: { spellcheck: "false" } });
            t.FindCase = opt("Aa", _TF("Match case", "Tool tip of a find option of a code editor"));
            t.FindWord = opt("W", _TF("Whole word", "Tool tip of a find option of a code editor"));
            t.FindRegex = opt(".*", _TF("Regular expression", "Tool tip of a find option of a code editor"));
            t.FindCount = el("span.sw-code-find-count");
            const small = (icon, title, fn) => new Button({ Icon: icon, Title: title, Small: true, Kind: "ghost", OnClick: fn }).E;
            t.ReplaceRow = el("div.sw-code-find-row", null, t.ReplaceInput,
                new Button({ Text: _TF("Replace", "Button of a code editor that replaces the current match"), Small: true, Kind: "ghost", Title: _TF("Replace the current match (Enter)", "Tool tip of a button of a code editor"), OnClick: () => t.ReplaceOne() }).E,
                new Button({ Text: _TF("All", "Button of a code editor that replaces all matches"), Small: true, Kind: "ghost", Title: _TF("Replace all matches (Ctrl+Alt+Enter)", "Tool tip of a button of a code editor"), OnClick: () => t.ReplaceAll() }).E);
            t.FindBar = el("div.sw-code-find", null,
                el("div.sw-code-find-row", null, t.FindInput, t.FindCase.E, t.FindWord.E, t.FindRegex.E, t.FindCount,
                    small("up", _TF("Previous (Shift+F3 / Shift+Enter)", "Tool tip of a button of a code editor that finds the previous match"), () => t.FindNext(-1)),
                    small("down", _TF("Next (F3 / Enter)", "Tool tip of a button of a code editor that finds the next match"), () => t.FindNext(1)),
                    small("replace", _TF("Toggle replace (Ctrl+H)", "Tool tip of a button of a code editor that shows the replace box"), () => {
                        t.ReplaceRow.hidden = !t.ReplaceRow.hidden;
                        (t.ReplaceRow.hidden ? t.FindInput : t.ReplaceInput).focus();
                    }),
                    small("close", _TF("Close (Escape)", "Tool tip of a button of a code editor that closes the find bar"), () => t.CloseFind())),
                t.ReplaceRow);
            t.FindInput.addEventListener("input", () => t.RunFind(true));
            const keys = e => {
                if (e.key === "Escape") {
                    e.preventDefault();
                    e.stopPropagation();
                    t.CloseFind();
                }
                else if (e.key === "F3" || (e.key === "Enter" && e.target === t.FindInput)) {
                    e.preventDefault();
                    t.FindNext(e.shiftKey ? -1 : 1);
                }
                else if (e.key === "Enter" && e.target === t.ReplaceInput) {
                    e.preventDefault();
                    if (e.ctrlKey && e.altKey)
                        t.ReplaceAll();
                    else
                        t.ReplaceOne();
                }
                else if ((e.ctrlKey || e.metaKey) && (e.key === "f" || e.key === "h")) {
                    e.preventDefault();
                    t.ReplaceRow.hidden = e.key === "f";
                    (e.key === "f" ? t.FindInput : t.ReplaceInput).focus();
                }
            };
            t.FindBar.addEventListener("keydown", keys);
            t.E.append(t.FindBar);
        }

        /** The regular expression of the find options. @returns {RegExp|null|undefined} Null for an empty search, undefined when invalid. */
        FindRegExp() {
            const t = this;
            const q = t.FindInput ? t.FindInput.value : "";
            if (!q)
                return null;
            let src = t.FindRegex.Pressed ? q : q.replace(/[.*+?^$(){}|[\]\\]/g, "\\$&");
            if (t.FindWord.Pressed)
                src = "\\b" + src + "\\b";
            try {
                return new RegExp(src, "g" + (t.FindCase.Pressed ? "" : "i") + "m");
            }
            catch {
                return undefined;
            }
        }

        /** Find all matches (jump: select the first one from the cursor on) */
        RunFind(jump) {
            const t = this;
            if (!t.FindBar || t.FindBar.hidden) {
                t.FindState = null;
                return;
            }
            const rx = t.FindRegExp();
            const matches = [];
            if (rx) {
                const v = t.Input.value;
                let m;
                while ((m = rx.exec(v)) && matches.length < 20000) {
                    if (m[0].length === 0) {
                        ++rx.lastIndex;
                        continue;
                    }
                    matches.push({ Start: m.index, End: m.index + m[0].length });
                }
            }
            t.FindState = { Rx: rx, Matches: matches, Current: -1 };
            t.FindInput.classList.toggle("sw-invalid", rx === undefined || (rx && matches.length === 0));
            if (jump && matches.length) {
                const p = t.Input.selectionStart;
                const i = matches.findIndex(m => m.Start >= p);
                t.SelectMatch(i < 0 ? 0 : i, false);
            }
            t.UpdateCount();
            t.Render();
        }

        /** Update the match counter of the find bar. */
        UpdateCount() {
            const f = this.FindState;
            if (!f || !this.FindCount)
                return;
            this.FindCount.textContent = f.Matches.length === 0 ? (this.FindInput.value ? _TF("No results", "Shown in the find bar of a code editor when nothing matches") : "") : (f.Current >= 0 ? (f.Current + 1) + " / " : "") + f.Matches.length;
        }

        /** Select a match. @param {number} i The match index. @param {boolean} focus Move the focus to the editor. */
        SelectMatch(i, focus) {
            const t = this;
            const f = t.FindState;
            const m = f.Matches[i];
            if (!m)
                return;
            f.Current = i;
            t.Input.setSelectionRange(m.Start, m.End);
            t.View.SelStart = m.Start;
            t.View.SelEnd = m.End;
            if (focus)
                t.Input.focus({ preventScroll: true });
            t.ScrollToPos(m.Start, true);
            t.UpdateCount();
            t.Render();
        }

        /** Go to the next / previous match. @param {number} dir 1 next, -1 previous. */
        FindNext(dir) {
            const t = this;
            if (!t.FindState)
                t.RunFind(false);
            const f = t.FindState;
            if (!f || !f.Matches.length)
                return;
            const p = dir > 0 ? t.Input.selectionEnd : t.Input.selectionStart;
            let i;
            if (dir > 0) {
                i = f.Matches.findIndex(m => m.Start >= p);
                if (i < 0)
                    i = 0;
            }
            else {
                i = -1;
                for (let k = f.Matches.length - 1; k >= 0; --k)
                    if (f.Matches[k].End <= p && f.Matches[k].Start < p) {
                        i = k;
                        break;
                    }
                if (i < 0)
                    i = f.Matches.length - 1;
            }
            t.SelectMatch(i, false);
        }

        /** The replacement of a match (regex: $1 etc) */
        ReplacementOf(text) {
            const t = this;
            const rep = t.ReplaceInput.value;
            if (!t.FindRegex.Pressed)
                return rep;
            const rx = t.FindRegExp();
            return rx ? text.replace(new RegExp(rx.source, rx.flags.replace("g", "")), rep) : rep;
        }

        /** Replace the current match. */
        ReplaceOne() {
            const t = this;
            const f = t.FindState;
            if (!f || !f.Matches.length || t.Input.readOnly)
                return;
            const s = t.Input.selectionStart, e = t.Input.selectionEnd;
            const cur = f.Matches.find(m => m.Start === s && m.End === e);
            if (!cur) {
                t.FindNext(1);
                return;
            }
            const text = t.ReplacementOf(t.Input.value.slice(s, e));
            t.Replace(s, e, text, s + text.length, s + text.length);
            t.RunFind(false);
            t.FindNext(1);
            t.ReplaceInput.focus();
        }

        /** Replace all matches (one undoable edit). */
        ReplaceAll() {
            const t = this;
            const f = t.FindState;
            if (!f || !f.Matches.length || t.Input.readOnly)
                return;
            const v = t.Input.value;
            let out = "", last = 0;
            for (const m of f.Matches) {
                out += v.slice(last, m.Start) + t.ReplacementOf(v.slice(m.Start, m.End));
                last = m.End;
            }
            out += v.slice(last);
            const n = f.Matches.length;
            t.Edit(out);
            t.RunFind(false);
            Sw.Toast(_T("{0} replaced", n, "Message of a code editor after replacing matches, {0} is the number of replacements"));
            t.ReplaceInput.focus();
        }

        /** Close the find bar. */
        CloseFind() {
            const t = this;
            if (t.FindBar)
                t.FindBar.hidden = true;
            t.FindState = null;
            t.Render();
            t.Focus();
        }

        // -------------------------------------------------------------- markers

        SetMarkers(list) {
            const t = this;
            t.Markers.clear();
            for (const m of list || []) {
                if (!(m.Line >= 1))
                    continue;
                const prev = t.Markers.get(m.Line);
                if (!prev || (prev.Kind !== "error" && m.Kind === "error"))
                    t.Markers.set(m.Line, { Kind: m.Kind || "error", Text: m.Text || "" });
                else if (prev.Kind === m.Kind)
                    prev.Text += " · " + m.Text;
            }
            t.Render();
        }

        /**
         * Clickable gutter dots (ex: a breakpoint), drawn over the line number; a click calls the option OnDot(line, dot, event).
         * @param {Array<{Line: number, Kind: string, Title: string}>} list The dots (1 based lines; Kind: a class sw-dot-<Kind>).
         */
        SetDots(list) {
            const t = this;
            t.Dots.clear();
            for (const d of list || [])
                if (d.Line >= 1)
                    t.Dots.set(d.Line, { Kind: d.Kind || "dot", Title: d.Title || "" });
            t.Render();
        }

        /**
         * Highlighted text ranges (ex: the expression a tool looks at).
         * @param {Array<{Line: number, Col: number, Len: number, Kind: string}>} list The ranges (1 based line and column; class sw-code-span sw-<Kind>).
         */
        SetSpans(list) {
            this.Spans = (list || []).filter(s => s.Line >= 1 && s.Len > 0);
            this.Render();
        }

        // -------------------------------------------------------------- layout / rendering

        /** Line height / character width / padding (after font changes) */
        Metrics() {
            const t = this;
            const cs = getComputedStyle(t.Input);
            const fs = parseFloat(cs.fontSize) || 13;
            const lh = Math.round(fs * 1.5);
            if (t.LH !== lh) {
                t.LH = lh;
                t.E.style.setProperty("--sw-code-lh", lh + "px");
            }
            t.CW = t.Measure.getBoundingClientRect().width / 100 || fs * 0.6;
            t.PadTop = parseFloat(cs.paddingTop) || 0;
            t.PadLeft = parseFloat(cs.paddingLeft) || 0;
        }

        /** Re-measure and redraw (after font, size or theme changes). */
        Refresh() {
            this.Metrics();
            this.Cache.clear();
            this.Render();
        }

        /** Pixel position (relative to the visible area) of a text position */
        PosXY(pos) {
            const t = this;
            const line = t.LineOf(pos);
            return {
                X: t.PadLeft + t.VCol(line, pos - t.Starts[line]) * t.CW - t.Input.scrollLeft,
                Y: t.PadTop + line * t.LH - t.Input.scrollTop,
            };
        }

        /** The line at a viewport y coordinate. @param {number} clientY The y coordinate. @returns {number} 0 based line, -1 outside. */
        LineAtY(clientY) {
            const t = this;
            const r = t.Gutter.getBoundingClientRect();
            const line = Math.floor((clientY - r.top + t.Input.scrollTop - t.PadTop) / t.LH);
            return line >= 0 && line < t.Lines.length ? line : -1;
        }

        /** Scroll so a position is visible (center: in the middle when it's out of view) */
        ScrollToPos(pos, center) {
            const t = this;
            const ta = t.Input;
            if (!t.LH)
                t.Metrics();
            const line = t.LineOf(pos);
            const y = t.PadTop + line * t.LH;
            const x = t.PadLeft + t.VCol(line, pos - t.Starts[line]) * t.CW;
            const h = ta.clientHeight, w = ta.clientWidth;
            if (y < ta.scrollTop || y + t.LH > ta.scrollTop + h)
                ta.scrollTop = center ? y - h / 2 : (y < ta.scrollTop ? y - t.LH : y + t.LH * 2 - h);
            if (x < ta.scrollLeft + 20 || x > ta.scrollLeft + w - 40)
                ta.scrollLeft = Math.max(0, x - w / 2);
            t.View.Top = ta.scrollTop;
            t.View.Left = ta.scrollLeft;
        }

        /** The matching bracket positions of the bracket at / before the cursor, null if none */
        Brackets() {
            const t = this;
            const v = t.Input.value;
            const p = t.Input.selectionStart;
            if (p !== t.Input.selectionEnd)
                return null;
            const pairs = { "(": ")", "[": "]", "{": "}" };
            const closes = { ")": "(", "]": "[", "}": "{" };
            for (const at of [p - 1, p]) {
                const ch = v[at];
                if (pairs[ch]) {
                    let d = 0;
                    for (let i = at; i < v.length && i < at + 50000; ++i) {
                        if (v[i] === ch)
                            ++d;
                        else if (v[i] === pairs[ch] && --d === 0)
                            return [at, i];
                    }
                    return [at, -1];
                }
                if (closes[ch]) {
                    let d = 0;
                    for (let i = at; i >= 0 && i > at - 50000; --i) {
                        if (v[i] === ch)
                            ++d;
                        else if (v[i] === closes[ch] && --d === 0)
                            return [i, at];
                    }
                    return [-1, at];
                }
            }
            return null;
        }

        /** Schedule a redraw (once per animation frame). */
        Render() {
            const t = this;
            if (t.RenderPending)
                return;
            t.RenderPending = true;
            requestAnimationFrame(() => {
                t.RenderPending = false;
                t.Draw();
            });
        }

        /** Draw the visible lines, the gutter, markers, matches and brackets. */
        Draw() {
            const t = this;
            const ta = t.Input;
            const h = ta.clientHeight;
            if (h <= 0)
                return;
            if (!t.LH)
                t.Metrics();
            const lh = t.LH, cw = t.CW, pt = t.PadTop, pl = t.PadLeft;
            const top = ta.scrollTop, left = ta.scrollLeft;
            const n = t.Lines.length;
            const first = Math.max(0, Math.floor((top - pt) / lh) - 2);
            const last = Math.min(n - 1, Math.ceil((top + h - pt) / lh) + 2);
            const lang = t.Lang;
            const s = ta.selectionStart, e = ta.selectionEnd;
            const curLine = t.LineOf(s);
            const focused = document.activeElement === ta;
            let html = "";
            const box = (line, c0, c1, cls) => {
                const x0 = pl + t.VCol(line, c0) * cw, x1 = pl + t.VCol(line, c1) * cw;
                html += '<div class="' + cls + '" style="top:' + (pt + line * lh) + "px;left:" + x0 + "px;width:" + Math.max(2, x1 - x0) + 'px"></div>';
            };
            //  Background: current line, error lines, find matches, brackets
            if (s === e && curLine >= first && curLine <= last)
                html += '<div class="sw-code-cur' + (focused ? "" : " sw-blur") + '" style="top:' + (pt + curLine * lh) + 'px"></div>';
            for (const [ln, m] of t.Markers) {
                const i = ln - 1;
                if (i >= first && i <= last)
                    html += '<div class="sw-code-mark sw-' + m.Kind + '" style="top:' + (pt + i * lh) + 'px"></div>';
            }
            for (const sp of t.Spans) {
                const i = sp.Line - 1;
                if (i >= first && i <= last && i < t.Lines.length)
                    box(i, sp.Col - 1, Math.min(t.Lines[i].length, sp.Col - 1 + sp.Len), "sw-code-span sw-" + (sp.Kind || "span"));
            }
            const f = t.FindState;
            if (f && f.Matches.length) {
                const startPos = t.Starts[first], endPos = last + 1 < n ? t.Starts[last + 1] : ta.value.length;
                let lo = 0, hi = f.Matches.length;
                while (lo < hi) {
                    const mid = (lo + hi) >> 1;
                    if (f.Matches[mid].End < startPos)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                for (let k = lo; k < f.Matches.length && f.Matches[k].Start <= endPos; ++k) {
                    const m = f.Matches[k];
                    const l0 = t.LineOf(m.Start), l1 = t.LineOf(m.End);
                    const cls = "sw-code-match" + (k === f.Current ? " sw-current" : "");
                    if (l0 === l1)
                        box(l0, m.Start - t.Starts[l0], m.End - t.Starts[l0], cls);
                    else
                        box(l0, m.Start - t.Starts[l0], t.Lines[l0].length + 1, cls);
                }
            }
            const br = focused ? t.Brackets() : null;
            if (br) {
                for (const p of br) {
                    if (p < 0)
                        continue;
                    const l = t.LineOf(p);
                    if (l >= first && l <= last)
                        box(l, p - t.Starts[l], p - t.Starts[l] + 1, "sw-code-bracket" + (br.includes(-1) ? " sw-unmatched" : ""));
                }
            }
            //  The text
            for (let i = first; i <= last; ++i) {
                const line = t.Lines[i];
                const st = t.States[i] || 0;
                const key = st + "\u0001" + line;
                let tok = t.Cache.get(key);
                if (tok === undefined) {
                    tok = lang.Html(line, st);
                    t.Cache.set(key, tok);
                }
                html += '<div class="sw-code-line" style="top:' + (pt + i * lh) + "px;left:" + pl + 'px">' + tok + "</div>";
                const m = t.Markers.get(i + 1);
                if (m && m.Text)
                    html += '<div class="sw-code-msg sw-' + m.Kind + '" style="top:' + (pt + i * lh) + "px;left:" + (pl + (t.VCol(i, line.length) + 3) * cw) + 'px">' + esc(m.Text) + "</div>";
            }
            t.Layer.innerHTML = html;
            t.Layer.style.transform = "translate(" + (-left) + "px," + (-top) + "px)";
            //  Gutter: line numbers, markers
            const digits = Math.max(3, String(n).length);
            const gw = Math.ceil(digits * cw + 26);
            if (t.GutterWidth !== gw) {
                t.GutterWidth = gw;
                t.Gutter.style.width = gw + "px";
            }
            let g = "";
            for (let i = first; i <= last; ++i) {
                const m = t.Markers.get(i + 1);
                const d = t.Dots.get(i + 1);
                const tip = [m ? m.Text : "", d ? d.Title : ""].filter(x => x).join("\n");
                g += '<div class="sw-code-num' + (i === curLine ? " sw-cur" : "") + (m ? " sw-" + m.Kind : "") + (d ? " sw-dot sw-dot-" + d.Kind : "") + '" style="top:' + (pt + i * lh) + 'px"' +
                    (tip ? ' title="' + esc(tip).replace(/"/g, "&quot;") + '"' : "") + ">" + (i + 1) + "</div>";
            }
            t.GutterInner.innerHTML = g;
            t.GutterInner.style.transform = "translateY(" + (-top) + "px)";
        }
    }

    Sw.CodeEditor = CodeEditor;
    //  (the one registry of option schemas: a typo in an option is a developer warning)
    if (Sw.Work && typeof Sw.Work.RegisterOptions === "function")
        Sw.Work.RegisterOptions("Sw.CodeEditor", {
            Value: "string", Language: { Type: "string", Check: v => Languages[v] ? "" : '"' + v + '" is no language (' + Object.keys(Languages).join(", ") + ")." }, ReadOnly: "boolean", TabSize: "number",
            AutoComplete: "boolean", Completions: "function", OnChange: "function", OnCursor: "function", Placeholder: "string", OnComplete: "function", Hover: "function", OnAltClick: "function", OnDot: "function",
        });
    Sw.CodeLanguages = Languages;
    Sw.GlslDocs = GlslDocs;
})();

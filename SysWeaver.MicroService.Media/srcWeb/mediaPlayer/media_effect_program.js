/**
 * @file media_effect_program.js
 * GLSL effect source parsing and WebGL program compilation for MediaPlayerEffect.
 *
 * Loading: included by MediaPlayer.Init (only when window.MediaLoadAll is set) or lazily by
 * MediaPlayer.Create (together with media_player_effect.js) through common.js includeJs; also loaded with
 * static <script defer> tags by the mediaView/*Effects/index.html and *EffectsLib.html pages.
 *
 * Globals defined: EffectProgram, EffectProgramData; window.ShaderDebug is set by the first
 * EffectProgramData constructor (from the ?ShaderDebug=true query parameter) unless already defined.
 *
 * Dependencies: MediaPlayerTools.delay and MediaPlayerTools.SetError (media_player_tools.js).
 *
 * Conventions:
 * - Effects are GLSL ES 1.0 fragment shaders in GlslSandbox style (uniform time/resolution) or ShaderToy
 *   style (mainImage; a main() wrapper and the iXxx uniforms are injected automatically).
 * - Editable effect parameters are compile-time constants written as
 *   "const <type> <name> = <value>; // var:{name:..., desc:..., min:..., max:..., step:..., type:...}".
 *   Changing a parameter rewrites that source line and recompiles the program (no uniforms).
 * - The parameter schema is produced in the SysWeaver type-info JSON format used by the generic editor
 *   (Flags 256 = scalar, 512 = object, |8 = has min and max; TypeName such as System.Single or glsl.vec3_col).
 */

/**
 * A compiled and linked WebGL program together with its vertex and fragment shaders.
 * Created by EffectProgramData.Compile; owned by MediaPlayerEffect (its Program field) which disposes the
 * previous instance whenever a new program is compiled, and in ReleaseGL.
 */
class EffectProgram
{
    /**
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - Context that owns the objects.
     * @param {WebGLShader} vs - Vertex shader.
     * @param {WebGLShader} fs - Fragment shader.
     * @param {WebGLProgram} program - Program with both shaders attached.
     */
    constructor(gl, vs, fs, program)
    {
        const t = this;
        t.VS = vs;
        t.FS = fs;
        t.Program = program;
        t.GL = gl;
    }

    /**
     * Deletes the program and both shaders from the GL context.
     * @returns {void}
     */
    Dispose()
    {
        const t = this;
        const gl = t.GL;
        gl.deleteProgram(t.Program);
        gl.deleteShader(t.FS);
        gl.deleteShader(t.VS);
    }
    /** @type {WebGLRenderingContext|WebGL2RenderingContext} Owning context. */
    GL;
    /** @type {WebGLShader} Vertex shader. */
    VS;
    /** @type {WebGLShader} Fragment shader. */
    FS;
    /** @type {WebGLProgram} The linked program. */
    Program;
    /** @type {number} Milliseconds from Compile start until the program was linked (set by EffectProgramData.Compile). */
    CompilationTime;
}

/**
 * Parsed effect source: the trimmed source lines, the editable parameter schema extracted from
 * "// var:{...}" annotated const lines, the injected prefix/suffix, and a compiler that produces
 * EffectProgram instances for a given set of parameter values.
 * Created once per loaded effect by MediaPlayerEffect.Create; Compile may be called repeatedly
 * (e.g. on every live parameter edit through MediaPlayerEffect.LazyCompile).
 */
class EffectProgramData {

    /**
     * Parses some shader source and extracts the annotated variables.
     * Side effects: caches the KHR_parallel_shader_compile extension as expando gl.PCompile (undefined/null if
     * missing), initializes window.ShaderDebug from the ?ShaderDebug=true query parameter when not yet defined,
     * trims every source line in place, logs parse warnings (unknown type / bad annotation) to the console,
     * and logs the detected features plus prefix/suffix when ShaderDebug is on.
     * Sets: Url, Lines, GL, Prefix, Suffix, HaveLod, waitCompilation, waitLinking, ScriptPropsApply,
     * ScriptDefText, ScriptTypeInfo, ScriptMember.
     * @param {WebGLRenderingContext|WebGL2RenderingContext} gl - Context used for compilation.
     * @param {string} src - GLSL source text of the effect.
     * @param {string} [fileName] - Source name/URL used in log messages and the editor summary ("[String]" if missing).
     */
    constructor(gl, src, fileName) {
        const t = this;
        t.Url = fileName;
        //  Setup async compilation
        if (typeof gl.PCompile === "undefined")
            gl.PCompile = gl.getExtension("KHR_parallel_shader_compile");
        const pcompile = gl.PCompile;
        if (pcompile) {
            /**
             * Waits for a shader compilation using the parallel-compile extension: polls every 5 ms until
             * COMPLETION_STATUS_KHR is reported.
             * @param {WebGLShader} shader - Shader being compiled.
             * @param {function():boolean} abortCheckFn - Returns true to abort waiting.
             * @returns {Promise<boolean>} The COMPILE_STATUS, or true if aborted (the caller checks abortCheckFn again).
             */
            t.waitCompilation = async function (shader, abortCheckFn) {
                for (; ;) {
                    await MediaPlayerTools.delay(5);
                    if (abortCheckFn())
                        return true;
                    if (gl.getShaderParameter(shader, pcompile.COMPLETION_STATUS_KHR))
                        break;
                }
                return gl.getShaderParameter(shader, gl.COMPILE_STATUS);
            };

            /**
             * Waits for program linking using the parallel-compile extension: polls every 5 ms until
             * COMPLETION_STATUS_KHR is reported.
             * @param {WebGLProgram} prog - Program being linked.
             * @param {function():boolean} abortCheckFn - Returns true to abort waiting.
             * @returns {Promise<boolean>} The LINK_STATUS, or true if aborted.
             */
            t.waitLinking = async function (prog, abortCheckFn) {
                for (; ;) {
                    await MediaPlayerTools.delay(5);
                    if (abortCheckFn())
                        return true;
                    if (gl.getProgramParameter(prog, pcompile.COMPLETION_STATUS_KHR))
                        break;
                }
                return gl.getProgramParameter(prog, gl.LINK_STATUS);
            };
        }
        else {
            /**
             * Synchronous fallback (no parallel-compile extension).
             * @param {WebGLShader} shader - Compiled shader.
             * @returns {boolean} The COMPILE_STATUS.
             */
            t.waitCompilation = shader => gl.getShaderParameter(shader, gl.COMPILE_STATUS);
            /**
             * Synchronous fallback (no parallel-compile extension).
             * @param {WebGLProgram} prog - Linked program.
             * @returns {boolean} The LINK_STATUS.
             */
            t.waitLinking = prog => gl.getProgramParameter(prog, gl.LINK_STATUS);
        }

        let debug = window.ShaderDebug;
        if (typeof debug === "undefined") {
            const p = new URL(window.location.href).searchParams;
            debug = p.has("ShaderDebug") ? (p.get("ShaderDebug") === "true") : false;
            /**
             * Global shader debug switch (from the ?ShaderDebug=true query parameter): enables logging of the
             * injected prefix/suffix, final and translated shader sources, and periodic draw-time logs.
             * @type {boolean}
             */
            window.ShaderDebug = debug;
        }
        //  Parse and find variables
        const typeMap = EffectProgramData.TypeMap;
        const lines = src.split("\n");
        t.Lines = lines;
        let scriptProps = {};
        const members = [];
        let dynTypeName = "";
        const c = fileName ?? "[String]";
        const apply = [];
        let haveMain = false;
        let haveMainImage = false;
        let havePrecision = false;
        let haveDerExt = false;
        let haveDer = false;

        let haveLodExt = false;
        let haveLod = false;


        /**
         * Tests if the character at pos is whitespace.
         * @param {string} line - Text line.
         * @param {number} pos - Character index.
         * @param {boolean} [falseOutSize] - When truthy an out-of-range pos returns false, otherwise true.
         * @returns {boolean} True for whitespace (or out of range, see falseOutSize).
         */
        function isSpace(line, pos, falseOutSize) {
            if ((pos < 0) || (pos >= line.length))
                return !falseOutSize;
            return line.charAt(pos).trim() === "";
        }

        /**
         * Tests if the character at pos can be part of an identifier (letter, digit, '_' or '@').
         * @param {string} line - Text line.
         * @param {number} pos - Character index.
         * @returns {boolean} False when out of range or not an identifier character.
         */
        function isIdentifier(line, pos) {
            if ((pos < 0) || (pos >= line.length))
                return false;
            const ch = line.charAt(pos);
            if (isLetter(ch))
                return true;
            if ((ch === '_') || (ch === '@'))
                return true;
            if ((ch >= '0') && (ch <= '9'))
                return true;
            return false;
        }

        /**
         * Tests if a character is a letter: characters with distinct upper/lower case, or non-ASCII characters
         * that are accepted as a JavaScript function name (tested by eval of "function c(){}").
         * @param {string} c - Single character.
         * @returns {boolean} True for a letter.
         */
        function isLetter(c) {
            if (c.toUpperCase() != c.toLowerCase())
                return true;
            if (c.charCodeAt(0) < 128)
                return false;
            try {
                eval("function " + c + "(){}");
                return true;
            } catch {
                return false;
            }
        }

        /**
         * Check if an identifier exists: name occurs as a whole identifier (not part of a longer one)
         * before any "//" comment on the line.
         * @param {string} line - Trimmed source line.
         * @param {string} name - Identifier to look for.
         * @returns {boolean} True if found.
         */
        function haveIdentifier(line, name) {
            const sl = name.length;
            if (sl <= 0)
                return false;
            const c = line.indexOf("//");
            let start = 0;
            for (; ;) {
                const ti = line.indexOf(name, start);
                if (ti < 0)
                    return false;
                if ((c >= 0) && (ti >= c))
                    return false;
                if (isIdentifier(line, ti - 1)) {
                    start = ti + sl;
                    continue;
                }
                start = ti + sl;
                if (isIdentifier(line, start))
                    continue;
                return true;
            }
        }

        /**
         * Check if an identifier exists (preceded by whitespace or the line start, such as keywords and function
         * names), not followed by an identifier character, before any "//" comment.
         * @param {string} line - Trimmed source line.
         * @param {string} name - Identifier to look for.
         * @returns {boolean} True if found.
         */
        function haveSpaceIdentifier(line, name) {
            const sl = name.length;
            if (sl <= 0)
                return false;
            const c = line.indexOf("//");
            let start = 0;
            for (; ;) {
                const ti = line.indexOf(name, start);
                if (ti < 0)
                    return false;
                if ((c >= 0) && (ti >= c))
                    return false;
                if (!isSpace(line, ti - 1)) {
                    start = ti + sl;
                    continue;
                }
                start = ti + sl;
                if (isIdentifier(line, start))
                    continue;
                return true;
            }
        }


        //  Scan every line: detect features (precision, main, mainImage, derivatives, texture2DLod and their
        //  extensions) on non-comment lines, and parse "const <type> <name> = <value>; // var:{...}" lines
        //  into defaults, editor members and rewrite closures. Lines starting with "//" skip feature detection
        //  but still reach the annotation parser if char 5 is whitespace (normally they are then rejected).
        const lc = lines.length;
        for (let i = 0; i < lc; ++i) {
            const line = lines[i].trim();
            lines[i] = line;
            if (!line.startsWith("//")) {
                if (!havePrecision)
                    havePrecision |= haveSpaceIdentifier(line, "precision");
                if (!haveMain)
                    haveMain |= haveSpaceIdentifier(line, "main");
                if (!haveMainImage)
                    haveMainImage |= haveSpaceIdentifier(line, "mainImage");
                if (!haveDerExt)
                    haveDerExt |= haveSpaceIdentifier(line, "GL_OES_standard_derivatives");
                if (!haveDer) {
                    haveDer |= haveIdentifier(line, "dFdx");
                    haveDer |= haveIdentifier(line, "dFdy");
                    haveDer |= haveIdentifier(line, "fwidth");
                }
                if (!haveLodExt)
                    haveLodExt |= haveSpaceIdentifier(line, "GL_EXT_shader_texture_lod");
                if (!haveLod) {
                    haveLod |= haveIdentifier(line, "texture2DLod");
                }
                if (!line.startsWith("const"))
                    continue;
            }
            if (!isSpace(line, 5))
                continue;
            try {
                const ci = line.indexOf('//');
                if (ci < 0)
                    continue;
                const comment = line.substring(ci + 2).trim();
                if (!comment.startsWith("var:"))
                    continue;
                const json = comment.substring(4).trim();
                const desc = json.length > 0 ? JSON.parse(json.replace(/([{,])\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*:/g, "$1\"$2\":")) : {};
                let pi = line.lastIndexOf('=', ci - 1);
                const defValueText = line.substring(pi + 1, ci).replaceAll(';', '').trim();
                while ((pi > 0) && (line.charAt(pi - 1) == ' '))
                    --pi;
                const vi = line.lastIndexOf(' ', pi - 1);
                const varName = line.substring(vi + 1, pi).trim();
                const typeName = line.substring(6, vi).trim();
                //  Type
                const type = typeMap.get(typeName.toLowerCase());
                if (!type) {
                    console.warn(c + "(" + (i + 1) + "): Unknown variable type \"" + typeName + "\"");
                    continue;
                }
                const name = desc.name ?? varName;
                const editType = desc.type ?? null;
                const defValue = type.parseDef(defValueText.replaceAll(" ", ""), editType);
                scriptProps[varName] = defValue;
                const member = {};
                Object.assign(member, type.member);
                member.Name = varName;
                member.DisplayName = name;
                let haveMinMax = 0;
                const min = desc.min;
                if (typeof min !== "undefined") {
                    member.Min = "" + min;
                    ++haveMinMax;
                }
                const max = desc.max;
                if (typeof max !== "undefined") {
                    member.Max = "" + max;
                    ++haveMinMax;
                }
                const summary = desc.desc;
                if (summary)
                    member.Summary = summary;
                if (haveMinMax === 2) {
                    member.Flags |= 8;
                    const step = desc.step;
                    if ((typeof step !== "undefined") && (step > 0))
                        member.EditParams = "" + step;
                }
                if ((typeof defValue !== "undefined") && (defValue !== null))
                    member.Default = JSON.stringify(defValue);
                if (editType) {
                    member.TypeName += ("_" + editType);
                    member.EditParams = editType;
                }
                members.push(member);
                if (dynTypeName.length > 0)
                    dynTypeName += "_";
                dynTypeName += typeName + "_" + varName;
                const ii = i;
                apply.push(obj => {
                    const val = obj[varName];
                    if (typeof val === "undefined")
                        return;
                    lines[ii] = "const " + typeName + " " + varName + "=" + type.toString(val, editType) + ";";
                });
            }
            catch (e) {
                console.warn(c + "(" + (i + 1) + "): Failed to parse variable, error: " + e);
            }
        }
        if (members.length > 0) {
            const tnn = "EffectProps." + dynTypeName;
            //scriptProps = Object.assign({ $type: tnn }, scriptProps);
            const scriptDefText = JSON.stringify(scriptProps);
            t.ScriptDefText = scriptDefText;
            /**
             * Rewrites each annotated source line to "const <type> <name>=<literal>;" for every key present in values.
             * @param {object} v - Parameter values keyed by GLSL variable name.
             * @returns {void}
             */
            t.ScriptPropsApply = v => apply.forEach(x => x(v));
            t.ScriptTypeInfo = {
                Asm: "EffectProps",
                Members: members,
                TypeName: tnn,
                ElementTypeName: null,
                KeyTypeName: null,
                Ext: null,
                DisplayName: "Effect props",
                Min: null,
                Max: null,
                Default: scriptDefText,
                Flags: 512,
                EditParams: null,
                Summary: "Effect properties for effect \"" + c + "\"",
                Remarks: null,
                Type: null,
                KeyInst: null,
                ElementInst: null
            };
            t.ScriptMember = {
                Name: "EffectProps",
                TypeName: tnn,
                ElementTypeName: null,
                Ext: null,
                DisplayName: "Effect props",
                Min: null,
                Max: null,
                Default: scriptDefText,
                Flags: 512,
                EditParams: null,
                Summary: "Effect properties for effect \"" + c + "\"",
                Remarks: null,
                Type: null,
                KeyInst: null,
                ElementInst: null
            };
        }
        else {
            /**
             * No annotated parameters: no-op.
             * @returns {void}
             */
            t.ScriptPropsApply = () => { };
            t.ScriptDefText = null;
            t.ScriptTypeInfo = null;
            t.ScriptMember = null;
        }
        //  Build the injected header (Prefix) and footer (Suffix): missing #extension lines, the texture2DLod
        //  define, a default precision, and the ShaderToy uniforms + main() wrapper when only mainImage exists.
        t.GL = gl;
        t.Prefix = "";
        t.Suffix = "";
        if (haveDer && (!haveDerExt)) {
            t.Prefix +=
                `#extension GL_OES_standard_derivatives : enable
`;
        }
        t.HaveLod = haveLod;
        if (haveLod) {
            if (!haveLodExt) {
                t.Prefix +=
                `#extension GL_EXT_shader_texture_lod : enable
`;
            }
            t.Prefix +=
                `#define texture2DLod texture2D
`;
        }

        


        if (!havePrecision) {
            t.Prefix +=
                `#ifdef GL_ES
precision highp float;
#endif
`;
        }
        if ((!haveMain) && haveMainImage) {
            t.Suffix += `
void main(void)
{
    mainImage(gl_FragColor, gl_FragCoord.xy);
}
`;
            t.Prefix +=
`uniform vec3 iResolution;
uniform float iTime;                 // shader playback time (in seconds)
uniform float iTimeDelta;            // render time (in seconds)
uniform float iFrameRate;            // shader frame rate
uniform int iFrame;                // shader playback frame
uniform vec4 iMouse;                // mouse pixel coords. xy: current (if MLB down), zw: click
uniform vec4 iDate;                 // (year, month, day, time in seconds)
`;
        }


        if (debug) {
            console.log("haveDer: " + haveDer);
            console.log("haveDerExt: " + haveDerExt);

            console.log("haveLod: " + haveLod);
            console.log("haveLodExt: " + haveLodExt);

            console.log("haveLod: " + haveLod);
            console.log("haveLodExt: " + haveLodExt);

            console.log("haveMain: " + haveMain);
            console.log("haveMainImage: " + haveMainImage);

            console.log("### Shader prefix ###\n" + t.Prefix);
            console.log("### Shader suffix ###\n" + t.Suffix);
        }

    }

    // Compiles the program using some vars

    /**
     * Compiles and links a program for the given parameter values.
     * Applies vars through ScriptPropsApply, builds Prefix + Lines + Suffix. If the source contains both
     * "#ifdef VERTEX" and "#ifdef FRAGMENT" the same source is compiled as vertex and fragment shader with
     * "#define VERTEX" / "#define FRAGMENT" prepended; otherwise defaultVS is used as vertex shader.
     * Logs final sources (and WEBGL_debug_shaders translated sources) when window.ShaderDebug is set.
     * Compile errors are logged and reported via MediaPlayerTools.SetError; link errors are only logged.
     * @param {object} vars - Parameter values keyed by GLSL variable name (may be undefined/partial).
     * @param {string} defaultVS - Vertex shader source used for fragment-only effects.
     * @param {function():boolean} [abortCheckFn=() => false] - Polled while waiting; true aborts.
     * @returns {Promise<EffectProgram|null|string>} The program (with CompilationTime set), null on a compile or
     * link error, or the string "Aborted".
     */
    async Compile(vars, defaultVS, abortCheckFn) {
        const start = performance.now();
        const t = this;
        const gl = t.GL;
        const c = t.Url;
        //  Create shaders                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  
        const vs = gl.createShader(gl.VERTEX_SHADER);
        const fs = gl.createShader(gl.FRAGMENT_SHADER);
        const p = gl.createProgram();
        gl.attachShader(p, vs);
        gl.attachShader(p, fs);
        const program = new EffectProgram(gl, vs, fs, p);
        if (!abortCheckFn)
            abortCheckFn = () => false;
        //  Apply vars and build source 

        t.ScriptPropsApply(vars);
        const src = t.Prefix + t.Lines.join("\n") + t.Suffix;
        let vsSrc;
        let fsSrc;
        if ((src.indexOf("#ifdef VERTEX") >= 0) && (src.indexOf("#ifdef FRAGMENT") >= 0)) {
            vsSrc = "#define VERTEX\n" + src;
            fsSrc = "#define FRAGMENT\n" + src;
        }
        else {
            vsSrc = defaultVS;
            fsSrc = src;
        }
        const debug = window.ShaderDebug;
        if (debug) {
            console.log("### Final vertex shader ###\n" + vsSrc);
            console.log("### Final fragment shader ###\n" + fsSrc);
        }
        //  Compile shaders
        gl.shaderSource(fs, fsSrc);
        gl.compileShader(fs);
        gl.shaderSource(vs, vsSrc);
        gl.compileShader(vs);
        //  Wait for compilation to finish (or abort)
        if (!(await t.waitCompilation(vs, abortCheckFn))) {
            let err;
            try {
                err = gl.getShaderInfoLog(vs);
            }
            catch (e) {
                err = e.message;
            }
            console.warn("Failed to compile vertex shader \"" + c + "\":\n" + err);
            program.Dispose();
            await MediaPlayerTools.SetError(err);
            return null;
        }
        if (!(await t.waitCompilation(fs, abortCheckFn))) {
            let err;
            try {
                err = gl.getShaderInfoLog(fs);
            }
            catch (e) {
                err = e.message;
            }
            console.warn("Failed to compile fragment shader \"" + c + "\":\n" + err);
            program.Dispose();
            await MediaPlayerTools.SetError(err);
            return null;
        }
        if (abortCheckFn()) {
            program.Dispose();
            return "Aborted";
        }
		//	Output true shader 
		if (debug)
		{
			const compTool = gl.getExtension("WEBGL_debug_shaders");
			if (compTool)
			{
				console.log("### OpenGL vertex shader ###\n" + compTool.getTranslatedShaderSource(vs));
		        console.log("### OpenGL fragment shader ###\n" + compTool.getTranslatedShaderSource(fs));
			}
		}
		//  Link the program and wait for finish (or abort)
        gl.linkProgram(p);
        if (!(await t.waitLinking(p, abortCheckFn))) {
            console.warn("Failed to initialize the shader program \"" + c + "\":\n" + gl.getProgramInfoLog(p));
            program.Dispose();
            return null;
        }
        if (abortCheckFn()) {
            program.Dispose();
            return "Aborted";
        }
        program.CompilationTime = performance.now() - start;
        return program;
    }

    //  Validate some vars (serialized json string), returns a new string with validated params
    /**
     * Merges the supplied parameter values over the defaults and drops keys that the effect does not define.
     * @param {string} [propString] - JSON object of parameter values.
     * @returns {string|null} Validated JSON; the defaults when propString is empty; null when the effect has no parameters.
     */
    ValidateVars(propString) {
        const dt = this.ScriptDefText;
        if (!dt)
            return null;
        if (!propString)
            return dt;
        const scriptProps = JSON.parse(dt);
        const scriptDef = JSON.parse(dt);
        const ep = JSON.parse(propString);
        Object.assign(scriptProps, ep);
        Object.keys(scriptProps).forEach(function (key, index) {
            if (typeof scriptDef[key] === "undefined")
                delete scriptProps[key];
        });
        return JSON.stringify(scriptProps);
    }

    /**
     * Synthetic type-info for the generic editor (Asm "EffectProps", TypeName "EffectProps.<type_name_...>",
     * one member per annotation, Flags 512, Default = ScriptDefText), or null without annotations.
     * @type {object|null}
     */
    ScriptTypeInfo;
    /**
     * Member descriptor named "EffectProps" of the ScriptTypeInfo type (used by the parameter editor), or null.
     * @type {object|null}
     */
    ScriptMember;

    //  Special type handling
    /**
     * Supported GLSL const types (float, bool, int, vec2, vec3, vec4), each as
     * {parseDef(text, editType), member, toString(value, editType)}:
     * parseDef parses the default literal (vec3/vec4 return {Red,Green,Blue[,Alpha]} for edit types "col"/"colhdr",
     * else {x,y,z[,w]}; missing components repeat the last one), member is the editor member template, and
     * toString produces the GLSL literal (missing vector components default to 1.0).
     * @type {Map<string, {parseDef:function(string,string):*, member:object, toString:function(*,string):string}>}
     */
    static TypeMap = (() => {
        const typeMap = new Map();
        /**
         * Formats a number as a GLSL float literal.
         * @param {number} v - Value.
         * @returns {string} Number text with ".0" appended when it has no decimal point.
         */
        function toFloat(v) {
            const t = "" + v;
            if (t.indexOf('.') < 0)
                return t + ".0";
            return t;
        }
        typeMap.set("float",
            {
                parseDef: v => parseFloat(v),
                member:
                {
                    Name: null,
                    TypeName: "System.Single",
                    ElementTypeName: null,
                    Ext: "https://learn.microsoft.com/en-us/dotnet/api/system.single?view=net-8.0",
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "0",
                    Flags: 256,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: toFloat,
            });
        typeMap.set("bool",
            {
                parseDef: v => v === "true",
                member:
                {
                    Name: null,
                    TypeName: "System.Boolean",
                    ElementTypeName: null,
                    Ext: "https://learn.microsoft.com/en-us/dotnet/api/system.boolean?view=net-8.0",
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "false",
                    Flags: 256,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: v => v ? "true" : "false",
            });
        typeMap.set("int",
            {
                parseDef: v => parseInt(v),
                member:
                {
                    Name: null,
                    TypeName: "System.Int32",
                    ElementTypeName: null,
                    Ext: "https://learn.microsoft.com/en-us/dotnet/api/system.int32?view=net-8.0",
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "0",
                    Flags: 256,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: v => "" + (v | 0),
            });
        typeMap.set("vec2",
            {
                parseDef: (v, editType) => {
                    const sf = v.indexOf('(');
                    const se = v.lastIndexOf(')');
                    const vals = v.substring(sf + 1, se).split(',');
                    const vl = vals.length;
                    return {
                        x: parseFloat(vals[0]),
                        y: parseFloat(vals[vl > 1 ? 1 : (vl - 1)]),
                    };
                },
                member:
                {
                    Name: null,
                    TypeName: "glsl.vec2",
                    ElementTypeName: null,
                    Ext: null,
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "{x:0.0,y:0.0}",
                    Flags: 512,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: v => "vec2(" + toFloat(v.x) + "," + toFloat(v.y) + ")",
            });
        typeMap.set("vec3",
            {
                parseDef: (v, editType) => {
                    const sf = v.indexOf('(');
                    const se = v.lastIndexOf(')');
                    const vals = v.substring(sf + 1, se).split(',');
                    const vl = vals.length;
                    if ((editType === "colhdr") || (editType === "col"))
                        return {
                            Red: parseFloat(vals[0]),
                            Green: parseFloat(vals[vl > 1 ? 1 : (vl - 1)]),
                            Blue: parseFloat(vals[vl > 2 ? 2 : (vl - 1)]),
                        };
                    return {
                        x: parseFloat(vals[0]),
                        y: parseFloat(vals[vl > 1 ? 1 : (vl - 1)]),
                        z: parseFloat(vals[vl > 2 ? 2 : (vl - 1)]),
                    };
                },
                member:
                {
                    Name: null,
                    TypeName: "glsl.vec3",
                    ElementTypeName: null,
                    Ext: null,
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "{x:0.0,y:0.0,z:0.0}",
                    Flags: 512,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: (v, editType) => {
                    if ((editType === "colhdr") || (editType === "col"))
                        return "vec3(" + toFloat(v.Red ?? 1.0) + "," + toFloat(v.Green ?? 1.0) + "," + toFloat(v.Blue ?? 1.0) + ")";
                    return "vec3(" + toFloat(v.x ?? 1.0) + "," + toFloat(v.y ?? 1.0) + "," + toFloat(v.z ?? 1.0) + ")";
                },
            });
        typeMap.set("vec4",
            {
                parseDef: (v, editType) => {
                    const sf = v.indexOf('(');
                    const se = v.lastIndexOf(')');
                    const vals = v.substring(sf + 1, se).split(',');
                    const vl = vals.length;
                    if ((editType === "colhdr") || (editType === "col"))
                        return {
                            Red: parseFloat(vals[0]),
                            Green: parseFloat(vals[vl > 1 ? 1 : (vl - 1)]),
                            Blue: parseFloat(vals[vl > 2 ? 2 : (vl - 1)]),
                            Alpha: parseFloat(vals[vl > 3 ? 3 : (vl - 1)]),
                        };
                    return {
                        x: parseFloat(vals[0]),
                        y: parseFloat(vals[vl > 1 ? 1 : (vl - 1)]),
                        z: parseFloat(vals[vl > 2 ? 2 : (vl - 1)]),
                        w: parseFloat(vals[vl > 3 ? 3 : (vl - 1)]),
                    };
                },
                member:
                {
                    Name: null,
                    TypeName: "glsl.vec4",
                    ElementTypeName: null,
                    Ext: null,
                    DisplayName: null,
                    Min: null,
                    Max: null,
                    Default: "{x:0.0,y:0.0,z:0.0,w:0.0}",
                    Flags: 512,
                    EditParams: null,
                    Summary: null,
                    Remarks: null,
                    Type: null,
                    KeyInst: null,
                    ElementInst: null
                },
                toString: (v, editType) => {
                    if ((editType === "colhdr") || (editType === "col"))
                        return "vec4(" + toFloat(v.Red ?? 1.0) + "," + toFloat(v.Green ?? 1.0) + "," + toFloat(v.Blue ?? 1.0) + "," + toFloat(v.Alpha ?? 1.0) + ")";
                    return "vec4(" + toFloat(v.x ?? 1.0) + "," + toFloat(v.y ?? 1.0) + "," + toFloat(v.z ?? 1.0) + "," + toFloat(v.w ?? 1.0) + ")";
                },
            });
        return typeMap;
    })();
}

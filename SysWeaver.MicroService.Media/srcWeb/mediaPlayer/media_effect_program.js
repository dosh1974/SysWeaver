
// A compiled and linked program representation
class EffectProgram
{
    constructor(gl, vs, fs, program)
    {
        const t = this;
        t.VS = vs;
        t.FS = fs;
        t.Program = program;
        t.GL = gl;
    }

    Dispose()
    {
        const t = this;
        const gl = t.GL;
        gl.deleteProgram(t.Program);
        gl.deleteShader(t.FS);
        gl.deleteShader(t.VS);
    }
    GL;
    VS;
    FS;
    Program;
    CompilationTime;
}

class EffectProgramData {
    // Parses some shader source and extract variables
    constructor(gl, src, fileName) {
        const t = this;
        //  Setup async compilation
        if (typeof gl.PCompile === "undefined")
            gl.PCompile = gl.getExtension("KHR_parallel_shader_compile");
        const pcompile = gl.PCompile;
        if (pcompile) {
            t.waitCompilation = async function (shader, abortCheckFn) {
                for (; ;) {
                    await delay(5);
                    if (abortCheckFn())
                        return true;
                    if (gl.getShaderParameter(shader, pcompile.COMPLETION_STATUS_KHR))
                        break;
                }
                return gl.getShaderParameter(shader, gl.COMPILE_STATUS);
            };

            t.waitLinking = async function (prog, abortCheckFn) {
                for (; ;) {
                    await delay(5);
                    if (abortCheckFn())
                        return true;
                    if (gl.getProgramParameter(prog, pcompile.COMPLETION_STATUS_KHR))
                        break;
                }
                return gl.getProgramParameter(prog, gl.LINK_STATUS);
            };
        }
        else {
            t.waitCompilation = shader => gl.getShaderParameter(shader, gl.COMPILE_STATUS);
            t.waitLinking = prog => gl.getProgramParameter(prog, gl.LINK_STATUS);
        }
        //  Parse and find variables
        const typeMap = EffectProgramData.TypeMap;
        const lines = src.split("\n");
        t.Lines = lines;
        const lc = lines.length;
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
        function isSpace(line, pos, falseOutSize) {
            if ((pos < 0) || (pos >= lc))
                return !falseOutSize;
            return line.charAt(pos).trim() === "";
        }

        function isIdentifier(line, pos) {
            if ((pos < 0) || (pos >= lc))
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
        function haveIdentifier(line, name) {
            const ti = line.indexOf(name);
            if (ti < 0)
                return false;
            if (isIdentifier(line, ti - 1))
                return false;
            if (isIdentifier(line, ti + name.length))
                return false;
            const c = line.indexOf("//");
            if ((c >= 0) && (c < ti))
                return false;
            return true;
        }

        function haveSpaceIdentifier(line, name) {
            const ti = line.indexOf(name);
            if (ti < 0)
                return false;
            if (!isSpace(line, ti - 1))
                return false;
            if (isIdentifier(line, ti + name.length))
                return false;
            const c = line.indexOf("//");
            if ((c >= 0) && (c < ti))
                return false;
            return true;
        }


        for (let i = 0; i < lc; ++i) {
            const line = lines[i].trim();
            lines[i] = line;
            if (!line.startsWith("const")) {

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
                }
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
                const desc = json.length > 0 ? JSON.parse(json) : {};
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
            t.ScriptPropsApply = () => { };
            t.ScriptDefText = null;
            t.ScriptTypeInfo = null;
            t.ScriptMember = null;
        }
        t.GL = gl;
        t.Prefix = "";
        t.Suffix = "";
        if (haveDer && (!haveDerExt)) {
            t.Prefix +=
                `#extension GL_OES_standard_derivatives : enable
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
    }

    // Compiles the program using some vars

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
        //  Compile shaders
        gl.shaderSource(fs, fsSrc);
        gl.compileShader(fs);
        gl.shaderSource(vs, vsSrc);
        gl.compileShader(vs);
        //  Wait for compilation to finish (or abort)
        if (!(await t.waitCompilation(vs, abortCheckFn))) {
            console.warn("Failed to compile vertex shader \"" + c + "\":\n" + gl.getShaderInfoLog(vs));
            program.Dispose();
            return null;
        }
        if (!(await t.waitCompilation(fs, abortCheckFn))) {
            console.warn("Failed to compile fragment shader \"" + c + "\":\n" + gl.getShaderInfoLog(fs));
            program.Dispose();
            return null;
        }
        if (abortCheckFn()) {
            program.Dispose();
            return "Aborted";
        }
        //  Link the program and wait for finis (or abort)
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

    ScriptTypeInfo;
    ScriptMember;

    //  Special type handling 
    static TypeMap = (() => {
        const typeMap = new Map();
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
                        return "vec3(" + toFloat(v.Red) + "," + toFloat(v.Green) + "," + toFloat(v.Blue) + ")";
                    return "vec3(" + toFloat(v.x) + "," + toFloat(v.y) + "," + toFloat(v.z) + ")";
                },
            });
        typeMap.set("vec4",
            {
                parseDef: v => {
                    const sf = v.indexOf('(');
                    const se = v.lastIndexOf(')');
                    const vals = v.substring(sf + 1, se).split(',');
                    const vl = vals.length;
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
                toString: v => "vec4(" + toFloat(v.x) + "," + toFloat(v.y) + "," + toFloat(v.z) + "," + toFloat(v.w) + ")",
            });
        return typeMap;
    })();
}

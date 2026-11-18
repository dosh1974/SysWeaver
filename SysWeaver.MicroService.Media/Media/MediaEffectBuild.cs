using System;

namespace SysWeaver.MicroService.Media
{
    /// <summary>
    /// An effect (a .glsl source) to build for the media player (see MediaService.BuildEffect)
    /// </summary>
    public sealed class MediaEffectBuildRequest
    {
        /// <summary>
        /// The effect source (the content of the .glsl file)
        /// </summary>
        public String Source;

        /// <summary>
        /// The name of the effect (file name or url), used in messages
        /// </summary>
        public String Name;

        /// <summary>
        /// True to build for WebGL2 (GLSL ES 3.00), else for WebGL1 (GLSL ES 1.00)
        /// </summary>
        public bool Gl2;

        /// <summary>
        /// Keep the alpha output of mainImage (a transparent effect, premultiplied), else the output is opaque
        /// </summary>
        public bool Alpha;
    }

    /// <summary>
    /// An effect built like Effect Weaver builds it (the Ew snippets the effect calls inserted, the ShaderToy uniforms and main(), the vertex stage)
    /// </summary>
    public sealed class MediaEffectBuildResponse
    {
        /// <summary>
        /// The program source: the fragment shader, if the effect has a vertex stage followed by the line "//@ew-vertex-shader" and the vertex shader.
        /// The variables ("const ... // var:{...}" lines) keep their lines, so the player can still change them.
        /// </summary>
        public String Program;

        /// <summary>
        /// True if the program is GLSL ES 3.00 (needs WebGL2)
        /// </summary>
        public bool Es3;

        /// <summary>
        /// True if the program has a vertex stage
        /// </summary>
        public bool Vertex;

        /// <summary>
        /// The default values of the effect's controls (iMouse x, y, z and w, in [0, 1]), always 4 values
        /// </summary>
        public double[] Controls;

        /// <summary>
        /// The names of the controls (iMouse x, y, z and w, from the effect's "// iMouse.x = Name | Description | Default" comments), "" if unknown
        /// </summary>
        public String[] ControlNames;

        /// <summary>
        /// The controls the effect describes (iMouse x, y, z and w, from its "// iMouse.x = Name | Description | Default | Min .. Max | Unit"
        /// comments), always 4 entries, null for a channel the effect doesn't describe
        /// </summary>
        public MediaEffectControl[] ControlDefs;
    }

    /// <summary>
    /// A live control of an effect: a value in [0, 1] the effect reads (iMouse x, y, z or w), described by the effect
    /// </summary>
    public sealed class MediaEffectControl
    {
        /// <summary>
        /// The name of the control
        /// </summary>
        public String Name;

        /// <summary>
        /// The description of the control, "" if none
        /// </summary>
        public String Desc;

        /// <summary>
        /// The default value [0, 1]
        /// </summary>
        public double Default;

        /// <summary>
        /// True if the effect's code reads the control
        /// </summary>
        public bool Used;

        /// <summary>
        /// The value the control stands for, null if it is just 0..1: Min at 0, Max at 1, with its Unit or display Format ("{0} BPM")
        /// (Sw.PropertyEditor's Map)
        /// </summary>
        public MediaEffectControlMap Map;
    }

    /// <summary>
    /// The value a 0..1 control stands for (see <see cref="MediaEffectControl.Map"/>)
    /// </summary>
    public sealed class MediaEffectControlMap
    {
        /// <summary>
        /// The value at 0
        /// </summary>
        public double Min;

        /// <summary>
        /// The value at 1
        /// </summary>
        public double Max;

        /// <summary>
        /// The unit of the value ("BPM"), null if none
        /// </summary>
        public String Unit;

        /// <summary>
        /// The display template of the value ("{0} BPM", wins over Unit), null if none
        /// </summary>
        public String Format;
    }
}

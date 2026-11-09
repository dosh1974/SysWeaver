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
    }
}

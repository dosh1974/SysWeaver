using System;
using System.IO;
using System.Linq;

namespace SysWeaver.AI
{
    /// <summary>
    /// The details of a loaded (analyzed) workflow of the task methods, see <see cref="ComfyTaskWorkflow"/>
    /// </summary>
    public sealed class ComfyWorkflowInfo
    {
        /// <summary>
        /// Ex: "image_t2i" TextToImage (FLUX.2): Prompt, Seed, Width, Height
        /// </summary>
        public override string ToString() => String.Concat(Name.ToQuoted(), " ", Type.ToString(), Model == null ? "" : (" (" + Model + ")"), ": ", String.Join(", ", Parameters ?? []));

        /// <summary>
        /// The workflow name (the file name without extension), use as the Workflow parameter
        /// </summary>
        public String Name;

        /// <summary>
        /// The task that the workflow performs
        /// </summary>
        public ComfyTaskTypes Type;

        /// <summary>
        /// True if this is the default workflow for the task (used when no workflow is specified)
        /// </summary>
        public bool IsDefault;

        /// <summary>
        /// The model (family) that the workflow uses, ex: "FLUX.2", null if not recognized
        /// </summary>
        public String Model;

        /// <summary>
        /// The parameters that the workflow supports (respects), ex: "Prompt", "Width", "Steps"; other parameters are ignored
        /// </summary>
        public String[] Parameters;

        /// <summary>
        /// The number of reference files (load image / audio nodes) that the workflow uses
        /// </summary>
        public int FileCount;

        /// <summary>
        /// The model files used by the workflow
        /// </summary>
        public String[] ModelFiles;

        /// <summary>
        /// The LoRA files used by the workflow
        /// </summary>
        public String[] Loras;

        /// <summary>
        /// Text that is added before the prompt (ex: a LoRA trigger), null if none
        /// </summary>
        public String PromptPrefix;

        /// <summary>
        /// Text that is added after the prompt, null if none
        /// </summary>
        public String PromptSuffix;

        /// <summary>
        /// The default (positive) prompt of the workflow, null if none
        /// </summary>
        public String DefaultPrompt;

        /// <summary>
        /// The default negative prompt of the workflow, null if none
        /// </summary>
        public String DefaultNegativePrompt;

        /// <summary>
        /// The id of the output node
        /// </summary>
        public String OutputNode;

        /// <summary>
        /// True if a size change uses a separate (sized) variant of the workflow, with links replaced by values and switches flipped
        /// </summary>
        public bool SizedVariant;

        /// <summary>
        /// The names of the annotated workflow(s) uploaded to the ComfyUI-Connect server
        /// </summary>
        public String[] ServerNames;

        /// <summary>
        /// How the parameters are mapped to the node inputs
        /// </summary>
        public String[] Mappings;

        /// <summary>
        /// The node classes used by the workflow
        /// </summary>
        public String[] NodeClasses;

        /// <summary>
        /// A system prompt for an LLM that rewrites a user request into a prompt optimized for this workflow
        /// </summary>
        [EditMultiline]
        public String PromptEnhancement;

        /// <summary>
        /// The full file name of the workflow, null if not loaded from a file
        /// </summary>
        public String FileName;

        /// <summary>
        /// The size of the workflow file in bytes (0 if not known)
        /// </summary>
        public long FileSize;

        /// <summary>
        /// When the workflow file was last changed (UTC)
        /// </summary>
        public DateTime FileChanged;

        /// <summary>
        /// Get the details of a workflow
        /// </summary>
        /// <param name="comfy">The service (to know the default workflow of the task)</param>
        /// <param name="w">The workflow</param>
        /// <returns>The details</returns>
        public static ComfyWorkflowInfo From(ComfyUiService comfy, ComfyTaskWorkflow w)
        {
            ArgumentNullException.ThrowIfNull(w);
            FileInfo fi = null;
            try
            {
                if (w.FileName != null)
                    fi = new FileInfo(w.FileName);
            }
            catch
            {
            }
            var exists = fi?.Exists ?? false;
            return new ComfyWorkflowInfo
            {
                Name = w.Name,
                Type = w.Type,
                IsDefault = String.Equals(comfy?.GetDefaultWorkflow(w.Type), w.Name, StringComparison.OrdinalIgnoreCase),
                Model = w.ModelName,
                Parameters = w.Parameters.ToArray(),
                FileCount = w.FileCount,
                ModelFiles = w.ModelFiles.ToArray(),
                Loras = w.Loras.ToArray(),
                PromptPrefix = w.PromptPrefix,
                PromptSuffix = w.PromptSuffix,
                DefaultPrompt = w.DefaultPrompt,
                DefaultNegativePrompt = w.DefaultNegativePrompt,
                OutputNode = w.OutputNode,
                SizedVariant = w.SizedWorkflow != null,
                ServerNames = w.SizedWorkflow == null ? [w.Workflow.Name] : [w.Workflow.Name, w.SizedWorkflow.Name],
                Mappings = w.Mappings.ToArray(),
                NodeClasses = w.NodeClasses.ToArray(),
                PromptEnhancement = w.GetPromptEnhancement(),
                FileName = w.FileName,
                FileSize = exists ? fi.Length : 0,
                FileChanged = exists ? fi.LastWriteTimeUtc : default,
            };
        }
    }

    /// <summary>
    /// The default workflow of a task type
    /// </summary>
    public sealed class ComfyTaskDefault
    {
        /// <summary>
        /// Ex: TextToImage: "image_t2i" (2 workflows)
        /// </summary>
        public override string ToString() => String.Concat(Type.ToString(), ": ", Workflow == null ? "none" : Workflow.ToQuoted(), " (", Count.ToString(), Count == 1 ? " workflow)" : " workflows)");

        /// <summary>
        /// The task type
        /// </summary>
        public ComfyTaskTypes Type;

        /// <summary>
        /// The name of the default workflow (used when no workflow is specified), null if there is no workflow for the task
        /// </summary>
        public String Workflow;

        /// <summary>
        /// The number of workflows that performs the task
        /// </summary>
        public int Count;
    }
}

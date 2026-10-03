using System;

namespace SysWeaver.AI
{
    /// <summary>
    /// Parameters for the ComfyUI service (using the ComfyUI-Connect REST API).
    /// See: https://github.com/Good-Dream-Studio/ComfyUI-Connect
    /// The (optional) ApiKey is sent as an "Authorization" header (use when ComfyUI is behind an authenticating proxy).
    /// </summary>
    public sealed class ComfyUiParams : ApiKeyParams
    {
        #region API setup

        /// <summary>
        /// The ComfyUI end point (base url), ex: "http://127.0.0.1:8188"
        /// </summary>
        public String EndPoint = "http://127.0.0.1:8188";

        /// <summary>
        /// The authorization scheme used when an api key is supplied
        /// </summary>
        public String AuthScheme = "Bearer";

        /// <summary>
        /// Optional ComfyUI token, sent as "_token" in the payload.
        /// ComfyUI-Connect uses this token when talking to ComfyUI (needed if ComfyUI requires a token, ex: comfyui-login).
        /// </summary>
        public String ComfyUiToken;

        /// <summary>
        /// The network time out in seconds (a workflow request doesn't return until the workflow has completed)
        /// </summary>
        public int NetworkTimeoutSeconds = 10 * 60;

        #endregion// API setup

        #region Workflows

        /// <summary>
        /// The folder where the workflows are stored (API format json files, as saved by "Workflow > Save API Endpoint" or "Export (API)").
        /// The workflow name is the file name without the ".json" extension.
        /// Variables can be used and with "$(" and ends with ")", ex: "$(ExecutableDir)/ComfyUi".
        /// </summary>
        public String WorkflowFolder = "ComfyUiWorkflows";

        /// <summary>
        /// If true, a workflow is uploaded (saved) to the ComfyUI-Connect server the first time it's used.
        /// If false, the workflow must already exist on the server (with the same name).
        /// </summary>
        public bool UploadWorkflows = true;

        #endregion// Workflows

        #region Subgraphs

        /// <summary>
        /// If true, all inputs of (top level) subgraphs are annotated automatically when a workflow is loaded.
        /// The API format doesn't contain the subgraphs (they are flattened), so the UI format workflow (with the same name) is required, see UiWorkflowFolder and FetchUiWorkflows.
        /// Each subgraph input becomes a tag named from the label (or name) of the subgraph input, ex: a subgraph input labeled "seed" can be set using a property named "Seed".
        /// </summary>
        public bool AnnotateSubgraphInputs = true;

        /// <summary>
        /// Optional folder with UI format workflows (as saved by ComfyUI using "Workflow > Save"), the file name must match the API format workflow.
        /// Variables can be used and with "$(" and ends with ")".
        /// If null or if the file doesn't exist, the UI workflow is fetched from the ComfyUI server if FetchUiWorkflows is true.
        /// </summary>
        public String UiWorkflowFolder;

        /// <summary>
        /// If true, the UI format workflow is fetched from the ComfyUI server (the user's saved workflows, "workflows/[name].json"), unless found in the UiWorkflowFolder.
        /// </summary>
        public bool FetchUiWorkflows = true;

        #endregion// Subgraphs
    }

}

using System;
using System.Collections.Generic;

namespace SysWeaver
{
    /// <summary>
    /// Defines some roles that should be used as defautl auth requirements
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// API's that should only be used during development of the back-end service.
        /// </summary>
        public const String Debug = "Debug";

        /// <summary>
        /// API's that an admin should be able to access, think of this as a non-technical repsonible person.
        /// </summary>
        public const String Admin = "Admin," + Debug;


        /// <summary>
        /// API's that a front-end or service cosumer developer should have access to.
        /// </summary>
        public const String Dev = "Dev," + Debug;

        /// <summary>
        /// API's that a op-manager (it-technician) should have access to.
        /// </summary>
        public const String Ops = "Ops," + Debug;

        /// <summary>
        /// API's that is intended to be consumed by some service
        /// </summary>
        public const String Service = "Service," + Debug + "," + Dev;

        /// <summary>
        /// Combined Admin and Ops
        /// </summary>
        public const String AdminOps = Admin + ",Ops";

        /// <summary>
        /// Combined Ops and Dev
        /// </summary>
        public const String OpsDev = Ops + ",Dev";

        /// <summary>
        /// Combined Dev and Admin
        /// </summary>
        public const String DevAdmin = "Dev," + Admin;

        /// <summary>
        /// Combined Dev, Admin and Ops
        /// </summary>
        public const String DevAdminOps = "Dev," + AdminOps;

        /// <summary>
        /// This will disable anyone from accessing the API
        /// </summary>
        public const String Disabled = "-";


        public static readonly IReadOnlyList<String> DebugTokens = Debug.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> AdminTokens = Admin.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> DevTokens = Dev.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> OpsTokens = Ops.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> AdminOpsTokens = AdminOps.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> OpsDevTokens = OpsDev.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> DevAdminTokens = DevAdmin.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        public static readonly IReadOnlyList<String> DevAdminOpsTokens = DevAdminOps.FastToLower().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    }

}

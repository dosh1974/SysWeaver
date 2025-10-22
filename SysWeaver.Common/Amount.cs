using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// This will disable anyone from accessing the API
        /// </summary>
        public const String Disabled = "-";

        /// <summary>
        /// Combined Admin and Ops
        /// </summary>
        public const String AdminOps = Admin + ",Ops";

        /// <summary>
        /// Combined Ops and Dev
        /// </summary>
        public const String OpsDev = Ops + ",Dev";


    }


    /// <summary>
    /// Use to specify the order (priority) of embedded resources (when serving them as files)
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly,  AllowMultiple = false)]
    public sealed class ResourceOrderAttribute : Attribute
    {
        /// <summary>
        /// Use to specify the order (priority) of embedded resources (when serving them as files)
        /// </summary>
        /// <param name="order">A higher value gives it priority over the same reosurce in some other assembly with a lower order</param>
        public ResourceOrderAttribute(double order)
        {
            Order = order;
        }

        /// <summary>
        /// The order (priority) of resources in this assembly
        /// </summary>
        public readonly double Order;
    }


    /// <summary>
    /// Represents an amount (value / currency pair)
    /// </summary>
    public sealed class Amount
    {
        public override string ToString() => String.Join(' ', Currency, Value.ToString("### ### ### ### ### ##0.#######", CultureInfo.InvariantCulture));

        /// <summary>
        /// The value part of the amount
        /// </summary>
        public Decimal Value;

        /// <summary>
        /// The ISO-4217 currency code
        /// </summary>
        public String Currency;
    }

}

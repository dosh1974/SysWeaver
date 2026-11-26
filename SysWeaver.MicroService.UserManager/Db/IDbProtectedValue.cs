using System;

namespace SysWeaver.MicroService.Db
{
    /// <summary>
    /// A row that stores one protected personal value (e-mail address, phone number, ...) of a user: a lookup hash (the primary key), a per row random value
    /// and the encrypted value, see <see cref="SysWeaver.Auth.DataProtection"/> and <see cref="UserManagerService"/> (ProtectedValues).
    /// To add a new kind of value: a new table class implementing this interface, and a kind string + normalization.
    /// </summary>
    public interface IDbProtectedValue
    {
        /// <summary>
        /// The lookup hash of the normalized value, lower case hex
        /// </summary>
        String Key { get; set; }

        /// <summary>
        /// The id of the user that has the value
        /// </summary>
        long UserId { get; set; }

        /// <summary>
        /// The random value of the row
        /// </summary>
        Byte[] Rnd { get; set; }

        /// <summary>
        /// The encrypted value
        /// </summary>
        Byte[] Enc { get; set; }

        /// <summary>
        /// The plain value (not stored)
        /// </summary>
        String Value { get; set; }
    }
}

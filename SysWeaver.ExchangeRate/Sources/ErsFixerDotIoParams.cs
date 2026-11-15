namespace SysWeaver.ExchangeRate.Sources
{
    /// <summary>
    /// Parameters for the <see cref="ErsFixerDotIo"/> rate source
    /// </summary>
    public sealed class ErsFixerDotIoParams : ApiKeyParams
    {
        /// <summary>
        /// Number of minutes between each refresh (at least 1).
        /// Defaults to 480 (8 hours).
        /// </summary>
        public int RefreshMinutes = 8 * 60;

        /// <summary>
        /// The reference currency to use as a three letter ISO 4217 reference currency code.
        /// EUR is the only option for FREE plans.
        /// Defaults to "EUR".
        /// </summary>
        public string Reference = "EUR";

    }




}

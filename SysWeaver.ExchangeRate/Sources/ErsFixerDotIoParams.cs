namespace SysWeaver.ExchangeRate.Sources
{
    public sealed class ErsFixerDotIoParams : ApiKeyParams
    {
        /// <summary>
        /// Number of minutes between each refresh
        /// </summary>
        public int RefreshMinutes = 8 * 60;

        /// <summary>
        /// The reference currency to use as a three letter ISO 4217 reference currency code.
        /// EUR is the only option for FREE plans.
        /// </summary>
        public string Reference = "EUR";

    }




}

using System;
using System.Threading.Tasks;

namespace SysWeaver.ExchangeRate
{
    /// <summary>
    /// A source of exchange rates
    /// </summary>
    public interface IRateSource
    {
        /// <summary>
        /// The name of the source (typically the address of it)
        /// </summary>
        String Source { get;  }

        /// <summary>
        /// The number of minutes between each refresh of the rates
        /// </summary>
        int RefreshMinutes { get; }

        /// <summary>
        /// Get the current rates from the source
        /// </summary>
        /// <returns>The rates</returns>
        Task<Rates> GetRates();

    }

}

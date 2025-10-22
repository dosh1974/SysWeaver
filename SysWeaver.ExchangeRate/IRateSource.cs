using System;
using System.Threading.Tasks;

namespace SysWeaver.ExchangeRate
{
    public interface IRateSource
    {
        String Source { get;  }

        int RefreshMinutes { get; }

        Task<Rates> GetRates();

    }

}

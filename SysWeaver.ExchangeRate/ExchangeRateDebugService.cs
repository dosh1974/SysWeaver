using SysWeaver.MicroService;

namespace SysWeaver.ExchangeRate
{
    [WebMenu(null, "RatesTableX", WebMenuItemTypes.Table, "Current rates", "The current exchange rates", "IconCashBill", 1, "", false, "Api/ExchangeRates/" + nameof(ExchangeRateService.RatesTable), null)]
    [WebMenu(null, "SourceTablesX", WebMenuItemTypes.Table, "Sources", "The sources for exchange rates", "IconTableCountry", 2, "", false, "Api/ExchangeRates/" + nameof(ExchangeRateService.SourcesTable), null)]
    [WebMenu(null, "AllRatesTableX", WebMenuItemTypes.Table, "All rates", "The exchange rates from all sources", "IconCashBills", 3, "", false, "Api/ExchangeRates/" + nameof(ExchangeRateService.AllRatesTable), null)]
    [RequiredDep<ExchangeRateService>]
    public sealed class ExchangeRateDebugService
    {
    }

}

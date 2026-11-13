# SysWeaver.HttpBench

[⬆ SysWeaver overview](../README.md)

> The HTTP bench service (`HttpBenchService` in this library): performance tests a SysWeaver web server: many workers call a selection of public end points for a while, then a report shows the results as SysWeaver tables and charts, from the totals down to single requests.

| | |
|---|---|
| **Kind** | Library: a service (`SysWeaver.HttpBench.HttpBenchService, SysWeaver.HttpBench`) with an embedded web page |
| **Engine** | [SysWeaver.HttpBench.Core](../SysWeaver.HttpBench.Core/README.md) (the tests run there) |
| **Host** | [HttpBenchService](https://github.com/dosh1974/SysWeaver.Services/blob/main/HttpBenchService/README.md) (the console tool [HttpBench](https://github.com/dosh1974/SysWeaver.Tools/blob/main/HttpBench/README.md) runs the same tests without a server, on Core only) |
| **Page** | `httpbench/bench.html`, menu *Debug / HTTP bench* |
| **Api** | `Api/httpbench/...` |

## Running

Add the service to the services file of a host:

```json
{
  "Type": "SysWeaver.HttpBench.HttpBenchService, SysWeaver.HttpBench",
  "Params": {
    "Auth": null
  }
}
```

[HttpBenchService](https://github.com/dosh1974/SysWeaver.Services/blob/main/HttpBenchService/README.md) is a ready host (`dotnet run --project HttpBenchService -- debug`, then <http://localhost:7480/httpbench/bench.html>). The host runs the same services as SwServerManager (except the folder sync and server manager), so it can also test itself (the workers then share the cpu with the server).

## The page

- **Target**: the root url of the server (http or https), *This server*, *Recent servers* (a menu of the last 20 tested servers: the address only, kept in this browser's localStorage), *Accept any certificate* for self signed / LAN certificates.
- **Load**: the duration (a log slider, 10 s to 120 min) and the number of workers (1 to 1024).
- **Requests**: how the apis that take a value get it (*Api values as*: **POST**, the value is the json body; **GET**, the value is the query string, see [Calling APIs](../SysWeaver.HttpServer/README.md#calling-apis)), the compression of those values (none, br, gzip, deflate: a POST body with `Content-Encoding`, a GET query string as `_b` / `_g` / `_d` + base64url; none = plain json, url encoded for GET) and of responses (`Accept-Encoding`: none, one, or all like a browser).
- **End points**: 47 end points in 7 modules (a switch per module, a chip per end point; the *Error cases* module is off by default).
- **Export** saves the form as a parameter file (json, `HttpBench_<server>.json`) and **Import** loads one into the form (in the browser, nothing is sent to the server). The file is the Start request plus `"DataType": "HttpBench"`, an import without that member is refused; missing members keep their value, missing `EndPoints` selects the modules that are on by default, unknown end points are skipped (with a warning) and the duration and workers are clamped to the limits of the service. The [HttpBench console tool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/HttpBench/README.md) reads and writes the same files.
- **GO** starts the test; while it runs: a progress bar, live KPI tiles (requests/s, response time, requests, errors) and live charts; **STOP** (confirmed) stops it.
- **Report**: facts, KPI tiles (requests, requests/s and peak, average / median / min, P95 / P99 / max, errors, bytes received and sent, time to first byte) and tabs:
  - *Charts*: requests and errors per second, response time per second (average, median, P95, P99), network traffic, the response time distribution, response time / requests per second / response size per end point, requests per module, status codes. Each is `chart/chart.html` with its menu (type, colours, export) and opens full size in a new tab.
  - Tables (`Sw.Table`: sort, filter, search, export): *End points*, *Modules*, *Per second*, *Per second and end point*, *Status codes*, *Errors* (status and the server's message), *Workers*, *Requests* (every request, or a uniform random sample of 100 000) and *Response times* (the histogram).
- **Tests**: the latest 10 tests (in memory), a row opens its report (`bench.html?run=<id>`).
- The page starts up as it was left: every setting of the form (target, duration, workers, api values as, compressions, accept any certificate, the selected end points) and the selected report tab are kept in this browser's localStorage (`SysWeaver.HttpBench.Settings`, `SysWeaver.HttpBench.Tab`); without them the form takes the settings of the latest test on the server.
- Every card (the form cards, the report and each chart) can be collapsed (the chevron or a click on its head); the state is kept per card in localStorage (`Sw.Card` `Collapsible`). Chart legends are at the bottom (clear of the chart menu button).

## How a test runs

- Every worker is one client: its own connection (one at a time, keep alive) and cookie jar, so one server session per worker, like a browser.
- Every worker calls the selected end points in the catalog order, over and over, one request at a time; worker *n* starts at end point *n*, so all end points are hit from the start.
- A request is timed from sending to the last byte of the body (and to the headers: TTFB). Bodies are read and discarded, never decompressed.
- Bytes on the wire are counted below TLS by a counting stream on the socket, so they include request / status lines, headers and TLS; body bytes are counted separately.
- Api values are encoded and compressed once at the start (the test measures the server, not the client's compression). The *Value sent* columns count the POST body or the GET query string as sent.
- Errors: no response, a time out (30 s) or a status of 400 or more; the first 200 characters of an error body (SysWeaver sends the reason as text) are kept.
- When the time is up, requests in flight get 5 s to complete; a STOP cancels them at once. Cancelled requests are not counted.
- Metrics are kept per end point and second (counters plus a latency histogram of 20 buckets per decade, percentiles about ±6 %), so the tables and charts of a test agree. Time series charts show only whole seconds; long tests are merged into at most 600 points.
- The client does as little as possible per request, so the test measures the server: everything a request needs (urls, bodies, compressed values, query strings) is prepared before the test; the headers that every request has (Accept-Encoding, User-Agent) are set once per client; no automatic decompression, redirects, proxy or tracing headers; one time out token source per worker, reset after each request.
  Each worker records its requests into counters of its own (no locks, no interlocked operations, no allocations) and merges them into the shared counters once per second (when it completes its first request of the next second, and when it ends), so the live totals lag by up to one request per worker.

## End points

Only public end points (no sign in needed; `serverTime`, the shell's clock sync, is the fastest one) of the services of SwServerManager (except FolderSync, ServerManager and the http server hosts), picked to hit as many common modules as possible. This is not a coverage test.

| Module | End points |
|---|---|
| Pages and scripts | `index.html`, `app/Home.html`, `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `common/theme.css`, `explore/table.js`, `chart/chart.js`, `edit/Edit.js`, `brand/fonts.css`, `app/manifest.json` (embedded, pre-compressed and templated files) |
| Icons and branding | `favicon.ico`, `icon.svg`, `logo.svg`, `banner.svg`, `icon-192.png`, `logo.png`, `icons/chart.svg`, `icons/api.svg` (generated app images, icon module) |
| Application and session | `serverTime`, `Api/application/GetMenu`, `GetLanguage`, `GetSupportedLanguages`, `GetLocalizedLanguages`, `Api/auth/GetUser`, `Api/auth/CanCall` (POST, 20 urls), `Api/HaveService`, `Api/GetTableExporters`, `Api/jobs/GetJobs` |
| Auth and validation | `Api/auth/GetPasswordPolicy`, `ValidateEmailAddress`, `ValidatePhoneNumber`, `GetPhoneNumberInfo`, `GetPhonePrefixInfo` (POSTed json) |
| Types and tables | `edit/GetTypeInfo` (POST), `edit/GetDefaultInstance`, `edit/GetTypeInfoTable` (POST, sorted and paged table data), `Api/GetExtensionViewer`, `Api/TableChartAvailable` |
| Explore and media | `explore`, `Api/explore`, `icons/explore` (listings built per request; public when the explore service has `"Auth": null`, as in SwServerManager), `mediaView/Api/GetBackgroundEffects`, `GetImageEffects`, `GetCollageEffects` |
| Error cases (off by default) | `invalid_url` (404 from the end of the http module chain), `Api/invalid_url` (404 from the api module): counted as errors, to measure how fast refusals are |

The catalog is `BenchCatalog.cs` in SysWeaver.HttpBench.Core.

## Configuration

`HttpBenchParams` (the `Params` of the service in the services file):

| Param | Default | |
|---|---|---|
| `Auth` | `Roles.Dev` | Who may run tests and read results (the apis and the menu item). The developer config here uses `null` (anyone): the service sends many requests to any url it is given |
| `MaxConcurrency` | 1024 | Most workers |
| `MinDurationSeconds` / `MaxDurationSeconds` | 10 / 7200 | Duration range |
| `KeepRuns` | 10 | Tests kept in memory |
| `MaxRequestSamples` | 100 000 | Single requests kept per test |
| `RequestTimeoutSeconds` | 30 | Time out of one request |
| `DrainSeconds` | 5 | Time for requests in flight at the end |

## Files

| File | |
|---|---|
| `HttpBenchService.cs` | The service: apis, run control, the table and chart apis (rows from `BenchReport` in Core), the menu item |
| `HttpBenchParams.cs` | The service parameters (and their `BenchLimits`) |
| `BenchOptions.cs` | What the page needs to build the form |
| `BenchRunRow.cs` | The row type of the list of tests |
| `BenchCharts.cs` | Chart.js configurations |
| `srcWeb/httpbench/` | `bench.html`, `bench.js`, `bench.css`, `bench.svg` (menu icon), compressed into `web/` at build time |

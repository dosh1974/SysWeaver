# SysWeaver.HttpBench.Core

[⬆ SysWeaver overview](../README.md)

> The test engine of the HTTP bench: the catalog of end points, the workers that call them, the metrics, the table rows of a report and the markdown report. No web server, service manager or web api dependencies.

| | |
|---|---|
| **Kind** | Library |
| **Depends on** | SysWeaver.Common, SysWeaver.Compression, SysWeaver.TableData |
| **Used by** | [SysWeaver.HttpBench](../SysWeaver.HttpBench/README.md) (the service and page), [HttpBench](https://github.com/dosh1974/SysWeaver.Tools/blob/main/HttpBench/README.md) (the console tool) |

## Using it

```csharp
var settings = BenchSettingsFile.Load("ServerTest.json");   // or new BenchStartRequest { Target = "https://myserver/" }
var limits = new BenchLimits();                             // time out, drain time, max workers and duration
var setup = BenchSetup.Create(settings, limits);            // validates, ArgumentException with a readable message
using var run = new BenchRun(1, setup, limits);
run.Start();
await run.WaitAsync();                                      // run.GetStatus() for live totals, run.Stop() to stop
File.WriteAllText("Report.md", BenchMarkDownReport.Create(run));
```

How a test runs (workers, timing, wire bytes, errors, the histogram) is described in [SysWeaver.HttpBench](../SysWeaver.HttpBench/README.md#how-a-test-runs).

## Parameter files

`BenchSettingsFile` reads and writes the settings of a test (`BenchStartRequest`) as json with a `"DataType": "HttpBench"` member; a file without it is refused (`InvalidDataException`). Members that are missing get their defaults, `EndPoints` missing or null = the modules that are on by default. The page exports and imports these files, the console tool reads (`-File`) and writes (`-Save`) them.

```json
{
  "DataType": "HttpBench",
  "Target": "http://localhost:7480/",
  "DurationSeconds": 60,
  "Concurrency": 16,
  "EndPoints": [ "Files.index.html", "Application.GetMenu" ],
  "SendAsGet": false,
  "RequestCompression": "",
  "ResponseCompression": "*",
  "IgnoreCertificateErrors": true
}
```

## The markdown report

`BenchMarkDownReport.Create(run)` writes the key results first (requests/s and peak, average, median, P90, P95, P99, min / max, time to first byte, requests, errors, bytes), then the parameters and the tables (exported with `MarkDownTableDataExporter`): end points, modules, status codes, errors, the timeline (at most 60 rows, seconds are merged on longer tests) and the response time distribution.

## Files

| File | |
|---|---|
| `BenchCatalog.cs` | Modules and end points |
| `BenchSettings.cs` | Settings (`BenchStartRequest`), limits, validation (`BenchSetup`), parameter files, status |
| `BenchRun.cs` | One test: workers, http clients, recording |
| `BenchStats.cs` | Counters, latency histogram, request samples, the wire byte counting stream |
| `BenchRows.cs` | Table row types |
| `BenchReport.cs` | The rows of a test (end points, modules, seconds, workers, status codes, errors, requests, histogram) |
| `BenchMarkDownReport.cs` | The markdown report |

# FastText.NetWrapper

[⬆ SysWeaver.LanguageIdentifier.FastText](../README.md)

> Source of a managed .NET wrapper around the native fastText library, compiled into this project.

| | |
|---|---|
| **Project** | [SysWeaver.LanguageIdentifier.FastText](../README.md) |
| **Kind** | Vendored library (source code, namespace `FastText.NetWrapper`) |

## Purpose

`FastTextLanguageIdentifier` uses `FastTextWrapper` to load the embedded model and run predictions. The wrapper calls the native `fasttext` library through P/Invoke (`DllImport`); the native binaries come from the `FastText.Native.Windows` and `FastText.Native.Linux` packages referenced by the project. The code refers to the fastText fork at [olegtarasov/fastText](https://github.com/olegtarasov/fastText) for its C API, and also exposes training, testing and autotune functionality that the language identifier does not use. It depends on AutoMapper and `Microsoft.Extensions.Logging.Abstractions`.

## Contents

| Item | Description |
|---|---|
| [`FastTextWrapper.cs`](FastTextWrapper.cs) | The wrapper: load a model (file or bytes), predict labels, supervised / unsupervised training, quantize, test, word and sentence vectors, nearest neighbours |
| [`FastTextWrapper.Api.cs`](FastTextWrapper.Api.cs) | Native function imports and the native argument structures |
| [`FastTextArgs.cs`](FastTextArgs.cs) | Managed training, quantization and autotune arguments, loss and model enums |
| [`Prediction.cs`](Prediction.cs) | A single label prediction (label and probability) |
| [`TestResult.cs`](TestResult.cs) | Result of a test run (global and per label metrics) |
| [`Metrics.cs`](Metrics.cs) | Per label metrics |
| [`Callbacks.cs`](Callbacks.cs) | Training and autotune progress callback delegates |
| [`NativeLibraryException.cs`](NativeLibraryException.cs) | Exception thrown on native library errors |
| [`Assembly.cs`](Assembly.cs) | `InternalsVisibleTo` attributes for `UnitTests` and `ConsoleTest` |

## Relationships

- **Part of:** [SysWeaver.LanguageIdentifier.FastText](../README.md)
- **Used by:** [`FastTextLanguageIdentifier.cs`](../FastTextLanguageIdentifier.cs)
- **Third-party:** fastText native libraries (FastText.Native.Windows, FastText.Native.Linux), AutoMapper

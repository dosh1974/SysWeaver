using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Swaps the folder of a service with a new version when the service doing the swap is the one being replaced (self update).
    /// The in-process swap (uninstall, rename, rename, start) can't be used since the process is killed by the uninstall,
    /// instead a detached OS script (cmd on Windows, bash on Linux) is generated and started, it:
    /// 1. Waits a moment (so that the http response of the request that triggered the swap is sent).
    /// 2. Pre-flight checks (folders / executables exist, backup name is free, parent is writable), aborts without any change on error.
    /// 3. Uninstalls (stops) the running service, if that doesn't finish within <see cref="Options.UninstallTimeoutSeconds"/> every process running from the active folder is killed and the swap continues.
    /// 4. Renames the active folder to the backup name, renames the new folder to the active name.
    /// 5. Starts the new version and requires it to stay running (and optionally answer a health url) for a while.
    /// On any failure in 3-5 the steps that were made are undone in reverse order and the original version is (re)started.
    /// If the folders can't be renamed back, the original version is started from the backup folder (the service must never stay down).
    /// The script deletes itself when done, the log file is kept (see <see cref="LogFilePattern"/>).
    /// The result codes (last line of the log): 0 = swapped, 1 = aborted before any change, 2 = rolled back, 3 = emergency (running from the backup folder), 4 = fatal.
    /// </summary>
    public static class SelfFolderSwap
    {
        /// <summary>
        /// The file name prefix of the scripts and logs.
        /// </summary>
        public const String FilePrefix = "SysWeaver_SelfSwap_";

        /// <summary>
        /// The search pattern of the log files (in the script folder).
        /// </summary>
        public const String LogFilePattern = FilePrefix + "*.log";

        /// <summary>
        /// Get the result of a (finished) script from it's log.
        /// </summary>
        /// <param name="log">The log text</param>
        /// <returns>The result code (0 = swapped, 1 = aborted before any change, 2 = rolled back, 3 = emergency, 4 = fatal), -1 if the script hasn't finished (or the log is invalid)</returns>
        public static int GetResult(String log)
        {
            const String key = "Finished, result ";
            var i = log?.LastIndexOf(key, StringComparison.Ordinal) ?? -1;
            if (i < 0)
                return -1;
            i += key.Length;
            if (i >= log.Length)
                return -1;
            var c = log[i] - '0';
            return (c >= 0 && c <= 9) ? c : -1;
        }

        public sealed class Options
        {
            /// <summary>Max time in seconds to wait for the running service to uninstall (stop), after that every process running from the active folder is killed and the swap continues.</summary>
            public int UninstallTimeoutSeconds = 10;
            /// <summary>Seconds to wait before doing anything (lets the http response that triggered the swap be sent).</summary>
            public int InitialDelaySeconds = 3;
            /// <summary>Number of attempts to rename a folder (one second apart), a stopped process can hold on to it's files for a while.</summary>
            public int RenameRetries = 60;
            /// <summary>The new version must report "running" (and answer the health url if set) for this many seconds, else it's rolled back.</summary>
            public int StableSeconds = 30;
            /// <summary>Number of attempts to start the original version when rolling back.</summary>
            public int StartRetries = 5;
            /// <summary>Max time in seconds that a service verb (uninstall, start, status) may take before it's killed.</summary>
            public int VerbTimeoutSeconds = 180;
            /// <summary>Optional url that the new version must answer (2xx/3xx, certificate not validated) within <see cref="StableSeconds"/>.</summary>
            public String HealthUrl;
            /// <summary>Folder to write the script and log to, defaults to <see cref="Path.GetTempPath"/>, must NOT be inside any of the swapped folders.</summary>
            public String ScriptFolder;
            /// <summary>If false, the script is only written (not started).</summary>
            public bool Launch = true;
        }

        /// <summary>
        /// Test if this process is running from the given folder (i.e. if a swap of that folder is a self update).
        /// </summary>
        public static bool IsRunningFrom(String folder)
        {
            var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
            var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            return String.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// Start a detached script that swaps the folders and restarts the service.
        /// Returns null if the script was started (the outcome is in the log file), else the exception (nothing has been changed).
        /// </summary>
        /// <param name="targetFolder">The active folder, ex: "C:\Services\ServerManager"</param>
        /// <param name="backupFolder">The name to rename the active folder to, ex: "C:\Services\ServerManager_2026-11-11_10.20.30_0000"</param>
        /// <param name="newFolder">The new version, ex: "C:\Services\ServerManager_2026-11-11_10.25.00_0000"</param>
        /// <param name="oldExe">The service executable in the active folder</param>
        /// <param name="newExe">The service executable in the new folder</param>
        /// <param name="options">Options, null for defaults</param>
        /// <param name="scriptFile">The script that was started</param>
        /// <param name="logFile">The log file that the script writes to</param>
        /// <remarks>Never throws.</remarks>
        public static Exception TryLaunch(String targetFolder, String backupFolder, String newFolder, String oldExe, String newExe, Options options, out String scriptFile, out String logFile)
        {
            scriptFile = null;
            logFile = null;
            String written = null;
            try
            {
                options ??= new Options();
                var windows = OperatingSystem.IsWindows();
                if (!(windows || OperatingSystem.IsLinux()))
                    return new PlatformNotSupportedException("Self swap is only supported on Windows and Linux");
                var cmp = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                targetFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetFolder));
                backupFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupFolder));
                newFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newFolder));
                oldExe = Path.GetFullPath(oldExe);
                newExe = Path.GetFullPath(newExe);
                //  Validate (everything is checked again by the script, this is to fail early with a good message)
                if (!Directory.Exists(targetFolder))
                    return new Exception("The active folder \"" + targetFolder + "\" doesn't exist");
                if (!Directory.Exists(newFolder))
                    return new Exception("The new folder \"" + newFolder + "\" doesn't exist");
                if (Directory.Exists(backupFolder) || File.Exists(backupFolder))
                    return new Exception("The backup folder \"" + backupFolder + "\" already exist");
                var parent = Path.GetDirectoryName(targetFolder);
                if (String.IsNullOrEmpty(parent)
                    || !String.Equals(parent, Path.GetDirectoryName(backupFolder), cmp)
                    || !String.Equals(parent, Path.GetDirectoryName(newFolder), cmp))
                    return new Exception("The active, backup and new folder must be in the same parent folder (it's a rename, not a move)");
                if (String.Equals(targetFolder, newFolder, cmp) || String.Equals(targetFolder, backupFolder, cmp) || String.Equals(newFolder, backupFolder, cmp))
                    return new Exception("The active, backup and new folder must be different");
                if (!String.Equals(Path.GetDirectoryName(oldExe), targetFolder, cmp) || !File.Exists(oldExe))
                    return new Exception("The service executable \"" + oldExe + "\" isn't in the active folder");
                if (!String.Equals(Path.GetDirectoryName(newExe), newFolder, cmp) || !File.Exists(newExe))
                    return new Exception("The service executable \"" + newExe + "\" isn't in the new folder");
                var dir = Path.GetFullPath(options.ScriptFolder ?? Path.GetTempPath());
                foreach (var x in new[] { targetFolder, newFolder, backupFolder })
                    if (dir.StartsWith(x + Path.DirectorySeparatorChar, cmp) || String.Equals(Path.TrimEndingDirectorySeparator(dir), x, cmp))
                        return new Exception("The script folder can't be inside a swapped folder (cmd re-reads the script while running)");
                Directory.CreateDirectory(dir);
                //  Generate
                var ticks = DateTime.UtcNow.Ticks;
                var name = FilePrefix + ticks.ToString(CultureInfo.InvariantCulture);
                var script = Path.Combine(dir, name + (windows ? ".cmd" : ".sh"));
                var log = Path.Combine(dir, name + ".log");
                var oldName = Path.GetFileName(oldExe);
                var newName = Path.GetFileName(newExe);
                var text = windows
                    ? CreateWindowsScript(targetFolder, backupFolder, newFolder, oldName, newName, log, ticks, options)
                    : CreateLinuxScript(targetFolder, backupFolder, newFolder, oldName, newName, script, log, ticks, options);
                //  CreateNew: never overwrite (or run) a file that someone else put there
                using (var fs = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    written = script;
                    fs.Write(new UTF8Encoding(false).GetBytes(text));
                    fs.Flush(true);
                }
                if (!windows)
                    File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                if (options.Launch)
                {
                    var ex = windows ? LaunchWindows(script, dir) : LaunchLinux(script, name);
                    if (ex != null)
                    {
                        TryDelete(script);
                        return ex;
                    }
                }
                scriptFile = script;
                logFile = log;
                return null;
            }
            catch (Exception ex)
            {
                if (written != null)
                    TryDelete(written);
                return ex;
            }
        }

        static void TryDelete(String file)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
            }
        }

        #region Launch

        static Exception LaunchWindows(String script, String dir)
        {
            //  /d = no AutoRun, /s /c ""file"" = run file (outer quotes stripped).
            //  The working directory must NOT be inside a swapped folder (an open directory handle blocks the rename).
            //  No redirection: a redirected pipe would break when this process dies.
            //  A service process isn't in a kill-on-close job, so the child outlives the service when it's stopped.
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/d /s /c \"\"" + script + "\"\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dir,
            };
            using var p = Process.Start(psi);
            if (p == null)
                return new Exception("Failed to start \"" + script + "\"");
            return null;
        }

        static String FindFile(params String[] candidates)
        {
            foreach (var x in candidates)
                if (File.Exists(x))
                    return x;
            return null;
        }

        static Exception LaunchLinux(String script, String name)
        {
            var bash = FindFile("/bin/bash", "/usr/bin/bash");
            if (bash == null)
                return new Exception("bash not found");
            var systemdRun = FindFile("/usr/bin/systemd-run", "/bin/systemd-run");
            ProcessStartInfo psi;
            if ((systemdRun != null) && Directory.Exists("/run/systemd/system"))
            {
                //  A transient unit, outside of the cgroup of this service.
                //  Required: stopping a systemd service kills every process in it's cgroup (KillMode=control-group), including children started with setsid / nohup.
                psi = new ProcessStartInfo(systemdRun);
                psi.ArgumentList.Add("--unit=" + name.ToLowerInvariant().Replace('_', '-'));
                psi.ArgumentList.Add("--description=SysWeaver self swap");
                psi.ArgumentList.Add("--collect");
                psi.ArgumentList.Add("--quiet");
                psi.ArgumentList.Add("--no-ask-password");
                psi.ArgumentList.Add("--service-type=exec");
                psi.ArgumentList.Add("--working-directory=/");
                psi.ArgumentList.Add(bash);
                psi.ArgumentList.Add(script);
            }
            else
            {
                //  No systemd (SysVinit): a new session, not killed with the service
                var setsid = FindFile("/usr/bin/setsid", "/bin/setsid");
                if (setsid == null)
                    return new Exception("Neither systemd-run nor setsid found");
                psi = new ProcessStartInfo(setsid);
                psi.ArgumentList.Add("--fork");
                psi.ArgumentList.Add(bash);
                psi.ArgumentList.Add(script);
                psi.WorkingDirectory = "/";
            }
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using var p = Process.Start(psi);
            if (p == null)
                return new Exception("Failed to start \"" + psi.FileName + "\"");
            var errTask = p.StandardError.ReadToEndAsync();
            var outTask = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(30000))
            {
                //  Unknown state: the script may or may not run, don't delete it (it removes itself)
                return null;
            }
            if (p.ExitCode != 0)
                return new Exception("\"" + psi.FileName + "\" failed with exit code " + p.ExitCode + ": " + errTask.Result + outTask.Result);
            return null;
        }

        #endregion//Launch

        #region Windows script

        /// <summary>
        /// Chars that can't be used in a path that is put into a cmd script:
        /// % and ! are expanded, ^ and " break the quoting of call arguments.
        /// All other chars (incl. &amp;, | and parentheses) are safe since every path is always quoted and the script has no parenthesized blocks.
        /// </summary>
        static void CheckCmdSafe(String value, String what)
        {
            if (value.AsSpan().IndexOfAny("%!^\"\r\n") >= 0)
                throw new Exception(what + " \"" + value + "\" contains a char that can't be used in a cmd script (% ! ^ \" or a line break)");
        }

        static String WinName(String path) => path.Substring(path.LastIndexOf('\\') + 1);
        static String WinParent(String path) => path.Substring(0, path.LastIndexOf('\\'));

        static String Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Create the content of the Windows (cmd) script, see <see cref="SelfFolderSwap"/>.
        /// </summary>
        public static String CreateWindowsScript(String targetFolder, String backupFolder, String newFolder, String oldExeName, String newExeName, String logFile, long ticks, Options o)
        {
            CheckCmdSafe(targetFolder, "Active folder");
            CheckCmdSafe(backupFolder, "Backup folder");
            CheckCmdSafe(newFolder, "New folder");
            CheckCmdSafe(oldExeName, "Executable");
            CheckCmdSafe(newExeName, "Executable");
            CheckCmdSafe(logFile, "Log file");
            CheckCmdSafe(o.HealthUrl ?? "", "Health url");
            var probe = WinParent(targetFolder) + "\\" + WinName(targetFolder) + "_SwapProbe_" + ticks.ToString(CultureInfo.InvariantCulture);
            var s = $$"""
@echo off
rem SysWeaver self swap script, generated {{new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}} UTC.
rem Swaps the active service folder with a new version, on any failure the original
rem folders are restored and the original service is restarted.
rem Deletes itself when done, the log file is kept.
rem Rules: every path is quoted, no parenthesized blocks (a path with ")" would break them),
rem errorlevel is tested right after each call (.cmd: "set" resets the errorlevel).
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul 2>&1
set "LOG={{logFile}}"
set "TARGET={{targetFolder}}"
set "TARGET_NAME={{WinName(targetFolder)}}"
set "BAK={{backupFolder}}"
set "BAK_NAME={{WinName(backupFolder)}}"
set "NEW={{newFolder}}"
set "NEW_NAME={{WinName(newFolder)}}"
set "OLD_EXE_NAME={{oldExeName}}"
set "NEW_EXE_NAME={{newExeName}}"
set "PROBE={{probe}}"
set "HEALTH_URL={{o.HealthUrl ?? ""}}"
set /a INITIAL_DELAY={{Int(o.InitialDelaySeconds)}}
set /a RETRIES={{Int(o.RenameRetries)}}
set /a STABLE={{Int(o.StableSeconds)}}
set /a START_RETRIES={{Int(o.StartRetries)}}
set /a VERB_TIMEOUT_MS={{Int(o.VerbTimeoutSeconds * 1000)}}
set /a UNINSTALL_TIMEOUT_MS={{Int(o.UninstallTimeoutSeconds * 1000)}}
set /a STATE=0
set /a RC=1
set /a V_N=0
set "ERR=unknown"
set "HAVE_PS=0"
set "CURL=%SystemRoot%\System32\curl.exe"

>>"%LOG%" echo ===== SysWeaver self swap started %DATE% %TIME% =====
>>"%LOG%" echo Active: "%TARGET%"
>>"%LOG%" echo New:    "%NEW%"
>>"%LOG%" echo Backup: "%BAK%"
powershell.exe -NoProfile -NonInteractive -Command "exit 0" >nul 2>&1
if not errorlevel 1 set "HAVE_PS=1"
if "%HAVE_PS%"=="0" call :log "WARNING: PowerShell is not usable, no verb time out and no process kill"
if %INITIAL_DELAY% GTR 0 call :sleep %INITIAL_DELAY%

rem ---- Pre-flight, nothing is changed if any of these fails ----
set "ERR=the active folder is missing"
if not exist "%TARGET%\" goto abort
set "ERR=the new folder is missing"
if not exist "%NEW%\" goto abort
set "ERR=the backup folder already exist"
if exist "%BAK%" goto abort
set "ERR=the service executable is missing in the active folder"
if not exist "%TARGET%\%OLD_EXE_NAME%" goto abort
set "ERR=the service executable is missing in the new folder"
if not exist "%NEW%\%NEW_EXE_NAME%" goto abort
set "ERR=can't create folders in the parent folder"
if exist "%PROBE%" goto abort
md "%PROBE%" >>"%LOG%" 2>&1
if not exist "%PROBE%\" goto abort
rd "%PROBE%" >>"%LOG%" 2>&1
if exist "%PROBE%" goto abort
call :log "Pre-flight ok"

rem ---- Swap ----
call :log "Step 1: uninstall (stop) the running service"
set /a STATE=1
set "ERR=failed to uninstall the running service"
call :verb "%TARGET%\%OLD_EXE_NAME%" uninstall %UNINSTALL_TIMEOUT_MS%
set /a U_RC=%ERRORLEVEL%
if %U_RC%==0 goto step2
if not %U_RC%==124 goto recover
call :log "The uninstall didn't finish in time, killing the service"
call :kill_in "%TARGET%"

:step2
call :log "Step 2: rename the active folder to the backup name"
set "ERR=failed to rename the active folder to the backup name"
call :ren_dir "%TARGET%" "%BAK_NAME%" "%BAK%"
if errorlevel 1 goto recover
set /a STATE=2

call :log "Step 3: rename the new folder to the active name"
set "ERR=failed to rename the new folder to the active name"
call :ren_dir "%NEW%" "%TARGET_NAME%" "%TARGET%"
if errorlevel 1 goto recover
set /a STATE=3

call :log "Step 4: start the new version"
set /a STATE=4
set "ERR=failed to start the new version"
call :verb "%TARGET%\%NEW_EXE_NAME%" start
if errorlevel 1 goto recover
set "ERR=the new version didn't stay running / healthy"
call :verify_stable "%TARGET%\%NEW_EXE_NAME%" %STABLE% 1
if errorlevel 1 goto recover
set /a RC=0
call :log "SUCCESS: the new version is running, the previous version is kept as the backup folder"
goto done

rem ---- Failure handling ----
:abort
set /a RC=1
call :log "ABORTED before any change: %ERR%"
goto done

:recover
set /a RC=2
call :log "FAILED: %ERR%"
call :log "Rolling back from state %STATE%"
if %STATE% LSS 4 goto recover_3
call :verb "%TARGET%\%NEW_EXE_NAME%" uninstall %UNINSTALL_TIMEOUT_MS%
call :kill_in "%TARGET%"
:recover_3
if %STATE% LSS 3 goto recover_2
call :ren_dir "%TARGET%" "%NEW_NAME%" "%NEW%"
if errorlevel 1 goto emergency
:recover_2
if %STATE% LSS 2 goto recover_1
call :ren_dir "%BAK%" "%TARGET_NAME%" "%TARGET%"
if errorlevel 1 goto emergency
:recover_1
call :start_verified "%TARGET%\%OLD_EXE_NAME%"
if errorlevel 1 goto fatal
call :log "ROLLED BACK: the folders are restored and the original version is running"
goto done

:emergency
set /a RC=3
call :log "EMERGENCY: can't restore the folder names, starting the original version from the backup folder"
call :verb "%BAK%\%OLD_EXE_NAME%" uninstall %UNINSTALL_TIMEOUT_MS%
call :start_verified "%BAK%\%OLD_EXE_NAME%"
if errorlevel 1 goto fatal
call :log "EMERGENCY: the original version is running from the backup folder, MANUAL CLEAN UP REQUIRED"
goto done

:fatal
set /a RC=4
call :log "FATAL: the original version could not be started, MANUAL INTERVENTION REQUIRED"
goto done

:done
>>"%LOG%" echo [%DATE% %TIME%] ===== Finished, result %RC% [0=swapped 1=aborted, unchanged 2=rolled back 3=emergency 4=fatal] =====
(goto) 2>nul & del /f /q "%~f0"

rem ---- Sub routines ----

:log
>>"%LOG%" echo [%DATE% %TIME%] %~1
exit /b 0

:sleep
set /a SL=%~1+1
ping -n %SL% 127.0.0.1 >nul 2>&1
exit /b 0

rem Run a service verb: %1 = exe, %2 = verb, %3 = optional time out in ms. Returns the exit code (124 = timed out and killed).
:verb
set "V_EXE=%~1"
set "V_VERB=%~2"
set /a V_TIMEOUT_MS=%VERB_TIMEOUT_MS%
if not "%~3"=="" set /a V_TIMEOUT_MS=%~3
set /a V_N+=1
set "V_OUT=%LOG%.%V_N%.out"
>>"%LOG%" echo [%DATE% %TIME%] Running "%V_EXE%" %V_VERB%
if "%HAVE_PS%"=="1" goto verb_ps
pushd "%~dp1"
"%V_EXE%" %V_VERB% >>"%LOG%" 2>&1
set /a V_RC=%ERRORLEVEL%
popd
goto verb_done
:verb_ps
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$o=$env:V_OUT; $p=Start-Process -FilePath $env:V_EXE -ArgumentList $env:V_VERB -WorkingDirectory (Split-Path -LiteralPath $env:V_EXE) -NoNewWindow -PassThru -RedirectStandardOutput $o -RedirectStandardError ($o+'.err'); $null=$p.Handle; if(-not $p.WaitForExit([int]$env:V_TIMEOUT_MS)){ try{$p.Kill()}catch{}; exit 124 }; exit $p.ExitCode" >>"%LOG%" 2>&1
set /a V_RC=%ERRORLEVEL%
if exist "%V_OUT%" type "%V_OUT%" >>"%LOG%" 2>nul
if exist "%V_OUT%.err" type "%V_OUT%.err" >>"%LOG%" 2>nul
del /f /q "%V_OUT%" "%V_OUT%.err" >nul 2>&1
:verb_done
>>"%LOG%" echo [%DATE% %TIME%]   exit code %V_RC%
exit /b %V_RC%

rem Rename a folder with retries: %1 = folder, %2 = new name, %3 = new full path. 0 = ok, 1 = failed, 2 = destination exist.
:ren_dir
set /a TRY=0
:ren_dir_loop
if exist "%~3" call :log "Rename refused, the destination already exist" & exit /b 2
ren "%~1" "%~2" >>"%LOG%" 2>&1
if exist "%~3\" if not exist "%~1" goto ren_dir_ok
set /a TRY+=1
if %TRY% GEQ %RETRIES% call :log "Rename failed, giving up" & exit /b 1
call :log "Rename attempt %TRY% failed, retrying"
if %TRY%==5 call :kill_in "%~1"
call :sleep 1
goto ren_dir_loop
:ren_dir_ok
>>"%LOG%" echo [%DATE% %TIME%] Renamed "%~1" to "%~2"
exit /b 0

rem Kill all processes whose executable (or an argument, ex: dotnet C:\x\x.dll) is inside a folder: %1 = folder.
rem An argument matches if the folder path is at the start of the command line or after a white space or quote ("/" is treated as "\").
:kill_in
if "%HAVE_PS%"=="0" exit /b 0
set "KILL_DIR=%~1"
>>"%LOG%" echo [%DATE% %TIME%] Killing processes running from "%KILL_DIR%"
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$d=$env:KILL_DIR.TrimEnd('\')+'\'; $r='(^|[\s'+[char]34+'])'+[regex]::Escape($d); Get-CimInstance Win32_Process | Where-Object { ($_.ProcessId -ne $PID) -and (($_.ExecutablePath -and $_.ExecutablePath.StartsWith($d, [StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and [regex]::IsMatch($_.CommandLine.Replace('/','\'), $r, 'IgnoreCase'))) } | ForEach-Object { 'Killing ' + $_.ProcessId + ' ' + $_.CommandLine; Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" >>"%LOG%" 2>&1
exit /b 0

rem Require a service to stay running: %1 = exe, %2 = seconds, %3 = 1 to also require the health url to answer.
:verify_stable
set "VS_EXE=%~1"
set /a VS_LEFT=%~2
set /a VS_RC=1
set "VS_HEALTH=1"
if "%~3"=="1" if not "%HEALTH_URL%"=="" set "VS_HEALTH=0"
if "%VS_HEALTH%"=="0" if not exist "%CURL%" set "VS_HEALTH=1" & call :log "WARNING: curl.exe not found, skipping the health url check"
:verify_stable_loop
call :sleep 2
set /a VS_LEFT-=2
call :verb "%VS_EXE%" status
set /a VS_RC=%ERRORLEVEL%
if %VS_RC%==153 goto verify_stable_next
if not %VS_RC%==0 call :log "The service is not running" & exit /b 1
if "%VS_HEALTH%"=="1" goto verify_stable_next
"%CURL%" -fsSk -m 5 -o nul "%HEALTH_URL%" >>"%LOG%" 2>&1
if not errorlevel 1 set "VS_HEALTH=1" & call :log "The health url answered"
:verify_stable_next
if %VS_LEFT% GTR 0 goto verify_stable_loop
if not %VS_RC%==0 call :log "The service is still not running" & exit /b 1
if "%VS_HEALTH%"=="0" call :log "The health url never answered" & exit /b 1
exit /b 0

rem Start a service and require it to stay running for 10 seconds, with retries: %1 = exe.
:start_verified
set "SV_EXE=%~1"
set /a SV_TRY=0
:start_verified_loop
set /a SV_TRY+=1
call :verb "%SV_EXE%" start
if errorlevel 1 goto start_verified_retry
call :verify_stable "%SV_EXE%" 10 0
if not errorlevel 1 exit /b 0
:start_verified_retry
if %SV_TRY% GEQ %START_RETRIES% exit /b 1
call :log "Start attempt %SV_TRY% failed, retrying"
call :sleep 5
goto start_verified_loop

""";
            return s.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        #endregion//Windows script

        #region Linux script

        /// <summary>
        /// Quote a value for bash (single quotes, any char except NUL is safe).
        /// </summary>
        static String Q(String value)
        {
            if (value.IndexOf('\0') >= 0)
                throw new Exception("A value can't contain a NUL char");
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        /// <summary>
        /// Create the content of the Linux (bash) script, see <see cref="SelfFolderSwap"/>.
        /// </summary>
        public static String CreateLinuxScript(String targetFolder, String backupFolder, String newFolder, String oldExeName, String newExeName, String scriptFile, String logFile, long ticks, Options o)
        {
            var parent = targetFolder.Substring(0, targetFolder.LastIndexOf('/'));
            if (parent.Length == 0)
                parent = "/";
            var probe = targetFolder + "_SwapProbe_" + ticks.ToString(CultureInfo.InvariantCulture);
            var s = $$"""
#!/bin/bash
# SysWeaver self swap script, generated {{new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}} UTC.
# Swaps the active service folder with a new version, on any failure the original
# folders are restored and the original service is restarted.
# Deletes itself when done, the log file is kept.
set -u
trap '' HUP INT PIPE
SELF={{Q(scriptFile)}}
LOG={{Q(logFile)}}
PARENT={{Q(parent)}}
TARGET={{Q(targetFolder)}}
BAK={{Q(backupFolder)}}
NEW={{Q(newFolder)}}
OLD_EXE_NAME={{Q(oldExeName)}}
NEW_EXE_NAME={{Q(newExeName)}}
PROBE={{Q(probe)}}
HEALTH_URL={{Q(o.HealthUrl ?? "")}}
INITIAL_DELAY={{Int(o.InitialDelaySeconds)}}
RETRIES={{Int(o.RenameRetries)}}
STABLE={{Int(o.StableSeconds)}}
START_RETRIES={{Int(o.StartRetries)}}
VERB_TIMEOUT={{Int(o.VerbTimeoutSeconds)}}
UNINSTALL_TIMEOUT={{Int(o.UninstallTimeoutSeconds)}}
STATE=0
RC=1

exec </dev/null >>"$LOG" 2>&1

log() { printf '[%s] %s\n' "$(date -u '+%Y-%m-%d %H:%M:%S')" "$*"; }

finish() {
    log "===== Finished, result $RC [0=swapped 1=aborted, unchanged 2=rolled back 3=emergency 4=fatal] ====="
    rm -f -- "$SELF"
}
trap finish EXIT

# Run a service verb: $1 = exe, $2 = verb, $3 = optional time out in seconds. Returns the exit code (124 = timed out and killed).
verb() {
    local rc t=${3:-$VERB_TIMEOUT}
    log "Running \"$1\" $2"
    if command -v timeout >/dev/null 2>&1; then
        ( cd -- "${1%/*}" && exec timeout -k 5 "$t" "$1" "$2" )
    else
        ( cd -- "${1%/*}" && exec "$1" "$2" )
    fi
    rc=$?
    # 137: timed out and had to be killed with SIGKILL (-k)
    if [ "$rc" = 137 ]; then rc=124; fi
    log "  exit code $rc"
    return $rc
}

# The systemd system service unit of a process (the last *.service in it's cgroup path, none for a user manager or a scope): $1 = pid.
unit_of() {
    local line path="" u="" seg
    [ -r "/proc/$1/cgroup" ] || return 0
    while IFS= read -r line; do
        case "$line" in
            *:name=systemd:*) path=${line#*:name=systemd:}; break ;;
            0::*) path=${line#0::} ;;
        esac
    done < "/proc/$1/cgroup"
    local IFS=/
    for seg in $path; do
        case "$seg" in
            user@*) return 0 ;;
            ?*.service) u=$seg ;;
        esac
    done
    printf '%s' "$u"
}
SELF_UNIT=$(unit_of $$)

# Kill all processes whose executable (or an argument, ex: dotnet x.dll) is inside a folder: $1 = folder.
# The systemd units of those processes are first stopped (without waiting), so that a "Restart=" setting doesn't restart them
# while the folders are swapped (a restart from a missing folder can also hit the start limit and make the next start fail).
# The unit running this script is never stopped.
kill_in() {
    local dir="${1%/}/" p pid exe arg hit u units=" " pids=""
    log "Killing processes running from \"$1\""
    for p in /proc/[0-9]*; do
        pid=${p#/proc/}
        [ "$pid" = "$$" ] && continue
        hit=0
        exe=$(readlink -- "$p/exe" 2>/dev/null) && case "$exe" in "$dir"*) hit=1 ;; esac
        if [ "$hit" = 0 ] && [ -r "$p/cmdline" ]; then
            while IFS= read -r -d '' arg; do
                case "$arg" in "$dir"*) hit=1; break ;; esac
            done < "$p/cmdline" 2>/dev/null
        fi
        [ "$hit" = 1 ] || continue
        pids="$pids $pid"
        u=$(unit_of "$pid")
        if [ -n "$u" ] && [ "$u" != "$SELF_UNIT" ]; then
            case "$units" in *" $u "*) ;; *) units="$units$u " ;; esac
        fi
    done
    if command -v systemctl >/dev/null 2>&1; then
        for u in $units; do
            log "  stopping the systemd unit $u (so that it isn't restarted)"
            systemctl stop --no-block -- "$u" || log "  failed to stop $u"
        done
    fi
    for pid in $pids; do
        if kill -KILL "$pid" 2>/dev/null; then log "  killed $pid"; fi
    done
}

# Rename a folder with retries: $1 = folder, $2 = new path. 0 = ok, 1 = failed, 2 = destination exist.
ren_dir() {
    local i=0
    while :; do
        if [ -e "$2" ] || [ -L "$2" ]; then log "Rename refused, \"$2\" already exist"; return 2; fi
        if mv -- "$1" "$2" && [ -d "$2" ] && [ ! -e "$1" ]; then log "Renamed \"$1\" to \"$2\""; return 0; fi
        i=$((i + 1))
        if [ "$i" -ge "$RETRIES" ]; then log "Rename failed, giving up"; return 1; fi
        log "Rename attempt $i failed, retrying"
        if [ "$i" = 5 ]; then kill_in "$1"; fi
        sleep 1
    done
}

# Returns 0 if the health url answered, 99 if there is no tool to test it.
health() {
    if command -v curl >/dev/null 2>&1; then
        curl -fsSk -m 5 -o /dev/null -- "$HEALTH_URL"
    elif command -v wget >/dev/null 2>&1; then
        wget -q -T 5 --no-check-certificate -O /dev/null -- "$HEALTH_URL"
    else
        return 99
    fi
}

# Require a service to stay running: $1 = exe, $2 = seconds, $3 = 1 to also require the health url to answer.
verify_stable() {
    local exe=$1 left=$2 need=0 rc=1 h
    if [ "$3" = 1 ] && [ -n "$HEALTH_URL" ]; then need=1; fi
    while [ "$left" -gt 0 ]; do
        sleep 2
        left=$((left - 2))
        verb "$exe" status
        rc=$?
        if [ "$rc" = 153 ]; then continue; fi
        if [ "$rc" != 0 ]; then log "The service is not running"; return 1; fi
        if [ "$need" = 1 ]; then
            health
            h=$?
            if [ "$h" = 0 ]; then need=0; log "The health url answered"
            elif [ "$h" = 99 ]; then need=0; log "WARNING: neither curl nor wget found, skipping the health url check"
            fi
        fi
    done
    if [ "$rc" != 0 ]; then log "The service is still not running"; return 1; fi
    if [ "$need" = 1 ]; then log "The health url never answered"; return 1; fi
    return 0
}

# Start a service and require it to stay running for 10 seconds, with retries: $1 = exe.
start_verified() {
    local i=0
    while :; do
        i=$((i + 1))
        if verb "$1" start && verify_stable "$1" 10 0; then return 0; fi
        if [ "$i" -ge "$START_RETRIES" ]; then return 1; fi
        log "Start attempt $i failed, retrying"
        sleep 5
    done
}

emergency() {
    RC=3
    log "EMERGENCY: can't restore the folder names, starting the original version from the backup folder"
    verb "$BAK/$OLD_EXE_NAME" uninstall "$UNINSTALL_TIMEOUT"
    if start_verified "$BAK/$OLD_EXE_NAME"; then
        log "EMERGENCY: the original version is running from the backup folder, MANUAL CLEAN UP REQUIRED"
    else
        RC=4
        log "FATAL: the original version could not be started, MANUAL INTERVENTION REQUIRED"
    fi
}

# Undo every step that was made (in reverse order) and start the original version.
recover() {
    trap '' TERM
    if [ "$STATE" = 0 ]; then RC=1; return; fi
    RC=2
    log "Rolling back from state $STATE"
    if [ "$STATE" -ge 4 ]; then
        verb "$TARGET/$NEW_EXE_NAME" uninstall "$UNINSTALL_TIMEOUT"
        kill_in "$TARGET"
    fi
    if [ "$STATE" -ge 3 ]; then ren_dir "$TARGET" "$NEW" || { emergency; return; }; fi
    if [ "$STATE" -ge 2 ]; then ren_dir "$BAK" "$TARGET" || { emergency; return; }; fi
    if start_verified "$TARGET/$OLD_EXE_NAME"; then
        log "ROLLED BACK: the folders are restored and the original version is running"
    else
        RC=4
        log "FATAL: the original version could not be started, MANUAL INTERVENTION REQUIRED"
    fi
}

fail() { log "FAILED: $1"; recover; exit "$RC"; }
abort() { RC=1; log "ABORTED before any change: $1"; exit "$RC"; }
trap 'log "SIGTERM received in state $STATE"; recover; exit "$RC"' TERM

log "===== SysWeaver self swap started ====="
log "Active: \"$TARGET\""
log "New:    \"$NEW\""
log "Backup: \"$BAK\""
if [ "$INITIAL_DELAY" -gt 0 ]; then sleep "$INITIAL_DELAY"; fi

# ---- Pre-flight, nothing is changed if any of these fails ----
[ -d "$TARGET" ] || abort "the active folder is missing"
[ -d "$NEW" ] || abort "the new folder is missing"
if [ -e "$BAK" ] || [ -L "$BAK" ]; then abort "the backup folder already exist"; fi
[ -x "$TARGET/$OLD_EXE_NAME" ] || abort "the service executable is missing (or not executable) in the active folder"
[ -x "$NEW/$NEW_EXE_NAME" ] || abort "the service executable is missing (or not executable) in the new folder"
dev=$(stat -c %d -- "$PARENT") || abort "can't stat the parent folder"
[ "$(stat -c %d -- "$TARGET")" = "$dev" ] || abort "the active folder is on another file system (mount point), a rename would be a copy"
[ "$(stat -c %d -- "$NEW")" = "$dev" ] || abort "the new folder is on another file system (mount point), a rename would be a copy"
if [ -e "$PROBE" ] || ! mkdir -- "$PROBE" || ! rmdir -- "$PROBE"; then abort "can't create folders in the parent folder"; fi
log "Pre-flight ok"

# ---- Swap ----
log "Step 1: uninstall (stop) the running service"
STATE=1
verb "$TARGET/$OLD_EXE_NAME" uninstall "$UNINSTALL_TIMEOUT"
rc=$?
if [ "$rc" = 124 ]; then
    log "The uninstall didn't finish in time, killing the service"
    kill_in "$TARGET"
elif [ "$rc" != 0 ]; then
    fail "failed to uninstall the running service"
fi

log "Step 2: rename the active folder to the backup name"
ren_dir "$TARGET" "$BAK" || fail "failed to rename the active folder to the backup name"
STATE=2

log "Step 3: rename the new folder to the active name"
ren_dir "$NEW" "$TARGET" || fail "failed to rename the new folder to the active name"
STATE=3

log "Step 4: start the new version"
STATE=4
verb "$TARGET/$NEW_EXE_NAME" start || fail "failed to start the new version"
verify_stable "$TARGET/$NEW_EXE_NAME" "$STABLE" 1 || fail "the new version didn't stay running / healthy"
trap '' TERM
RC=0
log "SUCCESS: the new version is running, the previous version is kept as the backup folder"
exit 0

""";
            return s.Replace("\r\n", "\n");
        }

        #endregion//Linux script
    }
}

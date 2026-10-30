using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IEnvironmentVariables"/> for Windows.
    /// User and machine variables are read and written in the registry directly (keeping REG_EXPAND_SZ values expandable, ex: PATH),
    /// a WM_SETTINGCHANGE "Environment" message is broadcast after a change (so that Explorer etc picks it up).
    /// </summary>
    sealed class Win32Environment : IEnvironmentVariables
    {
        public IReadOnlyList<OsEnvironmentScopes> Scopes { get; } = [OsEnvironmentScopes.Process, OsEnvironmentScopes.User, OsEnvironmentScopes.Machine];

        public bool CaseSensitive => false;

        public char ListSeparator => ';';

        const String MachineKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        const String UserKey = "Environment";

        static RegistryKey Open(OsEnvironmentScopes scope, bool write)
        {
            var k = scope switch
            {
                OsEnvironmentScopes.Machine => Registry.LocalMachine.OpenSubKey(MachineKey, write),
                OsEnvironmentScopes.User => write ? Registry.CurrentUser.CreateSubKey(UserKey, true) : Registry.CurrentUser.OpenSubKey(UserKey, false),
                _ => throw new ArgumentException("Invalid scope " + scope),
            };
            if ((k == null) && write)
                throw new Exception("Can't open the registry key of the " + scope + " environment!");
            return k;
        }

        static String KeyName(OsEnvironmentScopes scope) => scope == OsEnvironmentScopes.Machine ? (@"HKLM\" + MachineKey) : (@"HKCU\" + UserKey);

        public IReadOnlyList<OsEnvironmentVariable> GetVariables(OsEnvironmentScopes scope)
        {
            if (scope == OsEnvironmentScopes.Process)
                return OsEnvironmentHelper.GetProcessVariables();
            var res = new List<OsEnvironmentVariable>();
            using var k = Open(scope, false);
            if (k == null)
                return res;
            var src = KeyName(scope);
            foreach (var n in k.GetValueNames())
            {
                if (n.Length <= 0)
                    continue;
                var kind = k.GetValueKind(n);
                var v = k.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                res.Add(new OsEnvironmentVariable
                {
                    Name = n,
                    Value = v is String[] a ? String.Join(";", a) : (v?.ToString() ?? ""),
                    Scope = scope,
                    Expandable = kind == RegistryValueKind.ExpandString,
                    Source = src,
                });
            }
            return res;
        }

        public void SetVariable(OsEnvironmentScopes scope, String name, String value)
        {
            OsEnvironmentHelper.Validate(name, value, true);
            if (scope == OsEnvironmentScopes.Process)
            {
                Environment.SetEnvironmentVariable(name, value);
                return;
            }
            try
            {
                using var k = Open(scope, true);
                //  Keep the kind of an existing value, new values with references (%..%) are expandable
                var kind = RegistryValueKind.String;
                var exists = Array.Exists(k.GetValueNames(), n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                if (exists)
                    kind = k.GetValueKind(name) == RegistryValueKind.ExpandString ? RegistryValueKind.ExpandString : RegistryValueKind.String;
                if (value.IndexOf('%') >= 0)
                    kind = RegistryValueKind.ExpandString;
                k.SetValue(name, value, kind);
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Access denied, changing machine variables requires admin!");
            }
            Broadcast();
        }

        public void DeleteVariable(OsEnvironmentScopes scope, String name)
        {
            OsEnvironmentHelper.Validate(name, null, false);
            if (scope == OsEnvironmentScopes.Process)
            {
                Environment.SetEnvironmentVariable(name, null);
                return;
            }
            try
            {
                using var k = Open(scope, true);
                k.DeleteValue(name, true);
            }
            catch (ArgumentException)
            {
                throw new Exception("The variable \"" + name + "\" doesn't exist in the " + scope + " environment!");
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Access denied, changing machine variables requires admin!");
            }
            Broadcast();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint msg, UIntPtr wParam, String lParam, uint flags, uint timeout, out UIntPtr result);

        /// <summary>
        /// Tell all top level windows that the environment changed
        /// </summary>
        static void Broadcast()
        {
            try
            {
                const uint WM_SETTINGCHANGE = 0x1A;
                const uint SMTO_ABORTIFHUNG = 2;
                SendMessageTimeoutW(new IntPtr(0xffff), WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 1000, out _);
            }
            catch
            {
            }
        }
    }
}

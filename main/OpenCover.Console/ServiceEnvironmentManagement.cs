﻿/* ==++==
 * 
 *   Copyright (c) Microsoft Corporation.  All rights reserved.
 * 
 * ==--==
 *
 * Class:  Form1
 *
 * Description: CLR Profiler interface and logic
 */

/*
 * The following was taken from CLRProfiler4 MainFrame.cs 
 * and as such I have retained the Microsft copyright 
 * statement.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenCover.Console
{
    class ServiceEnvironmentManagementEx : ServiceEnvironmentManagement
    {
        public static bool IsServiceDisabled(string serviceName)
        {
            var entry = GetServiceKey(serviceName);
            return (int)entry.GetValue("Start") == 4;
        }
    }

    class ServiceEnvironmentManagement
    {
        private string _serviceAccountSid;
        private string _serviceName;
        private string[] _profilerEnvironment;

        public void PrepareServiceEnvironment(string serviceName, string[] profilerEnvironment)
        {
            _serviceName = serviceName;
            _profilerEnvironment = profilerEnvironment;

            // this is a bit intricate - if the service is running as LocalSystem, we need to set the environment
            // variables in the registry for the service, otherwise it's better to temporarily set it for the account,
            // assuming we can find out the account SID
            string serviceAccountName = GetServiceAccountName(_serviceName);
            if (serviceAccountName.StartsWith(@".\"))
                serviceAccountName = Environment.MachineName + serviceAccountName.Substring(1);
            if (serviceAccountName != "LocalSystem")
            {
                _serviceAccountSid = LookupAccountSid(serviceAccountName);
            }
            if (_serviceAccountSid != null)
            {
                SetAccountEnvironment(_serviceAccountSid, _profilerEnvironment);
            }
            else
            {
                string[] baseEnvironment = GetServicesEnvironment();
                string[] combinedEnvironment = CombineEnvironmentVariables(baseEnvironment, _profilerEnvironment);
                SetEnvironmentVariables(_serviceName, combinedEnvironment);
            }
        }

        public void ResetServiceEnvironment()
        {
            if (_serviceAccountSid != null)
            {
                ResetAccountEnvironment(_serviceAccountSid, _profilerEnvironment);
            }
            else
            {
                DeleteEnvironmentVariables(_serviceName);
            }
        }

        [DllImport("Kernel32.dll")]
        private static extern bool LocalFree(IntPtr ptr);

        [DllImport("Advapi32.dll")]
        private static extern bool ConvertSidToStringSidW(byte[] sid, out IntPtr stringSid);

        [DllImport("Advapi32.dll")]
        private static extern bool LookupAccountName(string machineName, string accountName, byte[] sid,
                                 ref int sidLen, StringBuilder domainName, ref int domainNameLen, out int peUse);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr OpenProcess(
            uint dwDesiredAccess,  // access flag
            bool bInheritHandle,    // handle inheritance option
            int dwProcessId       // process identifier
            );

        [DllImport("Advapi32.dll")]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            ref IntPtr TokenHandle
            );

        [DllImport("UserEnv.dll")]
        private static extern bool CreateEnvironmentBlock(
                out IntPtr lpEnvironment,
                IntPtr hToken,
                bool bInherit);


        private void SetEnvironmentVariables(string serviceName, string[] environment)
        {
            Microsoft.Win32.RegistryKey key = GetServiceKey(serviceName);
            if (key != null)
                key.SetValue("Environment", environment);
        }

        private string[] CombineEnvironmentVariables(string[] a, string[] b)
        {
            string[] c = new string[a.Length + b.Length];
            int i = 0;
            foreach (string s in a)
                c[i++] = s;
            foreach (string s in b)
                c[i++] = s;
            return c;
        }

        private string[] GetServicesEnvironment()
        {
            Process[] servicesProcesses = Process.GetProcessesByName("services");
            if (servicesProcesses == null || servicesProcesses.Length != 1)
            {
                servicesProcesses = Process.GetProcessesByName("services.exe");
                if (servicesProcesses == null || servicesProcesses.Length != 1)
                    return new string[0];
            }
            Process servicesProcess = servicesProcesses[0];
            IntPtr processHandle = OpenProcess(0x20400, false, servicesProcess.Id);
            if (processHandle == IntPtr.Zero)
                return new string[0];
            IntPtr tokenHandle = IntPtr.Zero;
            if (!OpenProcessToken(processHandle, 0x20008, ref tokenHandle))
                return new string[0];
            IntPtr environmentPtr = IntPtr.Zero;
            if (!CreateEnvironmentBlock(out environmentPtr, tokenHandle, false))
                return new String[0];
            unsafe
            {
                string[] envStrings = null;
                // rather than duplicate the code that walks over the environment, 
                // we have this funny loop where the first iteration just counts the strings,
                // and the second iteration fills in the strings
                for (int i = 0; i < 2; i++)
                {
                    char* env = (char*)environmentPtr.ToPointer();
                    int count = 0;
                    while (true)
                    {
                        int len = wcslen(env);
                        if (len == 0)
                            break;
                        if (envStrings != null)
                            envStrings[count] = new String(env);
                        count++;
                        env += len + 1;
                    }
                    if (envStrings == null)
                        envStrings = new string[count];
                }
                return envStrings;
            }
        }

        private static unsafe int wcslen(char* s)
        {
            char* e;
            for (e = s; *e != '\0'; e++)
                ;
            return (int)(e - s);
        }

        private void SetAccountEnvironment(string serviceAccountSid, string[] profilerEnvironment)
        {
            Microsoft.Win32.RegistryKey key = GetAccountEnvironmentKey(serviceAccountSid);
            if (key != null)
            {
                foreach (string envVariable in profilerEnvironment)
                {
                    key.SetValue(EnvKey(envVariable), EnvValue(envVariable));
                }
            }
        }

        private string GetServiceAccountName(string serviceName)
        {
            Microsoft.Win32.RegistryKey key = GetServiceKey(serviceName);
            if (key != null)
                return key.GetValue("ObjectName") as string;
            return null;
        }

        private string LookupAccountSid(string accountName)
        {
            int sidLen = 0;
            byte[] sid = new byte[sidLen];
            int domainNameLen = 0;
            int peUse;
            StringBuilder domainName = new StringBuilder();
            LookupAccountName(Environment.MachineName, accountName, sid, ref sidLen, domainName, ref domainNameLen, out peUse);

            sid = new byte[sidLen];
            domainName = new StringBuilder(domainNameLen);
            string stringSid = null;
            if (LookupAccountName(Environment.MachineName, accountName, sid, ref sidLen, domainName, ref domainNameLen, out peUse))
            {
                IntPtr stringSidPtr;
                if (ConvertSidToStringSidW(sid, out stringSidPtr))
                {
                    try
                    {
                        stringSid = Marshal.PtrToStringUni(stringSidPtr);
                    }
                    finally
                    {
                        LocalFree(stringSidPtr);
                    }
                }
            }
            return stringSid;
        }


        private void ResetAccountEnvironment(string serviceAccountSid, string[] profilerEnvironment)
        {
            Microsoft.Win32.RegistryKey key = GetAccountEnvironmentKey(serviceAccountSid);
            if (key != null)
            {
                foreach (string envVariable in profilerEnvironment)
                {
                    key.DeleteValue(EnvKey(envVariable));
                }
            }
        }

        private Microsoft.Win32.RegistryKey GetAccountEnvironmentKey(string serviceAccountSid)
        {
            Microsoft.Win32.RegistryKey users = Microsoft.Win32.Registry.Users;
            return users.OpenSubKey(serviceAccountSid + @"\Environment", true);
        }

        private string EnvValue(string envVariable)
        {
            int index = envVariable.IndexOf('=');
            Debug.Assert(index >= 0);
            return envVariable.Substring(index + 1);
        }

        private string EnvKey(string envVariable)
        {
            int index = envVariable.IndexOf('=');
            Debug.Assert(index >= 0);
            return envVariable.Substring(0, index);
        }

        private void DeleteEnvironmentVariables(string serviceName)
        {
            Microsoft.Win32.RegistryKey key = GetServiceKey(serviceName);
            if (key != null)
                key.DeleteValue("Environment");
        }

        protected static Microsoft.Win32.RegistryKey GetServiceKey(string serviceName)
        {
            Microsoft.Win32.RegistryKey localMachine = Microsoft.Win32.Registry.LocalMachine;
            Microsoft.Win32.RegistryKey key = localMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\" + serviceName, true);
            return key;
        }

    }
}

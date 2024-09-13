﻿using System;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;

namespace DynamicInterop
{
    [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
    internal sealed class SafeHandleUnmanagedDll : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeHandleUnmanagedDll(string dllName) : base(true)
        {
            IDynamicLibraryLoader libraryLoader;
            if (PlatformUtility.IsUnix)
                libraryLoader = new UnixLibraryLoader();
            else if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                libraryLoader = new WindowsLibraryLoader();
            else
                throw new NotSupportedException(PlatformUtility.GetPlatformNotSupportedMsg());
            this.libraryLoader = libraryLoader;
            handle = libraryLoader.LoadLibrary(dllName);
        }

        private readonly IDynamicLibraryLoader libraryLoader;

        /// <summary>
        /// Frees the native library this objects represents
        /// </summary>
        /// <returns>The result of the call to FreeLibrary</returns>
        protected override bool ReleaseHandle()
        {
            return FreeLibrary();
        }

        private bool FreeLibrary()
        {
            if (libraryLoader != null) return libraryLoader.FreeLibrary(handle);
            if (!IsInvalid)
            {
                throw new ApplicationException("Warning: unexpected condition of library loader and native handle - some native resources may not be properly disposed of");
            }

            return true;

        }

        public IntPtr GetFunctionAddress(string lpProcName)
        {
            return libraryLoader.GetFunctionAddress(handle, lpProcName);
        }

        /// <summary>
        /// Frees the native library this objects represents
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (FreeLibrary())
            {
                SetHandleAsInvalid();
            }
            base.Dispose(disposing);
        }

        public string GetLastError()
        {
            return libraryLoader.GetLastError();
        }
    }
}

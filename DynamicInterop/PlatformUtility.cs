using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace DynamicInterop
{
    /// <summary>
    /// Helper class with functions whose behavior may be depending on the platform
    /// </summary>
    public static class PlatformUtility
    {
        /// <summary>
        /// Is the platform unix-like (Unix or MacOX)
        /// </summary>
        public static bool IsUnix
        {
            get
            {
                var p = GetPlatform();
                return p is PlatformID.MacOSX or PlatformID.Unix;
            }
        }

        /// <summary>
        /// Gets the platform on which the current process runs.
        /// </summary>
        /// <remarks>
        /// <see cref="Environment.OSVersion"/>'s platform is not <see cref="PlatformID.MacOSX"/> even on Mac OS X.
        /// This method returns <see cref="PlatformID.MacOSX"/> when the current process runs on Mac OS X.
        /// This method uses UNIX's uname command to check the operating system,
        /// so this method cannot check the OS correctly if the PATH environment variable is changed (will returns <see cref="PlatformID.Unix"/>).
        /// </remarks>
        /// <returns>The current platform.</returns>
        public static PlatformID GetPlatform()
        {
            if (_curPlatform.HasValue) return _curPlatform.Value;
            var platform = Environment.OSVersion.Platform;
            if (platform != PlatformID.Unix)
            {
                _curPlatform = platform;
            }
            else
            {
                try
                {
                    var kernelName = ExecCommand("uname", "-s");
                    _curPlatform = (kernelName == "Darwin" ? PlatformID.MacOSX : platform);
                }
                catch (Win32Exception)
                { // probably no PATH to uname.
                    _curPlatform = platform;
                }
            }
            return _curPlatform.Value;
        }

        private static PlatformID? _curPlatform;

        /// <summary>
        /// Execute a command in a new process
        /// </summary>
        /// <param name="processName">Process name e.g. "uname"</param>
        /// <param name="arguments">Arguments e.g. "-s"</param>
        /// <returns>The output of the command to the standard output stream</returns>
        public static string ExecCommand(string processName, string arguments)
        {
            using var proc = new Process();
            proc.StartInfo.FileName = processName;
            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            var kernelName = proc.StandardOutput.ReadLine();
            proc.WaitForExit();
            return kernelName;
        }

        /// <summary>
        /// Gets a message saying the current platform is not supported
        /// </summary>
        /// <returns>The platform not supported message.</returns>
        public static string GetPlatformNotSupportedMsg()
        {
            return $"Platform {Environment.OSVersion.Platform.ToString()} is not supported.";
        }

        /// <summary>
        /// Given a DLL short file name, find all the matching occurences in directories as stored in an environment variable such as the PATH.
        /// </summary>
        /// <returns>One or more full file names found to exist</returns>
        /// <param name="dllName">short file name.</param>
        /// <param name="envVarName">Environment variable name - default PATH</param>
        public static string[] FindFullPathEnvVar(string dllName, string envVarName="PATH")
        {
            var searchPaths = (Environment.GetEnvironmentVariable(envVarName) ?? "").Split(Path.PathSeparator);
            return FindFullPath (dllName, searchPaths);
        }

        /// <summary>
        /// Given a DLL short file name, find all the matching occurences in directories.
        /// </summary>
        /// <returns>One or more full file names found to exist</returns>
        /// <param name="dllName">short file name.</param>
        /// <param name="directories">Directories in which to search for matching file names</param>
        public static string[] FindFullPath(string dllName, params string[] directories)
        {
            return directories.Select(directory => Path.Combine(directory, dllName)).Where(File.Exists).ToArray();
        }

        /// <summary> Given a DLL short file name, short or otherwise, searches for the first full path.</summary>
        ///
        /// <exception cref="DllNotFoundException"> Thrown when a DLL Not Found error condition occurs.</exception>
        ///
        /// <param name="nativeLibFilename"> Filename of the native library file.</param>
        /// <param name="libname">           (Optional) human-readable name of the library.</param>
        /// <param name="envVarName">        (Optional)
        ///                                  Environment variable to use for search path(s) -
        ///                                  defaults according to platform to PATH or LD_LIBRARY_PATH if empty.</param>
        /// <returns> The found full path.</returns>
        public static string FindFirstFullPath(string nativeLibFilename, string libname = "native library", string envVarName = "")
        {
            if (string.IsNullOrEmpty(nativeLibFilename) || !Path.IsPathRooted(nativeLibFilename))
                nativeLibFilename = findFirstFullPath(nativeLibFilename, envVarName);
            else if (!File.Exists(nativeLibFilename))
                throw new DllNotFoundException(
                    $"Could not find specified file {nativeLibFilename} to load as {libname}");
            return nativeLibFilename;
        }

        private static string findFirstFullPath(string shortFileName, string envVarName = "")
        {
            if (string.IsNullOrEmpty(shortFileName))
                throw new ArgumentNullException(nameof(shortFileName));

            var libSearchPathEnvVar = envVarName;
            if (string.IsNullOrEmpty(libSearchPathEnvVar))
                libSearchPathEnvVar = (Environment.OSVersion.Platform == PlatformID.Win32NT ? "PATH" : "LD_LIBRARY_PATH");
            var candidates = FindFullPathEnvVar(shortFileName, libSearchPathEnvVar);
            if ((candidates.Length == 0) && (Environment.OSVersion.Platform == PlatformID.Win32NT))
                if (File.Exists(shortFileName))
                    candidates = new[] { shortFileName };
            if (candidates.Length == 0)
                throw new DllNotFoundException(
                    $"Could not find native library named '{shortFileName}' within the directories specified in the '{libSearchPathEnvVar}' environment variable");
            else
                return candidates[0];
        }

        /// <summary> Given the stub name for a library get the likely platform specific file name</summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when one or more required arguments are null.</exception>
        ///
        /// <param name="libraryName"> Name of the library.</param>
        ///
        /// <returns> The likely file name for the shared library.</returns>
        public static string CreateLibraryFileName(string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
                throw new ArgumentNullException(nameof(libraryName));
            return
                (Environment.OSVersion.Platform == PlatformID.Win32NT ?
                libraryName + ".dll" :
                "lib" + libraryName + ".so");
        }
    }
}

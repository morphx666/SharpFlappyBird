using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SharpFlappyBird {
    public static class Runtime {
        public enum Platforms {
            Windows,
            Linux,
            MacOSX,
            ARMSoft,
            ARMHard,
            Unknown
        }

        private static Platforms? mPlatform;

        public static Platforms Platform {
            get {
                if(mPlatform == null) DetectPlatform();
                return mPlatform ?? Platforms.Unknown;
            }
        }

        private static void DetectPlatform() {
            switch(Environment.OSVersion.Platform) {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                case PlatformID.Xbox:
                    mPlatform = Platforms.Windows;
                    break;
                case PlatformID.MacOSX:
                    mPlatform = Platforms.MacOSX;
                    break;
                default:
                    if(Directory.Exists("/Applications") &&
                       Directory.Exists("/System") &&
                       Directory.Exists("/Users") &&
                       Directory.Exists("/Volumes")) {
                        mPlatform = Platforms.MacOSX;
                    } else {
                        mPlatform = Platforms.Linux;

                        string distro = GetLinuxDistro().ToLower();
                        if(distro.Contains("raspberrypi")) mPlatform = distro.Contains("armv7l") ? Platforms.ARMHard : Platforms.ARMSoft;
                    }
                    break;
            }
        }

        private static string GetLinuxDistro() {
            List<string> lines = new List<string>();

            ProcessStartInfo si = new ProcessStartInfo() {
                FileName = "uname",
                Arguments = "-a",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };
            Process catProcess = new Process { StartInfo = si };
            catProcess.OutputDataReceived += (object s, DataReceivedEventArgs e) => lines.Add(e.Data);

            try {
                catProcess.Start();
                catProcess.BeginOutputReadLine();
                catProcess.WaitForExit();
                catProcess.Dispose();

                System.Threading.Thread.Sleep(500);

                return lines.Count > 0 ? lines[0] : "Unknown";
            } catch {
                return Environment.OSVersion.Platform.ToString();
            }
        }
    }
}
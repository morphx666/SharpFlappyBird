using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                return mPlatform.HasValue ? mPlatform.Value : Platforms.Unknown;
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
                        if(distro.Contains("raspberrypi")) {
                            mPlatform = Platforms.ARMSoft;
                            if(distro.Contains("armv7l")) mPlatform = Platforms.ARMHard;
                        }
                    }
                    break;
            }
        }

        private static string GetLinuxDistro() {
            List<string> lines = new List<string>();

            Process catProcess = new Process();
            catProcess.StartInfo.FileName = "uname";
            catProcess.StartInfo.Arguments = "-a";
            catProcess.StartInfo.CreateNoWindow = true;
            catProcess.StartInfo.UseShellExecute = false;
            catProcess.StartInfo.RedirectStandardOutput = true;
            catProcess.StartInfo.RedirectStandardError = true;
            catProcess.StartInfo.RedirectStandardInput = false;
            catProcess.OutputDataReceived += (object s, DataReceivedEventArgs e) => lines.Add(e.Data);

            try {
                catProcess.Start();
                catProcess.BeginOutputReadLine();
                catProcess.WaitForExit();
                catProcess.Dispose();

                System.Threading.Thread.Sleep(500);

                if(lines.Count > 0) {
                    return lines.First();
                } else {
                    return "Unknown";
                }
            } catch {
                return Environment.OSVersion.Platform.ToString();
            }
        }
    }
}

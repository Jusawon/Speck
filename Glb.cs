using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speck
{
    internal class Glb
    {

        enum Platform
        {
            Windows,
            Linux,
            MacOS
        }

        static Platform GetPlatform()
        {
            if (OperatingSystem.IsWindows()) return Platform.Windows;
            if (OperatingSystem.IsLinux()) return Platform.Linux;
            if (OperatingSystem.IsMacOS()) return Platform.MacOS;

            throw new NotSupportedException("Unsupported OS");
        }
    }
}

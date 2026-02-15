using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Speck
{
    internal class Glb
    {
        public const string ConnectionString = "Host=localhost;Port=5434;Username=postgres;Password=1234;Database=dbspeck";
        public static string SpeckBreed { get; set; } = "Kampoeng";
        public static string ExportType { get; set; }  = "JSON"; //JSON/CSV/XML

        enum Platform
        {
            Windows,
            Linux,
            MacOS
        }
    }
}

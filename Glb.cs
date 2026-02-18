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
        public static string _SpeckBreed = "Kampoeng";
        public static string _ExportType  = "JSON"; //JSON/CSV/XML

        public static string SpeckBreed
        {
            get { return _SpeckBreed; }
            set
            {
                _SpeckBreed = value;
                LoadSpecks();
            }
        }

        public static string ExportType
        {
            get { return _ExportType; }
            set
            {
                _ExportType = value;
                LoadExport();
            }
        }


        enum Platform
        {
            Windows,
            Linux,
            MacOS
        }

        public static void LoadSpecks()
        {
            var baseUrl = "avares://Speck/Assets/Specks/";
            var Normal = "Normals/";
            var Talking = "Talking/";
            var Basics = "Basic/";
            var Extended = "Extended/";
            var HoldGlasses = "Holding Glasses/";
            var MagGlasses = "Magnifying Glasses/";
            var URIEnd = ".svg";

            Scans.SpeckScan = baseUrl + Normal + MagGlasses + SpeckBreed + URIEnd;
        }

        public static void LoadExport()
        {

        }
    }
}

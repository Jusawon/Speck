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
        public static string _SpeckBreed = "Orpington";
        public static string ExportType  = "JSON"; //JSON/CSV/XML

        public static string SpeckBreed
        {
            get { return _SpeckBreed; }
            set
            {
                _SpeckBreed = value;
                LoadSpecks();
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
            var ChatIcon = "Chat Icons/";
            var Basics = "Basic/";
            var Extended = "Extended/";
            var HoldGlasses = "Holding Glasses/";
            var MagGlasses = "Magnifying Glasses/";
            var UrlEnd = ".svg";

            MainWindow.SpeckIcon = baseUrl + ChatIcon + SpeckBreed + UrlEnd;
            Dashboard.SpeckImg = baseUrl + Talking + Extended + SpeckBreed + UrlEnd;
            Scans.SpeckImg = baseUrl + Normal + MagGlasses + SpeckBreed + UrlEnd;
            Vulnerabilities.SpeckImg = baseUrl + Talking + HoldGlasses + SpeckBreed + UrlEnd;
            Logs.SpeckImg = baseUrl + Talking + Basics + SpeckBreed + UrlEnd;
            
            
        }
    }
}

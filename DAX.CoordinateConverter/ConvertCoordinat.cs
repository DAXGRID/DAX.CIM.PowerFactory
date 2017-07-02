using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CoordinateConverter
{
    using GeoAPI.CoordinateSystems;
    using GeoAPI.CoordinateSystems.Transformations;
    using ProjNet.CoordinateSystems.Transformations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace DAX.IO.CIM.PowerFactory
    {
        public static class ConvertCoordinat
        {
            static IProjectedCoordinateSystem _fromCS;
            static IGeographicCoordinateSystem _toCS;
            static CoordinateTransformationFactory _ctfac;
            static ICoordinateTransformation _trans;

            public static double[] ConvertFromUTM32NToWGS84(double x, double y)
            {
                Initialize();

                // Transform point to WGS84 latitude longitude 
                double[] fromPoint = new double[] { x, y };
                double[] toPoint = _trans.MathTransform.Transform(fromPoint);

                return toPoint;
            }


            private static void Initialize()
            {
                if (_fromCS == null)
                {
                    string utmWkt = @"PROJCS[""ETRS89 / UTM zone 32N"",
    GEOGCS[""ETRS89"",
        DATUM[""European_Terrestrial_Reference_System_1989"",
            SPHEROID[""GRS 1980"",6378137,298.257222101,
                AUTHORITY[""EPSG"",""7019""]],
            AUTHORITY[""EPSG"",""6258""]],
        PRIMEM[""Greenwich"",0,
            AUTHORITY[""EPSG"",""8901""]],
        UNIT[""degree"",0.01745329251994328,
            AUTHORITY[""EPSG"",""9122""]],
        AUTHORITY[""EPSG"",""4258""]],
    UNIT[""metre"",1,
        AUTHORITY[""EPSG"",""9001""]],
    PROJECTION[""Transverse_Mercator""],
    PARAMETER[""latitude_of_origin"",0],
    PARAMETER[""central_meridian"",9],
    PARAMETER[""scale_factor"",0.9996],
    PARAMETER[""false_easting"",500000],
    PARAMETER[""false_northing"",0],
    AUTHORITY[""EPSG"",""25832""],
    AXIS[""Easting"",EAST],
    AXIS[""Northing"",NORTH]]";


                    // WGS 84
                    string wgsWkt = @"
                GEOGCS[""GCS_WGS_1984"",
                    DATUM[""D_WGS_1984"",SPHEROID[""WGS_1984"",6378137,298.257223563]],
                    PRIMEM[""Greenwich"",0],
                    UNIT[""Degree"",0.0174532925199433]
                ]";


                    // Initialize objects needed for coordinate transformation
                    _fromCS = ProjNet.Converters.WellKnownText.CoordinateSystemWktReader.Parse(utmWkt) as IProjectedCoordinateSystem;
                    _toCS = ProjNet.Converters.WellKnownText.CoordinateSystemWktReader.Parse(wgsWkt) as IGeographicCoordinateSystem;
                    _ctfac = new CoordinateTransformationFactory();
                    _trans = _ctfac.CreateFromCoordinateSystems(_fromCS, _toCS);
                }
            }

        }
    }

}

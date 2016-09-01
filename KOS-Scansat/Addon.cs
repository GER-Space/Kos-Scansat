using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using kOS;
using kOS.Safe;
using kOS.Suffixed;
using UnityEngine;
using SCANsat;
using System.Reflection;

using kOS.Safe.Encapsulation;
using SCANsat.SCAN_Data;

namespace kOS.AddOns.kOSSCANsat
{
    [kOSAddon("SCANSAT")]
    [kOS.Safe.Utilities.KOSNomenclature("SCANsatAddon")]
    public class Addon : Suffixed.Addon
    {
        public Addon(SharedObjects shared) : base(shared)
        {
            InitializeSuffixes();
        }
        public override BooleanValue Available()
        {
            return IsModInstalled("scansat");
        }
        private void InitializeSuffixes()
        {

            if (IsModInstalled("scansat"))
            {
                AddSuffix("CURRENTBIOME", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<StringValue>(GetCurrentBiome, "Get Name of current Biome"));
                AddSuffix(new[] { "GETBIOME", "BIOMEAT" }, new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<StringValue, BodyTarget, GeoCoordinates>(GetBiomeAt, "Get Name of Biome of Body,GeoCoordinates"));
                AddSuffix("ELEVATION", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, GeoCoordinates>(GetAltAt, "Get scanned altitude of Body,GeoCoordinates"));
                AddSuffix("COMPLETEDSCANS", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ListValue, BodyTarget, GeoCoordinates>(GetScans, "Returns the list of the completed scans of Body,GeoCoordinates"));
                AddSuffix("ALLSCANTYPES", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<StringValue>(GetScanNames, "Names of all scan types"));
                AddSuffix("ALLRESOURCES", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<ListValue>(GetResourceNames, "List of all activated resource in the current game"));
                AddSuffix("RESOURCEAT", new kOS.Safe.Encapsulation.Suffixes.VarArgsSuffix<ScalarDoubleValue, Structure>(GetResourceByName, "Returns the amount of a resource by its scan type: Body,GeoCoordinates,scantype"));
                AddSuffix("SLOPE", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, GeoCoordinates>(GetSlope, "Returns the most accurate slope of the location"));
                AddSuffix("GETCOVERAGE", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, StringValue>(GetCoverage, "Returns completen percatage of a body,scantype"));
            }
        }

        internal static bool IsModInstalled(string assemblyName)
        {
            Assembly assembly = (from a in AssemblyLoader.loadedAssemblies
                                 where a.name.ToLower().Equals(assemblyName.ToLower())
                                 select a).FirstOrDefault().assembly;
            return assembly != null;
        }

        private ScalarDoubleValue GetResourceByName(params Structure[] args )
        {
            if (args.Length != 3 ) { return null; }
            BodyTarget body = args.Where(s => s.GetType() == typeof(BodyTarget)).Cast<BodyTarget>().First();
//            BodyTarget body = args[0] as BodyTarget;
            GeoCoordinates coordinate = args.Where(s => s.GetType() == typeof(GeoCoordinates)).Cast<GeoCoordinates>().First();
            StringValue s_type = args.Where(s => s.GetType() == typeof(StringValue)).Cast<StringValue>().First();

            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype(s_type)))
            {
                float amount = 0f;
                var aRequest = new AbundanceRequest
                {
                    Latitude = coordinate.Latitude,
                    Longitude = coordinate.Longitude,
                    BodyId = body.Body.flightGlobalsIndex,
                    ResourceName = s_type,
                    ResourceType = HarvestTypes.Planetary,
                    Altitude = 0,
                    CheckForLock = SCANcontroller.controller.resourceBiomeLock,
                    BiomeName = ScienceUtil.GetExperimentBiome(body.Body, coordinate.Latitude, coordinate.Longitude),
                    ExcludeVariance = false,
                };

                amount = ResourceMap.Instance.GetAbundance(aRequest);
                return amount;
            } else
            {
                return -1.0;
            }
                
        }

        private StringValue GetCurrentBiome()
        {
            var vessel = FlightGlobals.ActiveVessel;
            var body = FlightGlobals.ActiveVessel.mainBody;
            var Biome = "";
            // check if we have crew onboard, which can look outside of a window, to determinate where we are.
            if (vessel.GetCrewCount() > 0)
            {
                Biome = string.IsNullOrEmpty(vessel.landedAt)
                ? ScienceUtil.GetExperimentBiome(body, vessel.latitude, vessel.longitude)
                : Vessel.GetLandedAtString(vessel.landedAt).Replace(" ", "");
            } else
            {
                Biome = GetScannedBiomeName(body, vessel.latitude, vessel.longitude);
            }
                
            return Biome;
        }

        private StringValue GetBiomeAt(BodyTarget body, GeoCoordinates coordinate)
        {
            return GetScannedBiomeName(body.Body, coordinate.Latitude, coordinate.Longitude);
        }


        internal string GetScannedBiomeName(CelestialBody body,double lat, double lng)
        {
            if (SCANUtil.isCovered (lng,lat,body, SCANUtil.GetSCANtype("Biome")))
            {
                return ScienceUtil.GetExperimentBiome(body, lat, lng);
            } else
            {
                return "unknown";
            }
        }

        private ScalarDoubleValue GetAltAt(BodyTarget body, GeoCoordinates coordinate)
        {
            double altitude = -1;

            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype("AltimetryHiRes")))
            {
                altitude = GetElevation(body.Body, coordinate.Longitude, coordinate.Latitude);
                return altitude;
            }
            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype("AltimetryLoRes")))
            {
                double alt = GetElevation(body.Body, coordinate.Longitude, coordinate.Latitude);
                altitude = (Math.Round(alt / 500)) * 500;
                return altitude;
            }
                return altitude;
        }

        private ScalarDoubleValue GetCoverage(BodyTarget body, StringValue scantype)
        {
            return SCANUtil.GetCoverage(SCANUtil.GetSCANtype(scantype),body.Body);
        }

        private ScalarDoubleValue GetAltAt(BodyTarget body, double lon, double lat)
        {
            GeoCoordinates coordinates = new GeoCoordinates(shared, lat, lon);
             return GetAltAt(body,coordinates);
        }


        private ScalarDoubleValue GetSlope(BodyTarget body, GeoCoordinates coordinate)
        {
            double slope = -1;
            double offsetm = 5;

            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype("AltimetryHiRes")))
            {
                offsetm = 5;
            }
            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype("AltimetryLoRes")))
            {
                offsetm = 500;
            }

            /*
            double circum = body.Body.Radius * 2 * Math.PI;
            double eqDistancePerDegree = circum / 360;
            degreeOffset = 5 / eqDistancePerDegree;
            */
            double offset = offsetm/(body.Body.Radius * 2 * Math.PI/360);

            double latOffset = 0;
            // matrix for z-values
            double[] z = new double[9];

            int i = 0;
            double lat = coordinate.Latitude;
            double lon = coordinate.Longitude;
            double altcenter = GetAltAt(body, lon, lat);

            // setup the matrix with eqidistant messurement. 
            // We have now the form from:
            // http://www.caee.utexas.edu/prof/maidment/giswr2011/docs/Slope.pdf 
            //
            for (int lac = 1; lac > -2; lac-- )
            {
                latOffset = offset * Math.Cos(Mathf.Deg2Rad * lat);

                for (int lnc = -1; lnc < 2; lnc++)
                {
                    z[i] = GetAltAt(body,lon+(lnc*latOffset),lat+(lac*offset));
                    if (z[i] == -1)
                    {
                        z[i] = altcenter;
                    }
                    i =+ 1;
                }
            }

            double dEW = ((z[0] + z[3] + z[6]) - (z[2] + z[5] + z[8])) / (8*offsetm);
            double dNS = ((z[6] + z[7] + z[8]) - (z[0] + z[1] + z[2])) / (8*offsetm);

            slope = 100* Math.Abs(Math.Sqrt(Math.Pow(dEW,2) + Math.Pow(dNS,2)));

            if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype("AltimetryLoRes")))
            {

            }
            return Math.Round(slope,2);
        }
        

        // this function is copied from SCANsat. https://github.com/S-C-A-N/SCANsat
        internal static double GetElevation(CelestialBody body, double lon, double lat)
        {
            if (body.pqsController == null) return 0;
            double rlon = Mathf.Deg2Rad * lon;
            double rlat = Mathf.Deg2Rad * lat;
            Vector3d rad = new Vector3d(Math.Cos(rlat) * Math.Cos(rlon), Math.Sin(rlat), Math.Cos(rlat) * Math.Sin(rlon));
            return Math.Round(body.pqsController.GetSurfaceHeight(rad) - body.pqsController.radius, 1);
        }

        private ListValue GetScans(BodyTarget body, GeoCoordinates coordinate)
        {
            ListValue scans = new ListValue();
            foreach (string s_type in Enum.GetNames(typeof(SCANtype)))
            {
                if (CheckScanBlacklisted(s_type)) { continue; }
                if (SCANUtil.isCovered(coordinate.Longitude, coordinate.Latitude, body.Body, SCANUtil.GetSCANtype(s_type)))
                {
                    scans.Add(new StringValue(s_type));
                }
            }
                return scans;
        }

        private ListValue GetResourceNames()
        {
            ListValue resources = new ListValue();
            foreach (SCANresourceGlobal res in SCANcontroller.setLoadedResourceList() )  {
                resources.Add(new StringValue(res.Name));
            }
            return resources;
        }

        private StringValue GetScanNames()
        {
            var allscans = "";
            foreach (string s_type in Enum.GetNames(typeof(SCANtype)))
            {
                if (CheckScanBlacklisted(s_type)) { continue; }
                allscans += s_type + "\n";

            }
            return allscans;
        }

        private bool CheckScanBlacklisted (string scanname)
        {
            string[] blacklist = { "Nothing", "Altimetry", "Everything_SCAN", "Science", "Everything", "MKSResources", "KSPIResourece", "DefinedResources" , "SCANsat_1" };
            if (blacklist.Contains(scanname))
            {
                return true;
            } else
            {
                return false;
            }

        }

    }
}

/*
Nothing = 0, 		    // no data (MapTraq)
		AltimetryLoRes = 1 << 0,  // low resolution altimetry (limited zoom)
		AltimetryHiRes = 1 << 1,  // high resolution altimetry (unlimited zoom)
		Altimetry = (1 << 2) - 1, 	        // both (setting) or either (testing) altimetry
		SCANsat_1 = 1 << 2,		// Unused, reserved for future SCANsat scanner
		Biome = 1 << 3,		    // biome data
		Anomaly = 1 << 4,		    // anomalies (position of anomaly)
		AnomalyDetail = 1 << 5,	// anomaly detail (name of anomaly, etc.)
		Kethane = 1 << 6,         // Kethane
		MetallicOre = 1 << 7,             // CRP Ore
		Ore = 1 << 8,				//Stock Ore
		SolarWind = 1 << 9,       // SolarWind - He-3 - KSPI
		Uraninite = 1 << 10,        // Uranium - CRP
		Monazite = 1 << 11,        // Monazite - Thorium - KSPI
		Alumina = 1 << 12,        // Alumina - CRP - KSPI
		Water = 1 << 13,          // Water - CRP
		Aquifer = 1 << 14,        // Aquifer - CRP
		Minerals = 1 << 15,       // Minerals - CRP
		Substrate = 1 << 16,      // Substrate - CRP
		MetalOre = 1 << 17,          // Metal Ore - EPL
		Karbonite = 1 << 18,    // Karbonite - CRP
		FuzzyResources = 1 << 19,         // Low Detail Resource
		Hydrates = 1 << 20,		// Hydrates - CRP
		Gypsum = 1 << 21,		// Gypsum - CRP
		RareMetals = 1 << 22, // Exotic Minerals - CRP
		ExoticMinerals = 1 << 23,			// Dirt - CRP
		Dirt = 1 << 24,	// Rare Metals - CRP
		Borate = 1 << 25,		// Borate - KSPI
		GeoEnergy = 1 << 26,	// Geo Energy - Pathfinder
		SaltWater = 1 << 27,	// Salt Water - KSPI
		Silicates = 1 << 28,	// Silicates - KSPI

		Everything_SCAN = (1 << 6) - 1,	// All default SCANsat scanners
		Science = 524299,				// All science collection types
		AllResources = 2147483584,		// All resource types
		DefinedResources = 536346496,		// All defined resource types
		MKSResources = 32613504,			// All standard MKS/USI resources
		KSPIResourece = 437272320,					// All KSPI standard resources
		Everything = Int32.MaxValue      // All scanner types 

    */
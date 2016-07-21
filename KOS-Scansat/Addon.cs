using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using kOS;
using kOS.Safe;
using kOS.Suffixed;
using UnityEngine;
using SCANsat;

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
            if (SCANcontroller.controller) { return true; } else { return false; };
        }
        private void InitializeSuffixes()
        {
            AddSuffix("CURRENTBIOME", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<StringValue>(GetCurrentBiome, "Get Name of current Biome"));
            AddSuffix("BIOMEAT", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<StringValue, BodyTarget, GeoCoordinates>(GetBiomeAt, "Get Name of Biome of Body,GeoCoordinates"));
            AddSuffix("ELEVATION", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, GeoCoordinates>(GetAltAt, "Get scanned altitude of Body,GeoCoordinates"));
            AddSuffix("COMPLETEDSCANS", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ListValue, BodyTarget, GeoCoordinates>(GetScans, "Returns the list of the completed scans of Body,GeoCoordinates"));
            AddSuffix("ALLSCANTYPES", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<StringValue>(GetScanNames, "Names of all scan types"));

            AddSuffix("RESOURCEAT", new kOS.Safe.Encapsulation.Suffixes.VarArgsSuffix<ScalarDoubleValue, Structure>(GetResourceByName, "Returns the amount of a resource by its scan type: Body,GeoCoordinates,scantype"));

        }

        private ScalarDoubleValue GetResourceByName(params Structure[] args )
        {
            if (args.Length != 3 ) { return null; }
            BodyTarget body = args.Where(s => s.GetType() == typeof(BodyTarget)).Cast<BodyTarget>().First();
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
                    CheckForLock = false,
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
            string[] blacklist = { "Nothing", "Altimetry", "Everything_SCAN", "Science", "Everything", "AllResources", "MKSResources", "KSPIResourece", "DefinedResources" , "SCANsat_1" };
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
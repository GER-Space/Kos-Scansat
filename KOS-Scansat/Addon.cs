	using System;
	using System.Linq;
	using kOS.Suffixed;
	using UnityEngine;
	//using SCANsat;
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

			AddSuffix("CURRENTBIOME", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<StringValue>(GetCurrentBiome, "Get Name of current Biome"));
			AddSuffix(new[] { "GETBIOME", "BIOMEAT" }, new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<StringValue, BodyTarget, GeoCoordinates>(GetBiomeAt, "Get Name of Biome of Body,GeoCoordinates"));
			AddSuffix("ELEVATION", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, GeoCoordinates>(GetAltAtSuffix, "Get scanned altitude of Body,GeoCoordinates"));
			AddSuffix("COMPLETEDSCANS", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ListValue, BodyTarget, GeoCoordinates>(GetScans, "Returns the list of the completed scans of Body,GeoCoordinates"));
			AddSuffix("ALLSCANTYPES", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<ListValue>(GetScanNames, "Names of all scan types"));
			AddSuffix("ALLRESOURCES", new kOS.Safe.Encapsulation.Suffixes.NoArgsSuffix<ListValue>(GetResourceNames, "List of all activated resource in the current game"));
			AddSuffix("RESOURCEAT", new kOS.Safe.Encapsulation.Suffixes.VarArgsSuffix<ScalarDoubleValue, Structure>(GetResourceByName, "Returns the amount of a resource by its scan type: Body,GeoCoordinates,scantype"));
			AddSuffix("SLOPE", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, GeoCoordinates>(GetSlope, "Returns the most accurate slope of the location"));
			AddSuffix("GETCOVERAGE", new kOS.Safe.Encapsulation.Suffixes.TwoArgsSuffix<ScalarDoubleValue, BodyTarget, StringValue>(GetCoverage, "Returns completen percatage of a body,scantype"));


            SCANWrapper = new SCANWrapper();
            if (IsModInstalled("scansat"))
            {
                SCANWrapper.InitReflection();
            }
		}

        internal SCANWrapper SCANWrapper;

#region suffix_functions
        ///<summary>
        ///returns the amount of a given resource for a body and place 
        /// takes the args <body> <geocoordinates> and <resource-string> in any order
        ///</summary>
        public ScalarDoubleValue GetResourceByName(params Structure[] args )
		{
		    if (args.Length != 3 ) { return null; }
		    BodyTarget body = args.Where(s => s.GetType() == typeof(BodyTarget)).Cast<BodyTarget>().First();
	//            BodyTarget body = args[0] as BodyTarget;
		    GeoCoordinates coordinate = args.Where(s => s.GetType() == typeof(GeoCoordinates)).Cast<GeoCoordinates>().First();
		    StringValue s_type = args.Where(s => s.GetType() == typeof(StringValue)).Cast<StringValue>().First();

		    if ( (SCANWrapper.IsCovered(coordinate.Longitude, coordinate.Latitude, body.Body, s_type)) || ((HasKerbNet("Resource") && (IsInKerbNetFoV(body.Body, coordinate.Longitude, coordinate.Latitude)))) )

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
			    CheckForLock = SCANWrapper.GetResourceBiomeLock(),
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

		///<summary>
		/// Suffix funtion for returing the completed scans for a spot
		/// takes the kos parameters <BodyTarget>, <GeoCoordinates>
		///</summary>
		public ListValue GetScans(BodyTarget body, GeoCoordinates coordinate)
		{
		    ListValue scans = new ListValue();
            if (IsModInstalled("scansat"))
            {
                foreach (string s_type in Enum.GetNames(typeof(SCANtype)))
                {
                    if (CheckScanBlacklisted(s_type)) { continue; }
                    if (SCANWrapper.IsCovered(coordinate.Longitude, coordinate.Latitude, body.Body, s_type))
                    {
                        scans.Add(new StringValue(s_type));
                    }
                }
            }
			return scans;
		}

		///<summary>
		/// Suffix function
		/// returns a list with the loaded resources.
		///</summary>
		public ListValue GetResourceNames()
		{
		    ListValue resources = new ListValue();
            if (IsModInstalled("scansat"))
            {
                foreach (SCANresourceGlobal res in SCANWrapper.GetLoadedResourceList())
                {
                    resources.Add(new StringValue(res.Name));
                }
            }
		    return resources;
		}

		///<summary>
		/// returns a list with the possible scan types.
		///</summary>
		public ListValue GetScanNames()
		{
		    ListValue allscans = new ListValue();
            if (IsModInstalled("scansat"))
            {
                foreach (string s_type in Enum.GetNames(typeof(SCANtype)))
                {
                    if (CheckScanBlacklisted(s_type)) { continue; }
                    allscans.Add(new StringValue(s_type));
                }
            }
		    return allscans;
		}
		

		///<summary>
		///returns the name of the Biome at the vessels postition
		///</summary>
		public StringValue GetCurrentBiome()
		{
		    var vessel = shared.Vessel;
		    var body = vessel.mainBody;
		    var Biome = "";

		    // check if we have crew onboard, which can look outside of a window, to determinate where we are or have we kerbnet access
		    if ( (HasKerbNet("Biome")) || (vessel.GetCrewCount() > 0) || (SCANWrapper.IsCovered(vessel.longitude, vessel.latitude, body, "Biome")) )
		    {
			Biome = string.IsNullOrEmpty(vessel.landedAt)
			? ScienceUtil.GetExperimentBiome(body, vessel.latitude, vessel.longitude)
			: Vessel.GetLandedAtString(vessel.landedAt).Replace(" ", "");
		    } else
		    {
			Biome = GetScannedBiomeName(body, vessel.longitude, vessel.latitude);
		    }

		    return Biome;
		}

		///<summary>
		/// Returns the name of the biome for a goepostion. Used in the calling suffix.
		/// takes parameter <CelestialBody>, <GeoCoordinates>
		///</summary>
		public StringValue GetBiomeAt(BodyTarget body, GeoCoordinates coordinate)
		{
		    return GetScannedBiomeName(body.Body, coordinate.Longitude, coordinate.Latitude);
		}

		///<summary>
		/// Suffix function for returing the altitude for a spot
		/// takes the kos parameters <BodyTarget>, <GeoCoordinates>
		///</summary>
		public ScalarDoubleValue GetAltAtSuffix(BodyTarget body, GeoCoordinates coordinate)
		{
			return GetAltAt(body.Body,coordinate.Longitude, coordinate.Latitude);
		}

		///<summary>
		/// Suffix funtion for returing completed percentage of a scantype for a body
		/// takes the kos parameters <BodyTarget>, <StrinValue> scantype
		///</summary>
		public ScalarDoubleValue GetCoverage(BodyTarget body, StringValue scantype)
		{
		    return SCANWrapper.GetCoverage(scantype,body.Body);
		}

		///<summary>
		/// Suffix funtion for returing the Slope for a spot
		/// takes the kos parameters <BodyTarget>, <GeoCoordinates>
		///</summary>
		public ScalarDoubleValue GetSlope(BodyTarget bodytgt, GeoCoordinates coordinate)
		{
		    double slope = -1;
		    double offsetm = 500;
		    double lon = coordinate.Longitude;
		    double lat = coordinate.Latitude;
		    CelestialBody body = bodytgt.Body;
		    
		    if ( (SCANWrapper.IsCovered(lon, lat, body, "AltimetryHiRes")) || ( (HasKerbNet("Terrain") && (IsInKerbNetFoV(body,lon,lat))) ) )
		    {
			offsetm = 5;
		    }
		    /*
		    double circum = body.Body.Radius * 2 * Math.PI;
		    double eqDistancePerDegree = circum / 360;
		    degreeOffset = 5 / eqDistancePerDegree;
		    */
		    double offset = offsetm/(body.Radius * 2 * Math.PI/360);
		    double latOffset = 0;
		    // matrix for z-values
		    double[] z = new double[9];

		    int i = 0;
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

		    return Math.Round(slope,2);
		}

#endregion
#region internal_function

		///<summary>
		/// checks if the mod with "assemblyName" is loaded into KSP.
		///</summary>
		internal static bool IsModInstalled(string assemblyName)
		{
		    Assembly assembly = (from a in AssemblyLoader.loadedAssemblies
					 where a.name.ToLower().Equals(assemblyName.ToLower())
					 select a).FirstOrDefault().assembly;
		    return assembly != null;
		}

		///<summary>
		///Returns if the vessel has the questioned KerbNet type available (and is connected
		///Takes the Parameter <string Type>
		///</summary>
		internal bool HasKerbNet(string kntype)
		{
			bool has_kerbnet = false;
			if (shared.Vessel.connection.IsConnectedHome || !HighLogic.CurrentGame.Parameters.Difficulty.EnableCommNet)
			{
				var kerbnets = shared.Vessel.FindPartModulesImplementing<ModuleKerbNetAccess>();
				if (kerbnets.Count > 0)
				{
					foreach (var net in kerbnets)
					{
						if (net.modes.Exists(s => s.Equals(kntype, StringComparison.OrdinalIgnoreCase)))
						{
							has_kerbnet = true;
						}
					}
				}
			}
			return has_kerbnet;
		}

		///<summary>
		///returns true is the given coordinates are within the Field of View of the KerbNet Scanner
		///</summary>
		internal bool IsInKerbNetFoV(CelestialBody body,double lng, double lat)
		{
			bool isinview = false;
			Vector3d body_vector = (body.position - shared.Vessel.CoMD);

			//check if we are orbiting the planet
			if (shared.Vessel.mainBody.name == body.name)
			{
				double altitude = GetElevation(body, lng, lat);
				Vector3d latLongCoords = body.GetWorldSurfacePosition(lat, lng, altitude);
				Vector3d hereCoords = shared.Vessel.CoMD;
				Vector3d tgt_position = (latLongCoords - hereCoords);
				// if the vector to the spot from body center is in our direction (opposite of the body direction,
				// then the spot is on our side of the body. 
				if (Vector3d.Dot(tgt_position - body_vector, body_vector) < 0)
				{
					double MaxFOV = 0.1;
					var kerbnets = shared.Vessel.FindPartModulesImplementing<ModuleKerbNetAccess>();
					if (kerbnets.Count > 0)
					{
						foreach (var net in kerbnets)
						{
                            MaxFOV = Math.Max(MaxFOV, net.GetKerbNetMaximumFoV());
						}
					}
					// check if we are inside the Field of View. The angle must be halve of the whole view.
					if (Vector3d.Angle(tgt_position, body_vector) < MaxFOV/2 )
					{
						isinview = true;
					}
				}
	
			}

			return isinview;
		}

		///<summary>
		/// Returns the name of the biome for a point
		/// takes parameter <CelestialBody>, <double lng>, <double lat>
		///</summary>
		internal string GetScannedBiomeName(CelestialBody body,double lng, double lat)
		{
		    if ( (SCANWrapper.IsCovered(lng,lat,body, "Biome")) || ( (HasKerbNet("Biome")) && (IsInKerbNetFoV(body,lng,lat)) ) )
		    {
			return ScienceUtil.GetExperimentBiome(body, lat, lng);
		    } else 	    
		    {
			return "unknown";
		    }
		}



		///<summary>
		/// internal function to return the altitude with the best scanned performance
		///</summary>
		internal double GetAltAt(CelestialBody body, double lon, double lat)
		{
			double altitude = -1;

			if ( (SCANWrapper.IsCovered(lon, lat, body, "AltimetryHiRes")) || ( (HasKerbNet("Terrain")) && (IsInKerbNetFoV(body,lon,lat)) ) )
			{
				altitude = GetElevation(body, lon, lat);
				return altitude;
			}
			if (SCANWrapper.IsCovered(lon, lat, body, "AltimetryLoRes"))
			{
				double alt = GetElevation(body, lon, lat);
				altitude = (Math.Round(alt / 500)) * 500;
				return altitude;
			}
			return altitude;
		}


	       ///<summary>
	       /// returns the altitude of a spot on a body
	       /// this function is copied from SCANsat
	       ///</summary>
		internal double GetElevation(CelestialBody body, double lon, double lat)
		{
		    if (body.pqsController == null) return 0;
		    double rlon = Mathf.Deg2Rad * lon;
		    double rlat = Mathf.Deg2Rad * lat;
		    Vector3d rad = new Vector3d(Math.Cos(rlat) * Math.Cos(rlon), Math.Sin(rlat), Math.Cos(rlat) * Math.Sin(rlon));
		    return Math.Round(body.pqsController.GetSurfaceHeight(rad) - body.pqsController.radius, 1);
		}



		///<summary>
		///returns if the ScanType is forbidden by the blacklist
		///</summary>
		internal bool CheckScanBlacklisted (string scanname)
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

#endregion
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SCANsat;
using SCANsat.SCAN_Data;

namespace kOS.AddOns.kOSSCANsat
{


    internal class SCANWrapper
    {
        internal SCANWrapper instance;

        internal bool scansatinstalled;
        internal SCANWrapper()
        {
            instance = this;
            scansatinstalled = Addon.IsModInstalled("scansat");
        }
        internal void InitReflection()
        {
            //for future use
        }

        internal int GetSCANtype(string s_type)
        {
            return SCANUtil.GetSCANtype(s_type);
        }

        internal bool GetResourceBiomeLock()
        {
            bool resourcelock = true;
            if (scansatinstalled)
            {
                resourcelock = SCANUtil.resourceBiomeLockEnabled();
            }
            return resourcelock;
        }

        internal bool IsCovered(double lon, double lat, CelestialBody body, string scan_type) {
            bool iscovered = false;
            if (scansatinstalled) {
                iscovered = SCANUtil.isCovered(lon, lat, body, SCANUtil.GetSCANtype(scan_type));
            }
            return iscovered;
        }

        internal double GetCoverage(string scantype, CelestialBody body)
        {
            double completed = 0d;
            if (scansatinstalled)
            {
                completed = SCANUtil.GetCoverage(SCANUtil.GetSCANtype(scantype), body);
            }
            return completed;
        }

        internal List<SCANresourceGlobal> GetLoadedResourceList()
        {
            return SCANcontroller.setLoadedResourceList();
        }
    

    } 
}

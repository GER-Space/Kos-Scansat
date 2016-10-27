using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SCANsat;
using SCANsat.SCAN_Data;

namespace kOS.AddOns.kOSSCANsat
{


    public class SCANWrapper
    {
        public SCANWrapper instance;

        internal bool scansatinstalled;
        public SCANWrapper()
        {
            instance = this;
            scansatinstalled = Addon.IsModInstalled("scansat");
        }
        public void InitReflection()
        {
            //for future use
        }

        public int GetSCANtype(string s_type)
        {
            return SCANUtil.GetSCANtype(s_type);
        }

        public bool GetResourceBiomeLock()
        {
            bool resourcelock = true;
            if (scansatinstalled)
            {
                resourcelock = SCANUtil.resourceBiomeLockEnabled();
            }
            return resourcelock;
        }

        public bool IsCovered(double lon, double lat, CelestialBody body, string scan_type) {
            bool iscovered = false;
            if (scansatinstalled) {
                iscovered = SCANUtil.isCovered(lon, lat, body, SCANUtil.GetSCANtype(scan_type));
            }
            return iscovered;
        }

        public double GetCoverage(string scantype, CelestialBody body)
        {
            double completed = 0d;
            if (scansatinstalled)
            {
                completed = SCANUtil.GetCoverage(SCANUtil.GetSCANtype(scantype), body);
            }
            return completed;
        }

        public List<SCANresourceGlobal> GetLoadedResourceList()
        {
            return SCANcontroller.setLoadedResourceList();
        }
    

    } 
}

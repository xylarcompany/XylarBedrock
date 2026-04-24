using XylarBedrock.UpdateProcessor.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XylarBedrock.UpdateProcessor.Interfaces
{
    public interface IVersionInfo
    {
        public string GetVersion();
        public Guid GetUUID();
        public string GetArchitecture();
        public VersionType GetVersionType();
        public bool GetIsBeta();
    }
}


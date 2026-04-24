using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XylarBedrock.Enums
{
    public enum LauncherState
    {
        None,
        isInitializing,
        isExtracting,
        isUninstalling,
        isLaunching,
        isDownloading,
        isBackingUp,
        isRemovingPackage,
        isRegisteringPackage,
        isCanceling
    }
}


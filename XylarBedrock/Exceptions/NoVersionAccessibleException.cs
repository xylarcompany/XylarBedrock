using System;

namespace XylarBedrock.Exceptions
{
    internal class NoVersionAccessibleException : Exception
    {
        public NoVersionAccessibleException(): base("This version can not be installed using the XylarBedrock.\n" +
            "This is likely due to the version being too old to be downloaded, or a faulty internet connection.") { }
    }
}


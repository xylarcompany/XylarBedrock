using System;

namespace XylarBedrock.Exceptions
{
    public class PackageDownloadFailedException : PackageManagerException
    {
        public PackageDownloadFailedException(string message, Exception innerException) : base(message, innerException) { }
        public PackageDownloadFailedException(Exception innerException) : base(innerException.Message, innerException) { }

    };
}


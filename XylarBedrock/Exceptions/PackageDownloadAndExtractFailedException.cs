using System;

namespace XylarBedrock.Exceptions
{
    public class PackageDownloadAndExtractFailedException : PackageManagerException
    {
        public PackageDownloadAndExtractFailedException(string message, Exception innerException) : base(message, innerException) { }
        public PackageDownloadAndExtractFailedException(Exception innerException) : base(innerException.Message, innerException) { }
    }
}


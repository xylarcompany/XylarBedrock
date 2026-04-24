using System;
using System.Runtime.Serialization;

namespace XylarBedrock.Exceptions
{

    public class PackageAddFailedException : PackageManagerException
    {
        public PackageAddFailedException(string message, Exception innerException) : base(message, innerException) { }
        public PackageAddFailedException(Exception innerException) : base(innerException.Message, innerException) { }

    };
}

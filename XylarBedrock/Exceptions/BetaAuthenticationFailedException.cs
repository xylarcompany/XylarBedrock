using System;

namespace XylarBedrock.Exceptions
{
    public class BetaAuthenticationFailedException : PackageManagerException
    {
        public BetaAuthenticationFailedException(string message, Exception innerException) : base(message, innerException) { }
        public BetaAuthenticationFailedException(Exception innerException) : base(innerException.Message, innerException) { }

    };
}


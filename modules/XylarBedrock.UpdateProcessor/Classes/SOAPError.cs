using System;

namespace XylarBedrock.UpdateProcessor.Classes
{
    public class SOAPError : Exception
    {
        public string code;
        public SOAPError(string code) : base()
        {

        }
    };
}


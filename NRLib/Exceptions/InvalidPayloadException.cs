using System;

namespace NRLib.Exceptions
{
    public class InvalidPayloadException : Exception
    {
        public InvalidPayloadException(string msg = "The specified payload is invalid") : base(msg)
        {
            
        }
    }
}
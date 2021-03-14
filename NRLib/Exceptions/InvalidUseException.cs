using System;

namespace NRLib.Exceptions
{
    public class InvalidUseException : Exception
    {
        public InvalidUseException(string message = null) : base(message)
        {
            
        }
    }
}
using System;

namespace UltimateStartPage.Core.Services
{
    /// <summary>Thrown when a layout file exists but cannot be understood.</summary>
    public sealed class LayoutFormatException : Exception
    {
        public LayoutFormatException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}

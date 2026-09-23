using System;

namespace UltimateStartPage.Core.Services
{
    /// <summary>
    /// Thrown by <see cref="ILayoutStore.SaveAsync"/> when the layout file was changed by someone else (another
    /// Visual Studio instance, a sync client or a hand edit) since this store last loaded or saved it.
    /// </summary>
    public sealed class LayoutConflictException : Exception
    {
        public LayoutConflictException(string message)
            : base(message)
        {
        }
    }
}

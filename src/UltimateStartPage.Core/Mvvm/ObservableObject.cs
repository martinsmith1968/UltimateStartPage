using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UltimateStartPage.Core.Mvvm
{
    /// <summary>
    /// Lightweight base class for observable view models. Hand-rolled because
    /// CommunityToolkit.Mvvm 8.x requires .NET 8 (net472 is incompatible) and
    /// 7.x source generators require C# 9+ partial properties. Keeping Core
    /// zero-external-dependency for MVVM infrastructure is the right call.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

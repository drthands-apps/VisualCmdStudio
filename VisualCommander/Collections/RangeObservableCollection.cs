using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace VisualCommander.Collections
{
    /// <summary>
    /// ObservableCollection con AddRange que dispara un solo evento
    /// CollectionChanged de tipo Reset al añadir muchos elementos de golpe.
    /// Muy útil para rendimiento con colecciones grandes.
    /// </summary>
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification;

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) return;

            _suppressNotification = true;

            try
            {
                foreach (var item in items)
                    Add(item);
            }
            finally
            {
                _suppressNotification = false;
            }

            // Un solo evento al final.
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotification) return;
            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_suppressNotification) return;
            base.OnPropertyChanged(e);
        }
    }
}
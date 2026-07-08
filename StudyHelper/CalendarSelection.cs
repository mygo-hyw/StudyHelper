using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace StudyHelper
{
    public static class CalendarSelection
    {
        public static readonly DependencyProperty SelectedDatesProperty =
            DependencyProperty.RegisterAttached(
                "SelectedDates",
                typeof(IEnumerable<DateTime>),
                typeof(CalendarSelection),
                new PropertyMetadata(null, OnSelectedDatesChanged));

        private static readonly DependencyProperty CollectionChangedHandlerProperty =
            DependencyProperty.RegisterAttached(
                "CollectionChangedHandler",
                typeof(NotifyCollectionChangedEventHandler),
                typeof(CalendarSelection),
                new PropertyMetadata(null));

        public static IEnumerable<DateTime>? GetSelectedDates(DependencyObject element)
        {
            return (IEnumerable<DateTime>?)element.GetValue(SelectedDatesProperty);
        }

        public static void SetSelectedDates(DependencyObject element, IEnumerable<DateTime>? value)
        {
            element.SetValue(SelectedDatesProperty, value);
        }

        private static NotifyCollectionChangedEventHandler? GetCollectionChangedHandler(DependencyObject element)
        {
            return (NotifyCollectionChangedEventHandler?)element.GetValue(CollectionChangedHandlerProperty);
        }

        private static void SetCollectionChangedHandler(DependencyObject element, NotifyCollectionChangedEventHandler? value)
        {
            element.SetValue(CollectionChangedHandlerProperty, value);
        }

        private static void OnSelectedDatesChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not Calendar calendar)
            {
                return;
            }

            if (e.OldValue is INotifyCollectionChanged oldCollection &&
                GetCollectionChangedHandler(calendar) is NotifyCollectionChangedEventHandler oldHandler)
            {
                oldCollection.CollectionChanged -= oldHandler;
            }

            ApplySelectedDates(calendar, e.NewValue as IEnumerable<DateTime>);

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                NotifyCollectionChangedEventHandler handler = (_, _) =>
                    ApplySelectedDates(calendar, GetSelectedDates(calendar));

                newCollection.CollectionChanged += handler;
                SetCollectionChangedHandler(calendar, handler);
            }
            else
            {
                SetCollectionChangedHandler(calendar, null);
            }
        }

        private static void ApplySelectedDates(Calendar calendar, IEnumerable<DateTime>? dates)
        {
            calendar.SelectedDates.Clear();

            if (dates == null)
            {
                return;
            }

            foreach (DateTime date in dates)
            {
                calendar.SelectedDates.Add(date.Date);
            }
        }
    }
}

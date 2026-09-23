using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UltimateStartPage.Core.ViewModels;

namespace UltimateStartPage.ToolWindows
{
    public partial class StartPageControl : UserControl
    {
        public StartPageControl(StartPageViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private StartPageViewModel ViewModel => (StartPageViewModel)DataContext;

        /// <summary>Fires when the tool window is opened or its document tab is switched to.</summary>
        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible && visible)
            {
                ViewModel.OnPageShown();
            }
        }

        private void OnSectionDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnSectionDrop(object sender, DragEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SectionViewModel section
                && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                ViewModel.AddDroppedPaths(section, paths);
                e.Handled = true;
            }
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.DataContext = DataContext;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}

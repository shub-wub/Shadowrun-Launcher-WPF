using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShadowrunLauncher
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Point startPoint;

        public MainWindow()
        {
            InitializeComponent();
            closeButton.PreviewMouseLeftButtonDown += CloseButton_PreviewMouseLeftButtonDown;
            closeButton.PreviewMouseLeftButtonUp += CloseButton_PreviewMouseLeftButtonUp;
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            MouseMove += MainWindow_MouseMove;
            MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;
        }

        private void CloseButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/close_button_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            closeImage.Source = bitmap;

            // Reduce scale of the image from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
            closeImage.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void CloseButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source and scale of the close button
            Uri uri = new Uri("pack://application:,,,/Images/close_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            closeImage.Source = bitmap;
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            closeImage.RenderTransform = scaleTransform;

            // Check if the mouse was released within the bounds of the close button
            Point position = e.GetPosition(closeButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < closeButton.ActualWidth && position.Y < closeButton.ActualHeight)
            {
                // Close the application
                Application.Current.Shutdown();
            }
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = true;
                startPoint = e.GetPosition(this);
            }
        }

        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPoint = e.GetPosition(this);
                double deltaX = currentPoint.X - startPoint.X;
                double deltaY = currentPoint.Y - startPoint.Y;
                Left += deltaX;
                Top += deltaY;
            }
        }

        private void MainWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = false;
            }
        }
    }
}

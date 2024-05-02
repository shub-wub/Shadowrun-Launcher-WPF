using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Net;
using ShadowrunLauncher.Logic;

namespace ShadowrunLauncher
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Point startPoint;
        private InstallLogic _installLogic;

        public MainWindow()
        {
            InitializeComponent();
            StartGlowAnimation();
            closeButton.PreviewMouseLeftButtonDown += CloseButton_PreviewMouseLeftButtonDown;
            closeButton.PreviewMouseLeftButtonUp += CloseButton_PreviewMouseLeftButtonUp;
            closeButton.MouseEnter += CloseButton_MouseEnter;
            closeButton.MouseLeave += CloseButton_MouseLeave;
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            MouseMove += MainWindow_MouseMove;
            MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;

            // Add the new button event handlers
            questionButton.PreviewMouseLeftButtonDown += QuestionButton_PreviewMouseLeftButtonDown;
            questionButton.PreviewMouseLeftButtonUp += QuestionButton_PreviewMouseLeftButtonUp;
            questionButton.MouseEnter += QuestionButton_MouseEnter;
            questionButton.MouseLeave += QuestionButton_MouseLeave;

            minimizeButton.PreviewMouseLeftButtonDown += MinimizeButton_PreviewMouseLeftButtonDown;
            minimizeButton.PreviewMouseLeftButtonUp += MinimizeButton_PreviewMouseLeftButtonUp;
            minimizeButton.MouseEnter += MinimizeButton_MouseEnter;
            minimizeButton.MouseLeave += MinimizeButton_MouseLeave;

            _installLogic = new InstallLogic(this);
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            _installLogic.CheckForUpdates();
        }

        internal void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            _installLogic.PlayButtonClickLogic(sender, e);
        }

        private void StartGlowAnimation()
        {
            Storyboard glowAnimation = (Storyboard)FindResource("GlowAnimation");
            if (glowAnimation != null)
            {
                glowingImage.BeginStoryboard(glowAnimation);
            }
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

        private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/close_button_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            closeImage.Source = bitmap;
        }

        private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/close_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            closeImage.Source = bitmap;
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

        private void QuestionButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/question_button_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            questionImage.Source = bitmap;

            // Reduce scale of the image from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
            questionImage.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void QuestionButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source and scale of the question button
            Uri uri = new Uri("pack://application:,,,/Images/question_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            questionImage.Source = bitmap;
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            questionImage.RenderTransform = scaleTransform;

            // Open the specified link in the default web browser if the mouse was released within the button bounds
            Point position = e.GetPosition(questionButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < questionButton.ActualWidth && position.Y < questionButton.ActualHeight)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/shub-wub/Shadowrun-Launcher-WPF",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening link: " + ex.Message);
                }
            }
        }

        private void QuestionButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/question_button_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            questionImage.Source = bitmap;
        }

        private void QuestionButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/question_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            questionImage.Source = bitmap;
        }

        private void MinimizeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/minimize_button_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            minimizeImage.Source = bitmap;

            // Reduce scale of the image from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
            minimizeImage.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void MinimizeButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source and scale of the minimize button
            Uri uri = new Uri("pack://application:,,,/Images/minimize_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            minimizeImage.Source = bitmap;
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            minimizeImage.RenderTransform = scaleTransform;

            // Minimize the window if the mouse was released within the button bounds
            Point position = e.GetPosition(minimizeButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < minimizeButton.ActualWidth && position.Y < minimizeButton.ActualHeight)
            {
                WindowState = WindowState.Minimized;
            }
        }

        private void MinimizeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/minimize_button_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            minimizeImage.Source = bitmap;
        }

        private void MinimizeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/minimize_button.png");
            BitmapImage bitmap = new BitmapImage(uri);
            minimizeImage.Source = bitmap;
        }
    }
}

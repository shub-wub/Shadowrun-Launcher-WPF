using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShadowrunLauncher.Logic;
using System.Diagnostics;
using System.IO;

namespace ShadowrunLauncher
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Point startPoint;
        private InstallLogic _installLogic;

        // Dictionary to hold MediaPlayer instances for different sound channels
        private Dictionary<string, MediaPlayer> soundChannels = new Dictionary<string, MediaPlayer>();

        //// Adjust this value to set the background music volume (0.0 to 1.0)
        private double backgroundMusicVolume = 0.1;
        private double buttonClickVolume = 0.4;

        public MainWindow()
        {
            InitializeComponent();
            StartGlowAnimation();
            MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            MouseMove += MainWindow_MouseMove;
            MouseLeftButtonUp += MainWindow_MouseLeftButtonUp;

            // Buttons
            playButton.PreviewMouseLeftButtonDown += PlayButton_PreviewMouseLeftButtonDown;
            playButton.PreviewMouseLeftButtonUp += PlayButton_PreviewMouseLeftButtonUp;
            playButton.MouseEnter += PlayButton_MouseEnter;
            playButton.MouseLeave += PlayButton_MouseLeave;

            questionButton.PreviewMouseLeftButtonDown += QuestionButton_PreviewMouseLeftButtonDown;
            questionButton.PreviewMouseLeftButtonUp += QuestionButton_PreviewMouseLeftButtonUp;
            questionButton.MouseEnter += QuestionButton_MouseEnter;
            questionButton.MouseLeave += QuestionButton_MouseLeave;
            
            minimizeButton.PreviewMouseLeftButtonDown += MinimizeButton_PreviewMouseLeftButtonDown;
            minimizeButton.PreviewMouseLeftButtonUp += MinimizeButton_PreviewMouseLeftButtonUp;
            minimizeButton.MouseEnter += MinimizeButton_MouseEnter;
            minimizeButton.MouseLeave += MinimizeButton_MouseLeave;

            closeButton.PreviewMouseLeftButtonDown += CloseButton_PreviewMouseLeftButtonDown;
            closeButton.PreviewMouseLeftButtonUp += CloseButton_PreviewMouseLeftButtonUp;
            closeButton.MouseEnter += CloseButton_MouseEnter;
            closeButton.MouseLeave += CloseButton_MouseLeave;

            // Initialize InstallLogic
            _installLogic = new InstallLogic(this);
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            ButtonHandler.installLogic.CheckForUpdates();
        }

        private void InitializeSoundChannels()
        {
            // Add sound channels for different types of sounds
            AddSoundChannel("backgroundMusic");
            AddSoundChannel("buttonClick");
            AddSoundChannel("buttonHover");
        }

        private void AddSoundChannel(string channelName)
        {
            // Create a new MediaPlayer instance for the channel
            soundChannels[channelName] = new MediaPlayer();
        }

        private void PlaySound(string relativePath, string channelName, double volume)
        {
            // Get the absolute path of the audio file
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string absolutePath = Path.Combine(basePath, relativePath);

            // Check if the file exists
            if (File.Exists(absolutePath))
            {
                // Get the MediaPlayer instance for the specified channel
                MediaPlayer mediaPlayer = soundChannels[channelName];

                // Load the sound file
                mediaPlayer.Open(new Uri(absolutePath));

                // Set the volume
                mediaPlayer.Volume = volume;

                // Play the sound
                mediaPlayer.Play();
            }
            else
            {
                MessageBox.Show("Sound file not found.");
            }
        }

        private void PlayBackgroundMusic(string relativePath)
        {
            // Use the class-level variable directly to set the volume
            PlaySound(relativePath, "backgroundMusic", backgroundMusicVolume);
        }

        private void SetBackgroundMusicVolume(double volume)
        {
            // Check if the background music player exists and has a source set
            if (soundChannels.ContainsKey("backgroundMusic") && soundChannels["backgroundMusic"].Source != null)
            {
                // Ensure the volume is within the valid range (0.0 to 1.0)
                if (volume < 0.0)
                    volume = 0.0;
                else if (volume > 1.0)
                    volume = 1.0;

                // Set the volume of the background music player
                soundChannels["backgroundMusic"].Volume = volume;

                // Update the backgroundMusicVolume property
                backgroundMusicVolume = volume;
            }
        }

        private void StartGlowAnimation()
        {
            Storyboard glowAnimation = (Storyboard)FindResource("GlowAnimation");
            if (glowAnimation != null)
            {
                glowingImage.BeginStoryboard(glowAnimation);
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

        private void PlayButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            playImage.Source = bitmap;

            // Reduce scale of the grid containing both the image and text from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
            playGrid.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        //private void PlayButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // Reset the image source and scale of the play button
        //    Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
        //    BitmapImage bitmap = new BitmapImage(uri);
        //    playImage.Source = bitmap;
        //    ScaleTransform scaleTransform = new ScaleTransform(1, 1);
        //    playGrid.RenderTransform = scaleTransform;

            // Check if the mouse was released within the bounds of the play button
            Point position = e.GetPosition(playButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < playButton.ActualWidth && position.Y < playButton.ActualHeight)
            {
                // Play the application
                _installLogic.PlayButtonClickLogic(sender, e);
            }
        }

        private void PlayButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            playImage.Source = bitmap;
        }

        private void PlayButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
            BitmapImage bitmap = new BitmapImage(uri);
            playImage.Source = bitmap;
        }

        private void CloseButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/close_button_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            closeImage.Source = bitmap;

        //    // Reduce scale of the grid containing both the image and text from the center
        //    ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
        //    closeGrid.RenderTransform = scaleTransform;

        //    // Optionally, you can add an animation for a smooth effect
        //    DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        //}

        //private void CloseButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // Reset the image source and scale of the close button
        //    Uri uri = new Uri("pack://application:,,,/Images/close_button.png");
        //    BitmapImage bitmap = new BitmapImage(uri);
        //    closeImage.Source = bitmap;
        //    ScaleTransform scaleTransform = new ScaleTransform(1, 1);
        //    closeGrid.RenderTransform = scaleTransform;

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

        //private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    // Reset the image source to the default version
        //    Uri uri = new Uri("pack://application:,,,/Images/close_button.png");
        //    BitmapImage bitmap = new BitmapImage(uri);
        //    closeImage.Source = bitmap;
        //}

        private void QuestionButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            questionImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/question_button_clicked.png"));

        //    // Reduce scale of the grid containing both the image and text from the center
        //    ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
        //    questionGrid.RenderTransform = scaleTransform;

        //    // Optionally, you can add an animation for a smooth effect
        //    DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        //}

        //private void QuestionButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // Reset the image source and scale of the question button
        //    questionImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/question_button.png"));
        //    ScaleTransform scaleTransform = new ScaleTransform(1, 1);
        //    questionGrid.RenderTransform = scaleTransform;

            // Open the specified link in the default web browser if the mouse was released within the button bounds
            Point position = e.GetPosition(questionButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < questionButton.ActualWidth && position.Y < questionButton.ActualHeight)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
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
            questionImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/question_button_highlight.png"));
        }

        //private void QuestionButton_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    // Reset the image source to the default version
        //    questionImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/question_button.png"));
        //}

        private void MinimizeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Change the image source to the clicked version
            minimizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/minimize_button_clicked.png"));

        //    // Reduce scale of the grid containing both the image and text from the center
        //    ScaleTransform scaleTransform = new ScaleTransform(0.9, 0.9); // Scale factor (0.95) can be adjusted
        //    minimizeGrid.RenderTransform = scaleTransform;

        //    // Optionally, you can add an animation for a smooth effect
        //    DoubleAnimation animation = new DoubleAnimation(1.0, 0.9, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        //    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        //}

        //private void MinimizeButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // Reset the image source and scale of the minimize button
        //    minimizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/minimize_button.png"));
        //    ScaleTransform scaleTransform = new ScaleTransform(1, 1);
        //    minimizeGrid.RenderTransform = scaleTransform;

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
            minimizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/minimize_button_highlight.png"));
        }

        //private void MinimizeButton_MouseLeave(object sender, MouseEventArgs e)
        //{
        //    // Reset the image source to the default version
        //    minimizeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/minimize_button.png"));
        //}
    }
}

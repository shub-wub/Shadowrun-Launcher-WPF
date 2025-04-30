using ShadowrunLauncher.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ShadowrunLauncher
{
    /// <summary>
    /// Interaction logic for KeyDisplay.xaml
    /// </summary>
    public partial class KeyDisplay : Window
    {
        private GenerateKeyLogic _generateKeyLogic;
        private InstallLogic _installLogic;
        private static string currentkey = " ";
        static Random random = new Random();
        private double buttonClickVolume = 0.4;
        // Dictionary to hold MediaPlayer instances for different sound channels
        private Dictionary<string, MediaPlayer> soundChannels = new Dictionary<string, MediaPlayer>();
        internal KeyDisplay(InstallLogic installLogic, string key = "", bool IsGen = false)
        {
            InitializeComponent();
            InitializeSoundChannels();
            _installLogic = installLogic;
            _generateKeyLogic = new GenerateKeyLogic(installLogic);

            // Buttons
            copyActivateButton.PreviewMouseLeftButtonDown += CopyActivateButton_PreviewMouseLeftButtonDown;
            copyActivateButton.PreviewMouseLeftButtonUp += CopyActivateButton_PreviewMouseLeftButtonUp;
            copyActivateButton.MouseEnter += CopyActivateButton_MouseEnter;
            copyActivateButton.MouseLeave += CopyActivateButton_MouseLeave;

            nextButton.PreviewMouseLeftButtonDown += NextButton_PreviewMouseLeftButtonDown;
            nextButton.PreviewMouseLeftButtonUp += NextButton_PreviewMouseLeftButtonUp;
            nextButton.MouseEnter += NextButton_MouseEnter;
            nextButton.MouseLeave += NextButton_MouseLeave;

            SetKey(key);

            //closeButton.PreviewMouseLeftButtonDown += CloseButton_PreviewMouseLeftButtonDown;
            closeButton.PreviewMouseLeftButtonUp += CloseButton_PreviewMouseLeftButtonUp;
        }
        private void InitializeSoundChannels()
        {
            // Add sound channels for different types of sounds
            AddSoundChannel("buttonClick");
            AddSoundChannel("buttonHover");
        }
        private void AddSoundChannel(string channelName)
        {
            // Create a new MediaPlayer instance for the channel
            soundChannels[channelName] = new MediaPlayer();
        }
        private void SetKey(string key)
        {
            if(key != null)
            {
                return;
            }
            currentkey = key;
            string[] Keysector = key.Split('-');
            r1.Text = Keysector[0];
            r2.Text = Keysector[1];
            r3.Text = Keysector[2];
            r4.Text = Keysector[3];
            r5.Text = Keysector[4];
        }
        private void exit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void CopyActivateButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Play Sound On Click
            PlaySound("Audio/buttonClick.wav", "buttonClick", buttonClickVolume);

            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            copyActivateImage.Source = bitmap;

            // Reduce scale of the grid containing both the image and text from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.95, 0.95); // Scale factor (0.95) can be adjusted
            copyActivateGrid.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.95, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void CopyActivateButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source and scale of the play button
            Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
            BitmapImage bitmap = new BitmapImage(uri);
            copyActivateImage.Source = bitmap;
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            copyActivateGrid.RenderTransform = scaleTransform;

            DoubleAnimation animation = new DoubleAnimation(0.95, 1, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            // Check if the mouse was released within the bounds of the play button
            Point position = e.GetPosition(copyActivateButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < copyActivateButton.ActualWidth && position.Y < copyActivateButton.ActualHeight)
            {
                // Play the application
            }
        }

        private void CopyActivateButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Play Sound On Mouseover
            PlaySound("Audio/buttonHover.wav", "buttonHover", buttonClickVolume);

            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            copyActivateImage.Source = bitmap;
        }

        private void CopyActivateButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
            BitmapImage bitmap = new BitmapImage(uri);
            copyActivateImage.Source = bitmap;
        }

        private void NextButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Play Sound On Click
            PlaySound("Audio/buttonClick.wav", "buttonClick", buttonClickVolume);

            // Change the image source to the clicked version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_clicked.png");
            BitmapImage bitmap = new BitmapImage(uri);
            nextImage.Source = bitmap;

            // Reduce scale of the grid containing both the image and text from the center
            ScaleTransform scaleTransform = new ScaleTransform(0.95, 0.95); // Scale factor (0.95) can be adjusted
            nextGrid.RenderTransform = scaleTransform;

            // Optionally, you can add an animation for a smooth effect
            DoubleAnimation animation = new DoubleAnimation(1.0, 0.95, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void NextButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source and scale of the play button
            Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
            BitmapImage bitmap = new BitmapImage(uri);
            nextImage.Source = bitmap;
            ScaleTransform scaleTransform = new ScaleTransform(1, 1);
            nextGrid.RenderTransform = scaleTransform;

            DoubleAnimation animation = new DoubleAnimation(0.95, 1, TimeSpan.FromSeconds(0.1)); // Duration (0.2 seconds) can be adjusted
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            // Check if the mouse was released within the bounds of the play button
            Point position = e.GetPosition(nextButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < nextButton.ActualWidth && position.Y < nextButton.ActualHeight)
            {
                // Reset PCID from backup
            }
        }

        private void NextButton_MouseEnter(object sender, MouseEventArgs e)
        {
            // Play Sound On Mouseover
            PlaySound("Audio/buttonHover.wav", "buttonHover", buttonClickVolume);

            // Change the image source to the highlighted version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic_highlight.png");
            BitmapImage bitmap = new BitmapImage(uri);
            nextImage.Source = bitmap;
        }

        private void NextButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            Uri uri = new Uri("pack://application:,,,/Images/button_generic.png");
            BitmapImage bitmap = new BitmapImage(uri);
            nextImage.Source = bitmap;
        }

        private void CloseButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Check if the mouse was released within the bounds of the close button
            Point position = e.GetPosition(closeButton);
            if (position.X >= 0 && position.Y >= 0 && position.X < closeButton.ActualWidth && position.Y < closeButton.ActualHeight)
            {
                // Close the application
                this.Close();
            }
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
    }
}

using ShadowrunLauncher.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ShadowrunLauncher
{
    internal class buttonHandler
    {
        // Inside MainWindow class

        private BitmapImage defaultPlayImage;
        private BitmapImage clickedPlayImage;
        private ScaleTransform playScaleTransform;

        public buttonHandler()
        {
            InitializeButtonImages();
            InitializeButtonEvents();
        }

        private void InitializeButtonImages()
        {
            defaultPlayImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic.png"));
            clickedPlayImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic_clicked.png"));

            // Initialize other button images here
        }

        private void InitializeButtonEvents()
        {

            // Initialize events for other buttons here
        }

        private void Button_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button button = (Button)sender;
            BitmapImage clickedImage;
            ScaleTransform scaleTransform;

            if (button == playButton)
            {
                clickedImage = clickedPlayImage;
                scaleTransform = playScaleTransform;
            }
            else if (button == closeButton)
            {
                // Assign clicked image and scale transform for close button
            }
            // Handle other buttons similarly

            // Change image source
            ((Image)button.Content).Source = clickedImage;

            // Apply scale transformation
            ScaleButton(button, scaleTransform);

            // Optionally, you can add an animation for a smooth effect
            ApplyButtonAnimation(scaleTransform);
        }

        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Button button = (Button)sender;
            BitmapImage defaultImage;
            ScaleTransform scaleTransform;

            if (button == playButton)
            {
                defaultImage = defaultPlayImage;
                scaleTransform = playScaleTransform;
            }
            else if (button == closeButton)
            {
                // Assign default image and scale transform for close button
            }
            // Handle other buttons similarly

            // Reset image source
            ((Image)button.Content).Source = defaultImage;

            // Reset scale transformation
            ResetButtonScale(button);

            // Check if the mouse was released within the bounds of the button
            if (IsMouseWithinButtonBounds(button))
            {
                // Perform button action
                PerformButtonAction(button);
            }
        }

        private void ScaleButton(Button button, ScaleTransform scaleTransform)
        {
            scaleTransform.ScaleX = 0.9;
            scaleTransform.ScaleY = 0.9;
            button.RenderTransform = scaleTransform;
        }

        private void ResetButtonScale(Button button)
        {
            ScaleTransform scaleTransform = GetScaleTransformForButton(button);
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
        }

        private ScaleTransform GetScaleTransformForButton(Button button)
        {
            // Return appropriate scale transform for the button
        }

        private bool IsMouseWithinButtonBounds(Button button)
        {
            // Check if the mouse was released within the bounds of the button
        }

        private void PerformButtonAction(Button button)
        {
            // Perform action based on the button clicked
        }

        private void ApplyButtonAnimation(ScaleTransform scaleTransform)
        {
            // Apply animation for smooth effect
        }

    }
}

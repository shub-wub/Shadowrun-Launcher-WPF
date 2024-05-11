using ShadowrunLauncher.Logic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShadowrunLauncher
{
    public class ButtonHandler : Button
    {
        private ScaleTransform scaleTransform = new ScaleTransform();
        internal static InstallLogic installLogic = null;

        public ButtonHandler()
        {
            InitializeButton();
            InitializeEvents();
        }

        private void InitializeButton()
        {
            this.RenderTransformOrigin = new Point(0.5, 0.5);
            this.RenderTransform = scaleTransform;
        }

        private void InitializeEvents()
        {
            this.PreviewMouseLeftButtonDown += Button_MouseLeftButtonDown;
            this.PreviewMouseLeftButtonUp += Button_PreviewMouseLeftButtonUp;
            this.MouseEnter += Button_MouseEnter;
            this.MouseLeave += Button_MouseLeave;
        }

        private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            Button button = (Button)sender;
            // Check if the mouse was released within the bounds of the play button
            Point position = e.GetPosition(button);
            switch (button.Name)
            {
                case "playButton":
                    PerformPlayButtonAction(sender, e, button, position);
                    break;
                case "closeButton":
                    PerformCloseButtonAction(button, position);
                    break;
                default:
                    PerformDefualtButtonAction();
                    break;
            }

        }

        private void PerformDefualtButtonAction()
        {
            // Change the image source to the clicked version
            BitmapImage clickedImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic_clicked.png"));
            SetButtonImage(clickedImage);

            // Apply scale transformation
            ScaleButton(0.9);
        }

        private void PerformPlayButtonAction(object sender, MouseButtonEventArgs e, Button button, Point position)
        {
            // Change the image source to the clicked version
            BitmapImage playclickedImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic_clicked.png"));
            SetButtonImage(playclickedImage);

            // Apply scale transformation
            ScaleButton(0.9);


            if (position.X >= 0 && position.Y >= 0 && position.X < button.ActualWidth && position.Y < button.ActualHeight)
            {
                // Play the application
                installLogic.PlayButtonClickLogic();
            }
        }

        private void PerformCloseButtonAction(Button button, Point position)
        {
            // Change the image source to the clicked version
            BitmapImage closeclickedImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic_clicked.png"));
            SetButtonImage(closeclickedImage);

            // Apply scale transformation
            ScaleButton(0.9);
            if (position.X >= 0 && position.Y >= 0 && position.X < button.ActualWidth && position.Y < button.ActualHeight)
            {
                // Close the application
                Application.Current.Shutdown();
            }
        }

        private void Button_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Reset the image source
            BitmapImage defaultImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic.png"));
            SetButtonImage(defaultImage);

            // Reset scale transformation
            ScaleButton(1.0);

            // Check if the mouse was released within the bounds of the button
            if (IsMouseWithinButtonBounds())
            {
                // Perform button action
                PerformButtonAction();
            }
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the image source to the highlighted version
            BitmapImage highlightImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic_highlight.png"));
            SetButtonImage(highlightImage);
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset the image source to the default version
            BitmapImage defaultImage = new BitmapImage(new Uri("pack://application:,,,/Images/button_generic.png"));
            SetButtonImage(defaultImage);
        }

        private void SetButtonImage(BitmapImage image)
        {
            Image? buttonImage = this.Content as Image;
            if (buttonImage != null)
            {
                buttonImage.Source = image;
            }
        }

        private void ScaleButton(double scale)
        {
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;
        }

        private bool IsMouseWithinButtonBounds()
        {
            Point mousePosition = Mouse.GetPosition(this);
            return new Rect(0, 0, this.ActualWidth, this.ActualHeight).Contains(mousePosition);
        }

        private void PerformButtonAction()
        {
            // Perform action based on the button clicked
        }
    }
}

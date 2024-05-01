using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Controls;


namespace ShadowrunLauncher
{
    public static class ImageProperties
    {
        public static readonly DependencyProperty RotationCenterProperty =
            DependencyProperty.RegisterAttached("RotationCenter", typeof(Point), typeof(ImageProperties), new PropertyMetadata(new Point(0.5, 0.5), OnRotationCenterChanged));

        public static readonly DependencyProperty RotationDirectionProperty =
            DependencyProperty.RegisterAttached("RotationDirection", typeof(RotationDirection), typeof(ImageProperties), new PropertyMetadata(RotationDirection.Clockwise));

        public static readonly DependencyProperty RotationSpeedProperty =
            DependencyProperty.RegisterAttached("RotationSpeed", typeof(double), typeof(ImageProperties), new PropertyMetadata(2.0));

        public static readonly DependencyProperty IsRotatingProperty =
            DependencyProperty.RegisterAttached("IsRotating", typeof(bool), typeof(ImageProperties), new PropertyMetadata(false, OnIsRotatingChanged));

        public static Point GetRotationCenter(DependencyObject obj)
        {
            return (Point)obj.GetValue(RotationCenterProperty);
        }

        public static void SetRotationCenter(DependencyObject obj, Point value)
        {
            obj.SetValue(RotationCenterProperty, value);
        }

        public static RotationDirection GetRotationDirection(DependencyObject obj)
        {
            return (RotationDirection)obj.GetValue(RotationDirectionProperty);
        }

        public static void SetRotationDirection(DependencyObject obj, RotationDirection value)
        {
            obj.SetValue(RotationDirectionProperty, value);
        }

        public static double GetRotationSpeed(DependencyObject obj)
        {
            return (double)obj.GetValue(RotationSpeedProperty);
        }

        public static void SetRotationSpeed(DependencyObject obj, double value)
        {
            obj.SetValue(RotationSpeedProperty, value);
        }

        public static bool GetIsRotating(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsRotatingProperty);
        }

        public static void SetIsRotating(DependencyObject obj, bool value)
        {
            obj.SetValue(IsRotatingProperty, value);
        }

        private static void OnRotationCenterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image)
            {
                UpdateRotationTransform(image);
            }
        }

        private static void OnIsRotatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image)
            {
                if ((bool)e.NewValue)
                {
                    // Start rotating
                    UpdateRotationTransform(image);
                }
                else
                {
                    // Stop rotating
                    image.BeginAnimation(Image.RenderTransformProperty, null);
                }
            }
        }

        private static void UpdateRotationTransform(Image image)
        {
            Point center = GetRotationCenter(image);
            double speed = GetRotationSpeed(image);
            RotationDirection direction = GetRotationDirection(image);

            RotateTransform rotateTransform = new RotateTransform();
            rotateTransform.CenterX = center.X * image.ActualWidth;
            rotateTransform.CenterY = center.Y * image.ActualHeight;
            rotateTransform.Angle = 0;

            DoubleAnimation animation = new DoubleAnimation();
            animation.From = 0;
            animation.To = direction == RotationDirection.Clockwise ? 360 : -360;
            animation.Duration = TimeSpan.FromSeconds(360 / speed);
            animation.RepeatBehavior = RepeatBehavior.Forever;

            image.RenderTransformOrigin = new Point(center.X, center.Y);
            image.RenderTransform = rotateTransform;

            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
    }

    public enum RotationDirection
    {
        Clockwise,
        CounterClockwise
    }
}

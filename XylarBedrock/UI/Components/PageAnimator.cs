using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace XylarBedrock.UI.Components
{
    public static class PageAnimator
    {
        private class AnimationArgs
        {
           public double stored_width { get; set; }
           public double stored_height { get; set; }
           public double stored_min_width { get; set; }
           public double stored_min_height { get; set; }
           public double stored_max_width { get; set; }
           public double stored_max_height { get; set; }
           public Page CurrentContent { get; set; }
        }

        #region General Methods

        private static void StorePageValues(AnimationArgs args)
        {
            if (args.CurrentContent == null) return;

            args.stored_min_width = args.CurrentContent.MinWidth;
            args.stored_min_height = args.CurrentContent.MinHeight;

            args.stored_max_width = args.CurrentContent.MaxWidth;
            args.stored_max_height = args.CurrentContent.MaxHeight;
        }
        private static void SetPageValuesForAnimation(AnimationArgs args)
        {
            if (args.CurrentContent == null) return;
            args.CurrentContent.MinWidth = args.stored_width;
            args.CurrentContent.MinHeight = args.stored_height;
            args.CurrentContent.MaxWidth = args.stored_width;
            args.CurrentContent.MaxHeight = args.stored_height;
        }
        private static void ResetPageValues(AnimationArgs args)
        {
            if (args.CurrentContent == null) return;
            args.CurrentContent.MinWidth = args.stored_min_width;
            args.CurrentContent.MinHeight = args.stored_min_height;
            args.CurrentContent.MaxWidth = args.stored_max_width;
            args.CurrentContent.MaxHeight = args.stored_max_height;
        }

        private static IEasingFunction GetSwipeAnimationEasingFunction(bool isOverlay, bool useCompatibilityMotion)
        {
            if (useCompatibilityMotion)
            {
                return new CubicEase() { EasingMode = EasingMode.EaseOut };
            }

            if (isOverlay) return new QuarticEase() { EasingMode = EasingMode.EaseOut };
            return new BackEase() { EasingMode = EasingMode.EaseOut, Amplitude = 0.24 };
        }

        private static IEasingFunction GetFadeAnimationEasingFunction(bool isOverlay, bool useCompatibilityMotion)
        {
            if (useCompatibilityMotion)
            {
                return new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            }

            if (isOverlay)
            {
                return new CubicEase() { EasingMode = EasingMode.EaseOut };
            }

            return new SineEase() { EasingMode = EasingMode.EaseOut };
        }

        private static int GetFadeSpeed(bool isOverlay, bool useCompatibilityMotion)
        {
            if (useCompatibilityMotion) return isOverlay ? 240 : 190;
            if (isOverlay) return 300;
            return 240;
        }

        private static int GetSwipeSpeed(bool isOverlay, bool useCompatibilityMotion)
        {
            if (useCompatibilityMotion) return isOverlay ? 250 : 185;
            if (isOverlay) return 340;
            return 260;
        }

        private static int GetSwipeSize(bool isOverlay, bool useCompatibilityMotion)
        {
            if (useCompatibilityMotion) return isOverlay ? 72 : 92;
            if (isOverlay) return 110;
            return 126;
        }

        private static ThicknessAnimation GetSwipeAnimation(AnimationArgs animationArgs, double size, Duration duration, ExpandDirection direction, bool isOverlay, bool useCompatibilityMotion)
        {
            ThicknessAnimation animation0 = new ThicknessAnimation();

            switch (direction)
            {
                case ExpandDirection.Left:
                    animation0.From = new Thickness(-size, 0, size, 0);
                    break;
                case ExpandDirection.Right:
                    animation0.From = new Thickness(size, 0, -size, 0);
                    break;
                case ExpandDirection.Up:
                    animation0.From = new Thickness(0, -size, 0, size);
                    break;
                case ExpandDirection.Down:
                    animation0.From = new Thickness(0, size, 0, -size);
                    break;
            }

            animation0.To = new Thickness(0, 0, 0, 0);
            animation0.Completed += (sender, e) =>
            {
                ResetPageValues(animationArgs);
            };
            animation0.Duration = duration;
            animation0.EasingFunction = GetSwipeAnimationEasingFunction(isOverlay, useCompatibilityMotion);
            return animation0;
        }
        private static DoubleAnimation GetFadeAnimation(Duration duration, bool fadeIn, bool isOverlay, bool useCompatibilityMotion)
        {
            DoubleAnimation animation1 = new DoubleAnimation();
            animation1.From = (fadeIn ? 0 : 1);
            animation1.To = (fadeIn ? 1 : 0);
            animation1.Duration = duration;
            animation1.EasingFunction = GetFadeAnimationEasingFunction(isOverlay, useCompatibilityMotion);
            return animation1;
        }
        private static void SetCurrentPage(AnimationArgs animationArgs, Frame frame, object content)
        {
            if (content is Page) animationArgs.CurrentContent = content as Page;
            else animationArgs.CurrentContent = null;

            animationArgs.stored_width = frame.ActualWidth;
            animationArgs.stored_height = frame.ActualHeight;
        }

        #endregion

        #region No Animation

        public static void Navigate(Frame frame, object content)
        {
             frame.Dispatcher.Invoke(() => frame.Navigate(content));
        }

        #endregion

        #region Dialog

        public static void FrameSet_Overlay(Frame frame, object content, bool animate)
        {
            bool isEmpty = content == null;

            if (animate && !isEmpty) FrameSwipe_PageIn(frame, content, true);
            else Navigate(frame, content);
        }

        public static void FrameSet_Dialog(Frame frame, object content)
        {
            Navigate(frame, content);
        }

        #endregion

        #region Fade Animations
        public static void FrameFadeIn(Frame frame, object content, bool isOverlay)
        {
            frame.Opacity = 0;
            frame.Navigate(content);
            Storyboard storyboard = new Storyboard();
            DoubleAnimation animation = GetFadeAnimation(new Duration(TimeSpan.FromMilliseconds(GetFadeSpeed(isOverlay, false))), true, isOverlay, false);
            storyboard.Children.Add(animation);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Frame.OpacityProperty));
            Storyboard.SetTarget(animation, frame);
            storyboard.Begin();
        }
        public static void FrameFadeOut(Frame frame, object content, bool isOverlay)
        {
            Storyboard storyboard = new Storyboard();
            DoubleAnimation animation = GetFadeAnimation(new Duration(TimeSpan.FromMilliseconds(GetFadeSpeed(isOverlay, false))), false, isOverlay, false);
            storyboard.Children.Add(animation);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Frame.OpacityProperty));
            Storyboard.SetTarget(animation, frame);
            storyboard.Completed += (sender, e) => frame.Navigate(content);
            storyboard.Begin();
        }
        #endregion

        #region Swipe Animations
        public static void FrameSwipe_PageIn(Frame frame, object content, bool isOverlay, bool useCompatibilityMotion = false)
        {
            var storyboard = FrameSwipe_Base(frame, content, ExpandDirection.Up, true, true, isOverlay, useCompatibilityMotion);
            frame.Dispatcher.Invoke(() => frame.Navigate(content));
            storyboard.Dispatcher.InvokeAsync(() => storyboard.Begin());
        }
        public static void FrameSwipe_PageOut(Frame frame, object content, bool isOverlay, bool useCompatibilityMotion = false)
        {
            var storyboard = FrameSwipe_Base(frame, frame.Dispatcher.Invoke(() => frame.Content), ExpandDirection.Up, true, false, isOverlay, useCompatibilityMotion);
            storyboard.Completed += (sender, e) => frame.Navigate(content);
            storyboard.Dispatcher.InvokeAsync(() => storyboard.Begin());
        }
        public static void FrameSwipe(Frame frame, object content, ExpandDirection direction, bool isOverlay = false, bool useCompatibilityMotion = false)
        {
            frame.Dispatcher.Invoke(() =>
            {
                frame.Navigate(content);
                var storyboard = FrameSwipe_Base(frame, content, direction, true, true, isOverlay, useCompatibilityMotion);
                storyboard.Begin();
            });
        }
        public static Storyboard FrameSwipe_Base(Frame frame, object content, ExpandDirection direction, bool useFade, bool fadeIn, bool isOverlay, bool useCompatibilityMotion = false)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                AnimationArgs animationArgs = new AnimationArgs();

                SetCurrentPage(animationArgs, frame, content);
                StorePageValues(animationArgs);
                SetPageValuesForAnimation(animationArgs);

                Storyboard storyboard = new Storyboard();
                Duration duration = new Duration(TimeSpan.FromMilliseconds(GetSwipeSpeed(isOverlay, useCompatibilityMotion)));

                if (useFade)
                {
                    var fadeAnim = GetFadeAnimation(duration, fadeIn, isOverlay, useCompatibilityMotion);
                    storyboard.Children.Add(fadeAnim);
                    Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(Frame.OpacityProperty));
                    Storyboard.SetTarget(fadeAnim, frame);
                }

                var swipeAnim = GetSwipeAnimation(animationArgs, GetSwipeSize(isOverlay, useCompatibilityMotion), duration, direction, isOverlay, useCompatibilityMotion);
                storyboard.Children.Add(swipeAnim);
                Storyboard.SetTargetProperty(swipeAnim, new PropertyPath(Frame.MarginProperty));
                Storyboard.SetTarget(swipeAnim, frame);
                return storyboard;
            });
        }
        #endregion




    }
}


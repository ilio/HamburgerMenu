using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;
#if WINDOWS_PHONE_APP
using Windows.Phone.UI.Input;
#endif

// 
// based on http://blogs.msdn.com/b/madenwal/archive/2015/03/25/how-to-create-a-hamburger-menu-control-for-windows-8-1-and-windows-phone.aspx
//

namespace Microsoft.DX.Controls
{
    public class HamburgerMenu : ContentControl
    {
        private ContentPresenter LeftPanePresenter { get; set; }
        private Rectangle MainPaneRectangle { get; set; }

        public HamburgerMenu()
        {
            DefaultStyleKey = typeof(HamburgerMenu);
#if WINDOWS_PHONE_APP
            // add back button support
            if (!DesignMode.DesignModeEnabled)
            {
                HardwareButtons.BackPressed += delegate(object sender, BackPressedEventArgs args)
                {
                    args.Handled = IsLeftPaneOpen;
                    IsLeftPaneOpen = false;
                };
            }

#endif
            var gestureRecognizer = new GestureRecognizer();
            Loaded += delegate
            {
                var visualStateGroups = VisualStateManager.GetVisualStateGroups((FrameworkElement)VisualTreeHelper.GetChild(this, 0));
                var state = visualStateGroups[0].States.First(s => s.Name == "OpenLeftPane");
                var storyboard = state.Storyboard;

                var animations = storyboard.Children;
                var animationDuration = TimeSpan.Zero;
                foreach (var animation in animations)
                {
                    if (!animation.Duration.HasTimeSpan)
                    {
                        continue;
                    }
                    var childDuration = animation.BeginTime.GetValueOrDefault(TimeSpan.Zero) + animation.Duration.TimeSpan;
                    if (childDuration > animationDuration)
                    {
                        animationDuration = childDuration;
                    }
                }
                // capture horizontal slide gesture
                gestureRecognizer.GestureSettings = GestureSettings.ManipulationTranslateX;//|GestureSettings.CrossSlide;

                var leftPanePresenter = LeftPanePresenter;

                if (leftPanePresenter == null)
                {
                    return;
                }

                leftPanePresenter.PointerCanceled += delegate(object o, PointerRoutedEventArgs args)
                {
                    gestureRecognizer.CompleteGesture();
                    args.Handled = true;
                };

                leftPanePresenter.PointerPressed += delegate(object o, PointerRoutedEventArgs args)
                {
                    // Route teh events to the gesture recognizer   
                    gestureRecognizer.ProcessDownEvent(args.GetCurrentPoint(leftPanePresenter));
                    // Set the pointer capture to the element being interacted with   
                    leftPanePresenter.CapturePointer(args.Pointer);
                    // Mark the event handled to prevent execution of default handlers   
                    args.Handled = true;
                };

                leftPanePresenter.PointerReleased += delegate(object sender, PointerRoutedEventArgs args)
                {
                    gestureRecognizer.ProcessUpEvent(args.GetCurrentPoint(leftPanePresenter));
                    args.Handled = true;
                };

                leftPanePresenter.PointerMoved += delegate(object sender, PointerRoutedEventArgs args)
                {
                    gestureRecognizer.ProcessMoveEvents(args.GetIntermediatePoints(leftPanePresenter));
                };

                // make a backup of easing functions
                var easingFunctionsBackup = animations.OfType<DoubleAnimation>().Select(animation => new { animation, function = animation.EasingFunction });

                gestureRecognizer.ManipulationStarted +=
                    delegate
                    {
                        // make easing function linear
                        foreach (var doubleAnimation in animations.OfType<DoubleAnimation>())
                        {
                            doubleAnimation.EasingFunction = null;
                        }
                    };
                gestureRecognizer.ManipulationUpdated +=
                    delegate(GestureRecognizer sender, ManipulationUpdatedEventArgs args)
                    {
                        var translateX = Math.Min(0, args.Cumulative.Translation.X);
                        translateX = Math.Max(translateX, -LeftPaneWidth);

                        //restart animation
                        VisualStateManager.GoToState(this, "BaseState", false);
                        VisualStateManager.GoToState(this, "OpenLeftPane", false);
                        var d = translateX / -LeftPaneWidth;
                        var timeSpan = new TimeSpan(animationDuration.Ticks - (long)(animationDuration.Ticks * d));
                        // seek to current position
                        storyboard.Seek(timeSpan);
                        storyboard.Pause();

                    };
                gestureRecognizer.ManipulationCompleted +=
                    delegate(GestureRecognizer sender, ManipulationCompletedEventArgs args)
                    {
                        //restore easing functions
                        foreach (var easingFunction in easingFunctionsBackup)
                        {
                            easingFunction.animation.EasingFunction = easingFunction.function;
                        }

                        // slide left more than half of left panel 
                        if (args.Cumulative.Translation.X < -LeftPaneWidth / 2)
                        {
                            IsLeftPaneOpen = false;
                        }
                        else
                        {
                            // reopen back
                            IsLeftPaneOpen = false;
                            IsLeftPaneOpen = true;
                        }
                    };

            };
        }

        protected override void OnApplyTemplate()
        {
            // Find the left pane in the control template and store a reference
            LeftPanePresenter = GetTemplateChild("leftPanePresenter") as ContentPresenter;
            MainPaneRectangle = GetTemplateChild("mainPaneRectangle") as Rectangle;

            if (MainPaneRectangle != null)
            {
                MainPaneRectangle.Tapped += (sender, e) => { IsLeftPaneOpen = false; };
            }

            // Ensure that the TranslateX on the RenderTransform of the left pane is set to the negative value of the left pa
            SetLeftPanePresenterX();

            base.OnApplyTemplate();
        }

        private void SetLeftPanePresenterX()
        {
            // Set the X position of the left pane content presenter to the negative of the pane so that it's off to the left of the screen when closed
            if (LeftPanePresenter != null)
            {
                LeftPanePresenter.RenderTransform = new CompositeTransform()
                {
                    TranslateX = -LeftPaneWidth
                };
            }
        }

        public static readonly DependencyProperty LeftPaneProperty = DependencyProperty.Register("LeftPane", typeof(UIElement), typeof(HamburgerMenu), new PropertyMetadata(null));
        public UIElement LeftPane
        {
            get { return (UIElement)GetValue(LeftPaneProperty); }
            set { SetValue(LeftPaneProperty, value); }
        }

        public static readonly DependencyProperty IsLeftPaneOpenProperty = DependencyProperty.Register("IsLeftPaneOpen", typeof(bool), typeof(HamburgerMenu), new PropertyMetadata(false, OnIsLeftPaneOpenChanged));
        public bool IsLeftPaneOpen
        {
            get { return (bool)GetValue(IsLeftPaneOpenProperty); }
            set { SetValue(IsLeftPaneOpenProperty, value); }
        }
        private static void OnIsLeftPaneOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var hamburgerMenu = sender as HamburgerMenu;
            if (hamburgerMenu == null || hamburgerMenu.LeftPane == null)
            {
                return;
            }
            // Change to appropriate view state when the the IsLeftPaneOpen is toggled
            var value = (bool)args.NewValue;
            if (value)
            {
                VisualStateManager.GoToState(hamburgerMenu, "OpenLeftPane", true);
            }
            else
            {
                VisualStateManager.GoToState(hamburgerMenu, "CloseLeftPane", true);
            }
        }

        public static readonly DependencyProperty LeftPaneWidthProperty = DependencyProperty.Register("LeftPaneWidth", typeof(double), typeof(HamburgerMenu), new PropertyMetadata(300.0, OnLeftPaneWidthChanged));
        public double LeftPaneWidth
        {
            get { return (double)GetValue(LeftPaneWidthProperty); }
            set { SetValue(LeftPaneWidthProperty, value); }
        }
        private static void OnLeftPaneWidthChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            // Update the starting X position of the left pane if the width is updated
            var hamburgerMenu = sender as HamburgerMenu;
            if (hamburgerMenu != null && args.NewValue is double)
            {
                hamburgerMenu.SetLeftPanePresenterX();
            }
        }
    }
}

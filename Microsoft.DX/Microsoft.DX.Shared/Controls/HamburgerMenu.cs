using Windows.ApplicationModel;
using Windows.UI.Xaml.Media.Animation;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

#if WINDOWS_PHONE_APP
using Windows.Phone.UI.Input;
#endif
//http://blogs.msdn.com/b/madenwal/archive/2015/03/25/how-to-create-a-hamburger-menu-control-for-windows-8-1-and-windows-phone.aspx
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
                var storyboard = Storyboard;
                var children = storyboard.Children;
                var duration = TimeSpan.Zero;
                foreach (var child in children)
                {
                    if (!child.Duration.HasTimeSpan)
                    {
                        continue;
                    }
                    var childDuration = child.BeginTime.GetValueOrDefault(TimeSpan.Zero) + child.Duration.TimeSpan;
                    if (childDuration > duration)
                    {
                        duration = childDuration;
                    }
                }
                gestureRecognizer.GestureSettings = GestureSettings.ManipulationTranslateX;//|GestureSettings.CrossSlide;

                var leftPanePresenter = GetTemplateChild("leftPanePresenter") as ContentPresenter;

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

                var easingFunctionsBackup = children.OfType<DoubleAnimation>().Select(animation => new { animation, function = animation.EasingFunction });

                gestureRecognizer.ManipulationStarted +=
                    delegate(GestureRecognizer sender, ManipulationStartedEventArgs args)
                    {
                        Debug.WriteLine("ManipulationStarted {0} {1} {2}", args.Cumulative.Translation, args.PointerDeviceType, args.Position);
                        foreach (var doubleAnimation in children.OfType<DoubleAnimation>())
                        {
                            doubleAnimation.EasingFunction = null;
                        }
                    };
                gestureRecognizer.ManipulationUpdated +=
                    delegate(GestureRecognizer sender, ManipulationUpdatedEventArgs args)
                    {
                        //                        Debug.WriteLine("ManipulationUpdated {0} {1} {2} {3} {4} {5} {6}", args.Cumulative.Translation.X, args.Cumulative.Translation.Y, args.PointerDeviceType, args.Position, args.Delta, args.Velocities.Angular, args.Velocities.Expansion, args.Velocities.Linear);
                        var translateX = Math.Min(0, args.Cumulative.Translation.X);
                        translateX = Math.Max(translateX, -LeftPaneWidth);

                        VisualStateManager.GoToState(this, "BaseState", false);
                        VisualStateManager.GoToState(this, "OpenLeftPane", false);
                        var d = translateX / -LeftPaneWidth;
                        var timeSpan = new TimeSpan(duration.Ticks - (long)(duration.Ticks * d));
                        storyboard.Seek(timeSpan);
                        storyboard.Pause();

                    };
                gestureRecognizer.ManipulationCompleted +=
                    delegate(GestureRecognizer sender, ManipulationCompletedEventArgs args)
                    {
                        Debug.WriteLine("ManipulationCompleted {0} {1} {2} {3} {4} {5}", args.Cumulative.Translation, args.PointerDeviceType, args.Position, args.Velocities.Angular, args.Velocities.Expansion, args.Velocities.Linear);
                        foreach (var easingFunction in easingFunctionsBackup)
                        {
                            easingFunction.animation.EasingFunction = easingFunction.function;
                        }
                        if (args.Cumulative.Translation.X < -LeftPaneWidth / 2)
                        {
                            IsLeftPaneOpen = false;
                        }
                        else
                        {
                            IsLeftPaneOpen = false;
                            IsLeftPaneOpen = true;
                        }
                    };

            };
        }

        private Storyboard Storyboard
        {
            get
            {
                var child = VisualTreeHelper.GetChild(this, 0);
                var visualStateGroups = VisualStateManager.GetVisualStateGroups((FrameworkElement)child);
                var state = visualStateGroups[0].States.First(s => s.Name == "OpenLeftPane");
                var storyboard = state.Storyboard;
                return storyboard;
            }
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
            var hamburgerToggleButton = FindName("HamburgerToggleButton") as ToggleButton;
            if (hamburgerToggleButton != null)
            {
                hamburgerToggleButton.Loaded += delegate(object sender, RoutedEventArgs args)
                {
                    var frameworkElement = (FrameworkElement)VisualTreeHelper.GetChild(hamburgerToggleButton, 0);

                    var visualStateGroups = VisualStateManager.GetVisualStateGroups(frameworkElement);
                    var group = visualStateGroups[0];
                    group.CurrentStateChanging += delegate(object sender1, VisualStateChangedEventArgs args1)
                    {
                        Debug.WriteLine("Changing " + " from " + args1.OldState.Name + " -> " + args1.NewState.Name);
                    };
                };
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
            if (hamburgerMenu != null)
            {
                if (hamburgerMenu.LeftPane != null)
                {
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

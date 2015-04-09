﻿extern alias wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Granular.Host.Wpf.Render;

namespace Granular.Host.Wpf
{
    public class WpfPresentationSourceFactory : IPresentationSourceFactory
    {
        public static readonly IPresentationSourceFactory Default = new WpfPresentationSourceFactory();

        private List<WpfPresentationSource> presentationSources;

        private WpfPresentationSourceFactory()
        {
            presentationSources = new List<WpfPresentationSource>();
        }

        public IPresentationSource CreatePresentationSource(UIElement rootElement)
        {
            WpfPresentationSource presentationSource = new WpfPresentationSource(rootElement, WpfValueConverter.Default);
            presentationSources.Add(presentationSource);

            return presentationSource;
        }

        public IPresentationSource GetPresentationSourceFromElement(UIElement element)
        {
            while (element.VisualParent is FrameworkElement)
            {
                element = (FrameworkElement)element.VisualParent;
            }

            return presentationSources.FirstOrDefault(presentationSource => presentationSource.RootElement == element);
        }
    }

    public class WpfPresentationSource : IPresentationSource
    {
        public event EventHandler HitTestInvalidated { add { } remove { } }

        public UIElement RootElement { get; private set; }

        public MouseDevice MouseDevice { get; private set; }
        public KeyboardDevice KeyboardDevice { get; private set; }

        public string Title
        {
            get { return window.Title; }
            set { window.Title = value; }
        }

        private IWpfValueConverter converter;

        private wpf::System.Windows.Controls.Canvas container;
        private wpf::System.Windows.Window window;

        public WpfPresentationSource(UIElement rootElement, IWpfValueConverter converter)
        {
            this.RootElement = rootElement;
            this.converter = converter;

            RootElement.IsRootElement = true;

            MouseDevice = new MouseDevice(this);
            KeyboardDevice = new KeyboardDevice(this);

            container = new wpf::System.Windows.Controls.Canvas { Background = wpf::System.Windows.Media.Brushes.Transparent };
            container.PreviewMouseMove += OnContainerMouseMove;
            container.PreviewMouseDown += OnContainerMouseDown;
            container.PreviewMouseUp += OnContainerMouseUp;
            container.PreviewMouseWheel += (sender, e) => e.Handled = MouseDevice.ProcessRawEvent(new RawMouseWheelEventArgs(e.Delta, converter.ConvertBack(e.GetPosition(container)), GetTimestamp()));

            MouseDevice.CursorChanged += (sender, e) => container.Cursor = converter.Convert(MouseDevice.Cursor);
            container.Cursor = converter.Convert(MouseDevice.Cursor);

            window = new wpf::System.Windows.Window { UseLayoutRounding = true, Content = container };
            window.Activated += (sender, e) => MouseDevice.Activate();
            window.Deactivated += (sender, e) => MouseDevice.Deactivate();
            window.SizeChanged += (sender, e) => UpdateLayout();
            window.PreviewKeyDown += (sender, e) => e.Handled = KeyboardDevice.ProcessRawEvent(new RawKeyboardEventArgs(converter.ConvertBack(e.Key), converter.ConvertBack(e.KeyStates), e.IsRepeat, GetTimestamp()));
            window.PreviewKeyUp += (sender, e) => e.Handled = KeyboardDevice.ProcessRawEvent(new RawKeyboardEventArgs(converter.ConvertBack(e.Key), converter.ConvertBack(e.KeyStates), e.IsRepeat, GetTimestamp()));
            window.Show();

            container.Children.Add(((IWpfRenderElement)rootElement.GetRenderElement(WpfRenderElementFactory.Default)).WpfElement);
            UpdateLayout();

            MouseDevice.Activate();
            KeyboardDevice.Activate();
        }

        private void UpdateLayout()
        {
            RootElement.Measure(new Size(container.ActualWidth, container.ActualHeight));
            RootElement.Arrange(new Rect(container.ActualWidth, container.ActualHeight));
        }

        private void OnContainerMouseDown(object sender, wpf::System.Windows.Input.MouseButtonEventArgs e)
        {
            e.MouseDevice.Capture(container);
            e.Handled = MouseDevice.ProcessRawEvent(new RawMouseButtonEventArgs(converter.ConvertBack(e.ChangedButton), converter.ConvertBack(e.ButtonState), converter.ConvertBack(e.GetPosition(container)), GetTimestamp()));

            if (!e.Handled)
            {
                e.MouseDevice.Capture(null);
            }
        }

        private void OnContainerMouseUp(object sender, wpf::System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = MouseDevice.ProcessRawEvent(new RawMouseButtonEventArgs(converter.ConvertBack(e.ChangedButton), converter.ConvertBack(e.ButtonState), converter.ConvertBack(e.GetPosition(container)), GetTimestamp()));

            if (e.MouseDevice.Captured == container)
            {
                e.MouseDevice.Capture(null);
            }
        }

        private void OnContainerMouseMove(object sender, wpf::System.Windows.Input.MouseEventArgs e)
        {
            e.Handled = MouseDevice.ProcessRawEvent(new RawMouseEventArgs(converter.ConvertBack(e.GetPosition(container)), GetTimestamp()));
        }

        public IInputElement HitTest(Point position)
        {
            return RootElement.HitTest(position) as IInputElement;
        }

        public int GetTimestamp()
        {
            return (int)(DateTime.Now.Ticks & 0xffffffff);
        }
    }
}


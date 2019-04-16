﻿using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaColor = Avalonia.Media.Color;
using GradientStop = Avalonia.Media.GradientStop;
using IBrush = Avalonia.Media.IBrush;
using Avalonia.Threading;

using Apollo.Structures;

namespace Apollo.Components {
    public class ColorPicker: UserControl {
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
        
        public delegate void ColorChangedEventHandler(Color value);
        public event ColorChangedEventHandler ColorChanged;

        private Color _color = new Color();
        public Color Color {
            get => _color;
            private set {
                _color = value;
                ColorChanged?.Invoke(_color);
            }
        }

        public void SetColor(Color color) {
            _color = color;
            InitCanvas();
        }

        Ellipse Preview;
        Canvas MainCanvas, HueCanvas;
        Thumb MainThumb, HueThumb;
        GradientStop MainColor;
        TextBox Hex;

        bool main_mouseHeld, hue_mouseHeld, hexValidation;

        public ColorPicker() {
            InitializeComponent();
            
            Preview = this.Get<Ellipse>("Color");

            MainCanvas = this.Get<Canvas>("MainCanvas");
            HueCanvas = this.Get<Canvas>("HueCanvas");

            MainCanvas.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated);
            HueCanvas.GetObservable(Visual.BoundsProperty).Subscribe(Bounds_Updated);

            MainThumb = this.Get<Thumb>("MainThumb");
            HueThumb = this.Get<Thumb>("HueThumb");

            MainColor = this.Get<GradientStop>("MainColor");

            hexValidation = true;
            Hex = this.Get<TextBox>("Hex");
            Hex.GetObservable(TextBox.TextProperty).Subscribe(Hex_Changed);
        }

        public void Bounds_Updated(Rect bounds) {
            if (!bounds.IsEmpty) InitCanvas();
        }

        private void InitCanvas() {
            double hueHeight = HueCanvas.Bounds.Height;
            double mainWidth = MainCanvas.Bounds.Width;
            double mainHeight = MainCanvas.Bounds.Height;

            if (hueHeight == 0 || mainWidth == 0 || mainHeight == 0) return;

            double r = Color.Red / 63.0;
            double g = Color.Green / 63.0;
            double b = Color.Blue / 63.0;
            double[] colors = new double[] {r, g, b};

            double min = colors.Min();
            double max = colors.Max();

            double hue = 0;
            if (min != max) {
                double diff = max - min;

                if (max == r) {
                    hue = (g - b) / diff;
                } else if (max == g) {
                    hue = (b - r) / diff + 2.0;
                } else if (max == b) {
                    hue = (r - g) / diff + 4.0;
                }
                if (hue < 0) hue += 6.0;
            }

            double saturation = 0;
            if (max != 0) saturation = 1 - (min / max);

            Canvas.SetTop(HueThumb, hue * hueHeight / 6);
            Canvas.SetLeft(MainThumb, saturation * mainWidth);
            Canvas.SetTop(MainThumb, (1 - max) * mainHeight);

            UpdateCanvas();

            Hex.Text = Color.ToHex();
        }

        private void UpdateColor() {
            double hue = Canvas.GetTop(HueThumb) * 6 / HueCanvas.Bounds.Height;
            double saturation = Canvas.GetLeft(MainThumb) / MainCanvas.Bounds.Width;
            double value = (1 - (Canvas.GetTop(MainThumb) / MainCanvas.Bounds.Height)) * 63;

            int hi = Convert.ToInt32(Math.Floor(hue)) % 6;
            double f = hue - Math.Floor(hue);

            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0)      Color = new Color(v, t, p);
            else if (hi == 1) Color = new Color(q, v, p);
            else if (hi == 2) Color = new Color(p, v, t);
            else if (hi == 3) Color = new Color(p, q, v);
            else if (hi == 4) Color = new Color(t, p, v);
            else              Color = new Color(v, p, q);

            Preview.Fill = Color.ToBrush();

            hexValidation = false;
            Hex.Text = Color.ToHex();
            hexValidation = true;
        }

        private void UpdateCanvas() {
            double hue = Canvas.GetTop(HueThumb) * 6 / HueCanvas.Bounds.Height;

            int hi = Convert.ToInt32(Math.Floor(hue)) % 6;
            double f = hue - Math.Floor(hue);

            byte v = 255;
            byte p = 0;
            byte q = Convert.ToByte(255 * (1 - f));
            byte t = Convert.ToByte(255 * f);

            if (hi == 0)      MainColor.Color = new AvaloniaColor(255, v, t, p);
            else if (hi == 1) MainColor.Color = new AvaloniaColor(255, q, v, p);
            else if (hi == 2) MainColor.Color = new AvaloniaColor(255, p, v, t);
            else if (hi == 3) MainColor.Color = new AvaloniaColor(255, p, q, v);
            else if (hi == 4) MainColor.Color = new AvaloniaColor(255, t, p, v);
            else              MainColor.Color = new AvaloniaColor(255, v, p, q);
        }

        private void MainThumb_Move(object sender, VectorEventArgs e) {
            double x = Canvas.GetLeft(MainThumb) + e.Vector.X;
            x = (x < 0)? 0 : x;
            x = (x > MainCanvas.Bounds.Width)? MainCanvas.Bounds.Width : x;

            double y = Canvas.GetTop(MainThumb) + e.Vector.Y;
            y = (y < 0)? 0 : y;
            y = (y > MainCanvas.Bounds.Height)? MainCanvas.Bounds.Height : y;

            Canvas.SetLeft(MainThumb, x);
            Canvas.SetTop(MainThumb, y);

            UpdateColor();
        }

        private void MainCanvas_MouseDown(object sender, PointerPressedEventArgs e) {
            if (e.MouseButton.HasFlag(MouseButton.Left)) {
                main_mouseHeld = true;
                e.Device.Capture(MainCanvas);

                Vector position = e.GetPosition(MainThumb);
                position = position.WithX(position.X - MainThumb.Bounds.Width / 2)
                                   .WithY(position.Y - MainThumb.Bounds.Height / 2);

                MainThumb_Move(null, new VectorEventArgs() { Vector = position });
            }
        }

        private void MainCanvas_MouseUp(object sender, PointerReleasedEventArgs e) {
            if (e.MouseButton.HasFlag(MouseButton.Left)) {
                main_mouseHeld = false;
                e.Device.Capture(null);
            }
        }

        private void MainCanvas_MouseMove(object sender, PointerEventArgs e) {
            if (main_mouseHeld) {
                Vector position = e.GetPosition(MainThumb);
                position = position.WithX(position.X - MainThumb.Bounds.Width / 2)
                                   .WithY(position.Y - MainThumb.Bounds.Height / 2);

                MainThumb_Move(null, new VectorEventArgs() { Vector = position });
            }
        }

        private void HueThumb_Move(object sender, VectorEventArgs e) {
            double y = Canvas.GetTop(HueThumb) + e.Vector.Y;
            y = (y < 0)? 0 : y;
            y = y > HueCanvas.Bounds.Height? HueCanvas.Bounds.Height : y;

            Canvas.SetTop(HueThumb, y);

            UpdateColor();
            UpdateCanvas();
        }

        private void HueCanvas_MouseDown(object sender, PointerPressedEventArgs e) {
            if (e.MouseButton.HasFlag(MouseButton.Left)) {
                hue_mouseHeld = true;
                e.Device.Capture(HueCanvas);

                Vector position = e.GetPosition(HueThumb);
                position = position.WithY(position.Y - HueThumb.Bounds.Height / 2);

                HueThumb_Move(null, new VectorEventArgs() { Vector = position });
            }
        }

        private void HueCanvas_MouseUp(object sender, PointerReleasedEventArgs e) {
            if (e.MouseButton.HasFlag(MouseButton.Left)) {
                hue_mouseHeld = false;
                e.Device.Capture(null);
            }
        }

        private void HueCanvas_MouseMove(object sender, PointerEventArgs e) {
            if (hue_mouseHeld) {
                Vector position = e.GetPosition(HueThumb);
                position = position.WithY(position.Y - HueThumb.Bounds.Height / 2);

                HueThumb_Move(null, new VectorEventArgs() { Vector = position });
            }
        }

        private Action HexAction(string text) {
            Action update = () => { Hex.Foreground = (IBrush)Application.Current.Styles.FindResource("ThemeForegroundBrush"); };

            foreach (char i in text.Substring(1))
                if (!"0123456789ABCDEF".Contains(i))
                    return update + (() => { Hex.Text = Color.ToHex(); });

            if (text == "#") return () => {
                Hex.Foreground = (IBrush)Application.Current.Styles.FindResource("ErrorBrush");
                Hex.Text = text;
            };

            if (text[0] != '#' || text.Length > 7) return update + (() => { Hex.Text = Color.ToHex(); });
            if (text.Length < 7) return () => { Hex.Foreground = (IBrush)Application.Current.Styles.FindResource("ErrorBrush"); };

            int r = Convert.ToInt32(text.Substring(1, 2), 16);
            int g = Convert.ToInt32(text.Substring(3, 2), 16);
            int b = Convert.ToInt32(text.Substring(5, 2), 16);

            r = (r > 63)? 63 : r;
            g = (g > 63)? 63 : g;
            b = (b > 63)? 63 : b;

            return update + (() => { 
                Color = new Color((byte)r, (byte)g, (byte)b);

                Preview.Fill = Color.ToBrush();
                InitCanvas();
            });
        }

        private void Hex_Changed(string text) {
            if (!hexValidation) return;
            
            if (text == null) return;
            if (text == "") text = "#";

            Dispatcher.UIThread.InvokeAsync(HexAction(text.ToUpper()));
        }
        
        private void Hex_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) this.Focus();
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CxxDependencyVisualizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        LibData data = new LibData();
        ActiveControls activeControls = null;
        
        public MainWindow()
        {
            InitializeComponent();

            activeControls = new ActiveControls(canvas);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;

            menu_Layout1.Checked += menu_Layout_Checked;
            menu_Layout2.Checked += menu_Layout_Checked;
            menu_Layout1.Unchecked += menu_Layout_Unchecked;
            menu_Layout2.Unchecked += menu_Layout_Unchecked;
            menu_CyclesLinesDistanceSlider.ValueChanged += menu_CyclesLinesDistanceSlider_ValueChanged;
            menu_LinesWidthSlider.ValueChanged += Menu_LinesWidthSlider_ValueChanged;
        }

        private void menu_Layout_Checked(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != menu_Layout1 && menu_Layout1 != null)
            {
                menu_Layout1.Unchecked -= menu_Layout_Unchecked;
                menu_Layout1.IsChecked = false;
                menu_Layout1.Unchecked += menu_Layout_Unchecked;
            }
            if (menuItem != menu_Layout2 && menu_Layout2 != null)
            {
                menu_Layout2.Unchecked -= menu_Layout_Unchecked;
                menu_Layout2.IsChecked = false;
                menu_Layout2.Unchecked += menu_Layout_Unchecked;
            }
        }

        private void menu_Layout_Unchecked(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            menuItem.Checked -= menu_Layout_Checked;
            menuItem.IsChecked = true;
            menuItem.Checked += menu_Layout_Checked;
        }

        private void menu_CyclesLinesDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            menu_CyclesLinesDistanceLabel.Content = (int)menu_CyclesLinesDistanceSlider.Value;
        }

        private void Menu_LinesWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            menu_LinesWidthLabel.Content = (int)menu_LinesWidthSlider.Value;
        }

        private void buttonAnalyze_Click(object sender, RoutedEventArgs e)
        {
            //textBlockStatus.Text = "Processing...";

            // Clear states
            activeControls.Reset(data);
            canvas.Children.Clear();

            GraphLayout.UseLevel useLevel = menu_Layout1.IsChecked ? GraphLayout.UseLevel.Min
                                          : menu_Layout2.IsChecked ? GraphLayout.UseLevel.Max
                                          : GraphLayout.UseLevel.Min;

            // Analyze includes and create dictionary
            data = new LibData(textBoxDir.Text, textBoxFile.Text, true);

            Size graphSize = GraphLayout.LevelBasedLayout(data.dict, useLevel);

            // Create textBlocks with borders for each include and calculate
            foreach (var d in data.dict)
            {
                TextBlock textBlock = new TextBlock();
                if (d.Value.duplicatedChildren)
                    textBlock.Background = Brushes.Yellow;
                else
                    textBlock.Background = Brushes.White;
                textBlock.Text = Util.FileFromPath(d.Key);
                textBlock.Padding = new Thickness(5);
                textBlock.Width = d.Value.size.Width;
                textBlock.Height = d.Value.size.Height;
                textBlock.DataContext = d.Key;
                string tooltip = d.Key
                                + "\r\nLevels: [" + d.Value.minLevel + ", " + d.Value.maxLevel + "]"
                                + (d.Value.duplicatedChildren ? "\r\nWARNING: duplicated includes" : "")
                                + "\r\n";
                foreach (var c in d.Value.children)
                    tooltip += "\r\n" + c;
                textBlock.ToolTip = tooltip;
                textBlock.MouseDown += TextBlock_MouseDown;

                d.Value.textBlock = textBlock;

                Border border = new Border();
                border.BorderThickness = new Thickness(1);
                if (d.Value.important)
                    border.BorderBrush = Brushes.Blue;
                else
                    border.BorderBrush = Brushes.Black;
                border.Child = textBlock;
                double l = d.Value.center.X - d.Value.textBlock.Width / 2;
                double t = d.Value.center.Y - d.Value.textBlock.Height / 2;
                Canvas.SetLeft(border, l);
                Canvas.SetTop(border, t);
                Canvas.SetZIndex(border, 1);

                canvas.Children.Add(border);
            }

            // add all lines - connections between includes
            if (menu_ShowLines.IsChecked)
            {
                foreach (var d in data.dict)
                {
                    double x = d.Value.center.X;
                    double y = d.Value.center.Y;

                    foreach (var cStr in d.Value.children)
                    {
                        var c = data.dict[cStr];

                        double xC = c.center.X;
                        double yC = c.center.Y;

                        var line = new Line();
                        line.X1 = x;
                        line.Y1 = y;
                        line.X2 = xC;
                        line.Y2 = yC;
                        line.Stroke = Brushes.DarkGray;
                        line.StrokeThickness = 1;
                        Canvas.SetZIndex(line, -1);

                        canvas.Children.Add(line);
                    }
                }
            }

            // calculate the scale of graph in order to fit it to window
            double scaleW = Math.Min(canvasGrid.ActualWidth / graphSize.Width, 1);
            double scaleH = Math.Min(canvasGrid.ActualHeight / graphSize.Height, 1);
            double scale = Math.Min(scaleW, scaleH);

            var matrix = Matrix.Identity;
            matrix.Scale(scale, scale);
            canvas.RenderTransform = new MatrixTransform(matrix);

            //textBlockStatus.Text = "Done.";
        }

        private void buttonFind_Click(object sender, RoutedEventArgs e)
        {
            foreach(var d in data.dict)
            {
                if (!Util.Empty(textBoxFind.Text) && d.Key.IndexOf(textBoxFind.Text) >= 0)
                {
                    Border border = d.Value.textBlock.Parent as Border;
                    double xFound = Canvas.GetLeft(border);
                    double yFound = Canvas.GetTop(border);
                    Point gridCenter = new Point(canvasGrid.ActualWidth / 2, canvasGrid.ActualHeight / 2);
                    Point atCanvas = canvasGrid.TranslatePoint(gridCenter, canvas);
                    
                    var transform = canvas.RenderTransform as MatrixTransform;
                    var matrix = transform.Matrix;
                    double xOff = atCanvas.X - xFound;
                    double yOff = atCanvas.Y - yFound;
                    matrix.TranslatePrepend(xOff, yOff);
                    transform.Matrix = matrix;

                    break;
                }
            }
        }

        class ActiveControls
        {
            public ActiveControls(Canvas canvas)
            {
                this.canvas = canvas;
            }

            public void Reset(LibData data)
            {
                foreach (var tb in textBlocks)
                {
                    var d = data.dict[tb.DataContext as string];

                    Border borderActive = tb.Parent as Border;
                    borderActive.BorderThickness = new Thickness(1);
                    if (d.important)
                        borderActive.BorderBrush = Brushes.Blue;
                    else
                        borderActive.BorderBrush = Brushes.Black;
                }
                textBlocks.Clear();

                foreach (var line in lines)
                {
                    canvas.Children.Remove(line);
                }
                lines.Clear();

                foreach(var shape in shapes)
                {
                    canvas.Children.Remove(shape);
                }
                shapes.Clear();
            }

            public enum ColorId { Selected = 0, Child = 1, Parent = 2 };

            public void Select(TextBlock textBlock, ColorId colorId)
            {
                var border = textBlock.Parent as Border;
                border.BorderBrush = brushes[(int)colorId];
                border.BorderThickness = new Thickness(2);
                textBlocks.Add(textBlock);
            }
            
            public void Add(Point p0, Point p1, ColorId colorId, double linesThickness)
            {
                var line = new Line();
                line.X1 = p0.X;
                line.Y1 = p0.Y;
                line.X2 = p1.X;
                line.Y2 = p1.Y;
                line.Stroke = brushes[(int)colorId];
                line.StrokeThickness = linesThickness;

                lines.Add(line);

                canvas.Children.Add(line);
            }

            public void Add(Shape shape, double linesThickness)
            {
                int brushId = shapes.Count;
                Brush brush = null;
                if (brushId < brushes2.Length)
                    brush = brushes2[brushId];
                else
                    brush = new SolidColorBrush(RandomColor());

                shape.Stroke = brush;
                shape.StrokeThickness = linesThickness;
                shapes.Add(shape);

                canvas.Children.Add(shape);
            }

            public TextBlock FirstSelected()
            {
                return textBlocks.Count > 0 ? textBlocks[0] : null;
            }

            public void ReSelect(int i, ColorId type)
            {
                if (i < textBlocks.Count)
                    (textBlocks[i].Parent as Border).BorderBrush = brushes[(int)type];
            }

            private Color RandomColor()
            {
                Random r = new Random();
                return Color.FromRgb((byte)r.Next(64, 192), (byte)r.Next(64, 192), (byte)r.Next(64, 192));
            }

            Canvas canvas;

            List<TextBlock> textBlocks = new List<TextBlock>();
            List<Line> lines = new List<Line>();
            List<Shape> shapes = new List<Shape>();
            Brush[] brushes = new Brush[]{ Brushes.Red, Brushes.DarkOrange, Brushes.Green };
            Brush[] brushes2 = new Brush[] { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.DarkOrange, Brushes.DarkCyan, Brushes.DarkMagenta};
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            double linesThickness = menu_LinesWidthSlider.Value;

            var textBlock = sender as TextBlock;
            TextBlock lastTextBlock = null;

            if (Keyboard.IsKeyDown(Key.LeftShift)
                && activeControls.FirstSelected() != null
                && textBlock != activeControls.FirstSelected())
            {
                lastTextBlock = activeControls.FirstSelected();
            }

            activeControls.Reset(data);
            
            if (lastTextBlock != null)
                activeControls.Select(lastTextBlock, ActiveControls.ColorId.Selected);

            activeControls.Select(textBlock, ActiveControls.ColorId.Selected);

            // select chldren and parents
            if (lastTextBlock == null)
            {
                var d = data.dict[textBlock.DataContext as string];
                foreach (var c in d.children)
                {
                    var child = data.dict[c];
                    activeControls.Select(child.textBlock, ActiveControls.ColorId.Child);
                    activeControls.Add(d.center, child.center, ActiveControls.ColorId.Child, linesThickness);
                }
                foreach (var p in d.parents)
                {
                    var parent = data.dict[p];
                    activeControls.Select(parent.textBlock, ActiveControls.ColorId.Parent);
                    activeControls.Add(d.center, parent.center, ActiveControls.ColorId.Parent, linesThickness);
                }    
            }
            // find and select path
            else
            {
                ActiveControls.ColorId selectedType = ActiveControls.ColorId.Child;
                List<string> path = Graph.FindPath(lastTextBlock.DataContext as string,
                                                   textBlock.DataContext as string,
                                                   data.dict);
                if (path.Count == 0)
                {
                    selectedType = ActiveControls.ColorId.Parent;
                    path = Graph.FindPath(textBlock.DataContext as string,
                                          lastTextBlock.DataContext as string,
                                          data.dict);
                    
                }

                if (path.Count > 1)
                {
                    // always iterate from child to parent because currently
                    //   childrenLines are synchronized with children
                    //   but parentsLines are not synchronized with parents
                    //   and below children are searched to find line
                    // without first
                    for (int i = path.Count - 2; i >= 0; --i)
                    {
                        var d = data.dict[path[i]];
                        // intermediate index
                        // edge borders are already handled
                        if (i > 0)
                            activeControls.Select(d.textBlock, selectedType);

                        // handle lines from previous (parent) to child (current)
                        var child = data.dict[path[i + 1]];
                        activeControls.Add(d.center, child.center, selectedType, linesThickness);
                    }

                    activeControls.ReSelect(1, selectedType);
                }
            }
        }

        private void MenuItemCycles_Click(object sender, RoutedEventArgs e)
        {
            double linesThickness = menu_LinesWidthSlider.Value;

            activeControls.Reset(data);

            List<List<string>> cycles = new List<List<string>>();
            foreach (var d in data.dict)
            {
                List<string> cycle = Graph.FindCycle(d.Key, data.dict);
                if (cycle.Count > 0)
                {
                    // Find smallest and rotate
                    int idRot = Util.IndexOfSmallest(cycle);
                    Util.Rotate(cycle, idRot);
                    if (!cycles.Contains(cycle, new Util.ListCompare<string>()))
                        cycles.Add(cycle);
                }
            }

            // sort cycles by name to compare between them and reject already added
            if (cycles.Count > 0)
            {
                double dist = menu_CyclesLinesDistanceSlider.Value;

                for (int i = 0; i < cycles.Count; ++i)
                {
                    PathGeometry pathGeom = new PathGeometry();
                    
                    var cycle = cycles[i];
                    var back = cycle[cycle.Count - 1];
                    var prev = data.dict[back];
                    foreach(var s in cycle)
                    {
                        var curr = data.dict[s];
                        activeControls.Select(curr.textBlock, ActiveControls.ColorId.Selected);

                        PathFigure fig = new PathFigure();
                        fig.StartPoint = prev.center;
                        QuadraticBezierSegment seg = new QuadraticBezierSegment();
                        seg.Point1 = CalculateBezierPoint(prev.center, curr.center, i, dist);
                        seg.Point2 = curr.center;
                        fig.Segments.Add(seg);
                        pathGeom.Figures.Add(fig);

                        prev = curr;
                    }

                    System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
                    path.Data = pathGeom;
                    activeControls.Add(path, linesThickness);
                }
            }
        }

        private Point CalculateBezierPoint(Point p1, Point p2, int i, double dist)
        {
            double x = p1.X + 0.5 * (p2.X - p1.X);
            double y = p1.Y + 0.5 * (p2.Y - p1.Y);
            if (i <= 0)
                return new Point(x, y);
            double l = Math.Sqrt(x * x + y * y);
            double x2 = x / l;
            double y2 = y / l;
            int sign = i % 2 != 0 ? 1 : -1;
            double d = dist * (i + 1) / 2;
            return new Point(x - sign * d * x2, y + sign * d * y2);
        }

        private void canvasGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double s = e.Delta > 0 ? 1.25 : (1 / 1.25);
            var position = e.GetPosition(canvas);

            var transform = canvas.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;
            matrix.ScaleAtPrepend(s, s, position.X, position.Y);
            transform.Matrix = matrix;

            e.Handled = true;
        }

        private void canvasGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width >= 1)
            {
                double s = e.NewSize.Width / e.PreviousSize.Width;
                var transform = canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;
                matrix.Scale(s, s);
                transform.Matrix = matrix;
            }

            e.Handled = true;
        }

        Point downPos;
        bool downSet = false;
        private void canvasGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            downPos = e.GetPosition(canvas);
            downSet = true;
        }

        private void canvasGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MoveCanvas(e);
            downSet = false;
        }

        private void canvasGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            MoveCanvas(e);
            downSet = false;
        }

        private void canvasGrid_MouseMove(object sender, MouseEventArgs e)
        {
            MoveCanvas(e);
        }

        private void MoveCanvas(MouseEventArgs e)
        {
            if (downSet)
            {
                var transform = canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;
                Point pos = e.GetPosition(canvas);
                double xOff = pos.X - downPos.X;
                double yOff = pos.Y - downPos.Y;
                matrix.TranslatePrepend(xOff, yOff);
                transform.Matrix = matrix;
            }
        }
    }
}

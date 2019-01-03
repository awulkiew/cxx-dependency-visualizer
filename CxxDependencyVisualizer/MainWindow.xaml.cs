using System;
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
        List<Line> lines = new List<Line>();
        ActiveControls activeControls = null;
        GraphLayout.GraphData gd;

        public MainWindow()
        {
            InitializeComponent();

            activeControls = new ActiveControls(canvas);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;

            menu_CyclesLinesDistanceSlider.ValueChanged += menu_CyclesLinesDistanceSlider_ValueChanged;
            menu_LinesWidthSlider.ValueChanged += Menu_LinesWidthSlider_ValueChanged;
        }

        private void menuButton_Click(object sender, RoutedEventArgs e)
        {
            if (menuBorder.Visibility == Visibility.Visible)
                menuBorder.Visibility = Visibility.Hidden;
            else
                menuBorder.Visibility = Visibility.Visible;
        }

        private void menu_ShowLines_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var line in lines)
                line.Visibility = Visibility.Visible;
        }

        private void menu_ShowLines_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var line in lines)
                line.Visibility = Visibility.Hidden;
        }

        private void menu_CyclesLinesDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            menu_CyclesLinesDistanceLabel.Content = (int)menu_CyclesLinesDistanceSlider.Value;

            activeControls.SetCyclesSeparation(gd.CellSize.Width * menu_CyclesLinesDistanceSlider.Value / 100,
                                               menu_LinesWidthSlider.Value);
        }

        private void Menu_LinesWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            menu_LinesWidthLabel.Content = (int)menu_LinesWidthSlider.Value;

            activeControls.SetLineThickness(menu_LinesWidthSlider.Value);
        }

        private void buttonAnalyze_Click(object sender, RoutedEventArgs e)
        {
            // Clear states
            activeControls.Reset(data);
            lines.Clear();
            canvas.Children.Clear();

            // Analyze includes and create dictionary
            data = new LibData(textBoxDir.Text, textBoxFile.Text, true, (bool)menu_IgnoreComments.IsChecked);

            if ((bool)menu_Layout4.IsChecked)
            {
                gd = GraphLayout.ForceDirectedLayout(data.dict);
            }
            else if ((bool)menu_Layout3.IsChecked)
            {
                gd = GraphLayout.RadialHierarchicalLayout(textBoxDir.Text, data.dict);
            }
            else
            {
                GraphLayout.UseLevel useLevel = (bool)menu_Layout2.IsChecked
                                              ? GraphLayout.UseLevel.Max
                                              : GraphLayout.UseLevel.Min;
                gd = GraphLayout.LevelBasedLayout(data.dict, useLevel);
            }

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
                border.BorderBrush = Brushes.Blue;
                border.Child = textBlock;
                double l = d.Value.center.X - d.Value.textBlock.Width / 2;
                double t = d.Value.center.Y - d.Value.textBlock.Height / 2;
                Canvas.SetLeft(border, l);
                Canvas.SetTop(border, t);
                Canvas.SetZIndex(border, 1);

                canvas.Children.Add(border);
            }

            // add all lines - connections between includes
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
                    line.Visibility = (bool)menu_ShowLines.IsChecked ? Visibility.Visible : Visibility.Hidden;
                    Canvas.SetZIndex(line, -1);

                    lines.Add(line);
                    canvas.Children.Add(line);
                }
            }

            // calculate the scale of graph in order to fit it to window
            double graphW = gd.GraphSize.Width;
            double graphH = gd.GraphSize.Height;
            double graphCenterX = graphW / 2;
            double graphCenterY = graphH / 2;
            double scaleW = Math.Min(canvasGrid.ActualWidth / graphW, 1);
            double scaleH = Math.Min(canvasGrid.ActualHeight / graphH, 1);
            double scale = Math.Min(scaleW, scaleH);

            var matrix = Matrix.Identity;
            matrix.Scale(scale, scale);
            matrix.Translate(canvasGrid.ActualWidth / 2 - graphCenterX * scale, 0);
            canvas.RenderTransform = new MatrixTransform(matrix);

            if (data.dict.ContainsKey(data.rootPath))
            {
                TextBlock_MouseDown(data.dict[data.rootPath].textBlock, null);
            }
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

                    TextBlock_MouseDown(d.Value.textBlock, null);

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
                    borderActive.BorderBrush = Brushes.Blue;
                }
                textBlocks.Clear();

                foreach (var line in lines)
                {
                    canvas.Children.Remove(line);
                }
                lines.Clear();

                foreach(var path in paths)
                {
                    canvas.Children.Remove(path);
                }
                pathsPoints.Clear();
                paths.Clear();
            }

            public enum ColorId { Selected = 0, Child = 1, Parent = 2 };

            public void Select(TextBlock textBlock, ColorId colorId)
            {
                var border = textBlock.Parent as Border;
                border.BorderBrush = brushes[(int)colorId];
                border.BorderThickness = new Thickness(1);
                textBlocks.Add(textBlock);
            }
            
            public void AddLine(Point p0, Point p1, ColorId colorId)
            {
                var line = new Line();
                line.X1 = p0.X;
                line.Y1 = p0.Y;
                line.X2 = p1.X;
                line.Y2 = p1.Y;
                line.Stroke = brushes[(int)colorId];
                line.StrokeThickness = 1;

                lines.Add(line);

                canvas.Children.Add(line);
            }

            public void AddCycle(List<Point> points, double crossTrackDist)
            {
                System.Windows.Shapes.Path path = CreateCyclePath(points, crossTrackDist);

                pathsPoints.Add(points);
                paths.Add(path);

                canvas.Children.Add(path);
            }

            public void SetLineThickness(double linesThickness)
            {
                foreach (var tb in textBlocks)
                {
                    Border borderActive = tb.Parent as Border;
                    borderActive.BorderThickness = new Thickness(linesThickness);
                }
                foreach (var line in lines)
                    line.StrokeThickness = linesThickness;
                foreach (var path in paths)
                    path.StrokeThickness = linesThickness;
            }

            public void SetCyclesSeparation(double dist, double linesThickness)
            {
                foreach (var path in paths)
                    canvas.Children.Remove(path);
                paths.Clear();

                for (int i = 0; i < pathsPoints.Count; ++i)
                {
                    double crossTrackDist = dist * (int)((i + 1) / 2) * (int)(i % 2 == 0 ? 1 : -1);

                    System.Windows.Shapes.Path path = CreateCyclePath(pathsPoints[i], crossTrackDist);
                    path.StrokeThickness = linesThickness;

                    paths.Add(path);

                    canvas.Children.Add(path);
                }
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

            private System.Windows.Shapes.Path CreateCyclePath(List<Point> points, double dist)
            {
                int brushId = paths.Count;
                Brush brush = null;
                if (brushId < brushes2.Length)
                    brush = brushes2[brushId];
                else
                    brush = new SolidColorBrush(RandomColor());

                PathGeometry pathGeom = new PathGeometry();

                for (int i = 0; i < points.Count; ++i)
                {
                    Point prev = points[i];
                    Point curr = points[(i + 1) % points.Count];
                    PathFigure fig = new PathFigure();
                    fig.StartPoint = prev;
                    QuadraticBezierSegment seg = new QuadraticBezierSegment();
                    seg.Point1 = Util.CalculateBezierPoint(prev, curr, dist);
                    seg.Point2 = curr;
                    fig.Segments.Add(seg);
                    pathGeom.Figures.Add(fig);
                }

                System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
                path.Data = pathGeom;
                path.Stroke = brush;
                path.StrokeThickness = 1;

                return path;
            }

            private Color RandomColor()
            {
                Random r = new Random();
                return Color.FromRgb((byte)r.Next(64, 192), (byte)r.Next(64, 192), (byte)r.Next(64, 192));
            }

            Canvas canvas;

            List<TextBlock> textBlocks = new List<TextBlock>();
            List<Line> lines = new List<Line>();
            List<List<Point>> pathsPoints = new List<List<Point>>();
            List<System.Windows.Shapes.Path> paths = new List<System.Windows.Shapes.Path>();
            Brush[] brushes = new Brush[]{ Brushes.Red, Brushes.DarkOrange, Brushes.Green };
            Brush[] brushes2 = new Brush[] { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.DarkOrange, Brushes.DarkCyan, Brushes.DarkMagenta};
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
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
                    activeControls.AddLine(d.center, child.center, ActiveControls.ColorId.Child);
                }
                foreach (var p in d.parents)
                {
                    var parent = data.dict[p];
                    activeControls.Select(parent.textBlock, ActiveControls.ColorId.Parent);
                    activeControls.AddLine(d.center, parent.center, ActiveControls.ColorId.Parent);
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
                        activeControls.AddLine(d.center, child.center, selectedType);
                    }

                    activeControls.ReSelect(1, selectedType);
                }
            }

            activeControls.SetLineThickness(menu_LinesWidthSlider.Value);
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
                double dist = menu_CyclesLinesDistanceSlider.Value / 100;

                for (int i = 0; i < cycles.Count; ++i)
                {
                    List<Point> points = new List<Point>();

                    foreach (var s in cycles[i])
                    {
                        var curr = data.dict[s];
                        activeControls.Select(curr.textBlock, ActiveControls.ColorId.Selected);

                        points.Add(curr.center);
                    }

                    double crossTrackDist = dist * (int)((i + 1) / 2) * (int)(i % 2 == 0 ? 1 : -1);

                    activeControls.AddCycle(points, crossTrackDist * gd.CellSize.Width);
                }

                activeControls.SetLineThickness(menu_LinesWidthSlider.Value);
            }
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

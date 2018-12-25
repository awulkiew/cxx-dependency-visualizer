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
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;

            menu_Layout1.Checked += menu_Layout_Checked;
            menu_Layout2.Checked += menu_Layout_Checked;
            menu_Layout3.Checked += menu_Layout_Checked;
            menu_Layout4.Checked += menu_Layout_Checked;
            menu_Layout1.Unchecked += menu_Layout_Unchecked;
            menu_Layout2.Unchecked += menu_Layout_Unchecked;
            menu_Layout3.Unchecked += menu_Layout_Unchecked;
            menu_Layout4.Unchecked += menu_Layout_Unchecked;
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
            if (menuItem != menu_Layout3 && menu_Layout3 != null)
            {
                menu_Layout3.Unchecked -= menu_Layout_Unchecked;
                menu_Layout3.IsChecked = false;
                menu_Layout3.Unchecked += menu_Layout_Unchecked;
            }
            if (menuItem != menu_Layout4 && menu_Layout4 != null)
            {
                menu_Layout4.Unchecked -= menu_Layout_Unchecked;
                menu_Layout4.IsChecked = false;
                menu_Layout4.Unchecked += menu_Layout_Unchecked;
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

        LibData data = new LibData();

        enum GraphCreation { LevelMin, LevelMax, LevelMinClosestParent, LevelMaxClosestParent };
        GraphCreation graphCreation = GraphCreation.LevelMin;

        private void buttonAnalyze_Click(object sender, RoutedEventArgs e)
        {
            //textBlockStatus.Text = "Processing...";

            // Clear states
            Visibility vis = menu_ShowLines.IsChecked
                           ? Visibility.Visible
                           : Visibility.Hidden;
            activeControls.Reset(canvas, data, vis);
            canvas.Children.Clear();

            graphCreation = menu_Layout1.IsChecked ? GraphCreation.LevelMin
                          : menu_Layout2.IsChecked ? GraphCreation.LevelMax
                          : menu_Layout3.IsChecked ? GraphCreation.LevelMinClosestParent
                          : menu_Layout4.IsChecked ? GraphCreation.LevelMaxClosestParent
                          : GraphCreation.LevelMin;

            // Analyze includes and create dictionary
            data = new LibData(textBoxDir.Text, textBoxFile.Text, true);

            // Generate containers of levels of inclusion (min or max level found).
            List<List<string>> levels = new List<List<string>>();
            bool useMax = graphCreation == GraphCreation.LevelMax
                       || graphCreation == GraphCreation.LevelMaxClosestParent;
            foreach (var include in data.dict)
            {
                int level = useMax
                          ? include.Value.maxLevel
                          : include.Value.minLevel;
                for (int i = levels.Count; i < level + 1; ++i)
                    levels.Add(new List<string>());
                levels[level].Add(include.Key);
            }

            // Creation algorithm 1
            if (graphCreation == GraphCreation.LevelMin
             || graphCreation == GraphCreation.LevelMax)
            {
                List<int> counts = new List<int>();
                foreach (var l in levels)
                    counts.Add(l.Count());
                counts.Sort();
                int medianCount = counts.Count > 0
                                ? counts[counts.Count / 2]
                                : 0;
                for (; ; )
                {
                    bool allSet = true;
                    int y = 0;
                    for (int i = 0; i < levels.Count; ++i)
                    {
                        List<string> lvl = levels[i];
                        int x = 0;
                        int remaining = lvl.Count;
                        for (int j = 0; j < lvl.Count; ++j)
                        {
                            var d = data.dict[lvl[j]];
                            if (x >= medianCount)
                            {
                                ++y;
                                x = 0;
                                remaining -= medianCount;
                            }

                            int shift = Math.Min(medianCount, remaining) - 1;
                            d.position = new PointI(-shift / 2 + x, y);
                            ++x;
                        }
                        ++y;
                    }

                    if (allSet)
                        break;
                }
            }
            // Creation algorithm 2
            else if (graphCreation == GraphCreation.LevelMinClosestParent
                  || graphCreation == GraphCreation.LevelMaxClosestParent)
            {
                PointI rootPos = new PointI(0, 0);
                data.dict[Path(textBoxDir.Text, textBoxFile.Text)].position = rootPos;
                HashSet<PointI> pointsSet = new HashSet<PointI>();
                pointsSet.Add(rootPos);
                for (; ; )
                {
                    bool allSet = true;
                    for (int i = 0; i < levels.Count; ++i)
                    {
                        List<string> lvl = levels[i];
                        for (int j = 0; j < lvl.Count; ++j)
                        {
                            var d = data.dict[lvl[j]];
                            if (d.position == null)
                            {
                                // TODO average from all parents?
                                PointI parentPos = null;
                                foreach (var pStr in d.parents)
                                {
                                    var p = data.dict[pStr];
                                    if (p.position != null)
                                        parentPos = p.position;
                                }

                                if (parentPos == null)
                                {
                                    allSet = false;
                                }
                                else
                                {
                                    PositionFeed feed = new PositionFeed(parentPos);
                                    PointI pt = feed.Next();
                                    for (; pointsSet.Contains(pt); pt = feed.Next()) ;
                                    pointsSet.Add(pt);
                                    d.position = pt;
                                }
                            }
                        }
                    }

                    if (allSet)
                        break;
                }
            }

            // Create textBlocks with borders for each include and calculate
            // bounding box in graph grid coordinates
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            double maxSize = 0;
            foreach (var d in data.dict)
            {
                minX = Math.Min(minX, d.Value.position.x);
                minY = Math.Min(minY, d.Value.position.y);
                maxX = Math.Max(maxX, d.Value.position.x);
                maxY = Math.Max(maxY, d.Value.position.y);
                if (d.Value.textBlock == null)
                {
                    TextBlock textBlock = new TextBlock();
                    if (d.Value.duplicatedChildren)
                        textBlock.Background = Brushes.Yellow;
                    else
                        textBlock.Background = Brushes.White;
                    textBlock.Text = FileFromPath(d.Key);
                    textBlock.Padding = new Thickness(5);
                    Size s = MeasureTextBlock(textBlock);
                    textBlock.Width = s.Width + 10;
                    textBlock.Height = s.Height + 10;
                    textBlock.DataContext = d.Key;
                    string tooltip = d.Key
                                   + "\r\nLevels: [" + d.Value.minLevel + ", " + d.Value.maxLevel + "]"
                                   + (d.Value.duplicatedChildren ? "\r\nWARNING: duplicated includes" : "")
                                   + "\r\n";
                    foreach (var c in d.Value.children)
                        tooltip += "\r\n" + c;
                    textBlock.ToolTip = tooltip;
                    textBlock.MouseDown += TextBlock_MouseDown;

                    Border border = new Border();
                    border.BorderThickness = new Thickness(1);
                    if (d.Value.important)
                        border.BorderBrush = Brushes.Blue;
                    else
                        border.BorderBrush = Brushes.Black;
                    border.Child = textBlock;

                    d.Value.textBlock = textBlock;
                }
                maxSize = Math.Max(maxSize, Math.Max(d.Value.textBlock.Width, d.Value.textBlock.Height));
            }

            // Calculate the size of graph in canvas coordinates
            // from bounding box in graph grid coordinates
            int graphW = maxX - minX;
            int graphH = maxY - minY;
            double cellSize = maxSize;
            double graphWidth = cellSize * graphW;
            double graphHeight = cellSize* graphH;
            double xOrig = graphWidth / 2;
            double yOrig = 0;
            foreach (var d in data.dict)
            {
                d.Value.center.X = xOrig + d.Value.position.x * cellSize;
                d.Value.center.Y = yOrig + d.Value.position.y * cellSize;
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
                    line.Visibility = vis;
                    Canvas.SetZIndex(line, -1);

                    d.Value.childrenLines.Add(line);

                    c.parentsLines.Add(line);

                    canvas.Children.Add(line);
                }
            }

            // add all textBoxes with borders
            foreach (var d in data.dict)
            {
                double l = d.Value.center.X - d.Value.textBlock.Width / 2;
                double t = d.Value.center.Y - d.Value.textBlock.Height / 2;
                
                Border border = d.Value.textBlock.Parent as Border;
                Canvas.SetLeft(border, l);
                Canvas.SetTop(border, t);
                Canvas.SetZIndex(border, 1);

                canvas.Children.Add(border);
            }

            // calculate the scale of graph in order to fit it to window
            double scaleW = Math.Min(canvasGrid.ActualWidth / graphWidth, 1);
            double scaleH = Math.Min(canvasGrid.ActualHeight / graphHeight, 1);
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
                if (!Empty(textBoxFind.Text) && d.Key.IndexOf(textBoxFind.Text) >= 0)
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
            public void Reset(Canvas canvas, LibData data, Visibility linesVisibility)
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
                foreach (var line in lines)
                {
                    line.Stroke = Brushes.DarkGray;
                    line.StrokeThickness = 1;
                    line.Visibility = linesVisibility;
                    Canvas.SetZIndex(line, -1);
                }
                textBlocks.Clear();
                lines.Clear();

                foreach(var shape in shapes)
                {
                    canvas.Children.Remove(shape);
                }
                shapes.Clear();
            }

            public enum SelectType { Selected = 0, Child = 1, Parent = 2 };

            public void Select(TextBlock textBlock, SelectType type)
            {
                var border = textBlock.Parent as Border;
                border.BorderBrush = brushes[(int)type];
                border.BorderThickness = new Thickness(2);
                textBlocks.Add(textBlock);
            }

            public void Select(Line line, SelectType type, double linesThickness)
            {
                line.Stroke = brushes[(int)type];
                line.StrokeThickness = linesThickness;
                line.Visibility = Visibility.Visible;
                Canvas.SetZIndex(line, 0);
                lines.Add(line);
            }

            public void Add(Canvas canvas, Shape shape, double linesThickness)
            {
                int brushId = shapes.Count;
                Brush brush = null;
                if (brushId < brushes2.Length)
                {
                    brush = brushes2[brushId];
                }
                else
                {
                    Random r = new Random();
                    brush = new SolidColorBrush(Color.FromRgb((byte)r.Next(64, 192),
                                                              (byte)r.Next(64, 192),
                                                              (byte)r.Next(64, 192)));
                }

                shape.Stroke = brush;
                shape.StrokeThickness = linesThickness;
                shapes.Add(shape);

                canvas.Children.Add(shape);
            }

            public TextBlock FirstSelected()
            {
                return textBlocks.Count > 0 ? textBlocks[0] : null;
            }

            public void ReSelect(int i, SelectType type)
            {
                if (i < textBlocks.Count)
                    (textBlocks[i].Parent as Border).BorderBrush = brushes[(int)type];
            }

            List<TextBlock> textBlocks = new List<TextBlock>();
            List<Line> lines = new List<Line>();
            List<Shape> shapes = new List<Shape>();
            Brush[] brushes = new Brush[]{ Brushes.Red, Brushes.DarkOrange, Brushes.Green };
            Brush[] brushes2 = new Brush[] { Brushes.Red, Brushes.Green, Brushes.Blue, Brushes.DarkOrange, Brushes.DarkCyan, Brushes.DarkMagenta};
        }

        ActiveControls activeControls = new ActiveControls();
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

            Visibility vis = menu_ShowLines.IsChecked
                           ? Visibility.Visible
                           : Visibility.Hidden;
            activeControls.Reset(canvas, data, vis);
            
            if (lastTextBlock != null)
                activeControls.Select(lastTextBlock, ActiveControls.SelectType.Selected);

            activeControls.Select(textBlock, ActiveControls.SelectType.Selected);

            // select chldren and parents
            if (lastTextBlock == null)
            {
                var d = data.dict[textBlock.DataContext as string];
                foreach (var c in d.children)
                    activeControls.Select(data.dict[c].textBlock, ActiveControls.SelectType.Child);
                foreach (var p in d.parents)
                    activeControls.Select(data.dict[p].textBlock, ActiveControls.SelectType.Parent);
                foreach (var line in d.childrenLines)
                    activeControls.Select(line, ActiveControls.SelectType.Child, linesThickness);
                foreach (var line in d.parentsLines)
                    activeControls.Select(line, ActiveControls.SelectType.Parent, linesThickness);
            }
            // find and select path
            else
            {
                ActiveControls.SelectType selectedType = ActiveControls.SelectType.Child;
                List<string> path = FindPath(lastTextBlock.DataContext as string,
                                             textBlock.DataContext as string,
                                             data.dict);
                if (path.Count == 0)
                {
                    selectedType = ActiveControls.SelectType.Parent;
                    path = FindPath(textBlock.DataContext as string,
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
                        int ip = d.children.IndexOf(path[i + 1]);
                        if (ip >= 0 && ip < d.childrenLines.Count) // just in case
                            activeControls.Select(d.childrenLines[ip], selectedType, linesThickness);
                    }

                    activeControls.ReSelect(1, selectedType);
                }
            }
        }

        class StringListsCompare : IEqualityComparer<List<string>>
        {
            public bool Equals(List<string> x, List<string> y)
            {
                if (x.Count != y.Count)
                    return false;
                for (int i = 0; i < x.Count; ++i)
                    if (x[i] != y[i])
                        return false;
                return true;
            }

            public int GetHashCode(List<string> obj)
            {
                return obj.GetHashCode();
            }
        }

        private int IndexOfSmallest(List<string> list)
        {
            int result = -1;
            if (list.Count > 0)
            {
                result = 0;
                string smallest = list[0];
                for (int i = 1; i < list.Count; ++i)
                    if (list[i].CompareTo(smallest) < 0)
                    {
                        result = i;
                        smallest = list[i];
                    }
            }
            return result;
        }

        private void Rotate(List<string> list, int firstId)
        {
            if (0 < firstId && firstId < list.Count)
            {
                List<string> l1 = new List<string>();
                List<string> l2 = new List<string>();
                for (int i = 0; i < firstId; ++i)
                    l1.Add(list[i]);
                for (int i = firstId; i < list.Count; ++i)
                    l2.Add(list[i]);
                list.Clear();
                foreach (var s in l2)
                    list.Add(s);
                foreach (var s in l1)
                    list.Add(s);
            }
        }

        private void MenuItemCycles_Click(object sender, RoutedEventArgs e)
        {
            Visibility vis = menu_ShowLines.IsChecked
                           ? Visibility.Visible
                           : Visibility.Hidden;
            double linesThickness = menu_LinesWidthSlider.Value;

            activeControls.Reset(canvas, data, vis);

            List<List<string>> cycles = new List<List<string>>();
            foreach (var d in data.dict)
            {
                List<string> cycle = FindCycle(d.Key, data.dict);
                if (cycle.Count > 0)
                {
                    // Find smallest and rotate
                    int idRot = IndexOfSmallest(cycle);
                    Rotate(cycle, idRot);
                    if (!cycles.Contains(cycle, new StringListsCompare()))
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
                        activeControls.Select(curr.textBlock, ActiveControls.SelectType.Selected);

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
                    activeControls.Add(canvas, path, linesThickness);
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

        class PositionFeed : RelativePositionFeed
        {
            public PositionFeed(PointI parentPos)
            {
                this.x = parentPos.x;
                this.y = parentPos.y;
            }

            public new PointI Next()
            {
                PointI res = base.Next();
                res.x += x;
                res.y += y;
                return res;
            }

            int x, y;
        }

        class RelativePositionFeed
        {
            public PointI Next()
            {
                if (sign == 0)
                {
                    x = 0;
                    y = 0 + dist;
                    sign = -1;
                }
                else
                {
                    // move
                    if (sign < 0)
                    {
                        if (x < dist || y > 0)
                        {
                            if (x < dist)
                                ++x;
                            else if (y > 0)
                                --y;
                        }
                        else
                        {
                            ++dist;
                            x = 0;
                            y = dist;
                            sign = 1;
                        }
                    }
                    sign *= -1;
                }

                return new PointI(x * sign, y);
            }

            int sign = 0;
            int dist = 1;
            int x = 0;
            int y = 0;
        }

        class PointI : IEquatable<PointI>
        {
            public PointI(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            public override int GetHashCode()
            {
                return Tuple.Create(x, y).GetHashCode();
            }

            public bool Equals(PointI other)
            {
                return x == other.x && y == other.y;
            }

            public int x;
            public int y;
        }

        class IncludeData
        {
            public IncludeData(string dir, string path, string parentPath,
                               List<string> children,
                               bool important, int level)
            {
                if (!Empty(parentPath))
                    this.parents.Add(parentPath);
                this.children = children;
                this.important = important;

                this.minLevel = level;
                this.maxLevel = level;
            }

            public void Update(string parentPath, int level)
            {
                if (!Empty(parentPath))
                    this.parents.Add(parentPath);
                this.minLevel = Math.Min(this.minLevel, level);
                this.maxLevel = Math.Max(this.maxLevel, level);
            }

            public List<string> children = new List<string>();
            public List<string> parents = new List<string>();
            public bool important = true;
            public int minLevel = -1;
            public int maxLevel = -1;
            public bool duplicatedChildren = false;

            public PointI position = null;
            public Point center;
            public TextBlock textBlock = null;
            public List<Line> childrenLines = new List<Line>();
            public List<Line> parentsLines = new List<Line>();
        }

        class LibData
        {
            public LibData()
            { }

            public LibData(string dir, string file, bool fromLibOnly)
            {
                rootPath = Path(dir, file);
                Analyze(dir, rootPath, null, fromLibOnly, 0, dict);
            }

            public List<string> ChildrenOf(string path)
            {
                if (!dict.ContainsKey(path))
                    return new List<string>();
                return dict[path].children;
            }

            public List<string> ParentsOf(string path)
            {
                if (!dict.ContainsKey(path))
                    return new List<string>();
                return dict[path].parents;
            }

            public string Root()
            {
                return rootPath;
            }

            public string rootPath = "";
            public Dictionary<string, IncludeData> dict = new Dictionary<string, IncludeData>();
        }

        class Grid
        {
            public void Set(int x, int y, string v)
            {
                ResizeToContain(x, y);
                int xCell = x - xBegin;
                xData[xCell].Set(y, v);
            }

            public string Get(int x, int y)
            {
                int xCell = x - xBegin;
                if (xCell < 0 || xCell >= xData.Count)
                    return "";
                return xData[xCell].Get(y);
            }

            public void ResizeToContain(int x, int y)
            {
                if (xData.Count == 0)
                {
                    xBegin = x;
                    xData.Add(new YEntry());
                }
                else
                {
                    int xCell = x - xBegin;
                    if (xCell >= 0)
                    {
                        for (int i = xData.Count; i < xCell + 1; ++i)
                            xData.Add(new YEntry());
                    }
                    else
                    {
                        for (int i = 0; i < -xCell; ++i)
                            xData.Insert(0, new YEntry());
                        xBegin = x;
                        xCell = 0;
                    }
                }

                xData[x - xBegin].ResizeToContain(y);
            }

            class YEntry
            {
                public void Set(int y, string v)
                {
                    ResizeToContain(y);
                    int yCell = y - yBegin;
                    yData[yCell] = v;
                }

                public string Get(int y)
                {
                    int yCell = y - yBegin;
                    if (yCell < 0 || yCell >= yData.Count)
                        return "";
                    return yData[yCell];
                }

                public void ResizeToContain(int y)
                {
                    if (yData.Count == 0)
                    {
                        yBegin = y;
                        yData.Add("");
                    }
                    else
                    {
                        int yCell = y - yBegin;
                        if (yCell >= 0)
                        {
                            for (int i = yData.Count; i < yCell + 1; ++i)
                                yData.Add("");
                        }
                        else
                        {
                            for (int i = 0; i < -yCell; ++i)
                                yData.Insert(0, "");
                            yBegin = y;
                        }
                    }
                }

                int yBegin;
                List<string> yData = new List<string>();
            }

            List<YEntry> xData = new List<YEntry>();
            int xBegin = 0;
        }

        private static void Analyze(string dir, string path, string parentPath, bool fromLibOnly, int level, Dictionary<string, IncludeData> dict)
        {
            if (dict.ContainsKey(path))
            {
                dict[path].Update(parentPath, level);
            }
            else
            {
                List<string> children = GetChildren(dir, path);
                IncludeData data = new IncludeData(dir,
                                                   path,
                                                   parentPath,
                                                   new List<string>(),
                                                   File.Exists(path),
                                                   level);
                foreach (string c in children)
                    if (!fromLibOnly || File.Exists(c))
                        if (data.children.Contains(c))
                            data.duplicatedChildren = true;
                        else
                            data.children.Add(c);

                dict.Add(path, data);

                foreach (string p in data.children)
                {
                    Analyze(dir, p, path, fromLibOnly, level + 1, dict);
                }
            }
        }

        private static List<string> FindPath(string start, string end,
                                             Dictionary<string, IncludeData> dict)
        {
            List<string> result = new List<string>();
            Dictionary<string, int> map = new Dictionary<string, int>();
            map.Add(start, 0);
            FindPath(start, start, end, dict, map, result);
            return result;
        }

        private static bool FindPath(string start, string curr, string end,
                                     Dictionary<string, IncludeData> dict,
                                     Dictionary<string, int> map,
                                     List<string> result)
        {
            if (curr == end)
            {
                return TrackPathBack(curr, start, dict, map, result);
            }

            var neighbors = dict[curr].children;
            var l = map[curr];
            List<string> updated = new List<string>();
            foreach (var n in neighbors)
                if (Update(map, n, l + 1))
                    updated.Add(n);
            foreach (var n in updated)
                if (FindPath(start, n, end, dict, map, result))
                    return true;
            return false;
        }

        private static bool TrackPathBack(string curr, string start,
                                          Dictionary<string, IncludeData> dict,
                                          Dictionary<string, int> map,
                                          List<string> result)
        {
            if (curr == start)
            {
                result.Add(curr);
                return true;
            }

            var l = map[curr];
            var neighbors = dict[curr].parents;
            foreach (var n in neighbors)
                if (map.ContainsKey(n) && map[n] < l)
                    if (TrackPathBack(n, start, dict, map, result))
                    {
                        result.Add(curr);
                        return true;
                    }
            return false;
        }

        private static List<string> FindCycle(string header,
                                              Dictionary<string, IncludeData> dict)
        {
            List<string> result = new List<string>();
            Dictionary<string, int> map = new Dictionary<string, int>();
            var d = dict[header];
            foreach(var cStr in d.children)
                map.Add(cStr, 0);
            foreach (var cStr in d.children)
                if (FindPath(cStr, cStr, header, dict, map, result))
                    return result;
            return result;
        }

        private static bool Update(Dictionary<string, int> map, string n, int l)
        {
            if (!map.ContainsKey(n))
            {
                map.Add(n, l);
                return true;
            }
            else if (l < map[n])
            {
                map[n] = l;
                return true;
            }
            return false;
        }
        
        private static string Path(string dir, string file)
        {
            string d = dir.Replace('/', '\\');
            string f = file.Replace('/', '\\');
            if (!d.EndsWith("\\"))
                d += '\\';
            return d + f;
        }

        private static string DirFromPath(string path)
        {
            int id = path.LastIndexOf('\\');
            return id >= 0
                 ? path.Substring(0, id)
                 : path.Clone() as string;
        }

        private static string FileFromPath(string path)
        {
            int id = path.LastIndexOf('\\');
            return id >= 0
                 ? path.Substring(id + 1)
                 : path.Clone() as string;
        }

        private static bool Empty(string s)
        {
            return s == null || s.Length == 0;
        }

        private static List<string> GetChildren(string dir, string path)
        {
            List<string> result = new List<string>();

            if (File.Exists(path))
            {
                StreamReader sr = new StreamReader(path);
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!Empty(line))
                    {
                        int idInclude = line.IndexOf("#include");
                        if (idInclude >= 0)
                        {
                            int idBegin = -1;
                            char closingChar = '\0';
                            if ((idBegin = line.IndexOf('<')) >= 0)
                                closingChar = '>';
                            else if ((idBegin = line.IndexOf('"')) >= 0)
                                closingChar = '"';
                            int idEnd = idBegin >= 0 ? line.IndexOf(closingChar) : -1;
                            if (idEnd >= 0 && idBegin <= idEnd)
                            {
                                string include = line.Substring(idBegin + 1, idEnd - idBegin - 1);
                                string p = Path(closingChar == '>'
                                                    ? dir
                                                    : DirFromPath(path),
                                                include);
                                result.Add(p);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static Size MeasureTextBlock(TextBlock textBlock)
        {
            var formattedText = new FormattedText(textBlock.Text,
                                                  CultureInfo.CurrentCulture,
                                                  FlowDirection.LeftToRight,
                                                  new Typeface(textBlock.FontFamily,
                                                               textBlock.FontStyle,
                                                               textBlock.FontWeight,
                                                               textBlock.FontStretch),
                                                  textBlock.FontSize,
                                                  Brushes.Black,
                                                  new NumberSubstitution(),
                                                  TextFormattingMode.Display);

            return new Size(formattedText.Width, formattedText.Height);
        }
    }
}

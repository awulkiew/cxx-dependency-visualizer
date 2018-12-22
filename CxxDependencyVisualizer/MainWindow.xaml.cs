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
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        LibData data = new LibData();

        private void buttonAnalyze_Click(object sender, RoutedEventArgs e)
        {
            //textBlockStatus.Text = "Processing...";
            
            data = new LibData(textBoxDir.Text, textBoxFile.Text, true);

            List<int> counts = new List<int>();
            foreach (var l in data.levels)
                counts.Add(l.Count());
            counts.Sort();
            int medianCount = counts.Count > 0
                            ? counts[counts.Count / 2]
                            : 0;
            for (; ; )
            {
                bool allSet = true;
                int y = 0;
                for (int i = 0; i < data.LevelsCount(); ++i)
                {
                    List<string> lvl = data.Level(i);
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

            /*
            PointI rootPos = new PointI(0, 0);
            data.dict[Path(textBoxDir.Text, textBoxFile.Text)].position = rootPos;
            HashSet<PointI> pointsSet = new HashSet<PointI>();
            pointsSet.Add(rootPos);
            for (; ; )
            {
                bool allSet = true;
                for (int i = 0; i < data.LevelsCount(); ++i)
                {
                    List<string> lvl = data.Level(i);
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
            */

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
                    textBlock.ToolTip = d.Key
                                      + "\r\nLevels: [" + d.Value.minLevel + ", " + d.Value.maxLevel + "]"
                                      + (d.Value.duplicatedChildren ? "\r\nWARNING: duplicated includes" : "");
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

            canvas.Children.Clear();

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
                    line.Visibility = Visibility.Hidden;

                    d.Value.childrenLines.Add(line);

                    c.parentsLines.Add(line);

                    canvas.Children.Add(line);
                }
            }

            foreach (var d in data.dict)
            {
                double l = d.Value.center.X - d.Value.textBlock.Width / 2;
                double t = d.Value.center.Y - d.Value.textBlock.Height / 2;
                
                Border border = d.Value.textBlock.Parent as Border;
                Canvas.SetLeft(border, l);
                Canvas.SetTop(border, t);

                canvas.Children.Add(border);
            }

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
                foreach (var line in lines)
                {
                    line.Stroke = Brushes.DarkGray;
                    line.StrokeThickness = 1;
                    line.Visibility = Visibility.Hidden;
                }
                textBlocks.Clear();
                lines.Clear();
            }

            public enum SelectType { Selected = 0, Child = 1, Parent = 2 };

            public void Select(TextBlock textBlock, SelectType type)
            {
                var border = textBlock.Parent as Border;
                border.BorderBrush = brushes[(int)type];
                border.BorderThickness = new Thickness(2);
                textBlocks.Add(textBlock);
            }

            public void Select(Line line, SelectType type)
            {
                line.Stroke = brushes[(int)type];
                line.StrokeThickness = 2;
                line.Visibility = Visibility.Visible;
                lines.Add(line);
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
            Brush[] brushes = new Brush[]{ Brushes.Red, Brushes.DarkOrange, Brushes.Green };
        }

        ActiveControls activeControls = new ActiveControls();
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
                    activeControls.Select(line, ActiveControls.SelectType.Child);
                foreach (var line in d.parentsLines)
                    activeControls.Select(line, ActiveControls.SelectType.Parent);
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
                            activeControls.Select(d.childrenLines[ip], selectedType);
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
            activeControls.Reset(data);

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
                string message = "Found cycles {" + cycles.Count.ToString() + "}:";
                int ci = 1;
                foreach (var cycle in cycles)
                {
                    message += "\r\n" + (ci++) + ":";
                    foreach (var h in cycle)
                        message += "\r\n" + h;
                }

                foreach (var cycle in cycles)
                {
                    var prev = data.dict[cycle[cycle.Count - 1]];
                    foreach(var s in cycle)
                    {
                        var curr = data.dict[s];
                        activeControls.Select(curr.textBlock, ActiveControls.SelectType.Selected);
                        int id = prev.children.IndexOf(s);
                        if (id >= 0)
                            activeControls.Select(prev.childrenLines[id], ActiveControls.SelectType.Selected);
                        prev = curr;
                    }
                }

                MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
                MessageBox.Show("No cycles found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                FillLevels(dict, levels);
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

            public int LevelsCount()
            {
                return levels.Count;
            }

            public List<string> Level(int i)
            {
                return levels[i];
            }

            public string rootPath = "";
            public Dictionary<string, IncludeData> dict = new Dictionary<string, IncludeData>();
            public List<List<string>> levels = new List<List<string>>();
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

        private static void Add(List<List<string>> levels, string path, int level)
        {
            for (int i = levels.Count; i < level + 1; ++i)
                levels.Add(new List<string>());
            levels[level].Add(path);
        }

        private static void FillLevels(Dictionary<string, IncludeData> dict, List<List<string>> levels)
        {
            foreach(var include in dict)
            {
                Add(levels, include.Key, include.Value.minLevel);
            }
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

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
        }

        LibData data = null;

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
                if (d.Value.border == null || d.Value.textBlock == null)
                {
                    TextBlock textBlock = new TextBlock();
                    textBlock.Background = Brushes.White;
                    textBlock.Text = FileFromPath(d.Key);
                    textBlock.Padding = new Thickness(5);
                    Size s = MeasureTextBlock(textBlock);
                    textBlock.Width = s.Width + 10;
                    textBlock.Height = s.Height + 10;
                    textBlock.DataContext = d.Key;
                    textBlock.ToolTip = d.Key + "\r\nLevels: [" + d.Value.minLevel + ", " + d.Value.maxLevel + "]";
                    textBlock.MouseDown += TextBlock_MouseDown;

                    Border border = new Border();
                    border.BorderThickness = new Thickness(1);
                    if (d.Value.important)
                        border.BorderBrush = Brushes.Blue;
                    else
                        border.BorderBrush = Brushes.Black;
                    border.Child = textBlock;

                    d.Value.textBlock = textBlock;
                    d.Value.border = border;
                }
                maxSize = Math.Max(maxSize, Math.Max(d.Value.textBlock.Width, d.Value.textBlock.Height));
            }

            int graphW = maxX - minX;
            int graphH = maxY - minY;
            double cellSize = maxSize * 0.75;
            double graphWidth = cellSize * graphW;
            double graphHeight = cellSize* graphH;
            double xOrig = graphWidth / 2;
            double yOrig = 0;

            canvas.Children.Clear();

            foreach (var d in data.dict)
            {
                double x = xOrig + d.Value.position.x * cellSize;
                double y = yOrig + d.Value.position.y * cellSize;

                foreach (var cStr in d.Value.children)
                {
                    var c = data.dict[cStr];

                    double xC = xOrig + c.position.x * cellSize;
                    double yC = yOrig + c.position.y * cellSize;

                    var line = new Line();
                    line.X1 = x;
                    line.Y1 = y;
                    line.X2 = xC;
                    line.Y2 = yC;
                    line.Stroke = Brushes.DarkGray;
                    line.Visibility = Visibility.Hidden;

                    d.Value.childrenEdges.Add(line);

                    c.parentEdges.Add(line);

                    canvas.Children.Add(line);
                }
            }

            foreach (var d in data.dict)
            {
                double x = xOrig + d.Value.position.x * cellSize;
                double y = yOrig + d.Value.position.y * cellSize;
                double l = x - d.Value.textBlock.Width / 2;
                double t = y - d.Value.textBlock.Height / 2;

                Canvas.SetLeft(d.Value.border, l);
                Canvas.SetTop(d.Value.border, t);

                canvas.Children.Add(d.Value.border);
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
                    double xFound = Canvas.GetLeft(d.Value.border);
                    double yFound = Canvas.GetTop(d.Value.border);
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

        TextBlock textBlockActive = null;
        Border borderActive = null;
        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If this event is fired data != null

            if (textBlockActive != null)
            {
                var d = data.dict[textBlockActive.DataContext as string];
                foreach (var line in d.childrenEdges)
                {
                    line.Stroke = Brushes.DarkGray;
                    line.Visibility = Visibility.Hidden;
                }
                foreach (var line in d.parentEdges)
                {
                    line.Stroke = Brushes.DarkGray;
                    line.Visibility = Visibility.Hidden;
                }
                if (borderActive != null)
                {
                    borderActive.BorderThickness = new Thickness(1);
                    if (d.important)
                        borderActive.BorderBrush = Brushes.Blue;
                    else
                        borderActive.BorderBrush = Brushes.Black;
                }
            }

            borderActive = null;
            textBlockActive = null;

            var textBlock = sender as TextBlock;
            var border = textBlock.Parent as Border;
            border.BorderThickness = new Thickness(2);
            border.BorderBrush = Brushes.Red;

            foreach (var line in data.dict[textBlock.DataContext as string].childrenEdges)
            {
                line.Stroke = Brushes.Red;
                line.Visibility = Visibility.Visible;
            }

            foreach (var line in data.dict[textBlock.DataContext as string].parentEdges)
            {
                line.Stroke = Brushes.Green;
                line.Visibility = Visibility.Visible;
            }

            textBlockActive = textBlock;
            borderActive = border;
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

            public PointI position = null;
            public Border border = null;
            public TextBlock textBlock = null;
            public List<Line> childrenEdges = new List<Line>();
            public List<Line> parentEdges = new List<Line>();
        }

        class LibData
        {
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
                        data.children.Add(c);

                dict.Add(path, data);

                foreach (string p in data.children)
                {
                    Analyze(dir, p, path, fromLibOnly, level + 1, dict);
                }
            }
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
            return s == null || s == "";
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

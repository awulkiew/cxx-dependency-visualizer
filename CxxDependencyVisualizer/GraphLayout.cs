using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CxxDependencyVisualizer
{
    class GraphLayout
    {
        public enum UseLevel { Min, Max };

        public struct GraphData
        {
            public Size CellSize;
            public Size GraphSize;
        }

        public static GraphData LevelBasedLayout(Dictionary<string, Node> dict, UseLevel useLevel)
        {
            // Generate containers of levels of inclusion (min or max level found).
            bool useMin = (useLevel == UseLevel.Min);
            List<List<Node>> levels = Levels(dict, useMin);

            Dictionary<Node, PointI> gridPositions = new Dictionary<Node, PointI>();

            // Simple layout algorithm based on include level
            List<int> counts = new List<int>();
            foreach (var l in levels)
                counts.Add(l.Count());
            counts.Sort();
            int medianCount = counts.Count > 0
                            ? counts[counts.Count / 2]
                            : 0;

            int y = 0;
            for (int i = 0; i < levels.Count; ++i)
            {
                List<Node> lvl = levels[i];
                int x = 0;
                int remaining = lvl.Count;
                for (int j = 0; j < lvl.Count; ++j)
                {
                    if (x >= medianCount)
                    {
                        ++y;
                        x = 0;
                        remaining -= medianCount;
                    }

                    int shift = Math.Min(medianCount, remaining) - 1;
                    PointI position = new PointI(-shift / 2 + x, y);
                    ++x;

                    gridPositions.Add(lvl[j], position);
                }
                ++y;
            }

            // Calculate bounding box in graph grid coordinates
            //   and max size
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            double maxSize = 0;
            foreach (var d in dict)
            {
                PointI position = gridPositions[d.Value];
                minX = Math.Min(minX, position.X);
                minY = Math.Min(minY, position.Y);
                maxX = Math.Max(maxX, position.X);
                maxY = Math.Max(maxY, position.Y);

                string file = Util.FileFromPath(d.Key);
                d.Value.size = Util.MeasureTextBlock(file);
                d.Value.size.Width += 10;
                d.Value.size.Height += 10;
                maxSize = Math.Max(maxSize, Math.Max(d.Value.size.Width, d.Value.size.Height));
            }

            // Calculate the size of graph in canvas coordinates
            // from bounding box in graph grid coordinates
            int graphW = maxX - minX;
            int graphH = maxY - minY;
            double cellSize = maxSize;
            double graphWidth = cellSize * graphW;
            double graphHeight = cellSize * graphH;
            double xOrig = graphWidth / 2;
            double yOrig = 0;
            foreach (var n in gridPositions)
            {
                PointI position = n.Value;
                n.Key.center.X = xOrig + (position.X + 0.5) * cellSize;
                n.Key.center.Y = yOrig + (position.Y + 0.5) * cellSize;
            }

            GraphData result;
            result.GraphSize = new Size(graphWidth + cellSize, graphHeight + cellSize);
            result.CellSize = new Size(cellSize, cellSize);
            return result;
        }

        // https://www.mathematica-journal.com/issue/v10i1/contents/graph_draw/graph_draw.pdf
        // k - optimal distance or natural spring length
        // c - regulates  the  relative  strength  of  the  repulsive and  attractive forces
        public static GraphData ForceDirectedLayout(Dictionary<string, Node> dict)
        {
            // initial layout
            GraphData gd = SquareLayout(dict);

            double k = gd.CellSize.Width;
            double c = 1;
            //double tol = dict.Count * 0.1;
            double maxIterations = 100;
            double t = 0.9;
            double initialStep = gd.CellSize.Width;

            List<Node> x = new List<Node>(dict.Count);
            foreach (var d in dict)
                x.Add(d.Value);

            double step = initialStep;
            double energy = double.PositiveInfinity;
            int progress = 0;

            for(int iter = 0; iter < maxIterations; ++iter)
            {
                /*List<Point> x0 = new List<Point>(x.Count);
                foreach (Node n in x)
                    x0.Add(n.center);*/

                double energy0 = energy;
                energy = 0;

                for (int i = 0; i < x.Count; ++i)
                {
                    Node node = x[i];

                    Point faSum = AttractiveForcesSum(node, k);
                    Point frSum = RepulsiveForcesSum(node, k, c, x);
                    Point f = Util.Add(faSum, frSum);
                    double fLenSqr = Util.LenSqr(f);
                    double fLen = Math.Sqrt(fLenSqr);

                    Point fn = Util.Div(f, fLen);
                    Point fs = Util.Mul(step, fn);
                    node.center = Util.Add(node.center, fs);

                    energy += fLenSqr;
                }

                if (energy < energy0)
                {
                    ++progress;
                    if (progress >= 5)
                    {
                        progress = 0;
                        step /= t;
                    }
                }
                else
                {
                    progress = 0;
                    step *= t;
                }

                /*double distSum = 0;
                for(int i = 0; i < x.Count; ++i)
                {
                    distSum += Util.Distance(x[i].center, x0[i]);
                }

                if (distSum < k * tol)
                    break;*/
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            for (int i = 0; i < x.Count; ++i)
            {
                minX = Math.Min(minX, x[i].center.X);
                minY = Math.Min(minY, x[i].center.Y);
                maxX = Math.Max(maxX, x[i].center.X);
                maxY = Math.Max(maxY, x[i].center.Y);
            }

            for (int i = 0; i < x.Count; ++i)
            {
                x[i].center.X += - minX + gd.CellSize.Width / 2;
                x[i].center.Y += - minY + gd.CellSize.Height / 2;
            }

            gd.GraphSize.Width = maxX - minX + gd.CellSize.Width;
            gd.GraphSize.Height = maxY - minY + gd.CellSize.Height;

            return gd;
        }

        public static GraphData RadialHierarchicalLayout(string dir, Dictionary<string, Node> dict)
        {
            string dirPath = Util.PathFromDirFile(dir, "");
            List<DirNode.PathNodePair> inputNodes = new List<DirNode.PathNodePair>();
            foreach (var d in dict)
            {
                if (d.Key.StartsWith(dirPath))
                {
                    string path = d.Key.Substring(dirPath.Length);
                    inputNodes.Add(new DirNode.PathNodePair(path, d.Value));
                }
            }

            DirNode hierarchy = DirNode.Create(inputNodes);
            
            double maxSize = 0;
            foreach (var d in dict)
            {
                string file = Util.FileFromPath(d.Key);
                d.Value.size = Util.MeasureTextBlock(file);
                d.Value.size.Width += 10;
                d.Value.size.Height += 10;
                maxSize = Math.Max(maxSize, Math.Max(d.Value.size.Width, d.Value.size.Height));
            }

            SetRadialPositions(hierarchy, maxSize, 2 * Math.PI / hierarchy.width);

            GraphData result = new GraphData();
            result.CellSize = new Size(maxSize, maxSize);

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            for (int i = 0; i < inputNodes.Count; ++i)
            {
                minX = Math.Min(minX, inputNodes[i].node.center.X);
                minY = Math.Min(minY, inputNodes[i].node.center.Y);
                maxX = Math.Max(maxX, inputNodes[i].node.center.X);
                maxY = Math.Max(maxY, inputNodes[i].node.center.Y);
            }

            for (int i = 0; i < inputNodes.Count; ++i)
            {
                inputNodes[i].node.center.X += -minX + result.CellSize.Width / 2;
                inputNodes[i].node.center.Y += -minY + result.CellSize.Height / 2;
            }

            result.GraphSize.Width = maxX - minX + result.CellSize.Width;
            result.GraphSize.Height = maxY - minY + result.CellSize.Height;
            
            return result;
        }

        private static GraphData SquareLayout(Dictionary<string, Node> dict)
        {
            int rowCount = (int)(Math.Sqrt(dict.Count) + 0.5);

            double maxSize = 0;
            foreach (var d in dict)
            {
                string file = Util.FileFromPath(d.Key);
                d.Value.size = Util.MeasureTextBlock(file);
                d.Value.size.Width += 10;
                d.Value.size.Height += 10;
                maxSize = Math.Max(maxSize, Math.Max(d.Value.size.Width, d.Value.size.Height));
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            int i = 0;
            int j = 0;
            foreach (var d in dict)
            {
                double x = i * maxSize;
                double y = j * maxSize;
                d.Value.center.X = x;
                d.Value.center.Y = y;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);

                ++i;
                if (i >= rowCount)
                {
                    ++j;
                    i = 0;
                }
            }

            foreach (var d in dict)
            {
                d.Value.center.X += 0.5 * maxSize;
                d.Value.center.Y += 0.5 * maxSize;
            }

            GraphData result;
            result.GraphSize = new Size(maxX - minX + maxSize, maxY - minY + maxSize);
            result.CellSize = new Size(maxSize, maxSize);
            return result;
        }

        private static Point AttractiveForcesSum(Node node, double k)
        {
            Point result = new Point(0, 0);
            foreach (var c in node.childNodes)
            {
                Point v = Util.Sub(c.center, node.center);
                double len = Util.Len(v);
                Point fi = Util.Mul(len / k, v);
                result = Util.Add(result, fi);
            }
            foreach (var p in node.parentNodes)
            {
                Point v = Util.Sub(p.center, node.center);
                double len = Util.Len(v);
                Point fi = Util.Mul(len / k, v);
                result = Util.Add(result, fi);
            }
            return result;
        }

        private static Point RepulsiveForcesSum(Node node, double k, double c, List<Node> nodes)
        {
            double numerator = -c * k * k;
            Point result = new Point(0, 0);
            foreach (var n in nodes)
            {
                if (node != n)
                {
                    Point v = Util.Sub(n.center, node.center);
                    double lenSqr = Util.LenSqr(v);
                    // ignore for now
                    // alternative: use all nodes centroid, so push outward the whole graph
                    //   problem: node may be in the center, so same situation
                    if (lenSqr == 0)
                        continue;
                    double factor = numerator / lenSqr;
                    Point fi = Util.Mul(factor, v);
                    result = Util.Add(result, fi);
                }
            }
            return result;
        }

        private static List<List<Node>> Levels(Dictionary<string, Node> dict, bool useMinLevel)
        {
            // Generate containers of levels of inclusion (min or max level found).
            List<List<Node>> result = new List<List<Node>>();
            foreach (var d in dict)
            {
                int level = useMinLevel ? d.Value.minLevel : d.Value.maxLevel;
                Util.Resize(result, level + 1);
                result[level].Add(d.Value);
            }
            return result;
        }

        private static void SetRadialPositions(DirNode node,
                                               double nodeSize, double angleFactor,
                                               double widthBegin = 0.0,
                                               int level = 0)
        {
            double angle = node.width * angleFactor;
            double angleBegin = widthBegin * angleFactor;
            double angleStep = angle / node.nodes.Count;

            if (level == 0 && node.nodes.Count > 1)
                level = 1;

            double r = level * nodeSize / angleFactor * 0.33;
            double a = angleBegin + angleStep * 0.5;
            foreach (var n in node.nodes)
            {
                n.center.X = r * Math.Cos(a);
                n.center.Y = r * Math.Sin(a);
                a += angleStep;
            }

            double w = 0.0;
            foreach (var ch in node.childDirs)
            {
                SetRadialPositions(ch, nodeSize, angleFactor, widthBegin + w, level + 1);
                w += ch.width;
            }
        }        

        private struct PointI
        {
            public PointI(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        private class DirNode
        {
            public struct PathNodePair
            {
                public PathNodePair(string p, Node n)
                {
                    path = p;
                    node = n;
                }
                public string path;
                public Node node;
            }

            public static DirNode Create(List<PathNodePair> nodes)
            {
                DirNode result = CreateHierarchy(nodes);
                FillLevels(result);
                return result;
            }

            private static DirNode CreateHierarchy(List<PathNodePair> nodes)
            {
                Dictionary<string, List<PathNodePair>> dict = new Dictionary<string, List<PathNodePair>>();
                foreach (var n in nodes)
                {
                    int i = n.path.IndexOf("\\");
                    string currentDir = "";
                    string restOfPath = n.path;
                    if (i >= 0)
                    {
                        currentDir = n.path.Substring(0, i);
                        restOfPath = n.path.Substring(i + 1);
                    }

                    if (!dict.ContainsKey(currentDir))
                        dict.Add(currentDir, new List<PathNodePair>());

                    dict[currentDir].Add(new PathNodePair(restOfPath, n.node));
                }

                DirNode result = new DirNode();

                foreach(var d in dict)
                {
                    if (d.Key != "") // list of directories
                    {
                        DirNode ch = CreateHierarchy(d.Value);
                        result.childDirs.Add(ch);
                    }
                    else // list of files
                    {
                        foreach(var n in d.Value)
                        {
                            result.nodes.Add(n.node);
                        }
                    }
                }

                // ommit empty dir nodes
                if (result.nodes.Count == 0 && result.childDirs.Count == 1)
                    result = result.childDirs[0];

                return result;
            }

            private static void FillLevels(DirNode node, int level = 1)
            {
                double wC = 0;
                foreach (var n in node.childDirs)
                {
                    FillLevels(n, level + 1);
                    wC += n.width;
                }

                double w = (double)node.nodes.Count / (double)level;
                node.width = Math.Max(wC, w);
            }

            public List<Node> nodes = new List<Node>();
            public List<DirNode> childDirs = new List<DirNode>();

            public double width = 0;
        }
    }
}

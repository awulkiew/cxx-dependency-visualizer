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

            double cellSize = CalculateNodeSizes(dict);

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

                    lvl[j].center.X = (-shift / 2 + x) * cellSize;
                    lvl[j].center.Y = y * cellSize;

                    ++x;
                }
                ++y;
            }

            return NormalizeNodePositions(dict, new Size(cellSize, cellSize));
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

            return NormalizeNodePositions(dict, gd.CellSize);
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

            return NormalizeNodePositions(dict, new Size(maxSize, maxSize));
        }

        private static GraphData SquareLayout(Dictionary<string, Node> dict)
        {
            int rowCount = (int)(Math.Sqrt(dict.Count) + 0.5);

            double cellSize = CalculateNodeSizes(dict);

            int i = 0;
            int j = 0;
            foreach (var d in dict)
            {
                double x = i * cellSize;
                double y = j * cellSize;
                d.Value.center.X = x;
                d.Value.center.Y = y;

                ++i;
                if (i >= rowCount)
                {
                    ++j;
                    i = 0;
                }
            }

            return NormalizeNodePositions(dict, new Size(cellSize, cellSize));
        }

        private static double CalculateNodeSizes(Dictionary<string, Node> dict)
        {
            double maxSize = 0;
            foreach (var d in dict)
            {
                string file = Util.FileFromPath(d.Key);
                d.Value.size = Util.MeasureTextBlock(file);
                d.Value.size.Width += 10;
                d.Value.size.Height += 10;
                maxSize = Math.Max(maxSize, Math.Max(d.Value.size.Width, d.Value.size.Height));
            }
            return maxSize;
        }

        private static GraphData NormalizeNodePositions(Dictionary<string, Node> dict, Size cellSize)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (var d in dict)
            {
                minX = Math.Min(minX, d.Value.center.X);
                minY = Math.Min(minY, d.Value.center.Y);
                maxX = Math.Max(maxX, d.Value.center.X);
                maxY = Math.Max(maxY, d.Value.center.Y);
            }

            foreach (var d in dict)
            {
                d.Value.center.X += -minX + 0.5 * cellSize.Width;
                d.Value.center.Y += -minY + 0.5 * cellSize.Height;
            }

            GraphData result;
            result.GraphSize = new Size(Math.Max(maxX - minX + cellSize.Width, 0),
                                        Math.Max(maxY - minY + cellSize.Height, 0));
            result.CellSize = cellSize;
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

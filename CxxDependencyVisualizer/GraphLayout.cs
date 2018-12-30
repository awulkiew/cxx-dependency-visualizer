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
            GraphData gd = LevelBasedLayout(dict, UseLevel.Min);

            double k = gd.CellSize.Width;
            double c = 1;
            //double tol = 0.0001;
            double maxIterations = 100;
            double t = 0.9;
            double initialStep = gd.CellSize.Width / 10;

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

                    Point faSum = AttractiveForcesSum(node, k, dict);
                    Point frSum = RepulsiveForcesSum(node, k, c, dict);
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
                /*
                double distSum = 0;
                for(int i = 0; i < x.Count; ++i)
                {
                    distSum += Util.Distance(x[i].point, x0[i].point);
                }*/

                //if (distSum < k * tol)
                //    break;
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

        private static Point AttractiveForcesSum(Node node, double k, Dictionary<string, Node> dict)
        {
            Point result = new Point(0, 0);
            foreach (var cId in node.children)
            {
                var c = dict[cId];
                Point v = Util.Sub(c.center, node.center);
                double len = Util.Len(v);
                Point fi = Util.Mul(len / k, v);
                result = Util.Add(result, fi);
            }
            foreach (var pId in node.parents)
            {
                var p = dict[pId];
                Point v = Util.Sub(p.center, node.center);
                double len = Util.Len(v);
                Point fi = Util.Mul(len / k, v);
                result = Util.Add(result, fi);
            }
            return result;
        }

        private static Point RepulsiveForcesSum(Node node, double k, double c, Dictionary<string, Node> dict)
        {
            double numerator = -c * k * k;
            Point result = new Point(0, 0);
            foreach (var d in dict)
            {
                if (node != d.Value)
                {
                    Point v = Util.Sub(d.Value.center, node.center);
                    double lenSqr = Util.LenSqr(v);
                    Point fi = Util.Mul(numerator / lenSqr, v);
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
    }
}

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

        public static List<List<string>> GenerateLevels(Dictionary<string, Node> dict, bool useMinLevel)
        {
            // Generate containers of levels of inclusion (min or max level found).
            List<List<string>> result = new List<List<string>>();
            foreach (var include in dict)
            {
                int level = useMinLevel
                          ? include.Value.minLevel
                          : include.Value.maxLevel;
                for (int i = result.Count; i < level + 1; ++i)
                    result.Add(new List<string>());
                result[level].Add(include.Key);
            }
            return result;
        }

        private struct PointI// : IEquatable<PointI>
        {
            public PointI(int x, int y)
            {
                this.x = x;
                this.y = y;
            }

            /*public override int GetHashCode()
            {
                return Tuple.Create(x, y).GetHashCode();
            }

            public bool Equals(PointI other)
            {
                return x == other.x && y == other.y;
            }*/

            public int x;
            public int y;
        }

        public static Size CreateLayout(Dictionary<string, Node> dict, UseLevel useLevel)
        {
            // Generate containers of levels of inclusion (min or max level found).
            bool useMin = (useLevel == UseLevel.Min);
            List<List<string>> levels = GenerateLevels(dict, useMin);

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
                List<string> lvl = levels[i];
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

                    var d = dict[lvl[j]];
                    gridPositions.Add(d, position);
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
                minX = Math.Min(minX, position.x);
                minY = Math.Min(minY, position.y);
                maxX = Math.Max(maxX, position.x);
                maxY = Math.Max(maxY, position.y);

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
            foreach (var d in dict)
            {
                PointI position = gridPositions[d.Value];
                d.Value.center.X = xOrig + position.x * cellSize;
                d.Value.center.Y = yOrig + position.y * cellSize;
            }

            return new Size(graphWidth, graphHeight);
        }
    }
}

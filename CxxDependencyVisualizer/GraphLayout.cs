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

        public static Size LevelBasedLayout(Dictionary<string, Node> dict, UseLevel useLevel)
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
                n.Key.center.X = xOrig + position.X * cellSize;
                n.Key.center.Y = yOrig + position.Y * cellSize;
            }

            return new Size(graphWidth, graphHeight);
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

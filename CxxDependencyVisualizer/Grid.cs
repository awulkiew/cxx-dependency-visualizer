using System.Collections.Generic;

namespace CxxDependencyVisualizer
{
    class StringGrid
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
}

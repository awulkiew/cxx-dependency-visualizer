using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CxxDependencyVisualizer
{
    class Util
    {
        public static bool Empty(string s)
        {
            return s == null || s.Length == 0;
        }

        public static string PathFromDirFile(string dir, string file)
        {
            string d = dir.Replace('/', '\\');
            string f = file.Replace('/', '\\');
            if (!d.EndsWith("\\"))
                d += '\\';
            return d + f;
        }

        public static string DirFromPath(string path)
        {
            int id = path.LastIndexOf('\\');
            return id >= 0
                 ? path.Substring(0, id)
                 : path.Clone() as string;
        }

        public static string FileFromPath(string path)
        {
            int id = path.LastIndexOf('\\');
            return id >= 0
                 ? path.Substring(id + 1)
                 : path.Clone() as string;
        }

        public static List<string> GetIncludePaths(string dir, string path)
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
                        int idHash = line.IndexOf("#");
                        int idInclude = line.IndexOf("include");
                        if (idHash >= 0 && idInclude >= 0 && idHash < idInclude)
                        {
                            int idBegin = -1;
                            char closingChar = '\0';
                            if ((idBegin = line.IndexOf('<')) >= 0)
                                closingChar = '>';
                            else if ((idBegin = line.IndexOf('"')) >= 0)
                                closingChar = '"';
                            int idEnd = idBegin >= 0 ? line.IndexOf(closingChar, idBegin + 1) : -1;
                            if (idEnd >= 0 && idBegin <= idEnd)
                            {
                                string include = line.Substring(idBegin + 1, idEnd - idBegin - 1);
                                string p = PathFromDirFile(closingChar == '>'
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

        public static Size MeasureTextBlock(string text)
        {
            TextBlock textBlock = new TextBlock();
            var formattedText = new FormattedText(text,
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

        public class ListCompare<T> : IEqualityComparer<List<T>>
            where T : IComparable<T>
        {
            public bool Equals(List<T> x, List<T> y)
            {
                if (x.Count != y.Count)
                    return false;
                for (int i = 0; i < x.Count; ++i)
                    if (x[i].CompareTo(y[i]) != 0)
                        return false;
                return true;
            }

            public int GetHashCode(List<T> obj)
            {
                return obj.GetHashCode();
            }
        }

        public static int IndexOfSmallest<T>(List<T> list)
            where T : IComparable<T>
        {
            int result = -1;
            if (list.Count > 0)
            {
                result = 0;
                T smallest = list[0];
                for (int i = 1; i < list.Count; ++i)
                {
                    if (list[i].CompareTo(smallest) < 0)
                    {
                        result = i;
                        smallest = list[i];
                    }
                }
            }
            return result;
        }

        public static void Rotate<T>(List<T> list, int firstId)
        {
            if (0 < firstId && firstId < list.Count)
            {
                List<T> l1 = new List<T>();
                List<T> l2 = new List<T>();
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

        public static void Resize<T>(List<T> list, int size)
            where T : new()
        {
            for (int i = list.Count; i < size; ++i)
                list.Add(new T());
        }

        public static Point Mul(Point p, double v)
        {
            return new Point(p.X * v, p.Y * v);
        }

        public static Point Mul(double v, Point p)
        {
            return Mul(p, v);
        }

        public static Point Div(Point p, double v)
        {
            return new Point(p.X / v, p.Y / v);
        }

        public static Point Add(Point p, Point q)
        {
            return new Point(p.X + q.X, p.Y + q.Y);
        }

        public static Point Sub(Point p, Point q)
        {
            return new Point(p.X - q.X, p.Y - q.Y);
        }

        public static double Dot(Point p, Point q)
        {
            return p.X * q.X + p.Y * q.Y;
        }

        public static double LenSqr(Point p)
        {
            return Dot(p, p);
        }

        public static double Len(Point p)
        {
            return Math.Sqrt(Dot(p, p));
        }

        public static double Distance(Point p, Point q)
        {
            return Len(Sub(p, q));
        }
    }
}

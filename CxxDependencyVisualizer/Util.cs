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

        public static List<string> GetIncludePaths(string dir, string path, bool ignoreComments)
        {
            List<string> result = new List<string>();

            if (File.Exists(path))
            {
                bool startsInComment = false;
                StreamReader sr = new StreamReader(path);
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (!Empty(line))
                    {
                        if (ignoreComments)
                        {
                            startsInComment = RemoveCommentsFromLine(ref line, startsInComment);
                        }

                        string p = GetIncludePath(dir, path, line);

                        if (!Empty(p))
                            result.Add(p);
                    }
                }
            }

            return result;
        }

        public static void TestRemoveCommentsFromLine()
        {
            TestRemoveCommentsFromLineOne("// comment", false, "", false);
            TestRemoveCommentsFromLineOne("abc // comment", false, "abc ", false);
            TestRemoveCommentsFromLineOne("/* comment */", false, "", false);
            TestRemoveCommentsFromLineOne("abc /* comment */ def", false, "abc  def", false);
            TestRemoveCommentsFromLineOne("abc // /* comment */ def", false, "abc ", false);
            TestRemoveCommentsFromLineOne("abc // /* comment", false, "abc ", false);
            TestRemoveCommentsFromLineOne("abc /* // comment", false, "abc ", true);
            TestRemoveCommentsFromLineOne("abc /* // comment */ def // comment", false, "abc  def ", false);
            TestRemoveCommentsFromLineOne("abc // comment", true, "", true);
            TestRemoveCommentsFromLineOne("comment */ abc // comment", true, " abc ", false);
        }

        public static void TestRemoveCommentsFromLineOne(string line, bool startsInComment, string expected, bool expectedResult)
        {
            bool r = RemoveCommentsFromLine(ref line, startsInComment);
            System.Diagnostics.Debug.Assert(line == expected);
            System.Diagnostics.Debug.Assert(r == expectedResult);
        }

        public static bool RemoveCommentsFromLine(ref string line, bool startsInComment)
        {
            if (startsInComment)
            {
                int idCommentEnd = line.IndexOf("*/");
                if (idCommentEnd >= 0)
                {
                    //string comment = line.Substring(0, idCommentEnd + 2);
                    line = line.Substring(idCommentEnd + 2);
                }
                else
                {
                    //string comment = line;
                    line = "";
                    return true;
                }
            }

            for (; ; )
            {
                int idLineComment = line.IndexOf("//");
                int idCommentBegin = line.IndexOf("/*");
                
                if (idLineComment >= 0 && (idCommentBegin < 0 || idLineComment < idCommentBegin))
                {
                    //string comment = line.Substring(idLineComment);
                    line = line.Substring(0, idLineComment);
                    return false;
                }

                if (idCommentBegin >= 0)
                {
                    int idCommentEnd = line.IndexOf("*/", idCommentBegin + 2);

                    //string comment = idCommentEnd >= 0
                    //               ? line.Substring(idCommentBegin, idCommentEnd - idCommentBegin)
                    //               : line.Substring(idCommentBegin);

                    // NOTE: With this approach the whole line is searched again
                    //       after removal of this comment block
                    string pre = line.Substring(0, idCommentBegin);
                    string post = idCommentEnd >= 0
                                ? line.Substring(idCommentEnd + 2)
                                : "";
                    line = pre + post;

                    if (idCommentEnd < 0)
                        return true;
                }
                else
                    return false;
            }
        }

        public static string GetIncludePath(string dir, string path, string line)
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
                    include = include.Trim();
                    string p = PathFromDirFile(closingChar == '>'
                                                 ? dir
                                                 : DirFromPath(path),
                                               include);
                    return p;
                }
            }

            return "";
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

        public static Point Rot90(Point v)
        {
            return new Point(-v.Y, v.X);
        }

        public static Point CalculateBezierPoint(Point p1, Point p2, double crossTrackDist)
        {
            Point c = new Point(0.5 * (p1.X + p2.X),
                                0.5 * (p1.Y + p2.Y));
            if (crossTrackDist == 0)
                return c;
            double l = Len(c);
            if (l == 0)
                return c;
            Point v = Rot90(Mul(Sub(c, p1), crossTrackDist / l));
            return Add(c, v);
        }
    }
}

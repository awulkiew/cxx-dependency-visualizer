using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CxxDependencyVisualizer
{
    class Node
    {
        public Node(string parentId, int level)
        {
            if (!Util.Empty(parentId))
                this.parents.Add(parentId);

            this.minLevel = level;
            this.maxLevel = level;
        }

        public void Update(string parentPath, int level)
        {
            if (!Util.Empty(parentPath))
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

        public Point center;
        public Size size;

        public TextBlock textBlock = null;
    }

    class LibData
    {
        public LibData()
        { }

        public LibData(string dir, string file, bool fromLibOnly)
        {
            string rootPath = Util.PathFromDirFile(dir, file);
            Analyze(dir, rootPath, null, fromLibOnly, 0);
        }

        private void Analyze(string dir, string path, string parentPath, bool fromLibOnly, int level)
        {
            if (dict.ContainsKey(path))
            {
                dict[path].Update(parentPath, level);
            }
            else
            {
                List<string> children = Util.GetIncludePaths(dir, path);
                Node data = new Node(parentPath, level);
                data.important = File.Exists(path);
                foreach (string c in children)
                    if (!fromLibOnly || File.Exists(c))
                        if (data.children.Contains(c))
                            data.duplicatedChildren = true;
                        else
                            data.children.Add(c);

                dict.Add(path, data);

                foreach (string p in data.children)
                {
                    Analyze(dir, p, path, fromLibOnly, level + 1);
                }
            }
        }

        public Dictionary<string, Node> dict = new Dictionary<string, Node>();
    }

    class Graph
    {
        public static List<string> FindPath(string start, string end,
                                             Dictionary<string, Node> dict)
        {
            List<string> result = new List<string>();
            Dictionary<string, int> map = new Dictionary<string, int>();
            map.Add(start, 0);
            FindPath(start, start, end, dict, map, result);
            return result;
        }

        public static bool FindPath(string start, string curr, string end,
                                     Dictionary<string, Node> dict,
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
                if (UpdateMap(map, n, l + 1))
                    updated.Add(n);
            foreach (var n in updated)
                if (FindPath(start, n, end, dict, map, result))
                    return true;
            return false;
        }

        public static bool TrackPathBack(string curr, string start,
                                          Dictionary<string, Node> dict,
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

        public static List<string> FindCycle(string header,
                                             Dictionary<string, Node> dict)
        {
            List<string> result = new List<string>();
            Dictionary<string, int> map = new Dictionary<string, int>();
            var d = dict[header];
            foreach (var cStr in d.children)
                map.Add(cStr, 0);
            foreach (var cStr in d.children)
                if (FindPath(cStr, cStr, header, dict, map, result))
                    return result;
            return result;
        }

        public static bool UpdateMap(Dictionary<string, int> map, string n, int l)
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
    }
}

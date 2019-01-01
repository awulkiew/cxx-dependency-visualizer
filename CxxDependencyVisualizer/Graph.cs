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
            AddParent(parentId, level);
        }

        public void AddParent(string parentId, int level)
        {
            if (!Util.Empty(parentId))
                this.parents.Add(parentId);
            this.minLevel = Math.Min(this.minLevel, level);
            this.maxLevel = Math.Max(this.maxLevel, level);
        }

        public void AddChild(string childId)
        {
            if (children.Contains(childId))
                duplicatedChildren = true;
            else
                children.Add(childId);
        }

        // graph
        public List<string> children = new List<string>();
        public List<string> parents = new List<string>();
        public int minLevel = int.MaxValue;
        public int maxLevel = int.MinValue;
        public bool duplicatedChildren = false;

        public List<Node> childNodes = new List<Node>();
        public List<Node> parentNodes = new List<Node>();

        // geometry
        public Size size; // in
        public Point center; // out

        // ui
        public TextBlock textBlock = null;
    }

    class LibData
    {
        public LibData()
        { }

        public LibData(string dir, string file, bool fromLibOnly)
        {
            rootPath = Util.PathFromDirFile(dir, file);
            Analyze(dir, rootPath, null, fromLibOnly, 0);

            foreach (var d in dict)
            {
                foreach (var c in d.Value.children)
                    d.Value.childNodes.Add(dict[c]);
                foreach (var p in d.Value.parents)
                    d.Value.parentNodes.Add(dict[p]);
            }
        }

        private void Analyze(string dir, string path, string parentPath, bool fromLibOnly, int level)
        {
            if (dict.ContainsKey(path))
            {
                dict[path].AddParent(parentPath, level);
            }
            else
            {
                if (fromLibOnly && !File.Exists(path))
                    return;

                List<string> children = Util.GetIncludePaths(dir, path);
                Node data = new Node(parentPath, level);
                foreach (string c in children)
                {
                    if (fromLibOnly && !File.Exists(c))
                        continue;

                    data.AddChild(c);
                }

                dict.Add(path, data);

                foreach (string p in data.children)
                {
                    Analyze(dir, p, path, fromLibOnly, level + 1);
                }
            }
        }

        public string rootPath = "";
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

        private static bool FindPath(string start, string curr, string end,
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

        private static bool TrackPathBack(string curr, string start,
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

        private static bool UpdateMap(Dictionary<string, int> map, string n, int l)
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

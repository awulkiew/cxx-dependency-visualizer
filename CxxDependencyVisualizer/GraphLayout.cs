using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxxDependencyVisualizer
{
    class GraphLayout
    {
        public enum Algorithm { LevelMin, LevelMax };

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
        
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxxDependencyVisualizer
{
    class PointI : IEquatable<PointI>
    {
        public PointI(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override int GetHashCode()
        {
            return Tuple.Create(x, y).GetHashCode();
        }

        public bool Equals(PointI other)
        {
            return x == other.x && y == other.y;
        }

        public int x;
        public int y;
    }

    class PositionFeed : RelativePositionFeed
    {
        public PositionFeed(PointI parentPos)
        {
            this.x = parentPos.x;
            this.y = parentPos.y;
        }

        public new PointI Next()
        {
            PointI res = base.Next();
            res.x += x;
            res.y += y;
            return res;
        }

        int x, y;
    }

    class RelativePositionFeed
    {
        public PointI Next()
        {
            if (sign == 0)
            {
                x = 0;
                y = 0 + dist;
                sign = -1;
            }
            else
            {
                // move
                if (sign < 0)
                {
                    if (x < dist || y > 0)
                    {
                        if (x < dist)
                            ++x;
                        else if (y > 0)
                            --y;
                    }
                    else
                    {
                        ++dist;
                        x = 0;
                        y = dist;
                        sign = 1;
                    }
                }
                sign *= -1;
            }

            return new PointI(x * sign, y);
        }

        int sign = 0;
        int dist = 1;
        int x = 0;
        int y = 0;
    }
}

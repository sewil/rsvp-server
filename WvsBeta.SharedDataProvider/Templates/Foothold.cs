using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using WvsBeta.Common;

namespace WvsBeta.SharedDataProvider.Templates
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Foothold
    {
        public short ID;
        public short PreviousIdentifier;
        public short NextIdentifier;
        public short X1;
        public short Y1;
        public short X2;
        public short Y2;

        public bool IsValid => X1 != X2 || Y1 != Y2;

        public double Angle => Math.Atan2(Y2 - Y1, X2 - X1);
        public Rectangle Rect => Rectangle.FromLTRB(X1, Y1, X2, Y2);

        public Pos Intersection(short x1, short y1, short x2, short y2)
        {
            return Intersection(new Line
                {
                    x1 = x1, y1 = y1,
                    x2 = x2, y2 = y2
                }
            );
        }

        public Pos Intersection(Line line)
        {
            var intersection = FindIntersection(new Line
                {
                    x1 = X1, y1 = Y1,
                    x2 = X2, y2 = Y2
                },
                line
            );

            if (intersection.HasValue)
            {
                return new Pos(
                    (short)intersection.Value.x,
                    (short)intersection.Value.y
                );
            }

            return null;
        }


        // Fuck me
        // https://stackoverflow.com/a/46045077/678950

        public struct Line
        {
            public double x1 { get; set; }
            public double y1 { get; set; }

            public double x2 { get; set; }
            public double y2 { get; set; }
        }

        private struct Point
        {
            public double x { get; set; }
            public double y { get; set; }
        }

        //  Returns Point of intersection if do intersect otherwise default Point (null)
        private static Point? FindIntersection(Line lineA, Line lineB, double tolerance = 0.001)
        {
            double x1 = lineA.x1, y1 = lineA.y1;
            double x2 = lineA.x2, y2 = lineA.y2;

            double x3 = lineB.x1, y3 = lineB.y1;
            double x4 = lineB.x2, y4 = lineB.y2;

            // equations of the form x = c (two vertical lines)
            if (Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance && Math.Abs(x1 - x3) < tolerance)
            {
                Trace.WriteLine($"Both lines overlap vertically, ambiguous intersection points. {x1} {x2} {x3} {x4} {y1} {y2} {y3} {y4}");
                return null;
            }

            //equations of the form y=c (two horizontal lines)
            if (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance && Math.Abs(y1 - y3) < tolerance)
            {
                Trace.WriteLine("Both lines overlap horizontally, ambiguous intersection points.");
                return null;
            }

            //equations of the form x=c (two vertical parallel lines)
            if (Math.Abs(x1 - x2) < tolerance && Math.Abs(x3 - x4) < tolerance)
            {
                //return default (no intersection)
                return null;
            }

            //equations of the form y=c (two horizontal parallel lines)
            if (Math.Abs(y1 - y2) < tolerance && Math.Abs(y3 - y4) < tolerance)
            {
                //return default (no intersection)
                return null;
            }

            //general equation of line is y = mx + c where m is the slope
            //assume equation of line 1 as y1 = m1x1 + c1 
            //=> -m1x1 + y1 = c1 ----(1)
            //assume equation of line 2 as y2 = m2x2 + c2
            //=> -m2x2 + y2 = c2 -----(2)
            //if line 1 and 2 intersect then x1=x2=x & y1=y2=y where (x,y) is the intersection point
            //so we will get below two equations 
            //-m1x + y = c1 --------(3)
            //-m2x + y = c2 --------(4)

            double x, y;

            //lineA is vertical x1 = x2
            //slope will be infinity
            //so lets derive another solution
            if (Math.Abs(x1 - x2) < tolerance)
            {
                //compute slope of line 2 (m2) and c2
                double m2 = (y4 - y3) / (x4 - x3);
                double c2 = -m2 * x3 + y3;

                //equation of vertical line is x = c
                //if line 1 and 2 intersect then x1=c1=x
                //subsitute x=x1 in (4) => -m2x1 + y = c2
                // => y = c2 + m2x1 
                x = x1;
                y = c2 + m2 * x1;
            }
            //lineB is vertical x3 = x4
            //slope will be infinity
            //so lets derive another solution
            else if (Math.Abs(x3 - x4) < tolerance)
            {
                //compute slope of line 1 (m1) and c2
                double m1 = (y2 - y1) / (x2 - x1);
                double c1 = -m1 * x1 + y1;

                //equation of vertical line is x = c
                //if line 1 and 2 intersect then x3=c3=x
                //subsitute x=x3 in (3) => -m1x3 + y = c1
                // => y = c1 + m1x3 
                x = x3;
                y = c1 + m1 * x3;
            }
            //lineA & lineB are not vertical 
            //(could be horizontal we can handle it with slope = 0)
            else
            {
                //compute slope of line 1 (m1) and c2
                double m1 = (y2 - y1) / (x2 - x1);
                double c1 = -m1 * x1 + y1;

                //compute slope of line 2 (m2) and c2
                double m2 = (y4 - y3) / (x4 - x3);
                double c2 = -m2 * x3 + y3;

                //solving equations (3) & (4) => x = (c1-c2)/(m2-m1)
                //plugging x value in equation (4) => y = c2 + m2 * x
                x = (c1 - c2) / (m2 - m1);
                y = c2 + m2 * x;

                //verify by plugging intersection point (x, y)
                //in original equations (1) & (2) to see if they intersect
                //otherwise x,y values will not be finite and will fail this check
                if (!(Math.Abs(-m1 * x + y - c1) < tolerance
                    && Math.Abs(-m2 * x + y - c2) < tolerance))
                {
                    //return default (no intersection)
                    return null;
                }
            }

            //x,y can intersect outside the line segment since line is infinitely long
            //so finally check if x, y is within both the line segments
            if (IsInsideLine(lineA, x, y) &&
                IsInsideLine(lineB, x, y))
            {
                return new Point { x = x, y = y };
            }

            //return default (no intersection)
            return null;

        }

        // Returns true if given point(x,y) is inside the given line segment
        private static bool IsInsideLine(Line line, double x, double y)
        {
            x = Math.Round(x);
            y = Math.Round(y);

            return ((x >= line.x1 && x <= line.x2) || (x >= line.x2 && x <= line.x1))
                   && 
                   ((y >= line.y1 && y <= line.y2) || (y >= line.y2 && y <= line.y1));
        }
    }
}
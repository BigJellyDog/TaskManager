namespace TaskManager.App.Controls;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Rewrites SVG path data into a form the MAUI path parser always reads correctly:
/// absolute coordinates and only the commands M, L and C, separated by spaces.
/// The parser of MAUI misreads compact data (arc flags written without spaces,
/// repeated commands, relative moves), so imported icons go through this first.
/// </summary>
public static partial class SvgPathNormalizer
{
    [GeneratedRegex(@"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex TokenPattern();

    /// <summary>Returns the normalized data, or null when the path cannot be read.</summary>
    public static string? Normalize(string data)
    {
        var tokens = TokenPattern().Matches(data).Select(x => x.Value).ToArray();
        var output = new StringBuilder();
        var index = 0;

        // Current point, start of the current figure, and the last control point (for S and T).
        double x = 0, y = 0, startX = 0, startY = 0, controlX = 0, controlY = 0;
        var command = ' ';
        var previous = ' ';

        bool HasNumber() => index < tokens.Length && !char.IsLetter(tokens[index][0]);
        double Next() => double.Parse(tokens[index++], CultureInfo.InvariantCulture);

        // Arc flags may be glued to the next number ("a1 1 0 011 1"), so they are read digit by digit.
        double Flag()
        {
            var token = tokens[index];

            if (token.Length > 1 && (token[0] is '0' or '1') && char.IsDigit(token[1]))
            {
                tokens[index] = token[1..];
                return token[0] - '0';
            }

            index++;
            return double.Parse(token, CultureInfo.InvariantCulture);
        }

        void MoveTo(double nx, double ny)
        {
            x = startX = nx;
            y = startY = ny;
            output.Append(CultureInfo.InvariantCulture, $"M{F(x)} {F(y)}");
        }

        void LineTo(double nx, double ny)
        {
            x = nx;
            y = ny;
            output.Append(CultureInfo.InvariantCulture, $"L{F(x)} {F(y)}");
        }

        void CurveTo(double x1, double y1, double x2, double y2, double nx, double ny)
        {
            output.Append(CultureInfo.InvariantCulture, $"C{F(x1)} {F(y1)} {F(x2)} {F(y2)} {F(nx)} {F(ny)}");
            controlX = x2;
            controlY = y2;
            x = nx;
            y = ny;
        }

        try
        {
            while (index < tokens.Length)
            {
                if (char.IsLetter(tokens[index][0]))
                {
                    command = tokens[index++][0];
                }
                else if (command is ' ' or 'Z' or 'z')
                {
                    return null;
                }

                var relative = char.IsLower(command);
                var ox = relative ? x : 0;
                var oy = relative ? y : 0;
                var kind = char.ToUpperInvariant(command);

                if (kind == 'Z')
                {
                    if (Math.Abs(x - startX) > 1e-9 || Math.Abs(y - startY) > 1e-9) LineTo(startX, startY);
                    previous = 'Z';
                    continue;
                }

                if (!HasNumber()) return null;

                switch (kind)
                {
                    case 'M':
                        MoveTo(ox + Next(), oy + Next());
                        // More pairs after a move are lines.
                        command = relative ? 'l' : 'L';
                        break;

                    case 'L':
                        LineTo(ox + Next(), oy + Next());
                        break;

                    case 'H':
                        LineTo(ox + Next(), y);
                        break;

                    case 'V':
                        LineTo(x, oy + Next());
                        break;

                    case 'C':
                        CurveTo(ox + Next(), oy + Next(), ox + Next(), oy + Next(), ox + Next(), oy + Next());
                        break;

                    case 'S':
                    {
                        // The first control point mirrors the last one of a previous curve.
                        var reflect = previous is 'C' or 'S';
                        var x1 = reflect ? 2 * x - controlX : x;
                        var y1 = reflect ? 2 * y - controlY : y;
                        CurveTo(x1, y1, ox + Next(), oy + Next(), ox + Next(), oy + Next());
                        break;
                    }

                    case 'Q':
                    {
                        double qx = ox + Next(), qy = oy + Next(), ex = ox + Next(), ey = oy + Next();
                        Quadratic(qx, qy, ex, ey);
                        break;
                    }

                    case 'T':
                    {
                        var reflect = previous is 'Q' or 'T';
                        var qx = reflect ? 2 * x - controlX : x;
                        var qy = reflect ? 2 * y - controlY : y;
                        Quadratic(qx, qy, ox + Next(), oy + Next());
                        break;
                    }

                    case 'A':
                    {
                        double rx = Next(), ry = Next(), rotation = Next();
                        var large = Flag() != 0;
                        var sweep = Flag() != 0;
                        Arc(rx, ry, rotation, large, sweep, ox + Next(), oy + Next());
                        break;
                    }

                    default:
                        return null;
                }

                previous = kind;
            }
        }
        catch (Exception exception) when (exception is FormatException or IndexOutOfRangeException)
        {
            return null;
        }

        return output.Length == 0 ? null : output.ToString();

        // A quadratic curve is the same curve as a cubic one with the control points moved two thirds.
        void Quadratic(double qx, double qy, double ex, double ey)
        {
            CurveTo(
                x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y),
                ex + 2.0 / 3 * (qx - ex), ey + 2.0 / 3 * (qy - ey),
                ex, ey);

            // Remember the quadratic control point, which T mirrors (not the cubic one set by CurveTo).
            controlX = qx;
            controlY = qy;
        }

        // Converts the endpoint form of an arc to the center form (SVG spec F.6.5),
        // then draws it as cubic curves of at most a quarter turn each.
        void Arc(double rx, double ry, double rotationDegrees, bool large, bool sweep, double ex, double ey)
        {
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);

            if (rx < 1e-9 || ry < 1e-9)
            {
                LineTo(ex, ey);
                return;
            }

            if (Math.Abs(ex - x) < 1e-9 && Math.Abs(ey - y) < 1e-9) return;

            var phi = rotationDegrees * Math.PI / 180;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (x - ex) / 2, dy = (y - ey) / 2;
            double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;

            // Radii too small to reach the endpoint are scaled up.
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);

            if (lambda > 1)
            {
                var scale = Math.Sqrt(lambda);
                rx *= scale;
                ry *= scale;
            }

            var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (large == sweep ? -1 : 1);
            double cx1 = factor * rx * y1 / ry, cy1 = -factor * ry * x1 / rx;
            double cx = cos * cx1 - sin * cy1 + (x + ex) / 2, cy = sin * cx1 + cos * cy1 + (y + ey) / 2;

            var start = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
            var sweepAngle = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx) - start;

            if (sweep && sweepAngle < 0) sweepAngle += 2 * Math.PI;
            else if (!sweep && sweepAngle > 0) sweepAngle -= 2 * Math.PI;

            var pieces = (int)Math.Ceiling(Math.Abs(sweepAngle) / (Math.PI / 2) - 1e-9);
            var step = sweepAngle / pieces;
            var handle = 4.0 / 3 * Math.Tan(step / 4);

            for (var i = 0; i < pieces; i++)
            {
                double a1 = start + i * step, a2 = a1 + step;
                double c1 = Math.Cos(a1), s1 = Math.Sin(a1), c2 = Math.Cos(a2), s2 = Math.Sin(a2);

                // Points of the unit circle, stretched, rotated and moved to the ellipse.
                (double X, double Y) Map(double ux, double uy) =>
                    (cx + cos * rx * ux - sin * ry * uy, cy + sin * rx * ux + cos * ry * uy);

                var p1 = Map(c1 - handle * s1, s1 + handle * c1);
                var p2 = Map(c2 + handle * s2, s2 - handle * c2);
                var end = i == pieces - 1 ? (ex, ey) : Map(c2, s2);

                CurveTo(p1.X, p1.Y, p2.X, p2.Y, end.Item1, end.Item2);
            }
        }
    }

    private static string F(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture) is var text && text is "-0" ? "0" : text;
}

using System;

namespace ScreenSafe.Domain
{
    /// <summary>
    /// Represents a screen rectangle with left, top, right, bottom coordinates.
    /// Immutable value type with structural equality.
    /// </summary>
    public readonly struct ScreenRect : IEquatable<ScreenRect>
    {
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }

        public int Width => Right - Left;
        public int Height => Bottom - Top;

        public ScreenRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool Equals(ScreenRect other)
        {
            return Left == other.Left &&
                   Top == other.Top &&
                   Right == other.Right &&
                   Bottom == other.Bottom;
        }

        public override bool Equals(object? obj)
        {
            return obj is ScreenRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Left;
                hash = hash * 23 + Top;
                hash = hash * 23 + Right;
                hash = hash * 23 + Bottom;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"ScreenRect({Left}, {Top}, {Right}, {Bottom}) — {Width}×{Height}";
        }

        public static bool operator ==(ScreenRect left, ScreenRect right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScreenRect left, ScreenRect right)
        {
            return !left.Equals(right);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using static TRGoChess.ChessTable;

namespace TRGoChess
{
    internal class ChessTable : IGrid
    {
        public class ChessBlock : IComparable
        {
            public const int NONEID = -1;
            public const int DEFID = 0;
            public int Id;
            public ChessBlock[] Surround;
            public ChessBlocksLink.LinkInf LinkInf;
            public readonly Point Loc;
            public ChessBlock(Point loc, int id = DEFID)
            {
                Id = id;
                Loc = loc;
                LinkInf = new ChessBlocksLink.LinkInf();
            }
            public int CompareTo(object obj)
            {
                ChessBlock other = obj as ChessBlock;
                if (Loc.X == other.Loc.X) return Loc.Y - other.Loc.Y;
                return Loc.X - other.Loc.X;
            }
            public override string ToString()
            {
                return Loc.ToString() + ": " + Id.ToString();
            }
        }
        public class ChessBlocksLink : IEnumerable<ChessBlock>, IEnumerable
        {
            private readonly ChessBlock last;
            private int count;

            public int Count { get => count; }

            public IEnumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            IEnumerator<ChessBlock> IEnumerable<ChessBlock>.GetEnumerator()
            {
                return new Enumerator(this);
            }
            public struct Enumerator : IEnumerator<ChessBlock>, IEnumerator
            {
                private ChessBlocksLink ChessBlocksLink;
                public Enumerator(ChessBlocksLink chessBlocksLink)
                {
                    ChessBlocksLink = chessBlocksLink;
                    current = null;
                }
                public object Current { get => current; }
                private ChessBlock current;

                ChessBlock IEnumerator<ChessBlock>.Current => current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (current == null) current = ChessBlocksLink.last.LinkInf.LinkLeft;
                    else
                        current = current.LinkInf.LinkLeft;
                    return current != null;
                }

                public void Reset()
                {
                    current = null;
                }
            }
            public class LinkInf
            {
                public ChessBlock LinkLeft, LinkRight;
            }
            public ChessBlocksLink()
            {
                last = new ChessBlock(new Point(-1, -1), ChessBlock.NONEID);
                count = 0;
            }
            public void AddLast(ChessBlock block)
            {
                if (count > 0)
                    last.LinkInf.LinkLeft.LinkInf.LinkRight = block;
                block.LinkInf.LinkLeft = last.LinkInf.LinkLeft;
                block.LinkInf.LinkRight = last;
                last.LinkInf.LinkLeft = block;
                count++;
            }
            public void Remove(ChessBlock a)
            {
                if (a.LinkInf.LinkLeft != null)
                    a.LinkInf.LinkLeft.LinkInf.LinkRight = a.LinkInf.LinkRight;
                a.LinkInf.LinkRight.LinkInf.LinkLeft = a.LinkInf.LinkLeft;
                a.LinkInf.LinkLeft = null;
                a.LinkInf.LinkRight = null;
                count--;
            }
            public static void Swap(ChessBlock a, ChessBlock b)
            {
                LinkInf t = a.LinkInf;
                a.LinkInf = b.LinkInf;
                b.LinkInf = t;
            }
        }
        public readonly ChessBlock[,] ChessT;
        public readonly Dictionary<int, ChessBlocksLink> id2chess;
        public CAction[] actionLast;
        public readonly Point[] directions;
        public bool OptifinedAround;
        private HashSet<ChessBlock> arroundLastRet;
        private LinkedList<CAction> arroundTemp;
        public readonly ChessBlock NoneBlock;
        public int Width => ChessT.GetLength(0);
        public int Height => ChessT.GetLength(1);
        public ChessTable(int width, int height, Point[] initDirections, int[] initIds, bool optifinedAround = false)
        {
            ChessT = new ChessBlock[width, height];
            id2chess = new Dictionary<int, ChessBlocksLink>();
            id2chess[ChessBlock.DEFID] = new ChessBlocksLink();
            NoneBlock = new ChessBlock(new Point(-1, -1), ChessBlock.NONEID);
            foreach (int i in initIds)
                id2chess[i] = new ChessBlocksLink();
            directions = initDirections;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    ChessT[x, y] = new ChessBlock(new Point(x, y));
                    id2chess[ChessBlock.DEFID].AddLast(ChessT[x, y]);
                }
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    ChessT[x, y].Surround = new ChessBlock[initDirections.Length];
                    for (int i = 0; i < initDirections.Length; i++)
                    {
                        Point p = PMath.Add(ChessT[x, y].Loc, initDirections[i]);
                        ChessT[x, y].Surround[i] = IsInTable(p) ? ChessT[p.X, p.Y] : NoneBlock;
                    }
                }
            arroundTemp = new LinkedList<CAction>();
            OptifinedAround = optifinedAround;
        }
        public ChessTable Clone()
        {
            ChessTable re=new ChessTable(Width, Height,directions, id2chess.Keys.ToArray(),OptifinedAround);
            foreach(int i in id2chess.Keys)
            {
                if (i == ChessBlock.DEFID) continue;
                foreach(ChessBlock c in id2chess[i])
                {
                    ChessBlock c2 = re[c.Loc];
                    re.id2chess[c2.Id].Remove(c2);
                    re.id2chess[i].AddLast(c2);
                    c2.Id= i;
                }
            }
            if(actionLast!= null)
                re.actionLast = actionLast;
            return re;
        }
        public bool IsInTable(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;
        public ChessBlock this[Point point]
        {
            get
            {
                return ChessT[point.X, point.Y];
            }
        }
        public int this[int x,int y] { get => ChessT[x,y].Id; }
        private void Apply(CAction c)
        {
            ChessBlock chess = ChessT[c.Loc.X, c.Loc.Y];
            chess.Id = c.to;
            id2chess[c.from].Remove(chess);
            id2chess[c.to].AddLast(chess);
            if (OptifinedAround)
            {
                if (arroundTemp.Count > 0 && arroundTemp.Last.Value.Loc == c.Loc)
                {
                    if (arroundTemp.Last.Value.from == c.to) arroundTemp.RemoveLast();
                    else
                        arroundTemp.Last.Value = new CAction(chess.Loc, arroundTemp.Last.Value.from, c.to);
                }
                else
                    arroundTemp.AddLast(c);
            }
        }
        public void Apply(CAction[] cAction)
        {
            actionLast = cAction;
            foreach (CAction c in cAction)
                Apply(c);
        }
        public void Withdraw(CAction[] cAction)
        {
            actionLast = new CAction[cAction.Length];
            for (int i = 0; i < cAction.Length; i++)
            {
                actionLast[i] = cAction[cAction.Length - 1 - i].Reverse();
                Apply(actionLast[i]);
            }
        }
        public bool MatchPatten(ChessBlock chess, int[] patten, int direIndex)
        {
            int iv = 0;
            while (iv < patten.Length)
            {
                if (chess.Id != patten[iv]) return false;
                chess = chess.Surround[direIndex];
                iv += 1;
            }
            return true;
        }
        public bool MatchPatten(ChessBlock chess, int[] patten, IEnumerable<int> direIndex)
        {
            foreach (int i in direIndex)
            {
                if (MatchPatten(chess, patten, i)) return true;
            }
            return false;
        }
        public bool MatchPatten(ChessBlock chess, int[] patten)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                if (MatchPatten(chess, patten, i)) return true;
            }
            return false;
        }
        public ChessBlock FindPatten(int[] patten, int direIndex)
        {
            foreach (ChessBlock c in id2chess[patten[0]])
                if (MatchPatten(c, patten, direIndex)) return c;
            return null;
        }
        public ChessBlock FindPatten(int[] patten, IEnumerable<int> direIndex, out int findDire)
        {
            ChessBlock re;
            foreach (int i in direIndex)
            {
                re = FindPatten(patten, i);
                if (re != null)
                {
                    findDire = i;
                    return re;
                }
            }
            findDire = 0;
            return null;
        }
        public ChessBlock FindPatten(int[] patten, out int findDire)
        {
            ChessBlock re;
            for (int i = 0; i < directions.Length; i++)
            {
                re = FindPatten(patten, i);
                if (re != null)
                {
                    findDire = i;
                    return re;
                }
            }
            findDire = 0;
            return null;
        }
        public int CountPatten(int[] patten, int direIndex)
        {
            int count = 0;
            foreach (ChessBlock c in id2chess[patten[0]])
                if (MatchPatten(c, patten, direIndex)) count += 1;
            return count;
        }
        public int CountPatten(int[] patten, IEnumerable<int> direIndex)
        {
            int count = 0;
            foreach (int i in direIndex)
            {
                count += CountPatten(patten, i);
            }
            return count;
        }
        public int CountPatten(int[] patten)
        {
            int count = 0;
            /*
            Parallel.For(0, directions.Length, (i) =>
            {
                int t = CountPatten(patten, i);
                if(t!=0)
                    Interlocked.Add(ref count, t);
            });*/
            for (int i = 0; i < directions.Length; i++)
            {
                int t = CountPatten(patten, i);
                count += t;
            }
            return count;
        }
        public static LinkedList<ChessBlock> GetLineSame(ChessBlock chess, int direIndex, int id, int includeEndId)
        {
            chess = chess.Surround[direIndex];
            LinkedList<ChessBlock> re = new LinkedList<ChessBlock>();
            while (chess.Id == id)
            {
                re.AddLast(chess);
                chess = chess.Surround[direIndex];
            }
            if(chess.Id==includeEndId)re.AddLast(chess);
            return re;
        }
        public static IEnumerable<ChessBlock> GetLineSame(ChessBlock chess, IEnumerable<int> direIndex, int id, int includeEndId)
        {
            IEnumerable<ChessBlock> re = new ChessBlock[0];
            foreach (int i in direIndex)
            {
                re = re.Concat(GetLineSame(chess, i, id, includeEndId));
            }
            return re;
        }
        public IEnumerable<ChessBlock> GetLineSame(ChessBlock chess, int id, int includeEnd)
        {
            IEnumerable<ChessBlock> re = new ChessBlock[0];
            for (int i = 0; i < directions.Length; i++)
            {
                re = re.Concat(GetLineSame(chess, i, id, includeEnd));
            }
            return re;
        }
        public LinkedList<ChessBlock> GetLine(ChessBlock chess, int direIndex, int step)
        {
            chess = chess.Surround[direIndex];
            LinkedList<ChessBlock> re = new LinkedList<ChessBlock>();
            while (step-- >= 0 && chess.Id != ChessBlock.NONEID)
                re.AddLast(chess);
            return re;
        }
        public HashSet<ChessBlock> GetAround(ChessBlock chess, int range, bool isFree)
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            for (int i = 0; i < directions.Length; i++)
                foreach (ChessBlock c in GetLine(chess, i, range))
                {
                    if (!isFree || c.Id == ChessBlock.DEFID)
                        re.Add(c);
                }
            return re;
        }
        public HashSet<ChessBlock> GetAround(int id, int range, bool isFree)
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            foreach (ChessBlock chess in id2chess[id])
                for (int i = 0; i < directions.Length; i++)
                    foreach (ChessBlock c in GetLine(chess, i, range))
                    {
                        if (!isFree || c.Id == ChessBlock.DEFID)
                            re.Add(c);
                    }
            return re;
        }
        public HashSet<ChessBlock> GetAround(int range, bool isFree)
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            foreach (int id in id2chess.Keys)
            {
                if (id == ChessBlock.DEFID) continue;
                foreach (ChessBlock chess in id2chess[id])
                    for (int i = 0; i < directions.Length; i++)
                        foreach (ChessBlock c in GetLine(chess, i, range))
                        {
                            if (!isFree || c.Id == ChessBlock.DEFID)
                                re.Add(c);
                        }
            }

            return re;
        }
        public HashSet<ChessBlock> GetAroundNearFree()
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            foreach (int i in id2chess.Keys)
            {
                if (i == ChessBlock.DEFID) continue;
                foreach (ChessBlock chess in id2chess[i])
                {
                    foreach (ChessBlock c in chess.Surround)
                        if (c.Id == ChessBlock.DEFID)
                            re.Add(c);
                }
            }
            return re;
        }
        public HashSet<ChessBlock> GetAroundNearFree(ChessBlock chess)
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            foreach (ChessBlock c in chess.Surround)
                if (c.Id == ChessBlock.DEFID)
                    re.Add(c);
            return re;
        }
        public HashSet<ChessBlock> GetAroundNearFreeStep()
        {
            if (arroundLastRet == null)
            {
                if (!OptifinedAround) throw new Exception("Not optifined!");
                arroundLastRet = GetAroundNearFree();
                return arroundLastRet;
            }
            foreach (CAction ca in arroundTemp)
            {
                ChessBlock c = this[ca.Loc];
                if (ca.to == ChessBlock.DEFID)
                {
                    foreach (ChessBlock c2 in c.Surround)
                    {
                        if (c2.Id == ChessBlock.DEFID)
                        {
                            arroundLastRet.Remove(c2);
                        }
                    }
                    foreach (ChessBlock c2 in c.Surround)
                    {
                        if (c2.Id == ChessBlock.NONEID || c2.Id == ChessBlock.DEFID) continue;
                        foreach (ChessBlock c3 in c2.Surround)
                        {
                            if (c3.Id == ChessBlock.DEFID)
                            {
                                arroundLastRet.Add(c3);
                            }
                        }
                    }
                }
                else
                {
                    arroundLastRet.Remove(c);
                    foreach (ChessBlock c2 in c.Surround)
                    {
                        if (c2.Id == ChessBlock.DEFID)
                            arroundLastRet.Add(c2);
                    }
                }
            }
            arroundTemp.Clear();
            return arroundLastRet;
        }
        public override string ToString()
        {
            string s = "";
            for (int y = 0; y < Height; y++)
            {
                if (y > 0) s += "\n";
                for (int x = 0; x < Width; x++)
                    s += ChessT[x, y].Id.ToString();
            }
            return s;
        }
    }
    internal class CAction
    {
        public readonly Point Loc;
        public readonly int from, to;
        public CAction(ChessBlock chess, int to)
        {
            Loc = chess.Loc;
            from = chess.Id;
            this.to = to;
        }

        public CAction(Point chess, int from, int to)
        {
            Loc = chess;
            this.from = from;
            this.to = to;
        }

        public static bool operator ==(CAction a, CAction b) => a.Loc == b.Loc && a.to == b.to;
        public static bool operator !=(CAction a, CAction b) => !(a == b);
        public override int GetHashCode()
        {
            return Loc.X + Loc.Y << 4 + to << 12 + from << 8;
        }
        public static CAction[] Add(ChessBlock chess, int id) => new CAction[] { new CAction(chess, id) };
        public static List<CAction[]> Add(List<ChessBlock> chess, int id)
        {
            List<CAction[]> ret = new List<CAction[]>();
            foreach (ChessBlock chessBlock in chess)
                ret.Add(Add(chessBlock, id));
            return ret;
        }
        public static CAction[] Move(ChessBlock from, ChessBlock to) => new CAction[] { new CAction(to, from.Id), new CAction(from, ChessBlock.DEFID) };
        public static List<CAction[]> Move(ChessBlock from, List<ChessBlock> to)
        {
            List<CAction[]> ret = new List<CAction[]>();
            foreach (ChessBlock chessBlock in to)
                ret.Add(Move(from, chessBlock));
            return ret;
        }
        public static CAction[] Find(IEnumerable<CAction[]> cActions, CAction[] cAction)
        {
            int i = 0;
            foreach (CAction[] c in cActions)
            {
                if (c.Length == cAction.Length)
                {
                    bool found = true;
                    for (int j = 0; j < cAction.Length; j++)
                        if (cAction[j] != c[j])
                        {
                            found = false; break;
                        }
                    if (found)
                        return c;
                }
                i += 1;
            }
            return null;
        }
        public static int GetHash(CAction[] cActions)
        {
            int hash = cActions.Length;
            foreach (CAction cAction in cActions)
                hash ^= cAction.GetHashCode();
            return hash;
        }
        public CAction Reverse() => new CAction(Loc,to, from);
        public static CAction[] Clone(CAction[] c, ChessTable to)
        {
            CAction[] re= new CAction[c.Length];
            for(int i = 0; i < c.Length; i++)
            {
                re[i]=c[i];
            }
            return re;
        }
    }
    internal class PMath
    {
        public static readonly Point[] towords = new Point[] { new Point(0, 1), new Point(-1, 1), new Point(-1, 0), new Point(-1, -1), new Point(1, 1), new Point(1, 0), new Point(1, -1), new Point(0, -1) };
        public static List<Point> GetStraight(Point point, Point toward, int step = 1)
        {
            List<Point> result = new List<Point>();
            for (int i = 1; i <= step; i++)
            {
                result.Add(new Point(point.X + toward.X * i, point.Y + toward.Y * i));
            }
            return result;
        }
        public static List<Point> GetSurround(Point point, int range = 1)
        {
            List<Point> re = new List<Point>();
            for (int i = 0; i <= range; i++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (i == 0)
                    {
                        if (y == 0) continue;
                        re.Add(new Point(point.X, point.Y + y));
                        continue;
                    }
                    re.Add(new Point(point.X + i, point.Y + y));
                    re.Add(new Point(point.X - i, point.Y + y));
                }
            }
            return re;
        }
        public static List<Point> GetPointsPattern(Point point, Point pattern)
        {
            List<Point> re = GetPointsStrictPattern(point, pattern);
            if (pattern.X != pattern.Y) re.AddRange(GetPointsStrictPattern(Turn(point), pattern));
            return re;
        }
        public static List<Point> GetPointsStrictPattern(Point point, Point pattern)
        {
            List<Point> re;
            if (pattern.X == 0) re = new List<Point>() { new Point(point.X, point.Y + pattern.Y), new Point(point.X, point.Y - pattern.Y) };
            else if (pattern.Y == 0) re = new List<Point>() { new Point(point.X + pattern.X, point.Y), new Point(point.X - pattern.X, point.Y) };
            else
            {
                int[] t = new int[] { -1, 1 };
                re = new List<Point>();
                foreach (int a in t)
                    foreach (int b in t)
                    {
                        re.Add(new Point(point.X + pattern.X * a, point.Y + pattern.Y * b));
                    }
            }
            return re;
        }
        public static Point Turn(Point point) => new Point(point.Y, point.X);
        public static Point Add(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
        public static Point Mius(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
    }
    internal class IconMaker
    {
        public Bitmap bitmap;
        public RectangleF nextDraw;
        public readonly Graphics g;
        public static IconMaker DEFChess(Size size, Color chessColor, Color aroundColor) => new IconMaker(size, Color.Transparent) * new Icon(0, chessColor, chessColor!=Color.Transparent) + new Icon(0, aroundColor, false);
        public static IconMaker DEFChess(Size size, Color chessColor)=>DEFChess(size, chessColor, Color.Black);
        public IconMaker(Size size, Color backGround, bool nextdraw = true)
        {
            bitmap = new Bitmap(size.Width, size.Height);
            nextDraw = nextdraw ? new RectangleF(size.Width / 10, size.Height / 10, size.Width * 4 / 5, size.Height * 4 / 5) : new RectangleF(0, 0, size.Width, size.Height);
            g = Graphics.FromImage(bitmap);
            if(backGround!=Color.Transparent)
                g.FillRectangle(new SolidBrush(backGround), nextDraw);
        }
        internal class Icon
        {
            public readonly int n;
            public readonly bool isFill;
            public readonly Color color;
            public readonly Image image;
            public readonly double angle;
            public readonly RectangleF delta;
            public Icon(int n, Color color, bool isFill = true, double angle = Math.PI / 2, RectangleF delta = default, Image image = null)
            {
                this.n = n;
                this.isFill = isFill;
                this.color = color;
                this.image = image;
                this.angle = angle;
                if (image != null) this.n = -1;
                if (delta == default) this.delta = new RectangleF(0, 0, 1, 1);
                else this.delta = delta;
            }
            public Icon(string s, Brush brush, int xiv, int yiv) :this(-1, Color.Transparent, true, 0, default,TextImage(s,brush, xiv, yiv)) { }
            public static Image TextImage(string s, Brush brush, int xiv, int yiv)
            {
                Bitmap bitmap=new Bitmap(100,100*s.Length);
                Graphics g = Graphics.FromImage(bitmap);
                g.DrawString(s, new Font(FontFamily.Families[2], 80), brush, xiv, yiv);
                return bitmap;
            }

            public static IconMaker operator +(IconMaker a, Icon b)
            {
                a *= b;
                a.nextDraw = new RectangleF(a.nextDraw.X + a.nextDraw.Width / 8, a.nextDraw.Y + a.nextDraw.Height / 8, a.nextDraw.Width * 3 / 4, a.nextDraw.Height * 3 / 4);
                return a;
            }
            public static IconMaker operator *(IconMaker a, Icon b)
            {
                b.Apply(a.g, a.nextDraw);
                return a;
            }
            public void Apply(Graphics g, RectangleF drawSize)
            {
                drawSize = new RectangleF(drawSize.X + drawSize.Width * delta.X, drawSize.Y + drawSize.Height * delta.Y, drawSize.Width * delta.Width, drawSize.Height * delta.Height);
                if (n >= 0)
                {
                    if (isFill)
                    {
                        Brush bBrush = new SolidBrush(color);
                        if (n == 0) g.FillEllipse(bBrush, drawSize);
                        else if (n == 1) g.FillRectangle(bBrush, new RectangleF(drawSize.X, drawSize.Y + drawSize.Height * 3 / 8, drawSize.Width, drawSize.Height / 4));
                        else if (n == 2) g.FillRectangle(bBrush, new RectangleF(drawSize.X + drawSize.Width * 3 / 8, drawSize.Y, drawSize.Width / 4, drawSize.Height));
                        else if (n > 2) g.FillPolygon(bBrush, GetPolygon(drawSize, n, angle));
                    }
                    else
                    {
                        Pen pen = new Pen(color);
                        if (n == 0) g.DrawEllipse(pen, drawSize);
                        else if (n == 1) g.DrawRectangle(pen, new Rectangle((int)drawSize.X, (int)(drawSize.Y + drawSize.Height * 3 / 8), (int)drawSize.Width, (int)(drawSize.Height / 4)));
                        else if (n == 2) g.DrawRectangle(pen, new Rectangle((int)(drawSize.X + drawSize.Width * 3 / 8), (int)drawSize.Y, (int)drawSize.Width / 4, (int)drawSize.Height));
                        else if (n > 2) g.DrawPolygon(pen, GetPolygon(drawSize, n, angle));
                    }
                }
                else if (n == -1)
                {
                    g.DrawImage(image, drawSize, new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
                }
            }
            public static PointF[] GetPolygon(RectangleF rectangle, int n, double angle)
            {
                if (n <= 1) throw new ArgumentException("n<=1");
                PointF[] re = new PointF[n];
                float a = rectangle.Width / 2f, b = rectangle.Height / 2f, x = rectangle.Left + a, y = rectangle.Top + b, s = 2 * (float)Math.PI / n;
                for (int i = 0; i < n; i++)
                {
                    re[i] = new PointF(x + (float)Math.Cos(s * i - angle) * a, y + (float)Math.Sin(s * i - angle) * b);
                }
                return re;
            }
        }
    }
    internal class TableMaker
    {
        public enum BlockMark
        {
            None = 0,
            Up = 1,
            Down = 1<<1,
            Left = 1<<2,
            Right = 1<<3,
            Cross = 1<<4,
            MinusCross = 1<<5,
            Box = 1 + (1 << 1) + (1 << 2) + (1 << 3),
            Full = 1 + (1 << 1) + (1 << 2) + (1 << 3) + (1 << 4) + (1 << 5),
        }
        public class BlockInf
        {
            public readonly Brush brush;
            public readonly BlockMark mark;
            public readonly Pen pen;

            public BlockInf(BlockMark mark, Brush brush,  Pen pen)
            {
                this.brush = brush;
                this.mark = mark;
                this.pen = pen;
            }
            public BlockInf(BlockMark mark, Brush brush) : this(mark, brush, Pens.Black) { }
            public BlockInf(BlockMark mark) : this(mark, Brushes.BurlyWood, Pens.Black) { }
            public static readonly BlockInf Defaut=new BlockInf(BlockMark.Box);
        }
        public static void DrawBlock(Graphics g, Rectangle rectangle, BlockInf blockInf)
        {
            g.FillRectangle(blockInf.brush, rectangle);
            if (blockInf.mark == BlockMark.None) return;
            if ((blockInf.mark & BlockMark.Up) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y);
            }
            if ((blockInf.mark & BlockMark.Down) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X, rectangle.Y + rectangle.Height - 1, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
            }
            if ((blockInf.mark & BlockMark.Left) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X, rectangle.Y, rectangle.X, rectangle.Y + rectangle.Height - 1);
            }
            if ((blockInf.mark & BlockMark.Right) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width - 1, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
            }
            if ((blockInf.mark & BlockMark.Cross) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X, rectangle.Y + rectangle.Height - 1, rectangle.X + rectangle.Width - 1, rectangle.Y);
            }
            if ((blockInf.mark & BlockMark.MinusCross) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
            }
        }
        public static Image GetStandredTable(int width,int height, Size iconSize)
        {
            Bitmap re=new Bitmap(width*iconSize.Width,height*iconSize.Height);
            Graphics g= Graphics.FromImage(re);
            for(int x=0;x<width;x++)
                for(int y = 0; y < height; y++)
                {
                    DrawBlock(g,new Rectangle(x*iconSize.Width,y*iconSize.Height,iconSize.Width,iconSize.Height),BlockInf.Defaut);
                }
            return re;
        }
        public static Image GetTable(int width, int height, Size iconSize, BlockInf[,] blockInfs)
        {
            Bitmap re = new Bitmap(width * iconSize.Width, height * iconSize.Height);
            Graphics g = Graphics.FromImage(re);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    DrawBlock(g, new Rectangle(x * iconSize.Width, y * iconSize.Height, iconSize.Width, iconSize.Height), blockInfs[x,y]);
                }
            return re;
        }
    }
    internal interface IGrid
    {
        int this[int x,int y] { get;}
        int Width { get; }
        int Height { get; }
    }
    internal class GridDrawer
    {
        private readonly IGrid Grid;
        private Image Background;
        public readonly Size IconSize;
        public readonly Dictionary<int, Image> id2Img;
        private Graphics g;
        private Bitmap bitmap;
        private Dictionary<Point, string> Locs;
        public static Pen BGLinePen = Pens.Black;
        public GridDrawer(IGrid grid, Image background, Size iconSize, Dictionary<int, Image> id2Img)
        {
            Grid = grid;
            Background = background;
            IconSize = iconSize;
            this.id2Img = id2Img;
            if (Background == null)
            {
                Background=TableMaker.GetStandredTable(grid.Width, grid.Height, iconSize);
            }
            Locs = new Dictionary<Point, string>();
            for (int x = 0; x < Grid.Width; x++)
                for (int y = 0; y < Grid.Height; y++)
                    Locs[new Point(x, y)] = "\n\n  " + x.ToString() + "," + y.ToString();
        }
        private Rectangle GetBlockRect(int X, int Y) => new Rectangle(X * IconSize.Width, Y * IconSize.Height, IconSize.Width, IconSize.Height);
        private Rectangle GetBlockRectSub(int X, int Y) => new Rectangle(X * IconSize.Width + 2, Y * IconSize.Height + 2, IconSize.Width - 4, IconSize.Height - 4);
        public Image GetImage(bool Line = false, bool Smaller = true, bool Loc = true, Dictionary<Point, string> data = null)
        {
            bitmap = new Bitmap(IconSize.Width * Grid.Width, IconSize.Height * Grid.Height);
            g = Graphics.FromImage(bitmap);
            GC.Collect();
            g.DrawImage(Background, 0, 0, bitmap.Width, bitmap.Height);
            if (Line) SetLine(BGLinePen, g);
            for (int x = 0; x < Grid.Width; x++)
                for (int y = 0; y < Grid.Height; y++)
                    if (id2Img.ContainsKey(Grid[x, y]))
                        g.DrawImage(id2Img[Grid[x, y]], Smaller ? GetBlockRectSub(x, y) : GetBlockRect(x, y));
            if (Loc) DrawData(Locs, Brushes.DarkGreen);
            if (data != null) DrawData(data, Brushes.Red);
            return bitmap;
        }
        public void SetLine(Pen pen, Graphics g)
        {
            for (int i = 1; i < Grid.Width; i++)
                g.DrawLine(pen, i * IconSize.Width, 0, i * IconSize.Width, IconSize.Height * Grid.Height);
            for (int i = 1; i < Grid.Height; i++)
                g.DrawLine(pen, 0, i * IconSize.Height, IconSize.Width * Grid.Width, i * IconSize.Height);
        }
        public void SetStandredBG(Color color, bool BGLine = true)
        {
            Background = new Bitmap(IconSize.Width * Grid.Width, IconSize.Height * Grid.Height);
            Graphics bg = Graphics.FromImage(Background);
            bg.Clear(color);
            if (BGLine) SetLine(BGLinePen, bg);
        }
        public Point GetClick(Point point) => new Point(point.X / IconSize.Width, point.Y / IconSize.Height);
        private void DrawData(Dictionary<Point, string> data, Brush brush)
        {
            foreach (Point p in data.Keys)
            {
                g.DrawString(data[p], new Font(FontFamily.Families[2], 8), brush, p.X * IconSize.Width, p.Y * IconSize.Height);
            }
        }
    }
    internal class PowerCounter
    {
        public class AutoMach
        {
            public readonly Dictionary<int, AutoMach> Nudes;
            public int Final;
            public AutoMach(int final)
            {
                Nudes = new Dictionary<int, AutoMach>();
                Final = final;
            }
            public AutoMach() : this(-2) { }
            public static AutoMach Err = new AutoMach(-1);
            public void InitKeys(IEnumerable<int> keys)
            {
                if (Final != -2) return;
                foreach (int k in keys)
                {
                    if (Nudes.ContainsKey(k)) Nudes[k].InitKeys(keys);
                    else Nudes[k] = Err;
                }
            }
            public override string ToString()
            {
                return Final.ToString();
            }
        }
        private readonly AutoMach start;
        private readonly int[] startWith;
        private readonly int PatternsLen;
        public PowerCounter(int[][] Patterns, IEnumerable<int> keys)
        {
            PatternsLen = Patterns.Length;
            start = new AutoMach();
            HashSet<int> startWiths = new HashSet<int>();
            for (int x = 0; x < Patterns.Length; x++)
            {
                AutoMach t = start;
                int y = 0;
                startWiths.Add(Patterns[x][0]);
                while (true)
                {
                    if (y == Patterns[x].Length - 1)
                    {
                        t.Nudes[Patterns[x][y]] = new AutoMach(x);
                        break;
                    }
                    if (!t.Nudes.ContainsKey(Patterns[x][y]))
                        t.Nudes[Patterns[x][y]] = new AutoMach();
                    t = t.Nudes[Patterns[x][y]];
                    if (t.Final != -2) break;
                    y++;
                }
            }
            startWith = startWiths.ToArray();
            start.InitKeys(keys);
        }
        public PowerCounter(AutoMach start, int[] startWith, int patternsLen)
        {
            this.start = start;
            this.startWith = startWith;
            PatternsLen = patternsLen;
        }
        public static int Match(ChessBlock chess, int diri, AutoMach t)
        {
            while (t.Final == -2)
            {
                chess = chess.Surround[diri];
                t = t.Nudes[chess.Id];
            }
            return t.Final;
        }
        public int[] CountPatterns(ChessTable chessTable)
        {
            int[] re = new int[PatternsLen];
            foreach (int s in startWith)
            {
                foreach (ChessBlock c in chessTable.id2chess[s])
                {
                    AutoMach n = start.Nudes[c.Id];
                    for (int i = 0; i < chessTable.directions.Length; i++)
                    {
                        int t = Match(c, i, n);
                        if (t != -1) re[t]++;
                    }
                }
            }
            return re;
        }
    }
}

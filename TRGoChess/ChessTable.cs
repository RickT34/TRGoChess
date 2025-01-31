﻿using System;
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
    /// <summary>
    /// 棋盘类
    /// </summary>
    public class ChessTable : IGrid
    {
        /// <summary>
        /// 棋盘格类
        /// </summary>
        public class ChessBlock : IComparable
        {
            /// <summary>
            /// 棋盘格不存在时的ID
            /// </summary>
            public const int NONEID = -1;
            /// <summary>
            /// 棋盘格上无棋子时的ID
            /// </summary>
            public const int DEFID = 0;
            /// <summary>
            /// 棋盘格的ID，用于记录棋子
            /// </summary>
            public int Id;
            /// <summary>
            /// 各个方向上的棋盘格，用于快速查找
            /// </summary>
            public ChessBlock[] Surround;
            /// <summary>
            /// 链表信息
            /// </summary>
            public ChessBlocksLink.LinkInf LinkInf;
            /// <summary>
            /// 位置
            /// </summary>
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

        /// <summary>
        /// 定制为棋盘格的链表，提高增删效率
        /// </summary>
        public class ChessBlocksLink : IEnumerable<ChessBlock>, IEnumerable
        {
            /// <summary>
            /// 链表尾，使用不存在于棋盘上的新建棋盘格
            /// </summary>
            private readonly ChessBlock last;

            private int count;
            /// <summary>
            /// 数量
            /// </summary>
            public int Count { get => count; }

            #region 枚举器部分，用于兼容foreach
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
            #endregion

            /// <summary>
            /// 链表信息
            /// </summary>
            public class LinkInf
            {
                public ChessBlock LinkLeft, LinkRight;
            }
            public ChessBlocksLink()
            {
                last = new ChessBlock(new Point(-1, -1), ChessBlock.NONEID);
                count = 0;
            }

            /// <summary>
            /// 在尾部添加元素
            /// </summary>
            /// <param name="block"></param>
            public void AddLast(ChessBlock block)
            {
                if (count > 0)
                    last.LinkInf.LinkLeft.LinkInf.LinkRight = block;
                block.LinkInf.LinkLeft = last.LinkInf.LinkLeft;
                block.LinkInf.LinkRight = last;
                last.LinkInf.LinkLeft = block;
                count++;
            }

            /// <summary>
            /// 直接删除元素，不检查是否存在于本链表
            /// </summary>
            /// <param name="a">要删除的元素</param>
            public void Remove(ChessBlock a)
            {
                if (a.LinkInf.LinkLeft != null)
                    a.LinkInf.LinkLeft.LinkInf.LinkRight = a.LinkInf.LinkRight;
                a.LinkInf.LinkRight.LinkInf.LinkLeft = a.LinkInf.LinkLeft;
                a.LinkInf.LinkLeft = null;
                a.LinkInf.LinkRight = null;
                count--;
            }

            /// <summary>
            /// 在两个链表间交换元素
            /// </summary>
            /// <param name="a">要交换的元素a</param>
            /// <param name="b">要交换的元素b</param>
            public static void Swap(ChessBlock a, ChessBlock b)
            {
                LinkInf t = a.LinkInf;
                a.LinkInf = b.LinkInf;
                b.LinkInf = t;
            }
        }

        /// <summary>
        /// 所有的棋盘格
        /// </summary>
        public readonly ChessBlock[,] ChessT;

        /// <summary>
        /// 用id定位棋盘格链表
        /// </summary>
        public readonly Dictionary<int, ChessBlocksLink> id2chess;
        /// <summary>
        /// 最后一步操作
        /// </summary>
        public CAction[] actionLast;
        public object Tag;
        /// <summary>
        /// 所有已初始化的方向
        /// </summary>
        public readonly Point[] directions;
        /// <summary>
        /// 是否开启边界查找优化
        /// </summary>
        public bool OptifinedAround;
        /// <summary>
        /// 墙壁方块
        /// </summary>
        public readonly ChessBlock NoneBlock;
        public int Width => ChessT.GetLength(0);
        public int Height => ChessT.GetLength(1);
        /// <summary>
        /// 初始化棋盘
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="initDirections">要初始化的方向，用于快速查找</param>
        /// <param name="initIds">所有要使用的ID</param>
        /// <param name="optifinedAround">是否开启边界查找优化</param>
        public ChessTable(int width, int height, Point[] initDirections, int[] initIds, bool optifinedAround = false)
        {
            ChessT = new ChessBlock[width, height];
            id2chess = new Dictionary<int, ChessBlocksLink>
            {
                [ChessBlock.DEFID] = new ChessBlocksLink()
            };
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
        /// <summary>
        /// 复制棋盘
        /// </summary>
        /// <returns></returns>
        public ChessTable Clone()
        {
            ChessTable re = new ChessTable(Width, Height, directions, id2chess.Keys.ToArray(), OptifinedAround);
            foreach (int i in id2chess.Keys)
            {
                if (i == ChessBlock.DEFID) continue;
                foreach (ChessBlock c in id2chess[i])
                {
                    ChessBlock c2 = re[c.Loc];
                    re.id2chess[c2.Id].Remove(c2);
                    re.id2chess[i].AddLast(c2);
                    c2.Id = i;
                }
            }
            if (actionLast != null)
                re.actionLast = actionLast;
            return re;
        }
        /// <summary>
        /// 检查是否位于棋盘内
        /// </summary>
        /// <param name="point">位置</param>
        /// <returns></returns>
        public bool IsInTable(Point point) => point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;

        #region 重载的索引器
        public ChessBlock this[Point point]
        {
            get
            {
                return ChessT[point.X, point.Y];
            }
        }
        public int this[int x, int y] { get => ChessT[x, y].Id; }
        #endregion

        #region 棋盘操作函数
        /// <summary>
        /// 应用操作
        /// </summary>
        /// <param name="c"></param>
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
        /// <summary>
        /// 应用操作
        /// </summary>
        /// <param name="c"></param>
        public void Apply(CAction[] cAction)
        {
            actionLast = cAction;
            foreach (CAction c in cAction)
                Apply(c);
        }
        /// <summary>
        /// 撤回操作
        /// </summary>
        /// <param name="c"></param>
        public void Withdraw(CAction[] cAction)
        {
            actionLast = new CAction[cAction.Length];
            for (int i = 0; i < cAction.Length; i++)
            {
                actionLast[i] = cAction[cAction.Length - 1 - i].Reverse();
                Apply(actionLast[i]);
            }
        }
        #endregion

        #region 模式检查函数
        /// <summary>
        /// 是否符合模式
        /// </summary>
        /// <param name="chess">棋盘格</param>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标</param>
        /// <returns>true，如果符合模式</returns>
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
        /// <summary>
        /// 是否符合模式
        /// </summary>
        /// <param name="chess">棋盘格</param>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标集合</param>
        /// <returns>true，如果符合模式</returns>
        public bool MatchPatten(ChessBlock chess, int[] patten, IEnumerable<int> direIndex)
        {
            foreach (int i in direIndex)
            {
                if (MatchPatten(chess, patten, i)) return true;
            }
            return false;
        }
        /// <summary>
        /// 是否符合模式，考虑所有已初始化的方向
        /// </summary>
        /// <param name="chess">棋盘格</param>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <returns>true，如果符合模式</returns>
        public bool MatchPatten(ChessBlock chess, int[] patten)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                if (MatchPatten(chess, patten, i)) return true;
            }
            return false;
        }
        /// <summary>
        /// 定位一个模式出现的位置
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标</param>
        /// <returns></returns>
        public ChessBlock FindPatten(int[] patten, int direIndex)
        {
            foreach (ChessBlock c in id2chess[patten[0]])
                if (MatchPatten(c, patten, direIndex)) return c;
            return null;
        }
        /// <summary>
        /// 定位一个模式出现的位置
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标集合</param>
        /// <returns></returns>
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
        /// <summary>
        /// 定位一个模式出现的位置和方向
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="findDire">匹配到的模式的方向</param>
        /// <returns></returns>
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
        /// <summary>
        /// 统计一个模式出现的次数
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标</param>
        /// <returns></returns>
        public int CountPatten(int[] patten, int direIndex)
        {
            int count = 0;
            foreach (ChessBlock c in id2chess[patten[0]])
                if (MatchPatten(c, patten, direIndex)) count += 1;
            return count;
        }
        /// <summary>
        /// 统计一个模式出现的次数
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <param name="direIndex">已初始化方向的下标集合</param>
        /// <returns></returns>
        public int CountPatten(int[] patten, IEnumerable<int> direIndex)
        {
            int count = 0;
            foreach (int i in direIndex)
            {
                count += CountPatten(patten, i);
            }
            return count;
        }
        /// <summary>
        /// 统计一个模式出现的次数，考虑所有方向
        /// </summary>
        /// <param name="patten">模式，内容为棋盘格ID序列</param>
        /// <returns></returns>
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
        #endregion

        #region 棋盘格查找函数
        /// <summary>
        /// 查找一个方向上所有相同id的棋盘格
        /// </summary>
        /// <param name="chess">起始位置，不检查</param>
        /// <param name="direIndex">已初始化方向的下标</param>
        /// <param name="id">id</param>
        /// <param name="includeEndId">允许的末尾id</param>
        /// <returns></returns>
        public static LinkedList<ChessBlock> GetLineSame(ChessBlock chess, int direIndex, int id, int includeEndId)
        {
            chess = chess.Surround[direIndex];
            LinkedList<ChessBlock> re = new LinkedList<ChessBlock>();
            while (chess.Id == id)
            {
                re.AddLast(chess);
                chess = chess.Surround[direIndex];
            }
            if (chess.Id == includeEndId) re.AddLast(chess);
            return re;
        }
        /// <summary>
        /// 查找一个方向上所有相同id的棋盘格
        /// </summary>
        /// <param name="chess">起始位置，不检查</param>
        /// <param name="direIndex">已初始化方向的下标集合</param>
        /// <param name="id">id</param>
        /// <param name="includeEndId">允许的末尾id</param>
        /// <returns></returns>
        public static IEnumerable<ChessBlock> GetLineSame(ChessBlock chess, IEnumerable<int> direIndex, int id, int includeEndId)
        {
            IEnumerable<ChessBlock> re = new ChessBlock[0];
            foreach (int i in direIndex)
            {
                re = re.Concat(GetLineSame(chess, i, id, includeEndId));
            }
            return re;
        }
        /// <summary>
        /// 查找一个方向上所有相同id的棋盘格，考虑所有已初始化的方向
        /// </summary>
        /// <param name="chess">起始位置，不检查</param>
        /// <param name="id">id</param>
        /// <param name="includeEndId">允许的末尾id</param>
        /// <returns></returns>
        public IEnumerable<ChessBlock> GetLineSame(ChessBlock chess, int id, int includeEndId)
        {
            IEnumerable<ChessBlock> re = new ChessBlock[0];
            for (int i = 0; i < directions.Length; i++)
            {
                re = re.Concat(GetLineSame(chess, i, id, includeEndId));
            }
            return re;
        }
        /// <summary>
        /// 查找一个方向的固定长度的棋盘格
        /// </summary>
        /// <param name="chess">起始位置，不检查</param>
        /// <param name="direIndex">已初始化方向的下标</param>
        /// <param name="step">长度</param>
        /// <returns></returns>
        public LinkedList<ChessBlock> GetLine(ChessBlock chess, int direIndex, int step)
        {
            chess = chess.Surround[direIndex];
            LinkedList<ChessBlock> re = new LinkedList<ChessBlock>();
            while (step-- >= 0 && chess.Id != ChessBlock.NONEID)
                re.AddLast(chess);
            return re;
        }
        /// <summary>
        /// 查找所有已初始化方向的，固定长度的棋盘格
        /// </summary>
        /// <param name="chess">起始位置，不检查</param>
        /// <param name="range">长度</param>
        /// <param name="isFree">是否必须无棋子</param>
        /// <returns></returns>
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
        /// <summary>
        /// 查找所有同id棋子在所有已初始化方向的，固定长度的棋盘格
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="range">长度</param>
        /// <param name="isFree">是否必须无棋子</param>
        /// <returns></returns>
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
        /// <summary>
        /// 查找所有棋子在所有已初始化方向的，固定长度的棋盘格
        /// </summary>
        /// <param name="range">长度</param>
        /// <param name="isFree">是否必须无棋子</param>
        /// <returns></returns>
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
        /// <summary>
        /// 查找棋子在所有已初始化方向上的，第一个的，无棋子的棋盘格
        /// </summary>
        /// <param name="chess">棋子</param>
        /// <returns></returns>
        public HashSet<ChessBlock> GetAroundNearFree(ChessBlock chess)
        {
            HashSet<ChessBlock> re = new HashSet<ChessBlock>();
            foreach (ChessBlock c in chess.Surround)
                if (c.Id == ChessBlock.DEFID)
                    re.Add(c);
            return re;
        }
        /// <summary>
        /// 查找所有棋子在所有已初始化方向上的，第一个的，无棋子的棋盘格
        /// </summary>
        /// <returns></returns>
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

        private HashSet<ChessBlock> arroundLastRet;
        private LinkedList<CAction> arroundTemp;
        /// <summary>
        /// 查找所有棋子在所有已初始化方向上的，第一个的，无棋子的棋盘格。优化版本，大幅提高效率，需要开启优化。
        /// </summary>
        /// <returns></returns>
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
        #endregion

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
    /// <summary>
    /// 棋盘操作类，用于表示一个棋盘格的Id变化
    /// </summary>
    public class CAction
    {
        /// <summary>
        /// 棋盘格位置
        /// </summary>
        public readonly Point Loc;
        /// <summary>
        /// 原始的Id
        /// </summary>
        public readonly int from;
        /// <summary>
        /// 操作后的Id
        /// </summary>
        public readonly int to;
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
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return Loc.X + Loc.Y << 4 + to << 12 + from << 8;
        }
        /// <summary>
        /// 添加棋子，需要一个操作
        /// </summary>
        /// <param name="chess">要添加棋子的棋盘格</param>
        /// <param name="id">Id</param>
        /// <returns></returns>
        public static CAction[] Add(ChessBlock chess, int id) => new CAction[] { new CAction(chess, id) };
        public static IEnumerable<CAction[]> Add(IEnumerable<ChessBlock> chess, int id)
        {
            return chess.Select((cb)=>Add(cb, id));
        }
        /// <summary>
        /// 移动棋子，需要两个操作
        /// </summary>
        /// <param name="from">从</param>
        /// <param name="to">到</param>
        /// <returns></returns>
        public static CAction[] Move(ChessBlock from, ChessBlock to) => new CAction[] { new CAction(to, from.Id), new CAction(from, ChessBlock.DEFID) };
        public static List<CAction[]> Move(ChessBlock from, List<ChessBlock> to)
        {
            List<CAction[]> ret = new List<CAction[]>();
            foreach (ChessBlock chessBlock in to)
                ret.Add(Move(from, chessBlock));
            return ret;
        }

        /// <summary>
        /// 从集合中查找某一操作
        /// </summary>
        /// <param name="cActions">集合</param>
        /// <param name="cAction">操作</param>
        /// <returns>如果找到，则返回操作，否则返回null</returns>
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
        /// <summary>
        /// 反向操作
        /// </summary>
        /// <returns></returns>
        public CAction Reverse() => new CAction(Loc, to, from);
        /// <summary>
        /// 复制操作
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static CAction[] Clone(CAction[] c)
        {
            CAction[] re = new CAction[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                re[i] = c[i];
            }
            return re;
        }
    }
    /// <summary>
    /// 用于坐标的常用函数类
    /// </summary>
    public class PMath
    {
        //                                                     0                1                 2                 3                  4                5                6                 7                 
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
    /// <summary>
    /// 棋子图标绘制类
    /// </summary>
    public class IconMaker
    {
        public Bitmap bitmap;
        public RectangleF nextDraw;
        public readonly Graphics g;
        public static IconMaker DEFChess(Size size, Color chessColor, Color aroundColor) => new IconMaker(size, Color.Transparent) * new Icon(0, chessColor, chessColor != Color.Transparent) + new Icon(0, aroundColor, false);
        public static IconMaker DEFChess(Size size, Color chessColor) => DEFChess(size, chessColor, Color.Black);
        public IconMaker(Size size, Color backGround, bool nextdraw = true)
        {
            bitmap = new Bitmap(size.Width, size.Height);
            nextDraw = nextdraw ? new RectangleF(size.Width / 10, size.Height / 10, size.Width * 4 / 5, size.Height * 4 / 5) : new RectangleF(0, 0, size.Width, size.Height);
            g = Graphics.FromImage(bitmap);
            if (backGround != Color.Transparent)
                g.FillRectangle(new SolidBrush(backGround), nextDraw);
        }
        public class Icon
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
            public Icon(string s, Brush brush, int xiv, int yiv) : this(-1, Color.Transparent, true, 0, default, TextImage(s, brush, xiv, yiv)) { }
            public Icon(string s, Brush brush, int xiv, int yiv, Font font) : this(-1, Color.Transparent, true, 0, default, TextImage(s, brush, xiv, yiv,font)) { }
            public static Image TextImage(string s, Brush brush, int xiv, int yiv, float emsize = 110)
            {
                return TextImage(s,brush, xiv, yiv, new Font(FontFamily.Families[2], emsize));
            }
            public static Image TextImage(string s, Brush brush, int xiv, int yiv, Font font)
            {
                Bitmap bitmap = new Bitmap(150 * s.Length, 150);
                Graphics g = Graphics.FromImage(bitmap);
                g.DrawString(s, font, brush, xiv, yiv);
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
    /// <summary>
    /// 棋盘底板绘制类
    /// </summary>
    public class TableMaker
    {
        public const int xiv = 0, yiv = 0;
        public enum BlockMark
        {
            None = 0,

            Up = 1,
            Down = 1 << 1,
            Left = 1 << 2,
            Right = 1 << 3,
            
            North = 1 << 4,
            South = 1 << 5,
            East = 1 << 6,
            West = 1 << 7,

            NE = 1 << 8,
            NW = 1 << 9,
            SE = 1 << 10,
            SW = 1 << 11,
            Cross = NE|SW,
            MinusCross = NW|SE,
            Box = Up | Down | Left | Right,
            Full = Box | Cross | MinusCross,
            Crossing = North | South | East | West,

        }
        public class BlockInf
        {
            public readonly Brush brush;
            public readonly BlockMark mark;
            public readonly Pen pen;

            public BlockInf(BlockMark mark, Brush brush, Pen pen)
            {
                this.brush = brush;
                this.mark = mark;
                this.pen = pen;
            }
            public BlockInf(BlockMark mark, Brush brush) : this(mark, brush, Pens.Black) { }
            public BlockInf(BlockMark mark) : this(mark, Brushes.BurlyWood, Pens.Black) { }
            public static readonly BlockInf Defaut = new BlockInf(BlockMark.Box);
        }
        public static Font LocFont = new Font("Times New Roman",90,FontStyle.Bold);
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

            if ((blockInf.mark & BlockMark.North) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width/2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y);
            }
            if ((blockInf.mark & BlockMark.South) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height - 1);
            }
            if ((blockInf.mark & BlockMark.East) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height / 2 - 1);
            }
            if ((blockInf.mark & BlockMark.West) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X, rectangle.Y + rectangle.Height / 2 - 1);
            }

            if ((blockInf.mark & BlockMark.NE) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X + rectangle.Width-1, rectangle.Y);
            }
            if ((blockInf.mark & BlockMark.NW) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X, rectangle.Y);
            }
            if ((blockInf.mark & BlockMark.SE) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1);
            }
            if ((blockInf.mark & BlockMark.SW) != 0)
            {
                g.DrawLine(blockInf.pen, rectangle.X + rectangle.Width / 2 - 1, rectangle.Y + rectangle.Height / 2 - 1, rectangle.X , rectangle.Y + rectangle.Height - 1);
            }
        }
        public static Image GetStandredTable(int width, int height, Size iconSize,BlockInf blockInf=null, bool Loc = true)
        {
            BlockInf[,] def = new BlockInf[width, height];
            if (blockInf == null) blockInf = BlockInf.Defaut;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    def[x, y] = blockInf;
                }
            return GetTable( iconSize, def, Loc ? ChessGame.DEFDrawIv.X : 0);
        }
        public static Image GetTable(Size iconSize, BlockInf[,] blockInfs, int LocSize)
        {
            int width = blockInfs.GetLength(0), height = blockInfs.GetLength(1);
            Bitmap re = new Bitmap(width * iconSize.Width + LocSize * 2, height * iconSize.Height + LocSize * 2);
            Graphics g = Graphics.FromImage(re);
            if (LocSize > 0)
            {
                for (int x = 0; x < width; x++)
                {
                    Image t = IconMaker.Icon.TextImage(((char)('a' + x)).ToString(), Brushes.Black, xiv, yiv,LocFont);
                    g.DrawImage(t, x * iconSize.Width + LocSize + (iconSize.Width - LocSize) / 2, height * iconSize.Height + LocSize, LocSize, LocSize);
                    g.DrawImage(t, x * iconSize.Width + LocSize + (iconSize.Width - LocSize) / 2, 0, LocSize, LocSize);
                }
                for(int x = 0; x < height; x++)
                {
                    Image n = IconMaker.Icon.TextImage((height - x).ToString(), Brushes.Black, xiv, yiv, LocFont);
                    int h = (height - x) > 9 ? LocSize * 2 : LocSize;
                    int x1 = (height - x) > 9 ? 0 : (LocSize / 2 - 2);
                    g.DrawImage(n, x1 - 2, x * iconSize.Height + (iconSize.Height - LocSize) / 2 + LocSize, h, LocSize);
                    g.DrawImage(n, width * iconSize.Width + LocSize-1, x * iconSize.Height + (iconSize.Height - LocSize) / 2 + LocSize, h, LocSize);
                }
            }
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    DrawBlock(g, new Rectangle(x * iconSize.Width + LocSize, y * iconSize.Height + LocSize, iconSize.Width, iconSize.Height), blockInfs[x, y]);
                }
            g.DrawLine(Pens.Black, LocSize, LocSize, LocSize, re.Height- LocSize);
            g.DrawLine(Pens.Black, LocSize, LocSize, re.Width- LocSize, LocSize);
            g.DrawLine(Pens.Black, LocSize, re.Height - LocSize, re.Width- LocSize, re.Height- LocSize);
            g.DrawLine(Pens.Black, re.Width - LocSize, LocSize, re.Width- LocSize, re.Height- LocSize);
            return re;
        }
    }
    /// <summary>
    /// 表格接口，表示支持当做表格读取数据
    /// </summary>
    public interface IGrid
    {
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="x">x</param>
        /// <param name="y">y</param>
        /// <returns></returns>
        int this[int x, int y] { get; }
        /// <summary>
        /// 宽度
        /// </summary>
        int Width { get; }
        /// <summary>
        /// 高度
        /// </summary>
        int Height { get; }
    }
    /// <summary>
    /// 棋盘绘制类
    /// </summary>
    public class GridDrawer
    {
        private readonly IGrid Grid;
        public Image Background;
        private Image BackgroundRev;
        public readonly Size IconSize;
        public readonly Dictionary<int, Image> id2Img;
        private Graphics g;
        private Bitmap bitmap;
        private bool reVersed;
        public readonly Point DrawIv;
        public GridDrawer(IGrid grid, Image background, Size iconSize, Dictionary<int, Image> id2Img, Point drawIv)
        {
            Grid = grid;
            Background = background;
            IconSize = iconSize;
            this.id2Img = id2Img;
            if (Background == null)
            {
                Background = TableMaker.GetStandredTable(grid.Width, grid.Height, iconSize);
            }
            BackgroundRev = (Image)Background.Clone();
            BackgroundRev.RotateFlip(RotateFlipType.RotateNoneFlipY);
            DrawIv = drawIv;
        }
        public Rectangle GetBlockRect(int X, int Y)
        {
            if(reVersed) return new Rectangle(X * IconSize.Width + DrawIv.X, (Grid.Height - Y - 1) * IconSize.Height + DrawIv.Y, IconSize.Width, IconSize.Height);
            return new Rectangle(X * IconSize.Width + DrawIv.X, Y * IconSize.Height + DrawIv.Y, IconSize.Width, IconSize.Height);
        }
        public Rectangle GetBlockRectSub(int X, int Y)
        {
            if (reVersed) return new Rectangle(X * IconSize.Width + 2 + DrawIv.X, (Grid.Height-Y-1) * IconSize.Height + 2 + DrawIv.Y, IconSize.Width - 4, IconSize.Height - 4);
            return new Rectangle(X * IconSize.Width + 2 + DrawIv.X, Y * IconSize.Height + 2 + DrawIv.Y, IconSize.Width - 4, IconSize.Height - 4);
        }
        public Image GetImage(bool Smaller = true,bool reVerse=false, Dictionary<Point, string> data = null)
        {
            reVersed = reVerse;
            if(reVersed) bitmap = new Bitmap(BackgroundRev);
            else bitmap = new Bitmap(Background);
            g = Graphics.FromImage(bitmap);
            GC.Collect();
            for (int x = 0; x < Grid.Width; x++)
                for (int y = 0; y < Grid.Height; y++)
                    if (id2Img.ContainsKey(Grid[x, y]))
                        g.DrawImage(id2Img[Grid[x, y]], Smaller ? GetBlockRectSub(x, y) : GetBlockRect(x, y));
            if (data != null) DrawData(data, Brushes.Red);
            return bitmap;
        }
        public Image DrawLines(IEnumerable<Point> points, Pen pen)
        {
            int c = points.Count();
            if (c > 1)
            {
                Point[] l = new Point[c];
                int i = 0;
                foreach (Point p in points)
                {
                    Rectangle r = GetBlockRect(p.X, p.Y);
                    l[i++] = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
                }
                g.DrawLines(pen, l);
            }
            return bitmap;
        }
        public Point GetClick(Point point)
        {
            if(reVersed) return new Point((point.X - DrawIv.X) / IconSize.Width,Grid.Height - (point.Y - DrawIv.Y) / IconSize.Height - 1);
            return new Point((point.X - DrawIv.X) / IconSize.Width, (point.Y - DrawIv.Y) / IconSize.Height);
        }
        private void DrawData(Dictionary<Point, string> data, Brush brush)
        {
            foreach (Point p in data.Keys)
            {
                g.DrawString(data[p], new Font(FontFamily.Families[2], 8), brush, GetBlockRect(p.X, p.Y));
            }
        }
    }
    /// <summary>
    /// 高效模式计数类，使用有限状态机
    /// </summary>
    public class PowerCounter
    {
        /// <summary>
        /// 有限状态机节点
        /// </summary>
        public class AutoMach
        {
            public readonly Dictionary<int, AutoMach> Nudes;
            public int Value;
            public AutoMach(int final)
            {
                Nudes = new Dictionary<int, AutoMach>();
                Value = final;
            }
            public AutoMach() : this(-2) { }
            public static AutoMach Err = new AutoMach(-1);
            public void InitKeys(IEnumerable<int> keys)
            {
                if (Value != -2) return;
                foreach (int k in keys)
                {
                    if (Nudes.ContainsKey(k)) Nudes[k].InitKeys(keys);
                    else Nudes[k] = Err;
                }
            }
            public override string ToString()
            {
                return Value.ToString();
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
                    if (t.Value != -2) break;
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
            while (t.Value == -2)
            {
                chess = chess.Surround[diri];
                t = t.Nudes[chess.Id];
            }
            return t.Value;
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

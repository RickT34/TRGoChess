﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static TRGoChess.ChessTable;

namespace TRGoChess
{
    public class ChessGame:IActionInf
    {
        protected readonly ChessTable ChessTable;
        public static readonly Size DEFBlockSize = new Size(45, 45);
        public static readonly Size DEFChessSize = new Size(30, 30);
        public static readonly Point DEFDrawIv = new Point(20, 20);
        public readonly GridDrawer GridDrawer;
        public readonly List<Player> players;
        protected readonly Dictionary<Player, Player> PlayerNext;
        protected readonly Dictionary<Player, int> PlayerId;
        protected LinkedList<CAction[]> History;
        public int HistoryCount { get=>History.Count;}
        public readonly Dictionary<int, string> IdStr;
        public Player NowPlayer;
        public Player LastPlayer;
        public readonly string Name;
        private bool endTurn;
        public bool EndTurn { get => endTurn; }
        public Player Winner { get => winner; }
        public Point DrawIv { get => GridDrawer.DrawIv; }
        public Size ImageSize { get => GridDrawer.Background.Size; }
        private Player winner;
        protected IEnumerable<CAction[]> NowLegalActions;
        public Player NextPlayer(Player player) => PlayerNext[player];
        public ChessGame(string name, int width, int height, Point[] initDirections, Image BackGround, Size IconSize, Dictionary<int, Image> id2image, int[] initIds, Point DrawIV, Dictionary<int, string> idStr, bool optifineAround = false)
        {
            ChessTable = new ChessTable(width, height, initDirections, initIds, optifineAround);
            GridDrawer = new GridDrawer(ChessTable, BackGround, IconSize, id2image, DrawIV);
            PlayerNext = new Dictionary<Player, Player>();
            players = new List<Player>();
            PlayerId = new Dictionary<Player, int>();
            Name = name;
            endTurn = false;
            winner = null;
            History = new LinkedList<CAction[]>();
            IdStr = idStr;
        }
        public void AddPlayer(Player player)
        {
            if (NowPlayer == null) NowPlayer = player;
            players.Add(player);
            PlayerId[player] = players.Count;
            if (players.Count > 1)
                PlayerNext[players[players.Count - 2]] = player;
            PlayerNext[player] = players[0];
            AfterAddPlayer(player);
        }
        public virtual void StartGame()
        {
            NowLegalActions = GetLegalActions(NowPlayer);
            PlayerPrepare();
        }
        public CAction[] LastAction() => ChessTable.actionLast;
        public int Width { get => ChessTable.Width; }
        public int Height { get => ChessTable.Height; }
        public Size IconSize { get => GridDrawer.IconSize; }
        protected virtual void AfterAddPlayer(Player player) { }
        public bool NextTurn()
        {
            if (winner != null) return false;
            NowPlayer.Go(this, NowLegalActions);
            if (endTurn)
            {
                LastPlayer = NowPlayer;
                NowPlayer = PlayerNext[NowPlayer];
                NowLegalActions = GetLegalActions(NowPlayer);
                endTurn = false;
                return true;
            }
            return false;
        }
        public void PlayerPrepare()
        {
            NowPlayer.PrePare(this, NowLegalActions);
        }
        public Image GetImage(bool reVerse = false, Dictionary<Point, string> data = null) => GridDrawer.GetImage(true, reVerse, data);
        public bool ApplyAction(CAction[] action)
        {
            CAction[] i = CAction.Find(NowLegalActions, action);
            if (i == null) return false;
            action = BeforeApply(ChessTable, i);
            ChessTable.Apply(action);
            endTurn = true;
            NowLegalActions = null;
            GC.Collect();
            AfterApplied(action);
            return true;
        }
        public virtual CAction[] BeforeApply(ChessTable chessTable, CAction[] action)
        {
            return action;
        }
        protected virtual void AfterApplied(CAction[] action)
        {
            History.AddLast(action);
        }
        public virtual IEnumerable<CAction[]> GetLegalActions(Player player)
        {
            return new LinkedList<CAction[]>();
        }
        protected void FinishGame(Player winner)
        {
            this.winner = winner;
        }
        public virtual Image GetPlayerIcon(Player player)
        {
            return GridDrawer.id2Img[PlayerId[player]];
        }
        public CAction[] GetAction(Point[] points, Player player)
        {
            if (points.Length == 1) return CAction.Add(ChessTable[points[0]], PlayerId[player]);
            if (points.Length == 2) return CAction.Move(ChessTable[points[0]], ChessTable[points[1]]);
            throw new Exception("Error action!");
        }
        public Point GetClick(Point point) => GridDrawer.GetClick(point);
        public ChessTable GetChessTable()
        {
            return ChessTable.Clone();
        }
        protected void InitTable(Dictionary<Point, int> p2i)
        {
            CAction[] re = new CAction[p2i.Count];
            int i = 0;
            foreach (Point p in p2i.Keys)
            {
                re[i++] = new CAction(ChessTable[p], p2i[p]);
            }
            ChessTable.Apply(re);
            ChessTable.actionLast = null;
        }
        public string ChessLocToStr(Point loc)
        {
            return ((char)(loc.X + 'a')).ToString() + (Height - loc.Y).ToString();
        }
        public void WithDraw()
        {
            if (History.Count >= players.Count)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    ChessTable.Withdraw(History.Last.Value);
                    History.RemoveLast();
                }
                NowLegalActions = GetLegalActions(NowPlayer);
                PlayerPrepare();
            }
        }

        public virtual string GetActionInf(CAction[] cActions)
        {
            if (cActions.Length == 1)
                if (cActions[0].from == ChessBlock.DEFID)
                    return ChessLocToStr(cActions[0].Loc) + " 下 " + IdStr[cActions[0].to];
                else
                    return ChessLocToStr(cActions[0].Loc) + IdStr[cActions[0].from] + " 换为 " + IdStr[cActions[0].to];
            if (cActions.Length == 2)
            {
                if (cActions[0].from == ChessBlock.DEFID && cActions[1].to == ChessBlock.DEFID)
                    return ChessLocToStr(cActions[1].Loc) + IdStr[cActions[1].from] + " 移动到 " + ChessLocToStr(cActions[0].Loc);
                if (cActions[0].to == cActions[1].from && cActions[1].to == ChessBlock.DEFID)
                    return ChessLocToStr(cActions[1].Loc) + IdStr[cActions[1].from] + " 吃 " + ChessLocToStr(cActions[0].Loc) + IdStr[cActions[0].from];
            }
            throw new Exception("No Inf!");

        }

        public virtual IEnumerable<CAction[]> GetMoves(Point chessPoint, Player player)
        {
            return new CAction[][] { CAction.Add(ChessTable[chessPoint], PlayerId[player]) };
        }
    }
    public class Player
    {
        public readonly int Id;
        public static int PlayerCount = 0;
        public readonly string Name;

        public Player(string name)
        {
            Id = ++PlayerCount;
            Name = name + "_" + Id.ToString();
        }
        public virtual void Go(ChessGame game, IEnumerable<CAction[]> legalActions)
        {
            game.ApplyAction(legalActions.First());
        }
        public virtual void PrePare(ChessGame game, IEnumerable<CAction[]> legalActions) { }
        public virtual void TurnEnded(int ticks)//100ms
        {

        }
    }
    public class UniversalAI : Player
    {
        private IAIControl AIControl;
        private int depth;
        public const int ParaCore = 8;
        public Dictionary<Point, string> data;
        private int lastTicks = 10;
        public UniversalAI(IAIControl aIControl, int deptht) : base("AI")
        {
            AIControl = aIControl;
            depth = deptht;
        }
        private CAction[] GetBestMove(Player player, ChessTable blocks, out int rate, int dep, int maxn, bool para = false, bool drawData = false)
        {
            IEnumerable<CAction[]> actions = AIControl.GetLimitedActions(blocks, player);
            rate = int.MinValue;
            if (!actions.Any()) return null;
            CAction[] re = actions.First();
            if (para)
            {
                CAction[][] ac = actions.ToArray();
                int l = Math.Min(ParaCore, ac.Length);
                Dictionary<int, CAction[]> res = new Dictionary<int, CAction[]>();
                Parallel.For(0, l, (j) =>
                {
                    ChessTable blocks2 = blocks.Clone();
                    int t = 0;
                    CAction[] ree = re;
                    int r = int.MinValue;
                    for (int i = j; i < ac.Length; i += ParaCore)
                    {
                        CAction[] cc = AIControl.BeforeApply(blocks2, ac[i]);
                        blocks2.Apply(cc);
                        if (dep == 0) t = AIControl.Envaluate(blocks2, player);
                        else
                        {
                            if (GetBestMove(AIControl.NextPlayer(player), blocks2, out t, dep - 1, -r - 1) == null)
                            {
                                if (t == int.MinValue)
                                {
                                    t = AIControl.Envaluate(blocks2, player) * dep;
                                }
                                else
                                {
                                    blocks2.Withdraw(cc);
                                    continue;
                                }
                            }
                            else t = -t;
                        }
                        if (r < t)
                        {
                            r = t;
                            ree = ac[i];
                        }
                        blocks2.Withdraw(cc);
                        if (drawData)
                            data[cc[0].Loc] = t.ToString();
                    }
                    res[r] = ree;
                });
                rate = res.Keys.Max();
                return res[rate];
            }
            else
            {
                int t = 0;
                foreach (CAction[] c in actions)
                {
                    CAction[] cc = AIControl.BeforeApply(blocks, c);
                    blocks.Apply(cc);
                    if (dep == 0) t = AIControl.Envaluate(blocks, player);
                    else
                    {
                        if (GetBestMove(AIControl.NextPlayer(player), blocks, out t, dep - 1, -rate - 1) == null)
                        {
                            if (t == int.MinValue)
                            {
                                t = AIControl.Envaluate(blocks, player) * dep;
                            }
                            else
                            {
                                blocks.Withdraw(cc);
                                continue;
                            }
                        }
                        else t = -t;
                    }
                    if (rate < t)
                    {
                        rate = t;
                        if (t > maxn)
                        {
                            blocks.Withdraw(cc);
                            return null;
                        }
                        re = c;
                    }
                    blocks.Withdraw(cc);
                    if (drawData)
                        data[cc[0].Loc] = t.ToString();
                }
                
                return re;
            }

        }
        public override void Go(ChessGame game, IEnumerable<CAction[]> legalActions)
        {
            data = new Dictionary<Point, string>();
            CAction[] act;
            if (legalActions.Count() == 1)
                act = legalActions.First();
            else
            {
                act = GetBestMove(this, AIControl.GetChessTable(), out int rate, depth, int.MaxValue, true);
                Console.WriteLine(Name + " rate=" + rate.ToString());
            }
            game.ApplyAction(act);
        }
        public override void TurnEnded(int ticks)
        {
            int r = (lastTicks + ticks * 2) / 3;
            if (r > 40) depth--;
            else if (r < 5) depth++;
            lastTicks = ticks;
            Console.WriteLine(Name + " depth=" + depth.ToString());
            base.TurnEnded(ticks);
        }
    }
    public interface IAIControl
    {
        ChessTable GetChessTable();
        int Envaluate(ChessTable chessTable, Player player);
        IEnumerable<CAction[]> GetLimitedActions(ChessTable chessTable, Player player);
        Player NextPlayer(Player player);
        CAction[] BeforeApply(ChessTable chessTable, CAction[] action);
    }
    public interface IActionInf
    {
        string GetActionInf(CAction[] cActions);
        IEnumerable<CAction[]> GetMoves(Point chessPoint, Player player);
    }
    public class WZChess : ChessGame, IAIControl
    {
        public static readonly Size DEFBlockSizeW = new Size(35, 35);
        public static readonly Size DEFChessSizeW = new Size(30, 30);
        public static readonly Point DEFDrawIvW = new Point(20, 20);
        private Dictionary<int, int[]> winPattern;
        private Dictionary<ChessBlock, CAction[][]> addAction;
        public PowerCounter powerCounter;
        public WZChess() : base("WZChess", 19, 19, PMath.towords, null, DEFBlockSizeW, new Dictionary<int, Image>
        {
            [1] = IconMaker.DEFChess(DEFChessSizeW, Color.White).bitmap,
            [2] = IconMaker.DEFChess(DEFChessSizeW, Color.Black).bitmap
        }, new int[] { 1, 2 }, DEFDrawIvW,new Dictionary<int, string> { [1] = "白子", [2]="黑子"} ,true)
        {
            winPattern = new Dictionary<int, int[]>
            {
                [1] = new int[] { 1, 1, 1, 1, 1 },
                [2] = new int[] { 2, 2, 2, 2, 2 }
            };
            addAction = new Dictionary<ChessBlock, CAction[][]>();
        }
        public override void StartGame()
        {
            foreach (ChessBlock c in ChessTable.id2chess[0])
            {
                addAction[c] = new CAction[][] { CAction.Add(c, 1), CAction.Add(c, 2) };
            }
            Dictionary<int[], int>[] PowerLib = new Dictionary<int[], int>[] { GetPower(1), GetPower(2) };
            Powers = PowerLib[0].Values.ToArray();
            List<int[]> mp = GetPower(1).Keys.ToList();
            mp.AddRange(GetPower(2).Keys);
            powerCounter = new PowerCounter(mp.ToArray(), new int[] { ChessBlock.NONEID, ChessBlock.DEFID, 1, 2 });
            base.StartGame();
        }
        public override IEnumerable<CAction[]> GetLegalActions(Player player)
        {
            LinkedList<CAction[]> re = new LinkedList<CAction[]>();
            foreach (ChessBlock c in ChessTable.id2chess[0])
            {
                re.AddLast(addAction[c][PlayerId[player] - 1]);
            }
            return re;
        }
        protected override void AfterApplied(CAction[] action)
        {
            if (ChessTable.FindPatten(winPattern[PlayerId[NowPlayer]], out _) != null) FinishGame(NowPlayer);
            base.AfterApplied(action);
        }
        private int[] Powers;
        private Dictionary<int[], int> GetPower(int x) => new Dictionary<int[], int>
        {
            [new int[] { x, x, x, x, x }] = 256,
            [new int[] { x, x, x, x, 0 }] = 32,
            [new int[] { x, x, x, 0 }] = 15,
            [new int[] { x, x, 0, x, 0 }] = 14,
            [new int[] { x, 0, x, x, 0 }] = 14,
            [new int[] { x, x, 0 }] = 1,
        };

        public int Envaluate(ChessTable chessTable, Player player)
        {
            int[] r = powerCounter.CountPatterns(chessTable);
            int re = 0;
            if (PlayerId[player] == 1)
            {
                for (int i = 0; i < Powers.Length; i++)
                {
                    re += (r[i] - r[i + Powers.Length] * 3) * Powers[i];
                }
            }
            else
            {
                for (int i = 0; i < Powers.Length; i++)
                {
                    re += (r[i + Powers.Length] - r[i] * 3) * Powers[i];
                }
            }
            return re;
            /*
            int re = 0, id = PlayerId[player]-1, oid = PlayerId[PlayerNext[player]]-1;
            for(int i=0;i<Powers.Length;i++)
            {
                re += (chessTable.CountPatten(Patterns[id][i]) - chessTable.CountPatten(Patterns[oid][i])*3) * Powers[i];
            }
            return re;*/
        }

        public IEnumerable<CAction[]> GetLimitedActions(ChessTable chessTable, Player player)
        {
            LinkedList<CAction[]> re = new LinkedList<CAction[]>();
            if (chessTable.actionLast == null) re.AddLast(CAction.Add(chessTable[new Point(9, 9)], PlayerId[player]));
            else
            {
                CAction last = chessTable.actionLast[0];
                if (last.to == ChessBlock.DEFID || chessTable.FindPatten(winPattern[last.to], out _) == null)
                {
                    HashSet<ChessBlock> cs = chessTable.GetAroundNearFreeStep();
                    foreach (ChessBlock c in cs)
                    {
                        re.AddLast(CAction.Add(c, PlayerId[player]));
                    }
                }
            }
            return re;
        }
    }
    public class BWChess : ChessGame, IAIControl
    {
        public BWChess() : base("BWChess", 8, 8, PMath.towords, null, DEFBlockSize,
            new Dictionary<int, Image>
            {
                [2] = IconMaker.DEFChess(DEFChessSize, Color.White).bitmap,
                [1] = IconMaker.DEFChess(DEFChessSize, Color.Black).bitmap
            }, new int[] { 1, 2 }, DEFDrawIv,new Dictionary<int, string> { [1] = "黑子", [2]="白子"}, true)
        {

        }
        public static readonly Dictionary<Point, int> p2i = new Dictionary<Point, int>()
        {
            [new Point(3, 3)] = 1,
            [new Point(4, 4)] = 1,
            [new Point(3, 4)] = 2,
            [new Point(4, 3)] = 2,
        };

        public override void StartGame()
        {
            InitTable(p2i);
            base.StartGame();
        }
        public int Envaluate(ChessTable chessTable, Player player)
        {
            int a = chessTable.id2chess[PlayerId[player]].Count, b = chessTable.id2chess[PlayerId[PlayerNext[player]]].Count;
            if (a == 0) return -100;
            if (b == 0) return 100;
            return a - b;
        }
        public override IEnumerable<CAction[]> GetLegalActions(Player player)
        {
            return GetLimitedActions(ChessTable, player);
        }

        public IEnumerable<CAction[]> GetLimitedActions(ChessTable chessTable, Player player)
        {
            HashSet<ChessBlock> blocks = chessTable.GetAroundNearFreeStep();
            LinkedList<CAction[]> actions = new LinkedList<CAction[]>();
            int oid = PlayerId[PlayerNext[player]];
            foreach (ChessBlock block in blocks)
            {
                if (HaveCross(block, oid))
                    actions.AddLast(CAction.Add(block, PlayerId[player]));
            }
            return actions;
        }
        public override CAction[] BeforeApply(ChessTable chessTable, CAction[] action)
        {
            IEnumerable<ChessBlock> blocks = GetCross(chessTable[action[0].Loc], 3 - action[0].to, out int count);
            CAction[] re = new CAction[1 + count];
            re[0] = action[0];
            int i = 1;
            foreach (ChessBlock block in blocks)
                re[i++] = new CAction(block, action[0].to);
            return re;
        }
        public IEnumerable<ChessBlock> GetCross(ChessBlock chess, int id, out int count)
        {
            IEnumerable<ChessBlock> re = new ChessBlock[0];
            count = 0;
            int oid = 3 - id;
            for (int i = 0; i < 8; i++)
            {
                LinkedList<ChessBlock> ree = new LinkedList<ChessBlock>();
                ChessBlock chess2 = chess.Surround[i];
                while (true)
                {
                    if (chess2.Id == id)
                    {
                        ree.AddLast(chess2);
                    }
                    else if (chess2.Id == oid)
                    {
                        re = re.Concat(ree);
                        count += ree.Count;
                        break;
                    }
                    else
                        break;
                    chess2 = chess2.Surround[i];
                }
            }
            return re;
        }
        public bool HaveCross(ChessBlock chess, int id)
        {
            int oid = 3 - id;
            for (int i = 0; i < 8; i++)
            {
                ChessBlock chess2 = chess.Surround[i];
                int s = 0;
                while (true)
                {
                    if (chess2.Id == id)
                    {
                        s++;
                    }
                    else if (chess2.Id == oid)
                    {
                        if (s > 0) return true;
                        break;
                    }
                    else
                        break;
                    chess2 = chess2.Surround[i];
                }
            }
            return false;
        }
        protected override void AfterApplied(CAction[] action)
        {
            int v = Envaluate(ChessTable, NowPlayer);
            NowLegalActions = GetLegalActions(PlayerNext[NowPlayer]);
            if (!NowLegalActions.Any() || v == 100 || v == -100) FinishGame(v > 0 ? NowPlayer : PlayerNext[NowPlayer]);
            base.AfterApplied(action);
        }
        public override string GetActionInf(CAction[] cActions)
        {
            return base.GetActionInf(new CAction[] { cActions[0] });
        }
    }
    public class RuledChessGame : ChessGame, IAIControl
    {
        public delegate IEnumerable<CAction[]> MoveRuleGetHandler(ChessBlock chessBlock, int pid, int t, RuledChessGame game);
        public delegate int ChessPowerBonus(ChessBlock chess, int pid, int t);
        public const int PlayerIdIv = 27;
        protected Dictionary<Player, int[]> PlayersChessId;
        public readonly MoveRuleGetHandler GetMove;
        public readonly ChessPowerBonus GetPowerBonus;
        protected int[] Types;
        protected Dictionary<int, int> IdsDEFPower;
        private int[] typwer;
        public static readonly Size DEFBlockSizeR = new Size(80, 80);
        public static readonly Size DEFChessSizeR = new Size(70, 70);
        public readonly int[] GeneralT;
        public RuledChessGame(string name, int width, int height, Point[] initDirections, MoveRuleGetHandler moveRule, Image table,
            Dictionary<int, Image>[] type2Image, int[] types, int playerCount, Dictionary<int, string>[] typesStr,
            int[] typesPower, ChessPowerBonus powerBonus, int generalT = 0)
            : base(name, width, height, initDirections, table, DEFBlockSizeR, ToId2Img(type2Image), GetinitIds(types, playerCount), DEFDrawIv,GetIdStr(typesStr), false)
        {
            Types = types;
            GetMove = moveRule;
            GetPowerBonus = powerBonus;
            PlayersChessId = new Dictionary<Player, int[]>();
            typwer = typesPower;
            IdsDEFPower = new Dictionary<int, int>();
            if (generalT != 0) GeneralT = new int[] { GetChessId(1, generalT), GetChessId(2, generalT) };
            
        }
        public static Dictionary<int,string> GetIdStr(Dictionary<int, string>[] typesStr)
        {
            Dictionary<int, string> re = new Dictionary<int, string>();
            for (int p = 0; p < 2; p++)
            {
                foreach (int t in typesStr[p].Keys)
                {
                    re[GetChessId(p + 1, t)] = typesStr[p][t];
                }
            }
            return re;
        }
        public override IEnumerable<CAction[]> GetLegalActions(Player player)
        {
            return GetLimitedActions(ChessTable, player);
        }
        protected override void AfterAddPlayer(Player player)
        {
            PlayersChessId[player] = new int[Types.Length];
            int p = PlayerId[player];
            for (int i = 0; i < Types.Length; i++)
            {
                int id = GetChessId(p, Types[i]);
                PlayersChessId[player][i] = id;
                IdsDEFPower[id] = p == 1 ? typwer[i] : -typwer[i];
            }
        }
        public static int[] GetinitIds(int[] types, int playerCount)
        {
            int[] re = new int[types.Length * playerCount];
            for (int p = 0; p < playerCount; p++)
            {
                for (int t = 0; t < types.Length; t++)
                {
                    re[p * types.Length + t] = GetChessId(p + 1, types[t]);
                }
            }
            return re;
        }
        public static int GetPlayerId(int chessId) => chessId >> PlayerIdIv;
        public static int GetType(int chessId) => chessId & ((1 << PlayerIdIv) - 1);
        public static int GetChessId(int pid, int t) => t | ((pid) << PlayerIdIv);
        public static Dictionary<int, Image> ToId2Img(Dictionary<int, Image>[] map)
        {
            Dictionary<int, Image> re = new Dictionary<int, Image>();
            for (int p = 0; p < map.Length; p++)
            {
                foreach (int t in map[p].Keys)
                    re[GetChessId(p + 1, t)] = map[p][t];
            }
            return re;
        }
        public int Envaluate(ChessTable chessTable, Player player)
        {
            int re = 0, pid = PlayerId[player];
            foreach (int i in chessTable.id2chess.Keys)
            {
                if (i == ChessBlock.DEFID) continue;
                int t = GetType(i);
                foreach (ChessBlock chess in chessTable.id2chess[i])
                    re += IdsDEFPower[i] + GetPowerBonus(chess, pid, t) * (pid == 1 ? 1 : -1);
            }
            if (pid == 2) return -re;
            return re;
        }

        public static ChessBlock GetLineEnd(ChessBlock chessBlock, int opid, int d)
        {
            ChessBlock chess = chessBlock.Surround[d];
            while (chess.Id == ChessBlock.DEFID)
            {
                chess = chess.Surround[d];
            }
            if (GetPlayerId(chess.Id) == opid) return chess;
            return null;
        }
        public static LinkedList<ChessBlock> GetLine(ChessBlock chessBlock, int opid, int d)
        {
            LinkedList<ChessBlock> re = new LinkedList<ChessBlock>();
            ChessBlock chess = chessBlock.Surround[d];
            while (chess.Id == ChessBlock.DEFID)
            {
                re.AddLast(chess);
                chess = chess.Surround[d];
            }
            if (GetPlayerId(chess.Id) == opid) re.AddLast(chess);
            return re;
        }
        public IEnumerable<CAction[]> GetLimitedActions(ChessTable chessTable, Player player)
        {
            IEnumerable<CAction[]> re = new CAction[0][];
            int p = PlayerId[player];
            if (GeneralT != null && chessTable.id2chess[GeneralT[p - 1]].Count == 0) return re;
            foreach (int t in PlayersChessId[player])
            {
                int t2 = GetType(t);
                foreach (ChessBlock c in chessTable.id2chess[t])
                {
                    re = re.Concat(GetMove(c, p, t2, this));
                }
            }
            return re;
        }
        public override Image GetPlayerIcon(Player player)
        {
            return GridDrawer.id2Img[GetChessId(PlayerId[player], Types[0])];
        }
        

        public override IEnumerable<CAction[]> GetMoves(Point chessPoint, Player player)
        {
            ChessBlock chessBlock = ChessTable[chessPoint];
            int p = GetPlayerId(chessBlock.Id);
            if (p == PlayerId[player])
            {
                return GetMove(chessBlock, p, GetType(chessBlock.Id), this);
            }
            return new CAction[0][];
        }
    }
    public class InternationalChess : RuledChessGame
    {
        public static readonly Point[] chessStep = new Point[] { new Point(0, 1), new Point(-1, 0), new Point(1, 0), new Point(0, -1),
             new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
             new Point(-2, -1), new Point(2, 1), new Point(-2, 1), new Point(2, -1), new Point(-1, -2), new Point(1, 2), new Point(1, -2), new Point(-1, 2)};
        public const int xiv = -30, yiv = -5;
        public static readonly Color w = Color.White, b = Color.FromArgb(32, 32, 32);
        public static readonly Dictionary<int, Image>[] type2img = new Dictionary<int, Image>[]
        {
            new Dictionary<int, Image>()
            {
                [1] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♙",Brushes.Black, xiv, yiv)).bitmap,
                [2] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♘",Brushes.Black, xiv, yiv)).bitmap,
                [3] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♗",Brushes.Black, xiv, yiv)).bitmap,
                [4] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♖",Brushes.Black, xiv, yiv)).bitmap,
                [5] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♕",Brushes.Black, xiv, yiv)).bitmap,
                [6] = (IconMaker.DEFChess(DEFChessSizeR, w)+new IconMaker.Icon("♔",Brushes.Black, xiv, yiv)).bitmap,
            },
            new Dictionary<int, Image>()
            {
                [1] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♙",Brushes.White, xiv, yiv)).bitmap,
                [2] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♘",Brushes.White, xiv, yiv)).bitmap,
                [3] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♗",Brushes.White, xiv, yiv)).bitmap,
                [4] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♖",Brushes.White, xiv, yiv)).bitmap,
                [5] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♕",Brushes.White, xiv, yiv)).bitmap,
                [6] = (IconMaker.DEFChess(DEFChessSizeR, b)+new IconMaker.Icon("♔",Brushes.White, xiv, yiv)).bitmap,
            }
        };
        public static readonly Dictionary<Point, int> InitT = new Dictionary<Point, int>
        {
            [new Point(0, 0)] = GetChessId(2, 4),
            [new Point(1, 0)] = GetChessId(2, 2),
            [new Point(2, 0)] = GetChessId(2, 3),
            [new Point(3, 0)] = GetChessId(2, 5),
            [new Point(4, 0)] = GetChessId(2, 6),
            [new Point(5, 0)] = GetChessId(2, 3),
            [new Point(6, 0)] = GetChessId(2, 2),
            [new Point(7, 0)] = GetChessId(2, 4),

            [new Point(0, 1)] = GetChessId(2, 1),
            [new Point(1, 1)] = GetChessId(2, 1),
            [new Point(2, 1)] = GetChessId(2, 1),
            [new Point(3, 1)] = GetChessId(2, 1),
            [new Point(4, 1)] = GetChessId(2, 1),
            [new Point(5, 1)] = GetChessId(2, 1),
            [new Point(6, 1)] = GetChessId(2, 1),
            [new Point(7, 1)] = GetChessId(2, 1),

            [new Point(0, 7)] = GetChessId(1, 4),
            [new Point(1, 7)] = GetChessId(1, 2),
            [new Point(2, 7)] = GetChessId(1, 3),
            [new Point(3, 7)] = GetChessId(1, 5),
            [new Point(4, 7)] = GetChessId(1, 6),
            [new Point(5, 7)] = GetChessId(1, 3),
            [new Point(6, 7)] = GetChessId(1, 2),
            [new Point(7, 7)] = GetChessId(1, 4),

            [new Point(0, 6)] = GetChessId(1, 1),
            [new Point(1, 6)] = GetChessId(1, 1),
            [new Point(2, 6)] = GetChessId(1, 1),
            [new Point(3, 6)] = GetChessId(1, 1),
            [new Point(4, 6)] = GetChessId(1, 1),
            [new Point(5, 6)] = GetChessId(1, 1),
            [new Point(6, 6)] = GetChessId(1, 1),
            [new Point(7, 6)] = GetChessId(1, 1),
        };
        public static readonly Dictionary<Point, int> InitTT = new Dictionary<Point, int>
        {
            [new Point(4, 0)] = GetChessId(2, 6),
            [new Point(5, 4)] = GetChessId(2, 1),

            [new Point(0, 7)] = GetChessId(1, 4),
            [new Point(1, 7)] = GetChessId(1, 2),
            [new Point(2, 7)] = GetChessId(1, 3),
            [new Point(3, 7)] = GetChessId(1, 5),
            [new Point(4, 7)] = GetChessId(1, 6),
            [new Point(5, 7)] = GetChessId(1, 3),
            [new Point(6, 7)] = GetChessId(1, 2),
            [new Point(7, 7)] = GetChessId(1, 4),

            [new Point(0, 6)] = GetChessId(1, 1),
            [new Point(1, 6)] = GetChessId(1, 1),
            [new Point(2, 6)] = GetChessId(1, 1),
            [new Point(3, 6)] = GetChessId(1, 1),
            [new Point(4, 6)] = GetChessId(1, 1),
            [new Point(5, 6)] = GetChessId(1, 1),
            [new Point(6, 6)] = GetChessId(1, 1),
            [new Point(7, 6)] = GetChessId(1, 1),
        };

        public static readonly Dictionary<int, int[]> MoveDirIs = new Dictionary<int, int[]>
        {
            [-1] = new int[] { 0, 6, 7 },
            [1] = new int[] { 3, 4, 5 },
            [2] = new int[] { 8, 9, 10, 11, 12, 13, 14, 15 },
            [3] = new int[] { 4, 5, 6, 7 },
            [4] = new int[] { 0, 1, 2, 3 },
            [5] = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 },
            [6] = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 },
        };
        public static readonly Dictionary<int, string>[] typestr = new Dictionary<int, string>[]
        {
            new Dictionary<int, string>
            {
                [1]="兵",
                [2]="马",
                [3]="象",
                [4]="车",
                [5]="后",
                [6]="王"
            },
            new Dictionary<int, string>
            {
                [1]="兵",
                [2]="马",
                [3]="象",
                [4]="车",
                [5]="后",
                [6]="王"
            }
        };
        //                                               P, N, B, R, Q, K
        public static readonly int[] types = new int[] { 1, 2, 3, 4, 5, 6 };
        public static readonly int[] tyPr = new int[] { 100, 325, 350, 500, 975, 20000 };
        public static readonly Dictionary<int, int[]> MobilityBonus = new Dictionary<int, int[]>
        {
            [2] = new int[] { -75, -57, -9, -2, 6, 14, 22, 29, 36 },
            [3] = new int[] { -48, -20, 16, 26, 38, 51, 55, 63, 63, 68, 81, 81, 91, 98 },
            [4] = new int[] { -58, -27, -15, -10, -5, -2, 9, 16, 30, 29, 32, 38, 46, 52, 55 },
            [5] = new int[] { -39, -21, 3, 3, 14, 22, 28, 41, 43, 48, 56, 60, 60, 66, 67, 70, 71, 73, 79, 88, 88, 99, 102, 102, 106, 109, 113, 116 }
        };
        public static readonly Dictionary<int, int[,]> PositionBonus = new Dictionary<int, int[,]>
        {
            [1] = new int[8, 8] {
                { 0, 0, 0, 0, 0, 0, 0, 0 },
                { 50, 50, 50, 50, 50, 50, 50, 50 },
                { 10, 10, 20, 30, 30, 20, 10, 10 },
                { 5, 5, 10, 25, 25, 10, 5, 5 },
                { 0, 0, 0, 20, 20, 0, 0, 0 },
                { 5, -5, -10, 0, 0, -10, -5, 5 },
                { 5, 10, 10, -20, -20, 10, 10, 5 },
                { 0, 0, 0, 0, 0, 0, 0, 0 } },
            [2] = new int[8, 8]{
                {-50, -40, -30, -30, -30, -30, -40, -50},
                {-40, -20, 0, 0, 0, 0, -20, -40},
                {-30, 0, 10, 15, 15, 10, 0, -30},
                {-30, 5, 15, 20, 20, 15, 5, -30},
                {-30, 0, 15, 20, 20, 15, 0, -30},
                {-30, 5, 10, 15, 15, 10, 5, -30},
                {-40, -20, 0, 5, 5, 0, -20, -40},
                {-50, -40, -30, -30, -30, -30, -40, -50}},
            [3] = new int[8, 8]{
                {-20, -10, -10, -10, -10, -10, -10, -20},
                {-10, 0, 0, 0, 0, 0, 0, -10},
                {-10, 0, 5, 10, 10, 5, 0, -10},
                {-10, 5, 5, 10, 10, 5, 5, -10},
                {-10, 0, 10, 10, 10, 10, 0, -10},
                {-10, 10, 10, 10, 10, 10, 10, -10},
                {-10, 5, 0, 0, 0, 0, 5, -10},
                {-20, -10, -10, -10, -10, -10, -10, -20}},
            [4] = new int[8, 8]{
                {0, 0, 0, 0, 0, 0, 0, 0},
                {5, 10, 10, 10, 10, 10, 10, 5},
                {-5, 0, 0, 0, 0, 0, 0, -5},
                {-5, 0, 0, 0, 0, 0, 0, -5},
                {-5, 0, 0, 0, 0, 0, 0, -5},
                {-5, 0, 0, 0, 0, 0, 0, -5},
                {-5, 0, 0, 0, 0, 0, 0, -5},
                { 0, 0, 0, 5, 5, 0, 0, 0}},
            [5] = new int[8, 8]{
                {-20, -10, -10, -5, -5, -10, -10, -20},
                {-10, 0, 0, 0, 0, 0, 0, -10},
                {-10, 0, 5, 5, 5, 5, 0, -10},
                {-5, 0, 5, 5, 5, 5, 0, -5},
                {0, 0, 5, 5, 5, 5, 0, -5},
                {-10, 5, 5, 5, 5, 5, 0, -10},
                {-10, 0, 5, 0, 0, 0, 0, -10},
                {-20, -10, -10, -5, -5, -10, -10, -20}},
            [6] = new int[8, 8]{
                {-30, -40, -40, -50, -50, -40, -40, -30},
                {-30, -40, -40, -50, -50, -40, -40, -30},
                {-30, -40, -40, -50, -50, -40, -40, -30},
                {-30, -40, -40, -50, -50, -40, -40, -30},
                {-20, -30, -30, -40, -40, -30, -30, -20},
                {-10, -20, -20, -20, -20, -20, -20, -10},
                {20, 20, 0, 0, 0, 0, 20, 20},
                {20, 30, 10, 0, 0, 10, 30, 20}},
        };
        public int[] CanSwap;
        public static ChessBlock IsVolnerable(ChessBlock chessBlock, int opid)
        {
            for (int i = 0; i < 8; i++)
            {
                ChessBlock c = chessBlock.Surround[i];
                if (c.Id != ChessBlock.DEFID && GetPlayerId(c.Id) == opid)
                {
                    int t = GetType(c.Id);
                    if (t == 6) return c;
                    if (t == 1)
                    {
                        if (opid == 1 && (i == 4 || i == 5)) return c;
                        if (opid == 2 && (i == 6 || i == 7)) return c;
                    }
                }
                c = GetLineEnd(chessBlock, opid, i);
                if (c != null)
                {
                    int t = GetType(c.Id);
                    if (t == 5) return c;
                    if (t == 3 && i > 3) return c;
                    if (t == 4 && i < 4) return c;
                }
            }
            for (int i = 8; i < 16; i++)
            {
                ChessBlock c = chessBlock.Surround[i];
                if (GetPlayerId(c.Id) == opid && GetType(c.Id) == 2)
                    return c;
            }
            return null;
        }

        public static int FindPid(IEnumerable<ChessBlock> chessBlocks, int pid)
        {
            foreach (ChessBlock chess in chessBlocks)
                if (GetPlayerId(chess.Id) == pid) return GetType(chess.Id);
            return 0;
        }
        protected override void AfterApplied(CAction[] action)
        {
            if (GetType(action[0].from) == 6)
                FinishGame(NowPlayer);
            else
            {
                int pid = GetPlayerId(action[0].to), t = GetType(action[0].to);
                if (CanSwap[pid - 1] > 0)
                {
                    if (t == 6) CanSwap[pid - 1] = 0;
                    else if (t == 4)
                    {
                        if (action[1].Loc.X == 0) CanSwap[pid - 1] &= 1;
                        else if (action[1].Loc.X == 7) CanSwap[pid - 1] &= 2;
                    }
                }
            }
            base.AfterApplied(action);
        }
        private static LinkedList<CAction[]> Getmove(ChessBlock chessBlock, int pid, int t, RuledChessGame game)
        {
            if (pid == 2 && t == 1) t = -1;
            LinkedList<CAction[]> re = new LinkedList<CAction[]>();
            switch (t)
            {
                case -1:
                case 1:
                    int d = MoveDirIs[t][0];
                    ChessBlock c2 = chessBlock.Surround[d];
                    if (c2.Id == ChessBlock.DEFID)
                    {
                        if (((chessBlock.Loc.Y == 6 && pid == 1) || (chessBlock.Loc.Y == 1 && pid == 2)) && c2.Surround[d].Id == ChessBlock.DEFID)
                        {
                            re.AddLast(CAction.Move(chessBlock, c2.Surround[d]));
                            int d2 = MoveDirIs[t][1];
                            if (GetPlayerId(c2.Surround[d2].Id) == 3 - pid && GetType(c2.Surround[d2].Id) == 1)
                                re.AddLast(new CAction[] { new CAction(c2.Surround[d], chessBlock.Id), new CAction(chessBlock, ChessBlock.DEFID), new CAction(c2.Surround[d2], ChessBlock.DEFID) });
                            d2 = MoveDirIs[t][2];
                            if (GetPlayerId(c2.Surround[d2].Id) == 3 - pid && GetType(c2.Surround[d2].Id) == 1)
                                re.AddLast(new CAction[] { new CAction(c2.Surround[d], chessBlock.Id), new CAction(chessBlock, ChessBlock.DEFID), new CAction(c2.Surround[d2], ChessBlock.DEFID) });
                        }
                        if ((chessBlock.Loc.Y == 1 && pid == 1) || (chessBlock.Loc.Y == 6 && pid == 2))
                        {
                            re.AddLast(new CAction[] { new CAction(c2, GetChessId(pid, 2)), new CAction(chessBlock, ChessBlock.DEFID) });
                            re.AddLast(new CAction[] { new CAction(c2, GetChessId(pid, 5)), new CAction(chessBlock, ChessBlock.DEFID) });
                        }
                        else
                            re.AddLast(CAction.Move(chessBlock, c2));
                    }
                    d = MoveDirIs[t][1];
                    if (GetPlayerId(chessBlock.Surround[d].Id) == 3 - pid)
                        re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[d]));
                    d = MoveDirIs[t][2];
                    if (GetPlayerId(chessBlock.Surround[d].Id) == 3 - pid)
                        re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[d]));
                    return re;
                case 2:
                    foreach (int d2 in MoveDirIs[2])
                        if (chessBlock.Surround[d2].Id == ChessBlock.DEFID || GetPlayerId(chessBlock.Surround[d2].Id) == 3 - pid)
                            re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[d2]));
                    return re;
                case 6:
                    bool sw = false;
                    foreach (int d2 in MoveDirIs[6])
                        if (chessBlock.Surround[d2].Id == ChessBlock.DEFID || GetPlayerId(chessBlock.Surround[d2].Id) == 3 - pid)
                        {
                            re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[d2]));
                            sw = true;
                        }
                    if (sw)
                    {
                        InternationalChess cg = game as InternationalChess;
                        if (cg.CanSwap[pid - 1] > 0 && IsVolnerable(chessBlock, 3 - pid) == null)
                        {
                            if ((cg.CanSwap[pid - 1] & 1) > 0)
                            {
                                LinkedList<ChessBlock> cl = GetLine(chessBlock, pid, 2);
                                if (cl.Count == 3 && GetType(cl.Last.Value.Id) == 4)
                                {
                                    if (IsVolnerable(cl.First.Value, 3 - pid) == null && IsVolnerable(cl.First.Next.Value, 3 - pid) == null)
                                        re.AddLast(new CAction[] {new CAction(cl.First.Next.Value,GetChessId(pid,t)), new CAction(chessBlock, ChessBlock.DEFID),
                                    new CAction(cl.First.Value,GetChessId(pid,4)), new CAction(cl.Last.Value, ChessBlock.DEFID)});
                                }
                            }
                            if ((cg.CanSwap[pid - 1] & 2) > 0)
                            {
                                LinkedList<ChessBlock> cl = GetLine(chessBlock, pid, 1);
                                if (cl.Count == 4 && GetType(cl.Last.Value.Id) == 4)
                                {
                                    if (IsVolnerable(cl.First.Value, 3 - pid) == null && IsVolnerable(cl.First.Next.Value, 3 - pid) == null && IsVolnerable(cl.First.Next.Next.Value, 3 - pid) == null)
                                        re.AddLast(new CAction[] {new CAction(cl.First.Next.Value,GetChessId(pid,t)), new CAction(chessBlock, ChessBlock.DEFID),
                                    new CAction(cl.First.Value,GetChessId(pid,4)), new CAction(cl.Last.Value, ChessBlock.DEFID)});
                                }
                            }
                        }
                    }
                    return re;
                case 3:
                case 4:
                case 5:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re.AddLast(CAction.Move(chessBlock, chess));
                            chess = chess.Surround[i];
                        }
                        if (GetPlayerId(chess.Id) == 3 - pid) re.AddLast(CAction.Move(chessBlock, chess));
                    }
                    return re;
            }
            throw new Exception("No this type");
        }
        private static int Countmove(ChessBlock chessBlock, int pid, int t)
        {
            if (pid == 2 && t == 1) t = -1;
            int re = 0;
            switch (t)
            {
                case -1:
                case 1:
                    int d = MoveDirIs[t][0];
                    if (chessBlock.Surround[d].Id == ChessBlock.DEFID)
                    {
                        re++;
                        if (((chessBlock.Loc.Y == 6 && pid == 1) || (chessBlock.Loc.Y == 1 && pid == 2)) && chessBlock.Surround[d].Surround[d].Id == ChessBlock.DEFID)
                            re++;
                    }
                    d = MoveDirIs[t][1];
                    if (GetPlayerId(chessBlock.Surround[d].Id) == 3 - pid)
                        re++;
                    d = MoveDirIs[t][2];
                    if (GetPlayerId(chessBlock.Surround[d].Id) == 3 - pid)
                        re++;
                    return re;
                case 2:
                case 6:
                    foreach (int d2 in MoveDirIs[t])
                        if (chessBlock.Surround[d2].Id == ChessBlock.DEFID || GetPlayerId(chessBlock.Surround[d2].Id) == 3 - pid)
                            re++;
                    return re;
                case 3:
                case 4:
                case 5:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re++;
                            chess = chess.Surround[i];
                        }
                        if (GetPlayerId(chess.Id) == 3 - pid) re++;
                    }
                    return re;
            }
            throw new Exception("No this type");
        }
        private static int chessPowerBonus(ChessBlock chess, int pid, int t)
        {
            int re = 0;
            if (t != 1 && t != 6)
                re += MobilityBonus[t][Countmove(chess, pid, t)];
            if (pid == 1)
                re += PositionBonus[t][chess.Loc.X, chess.Loc.Y];
            else
                re += PositionBonus[t][chess.Loc.X, 7 - chess.Loc.Y];
            return re;
        }
        public static Image GetTable()
        {
            TableMaker.BlockInf w = new TableMaker.BlockInf(TableMaker.BlockMark.Box, Brushes.White), b = new TableMaker.BlockInf(TableMaker.BlockMark.Box, Brushes.Black);
            TableMaker.BlockInf[,] r = new TableMaker.BlockInf[8, 8];
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 8; y++)
                {
                    r[x, y] = ((x + y) % 2 == 0) ? w : b;
                }
            return TableMaker.GetTable(DEFBlockSizeR, r, DEFDrawIv.X);
        }
        public InternationalChess() : base("ITNChess", 8, 8, chessStep, Getmove, GetTable(), type2img, types, 2, typestr, tyPr, chessPowerBonus, 6)
        {
            CanSwap = new int[] { 3, 3 };
        }
        public override void StartGame()
        {
            InitTable(InitT);
            base.StartGame();
        }
        public override string GetActionInf(CAction[] cActions)
        {
            if (cActions.Length == 3)
                return ChessLocToStr(cActions[1].Loc) + IdStr[cActions[1].from] + " 吃过路兵 " + ChessLocToStr(cActions[2].Loc) + IdStr[cActions[2].from];
            if (cActions.Length == 4)
                return ChessLocToStr(cActions[1].Loc) + IdStr[cActions[1].from] + " 异位 " + ChessLocToStr(cActions[3].Loc) + IdStr[cActions[3].from];
            if (cActions.Length == 2 && cActions[0].to != cActions[1].from)
                return ChessLocToStr(cActions[1].Loc) + IdStr[cActions[1].from] + "升变 " + IdStr[cActions[0].to];
            return base.GetActionInf(cActions);
        }
    }
    public class ChineseChess : RuledChessGame
    {
        //                                              兵 马 象 炮 车 士 将
        public static readonly int[] types = new int[] { 1, 2, 3, 4, 5, 6, 7 };
        public static readonly int[] typesP = new int[] { 100, 400, 200, 500, 1000, 100, 10000 };
        public static readonly Point[] chessStep = new Point[] { new Point(0, 1), new Point(-1, 0), new Point(1, 0), new Point(0, -1),
             new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
             new Point(1, 2), new Point(-1, 2), new Point(-2, -1), new Point(-2, 1), new Point(2, 1), new Point(2, -1), new Point(-1, -2), new Point(1, -2),
             new Point(-2, -2), new Point(2, -2), new Point(-2, 2), new Point(2, 2)
        };
        public static readonly Dictionary<int, int[]> MoveDirIs = new Dictionary<int, int[]>
        {
            [-1] = new int[] { 0, 1, 2 },
            [1] = new int[] { 3, 1, 2 },
            [2] = new int[] { 8, 9, 10, 11, 12, 13, 14, 15 },
            [3] = new int[] { 16, 17, 18, 19 },
            [4] = new int[] { 0, 1, 2, 3 },
            [5] = new int[] { 0, 1, 2, 3 },
            [6] = new int[] { 4, 5, 6, 7 },
            [7] = new int[] { 0, 1, 2, 3 }
        };
        public static readonly Color TableColor = Color.BurlyWood, ChessColor = Color.FromArgb(228, 171, 80);
        public const int xiv = -17, yiv = 0;
        public static readonly Font SFont = new Font("楷体", 100, FontStyle.Bold);
        public static readonly Brush red = Brushes.DarkRed, black = Brushes.Black;
        public static readonly Dictionary<int, Image>[] chessImg = new Dictionary<int, Image>[]
        {
            new Dictionary<int, Image>()
            {
                [1] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("兵",red, xiv, yiv,SFont)).bitmap,
                [2] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("傌",red, xiv, yiv,SFont)).bitmap,
                [3] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("相",red, xiv, yiv,SFont)).bitmap,
                [4] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("炮",red, xiv, yiv,SFont)).bitmap,
                [5] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("俥",red, xiv, yiv,SFont)).bitmap,
                [6] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("仕",red, xiv, yiv,SFont)).bitmap,
                [7] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("帥",red, xiv, yiv,SFont)).bitmap,
            },
            new Dictionary<int, Image>()
            {
                [1] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("卒",black, xiv, yiv,SFont)).bitmap,
                [2] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("馬",black, xiv, yiv,SFont)).bitmap,
                [3] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("象",black, xiv, yiv,SFont)).bitmap,
                [4] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("砲",black, xiv, yiv,SFont)).bitmap,
                [5] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("車",black, xiv, yiv,SFont)).bitmap,
                [6] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("士",black, xiv, yiv,SFont)).bitmap,
                [7] = (IconMaker.DEFChess(DEFChessSizeR, ChessColor)+new IconMaker.Icon("将",black, xiv, yiv,SFont)).bitmap,
            }
        };
        public static readonly Dictionary<int, string>[] typesS = new Dictionary<int, string>[]
        {
            new Dictionary<int, string>
            {
                [1]="兵",
                [2]="傌",
                [3]="相",
                [4]="炮",
                [5]="俥",
                [6]="仕",
                [7]="帥"
            },
            new Dictionary<int, string>
            {
                [1]="卒",
                [2]="馬",
                [3]="象",
                [4]="砲",
                [5]="車",
                [6]="士",
                [7]="将"
            }
        };
        public static readonly Dictionary<Point, int> InitT = new Dictionary<Point, int>
        {
            [new Point(0, 0)] = GetChessId(2, 5),
            [new Point(1, 0)] = GetChessId(2, 2),
            [new Point(2, 0)] = GetChessId(2, 3),
            [new Point(3, 0)] = GetChessId(2, 6),
            [new Point(4, 0)] = GetChessId(2, 7),
            [new Point(5, 0)] = GetChessId(2, 6),
            [new Point(6, 0)] = GetChessId(2, 3),
            [new Point(7, 0)] = GetChessId(2, 2),
            [new Point(8, 0)] = GetChessId(2, 5),

            [new Point(1, 2)] = GetChessId(2, 4),
            [new Point(7, 2)] = GetChessId(2, 4),

            [new Point(0, 3)] = GetChessId(2, 1),
            [new Point(2, 3)] = GetChessId(2, 1),
            [new Point(4, 3)] = GetChessId(2, 1),
            [new Point(6, 3)] = GetChessId(2, 1),
            [new Point(8, 3)] = GetChessId(2, 1),

            [new Point(0, 9)] = GetChessId(1, 5),
            [new Point(1, 9)] = GetChessId(1, 2),
            [new Point(2, 9)] = GetChessId(1, 3),
            [new Point(3, 9)] = GetChessId(1, 6),
            [new Point(4, 9)] = GetChessId(1, 7),
            [new Point(5, 9)] = GetChessId(1, 6),
            [new Point(6, 9)] = GetChessId(1, 3),
            [new Point(7, 9)] = GetChessId(1, 2),
            [new Point(8, 9)] = GetChessId(1, 5),

            [new Point(1, 7)] = GetChessId(1, 4),
            [new Point(7, 7)] = GetChessId(1, 4),

            [new Point(0, 6)] = GetChessId(1, 1),
            [new Point(2, 6)] = GetChessId(1, 1),
            [new Point(4, 6)] = GetChessId(1, 1),
            [new Point(6, 6)] = GetChessId(1, 1),
            [new Point(8, 6)] = GetChessId(1, 1),
        };
        public static readonly Dictionary<int, int[,]> PosBnous = new Dictionary<int, int[,]>
        {
            [7] = new int[,]{{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, -9, -9, -9, 0, 0, 0},
{0, 0, 0, -8, -8, -8, 0, 0, 0},
{0, 0, 0, 1, 5, 1, 0, 0, 0}},
            [5] = new int[,]{{6, 8, 7, 13, 14, 13, 7, 8, 6},
{6, 12, 9, 16, 33, 16, 9, 12, 6},
{6, 8, 7, 14, 16, 14, 7, 8, 6},
{6, 13, 13, 16, 16, 16, 13, 13, 6},
{8, 11, 11, 14, 15, 14, 11, 11, 8},
{8, 12, 12, 14, 15, 14, 12, 12, 8},
{4, 9, 4, 12, 14, 12, 4, 9, 4},
{-2, 8, 4, 12, 12, 12, 4, 8, -2},
{5, 8, 6, 12, 0, 12, 6, 8, 5},
{-6, 6, 4, 12, 0, 12, 4, 6, -6}},
            [2] = new int[,]{{2, 2, 2, 8, 2, 8, 2, 2, 2},
{2, 8, 15, 9, 6, 9, 15, 8, 2},
{4, 10, 11, 15, 11, 15, 11, 10, 4},
{5, 20, 12, 19, 12, 19, 12, 20, 5},
{2, 12, 11, 15, 16, 15, 11, 12, 2},
{2, 10, 13, 14, 15, 14, 13, 10, 2},
{4, 6, 10, 7, 10, 7, 10, 6, 4},
{5, 4, 6, 7, 4, 7, 6, 4, 5},
{-3, 2, 4, 5, -10, 5, 4, 2, -3},
{0, -3, 2, 0, 2, 0, 2, -3, 0}},
            [4] = new int[,]{{4, 4, 0, -5, -6, -5, 0, 4, 4},
{2, 2, 0, -4, -7, -4, 0, 2, 2},
{1, 1, 0, -5, -4, -5, 0, 1, 1},
{0, 3, 3, 2, 4, 2, 3, 3, 0},
{0, 0, 0, 0, 4, 0, 0, 0, 0},
{-1, 0, 3, 0, 4, 0, 3, 0, -1},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{1, 0, 4, 3, 5, 3, 4, 0, 1},
{0, 1, 2, 2, 2, 2, 2, 1, 0},
{0, 0, 1, 3, 3, 3, 1, 0, 0}},
            [3] = new int[,]{{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{-2, 0, 0, 0, 3, 0, 0, 0, -2},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0}},
            [6] = new int[,]{{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 3, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0}},
            [1] = new int[,]{{0, 0, 0, 2, 4, 2, 0, 0, 0},
{20, 30, 50, 65, 70, 65, 50, 30, 20},
{20, 30, 45, 55, 55, 55, 45, 30, 20},
{20, 27, 30, 40, 42, 40, 30, 27, 20},
{10, 18, 22, 35, 40, 35, 22, 18, 10},
{3, 0, 4, 0, 7, 0, 4, 0, 3},
{-2, 0, -2, 0, 6, 0, -2, 0, -2},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0},
{0, 0, 0, 0, 0, 0, 0, 0, 0}}
        };
        public static readonly Dictionary<int, int> MovBnous = new Dictionary<int, int>
        {
            [1] = 15,
            [2] = 12,
            [3] = 1,
            [4] = 6,
            [5] = 6,
            [6] = 1,
            [7] = 1
        };
        public ChineseChess() : base("CHSChess", 9, 10, chessStep, Getmove, MkTable(), chessImg, types, 2, typesS, typesP, GtPowerBonus, 7)
        {

        }
        public override void StartGame()
        {
            InitTable(InitT);
            base.StartGame();
        }
        public static IEnumerable<CAction[]> Getmove(ChessBlock chessBlock, int pid, int t, RuledChessGame game)
        {
            LinkedList<CAction[]> re = new LinkedList<CAction[]>();
            if (pid == 2 && t == 1) t = -1;
            ChessBlock j;
            switch (t)
            {
                case -1:
                case 1:
                    j = chessBlock.Surround[MoveDirIs[t][0]];
                    if (conMov(pid, j))
                        re.AddLast(CAction.Move(chessBlock, j));
                    if ((t == 1 && chessBlock.Loc.Y < 5) || (t == -1 && chessBlock.Loc.Y > 4))
                    {
                        j = chessBlock.Surround[MoveDirIs[t][1]];
                        if (conMov(pid, j))
                            re.AddLast(CAction.Move(chessBlock, j));
                        j = chessBlock.Surround[MoveDirIs[t][2]];
                        if (conMov(pid, j))
                            re.AddLast(CAction.Move(chessBlock, j));
                    }
                    return re;
                case 2:
                    for (int i = 0; i < 4; i++)
                    {
                        if (chessBlock.Surround[i].Id == ChessBlock.DEFID)
                        {
                            j = chessBlock.Surround[MoveDirIs[t][2 * i + 1]];
                            if (conMov(pid, j))
                                re.AddLast(CAction.Move(chessBlock, j));
                            j = chessBlock.Surround[MoveDirIs[t][2 * i]];
                            if (conMov(pid, j))
                                re.AddLast(CAction.Move(chessBlock, j));
                        }
                    }
                    return re;
                case 3:
                    for (int i = 4; i < 8; i++)
                    {
                        if (chessBlock.Surround[i].Id == ChessBlock.DEFID)
                        {
                            j = chessBlock.Surround[MoveDirIs[t][i - 4]];
                            if (conMov(pid, j) && ((pid == 1 && j.Loc.Y > 4) || (pid == 2 && j.Loc.Y < 5)))
                                re.AddLast(CAction.Move(chessBlock, j));
                        }
                    }
                    return re;
                case 4:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re.AddLast(CAction.Move(chessBlock, chess));
                            chess = chess.Surround[i];
                        }
                        if (chess.Id != ChessBlock.NONEID)
                        {
                            chess = chess.Surround[i];
                            while (chess.Id == ChessBlock.DEFID)
                            {
                                chess = chess.Surround[i];
                            }
                            if (GetPlayerId(chess.Id) == 3 - pid)
                                re.AddLast(CAction.Move(chessBlock, chess));
                        }
                    }
                    return re;
                case 5:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re.AddLast(CAction.Move(chessBlock, chess));
                            chess = chess.Surround[i];
                        }
                        if (GetPlayerId(chess.Id) == 3 - pid) re.AddLast(CAction.Move(chessBlock, chess));
                    }
                    return re;
                case 6:
                    foreach (int i in MoveDirIs[t])
                    {
                        j = chessBlock.Surround[i];
                        if (conMov(pid, j) && ((pid == 1 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y > 6) || (pid == 2 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y < 3)))
                            re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[i]));
                    }
                    return re;
                case 7:
                    foreach (int i in MoveDirIs[t])
                    {
                        j = chessBlock.Surround[i];
                        if (conMov(pid, j) && ((pid == 1 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y > 6) || (pid == 2 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y < 3)))
                        {
                            ChessBlock k = GetLineEnd(j, 3 - pid, pid == 1 ? 3 : 0);
                            if (k == null || GetType(k.Id) != 7)
                                re.AddLast(CAction.Move(chessBlock, chessBlock.Surround[i]));
                        }

                    }
                    return re;
            }
            throw new Exception("No this type!");

        }

        public static int Countmove(ChessBlock chessBlock, int pid, int t)
        {
            int re = 0;
            if (pid == 2 && t == 1) t = -1;
            ChessBlock j;
            switch (t)
            {
                case -1:
                case 1:
                    j = chessBlock.Surround[MoveDirIs[t][0]];
                    if (conMov(pid, j))
                        re++;
                    if ((t == 1 && chessBlock.Loc.Y < 5) || (t == -1 && chessBlock.Loc.Y > 4))
                    {
                        j = chessBlock.Surround[MoveDirIs[t][1]];
                        if (conMov(pid, j))
                            re++;
                        j = chessBlock.Surround[MoveDirIs[t][2]];
                        if (conMov(pid, j))
                            re++;
                    }
                    return re;
                case 2:
                    for (int i = 0; i < 4; i++)
                    {
                        if (chessBlock.Surround[i].Id == ChessBlock.DEFID)
                        {
                            j = chessBlock.Surround[MoveDirIs[t][2 * i + 1]];
                            if (conMov(pid, j))
                                re++;
                            j = chessBlock.Surround[MoveDirIs[t][2 * i]];
                            if (conMov(pid, j))
                                re++;
                        }
                    }
                    return re;
                case 3:
                    for (int i = 4; i < 8; i++)
                    {
                        if (chessBlock.Surround[i].Id == ChessBlock.DEFID)
                        {
                            j = chessBlock.Surround[MoveDirIs[t][i - 4]];
                            if (conMov(pid, j) && ((pid == 1 && j.Loc.Y > 4) || (pid == 2 && j.Loc.Y < 5)))
                                re++;
                        }
                    }
                    return re;
                case 4:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re++;
                            chess = chess.Surround[i];
                        }
                        if (chess.Id != ChessBlock.NONEID)
                        {
                            chess = chess.Surround[i];
                            while (chess.Id == ChessBlock.DEFID)
                            {
                                chess = chess.Surround[i];
                            }
                            if (GetPlayerId(chess.Id) == 3 - pid)
                                re++;
                        }
                    }
                    return re;
                case 5:
                    foreach (int i in MoveDirIs[t])
                    {
                        ChessBlock chess = chessBlock.Surround[i];
                        while (chess.Id == ChessBlock.DEFID)
                        {
                            re++;
                            chess = chess.Surround[i];
                        }
                        if (GetPlayerId(chess.Id) == 3 - pid) re++;
                    }
                    return re;
                case 6:
                    foreach (int i in MoveDirIs[t])
                    {
                        j = chessBlock.Surround[i];
                        if (conMov(pid, j) && ((pid == 1 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y > 6) || (pid == 2 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y < 3)))
                            re++;
                    }
                    return re;
                case 7:
                    foreach (int i in MoveDirIs[t])
                    {
                        j = chessBlock.Surround[i];
                        if (conMov(pid, j) && ((pid == 1 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y > 6) || (pid == 2 && j.Loc.X > 2 && j.Loc.X < 6 && j.Loc.Y < 3)))
                        {
                            ChessBlock k = GetLineEnd(j, 3 - pid, pid == 1 ? 3 : 0);
                            if (k == null || GetType(k.Id) != 7)
                                re++;
                        }

                    }
                    return re;
            }
            throw new Exception("No this type!");

        }
        private static bool conMov(int pid, ChessBlock j)
        {
            return j.Id == ChessBlock.DEFID || GetPlayerId(j.Id) == 3 - pid;
        }

        public static int GtPowerBonus(ChessBlock chess, int pid, int t)
        {
            int re = Countmove(chess, pid, t) * MovBnous[t];
            if(pid==1)return re + PosBnous[t][chess.Loc.Y, chess.Loc.X];
            else return re + PosBnous[t][9 - chess.Loc.Y, chess.Loc.X];
        }
        protected override void AfterApplied(CAction[] action)
        {
            if (GetType(action[0].from) == 7)
                FinishGame(NowPlayer);
            base.AfterApplied(action);
        }
        public static Image MkTable()
        {
            TableMaker.BlockInf up = new TableMaker.BlockInf(TableMaker.BlockMark.South | TableMaker.BlockMark.East | TableMaker.BlockMark.West), down = new TableMaker.BlockInf(TableMaker.BlockMark.North | TableMaker.BlockMark.East | TableMaker.BlockMark.West),
                left = new TableMaker.BlockInf(TableMaker.BlockMark.South | TableMaker.BlockMark.East | TableMaker.BlockMark.North), right = new TableMaker.BlockInf(TableMaker.BlockMark.North | TableMaker.BlockMark.South | TableMaker.BlockMark.West),
                con = new TableMaker.BlockInf(TableMaker.BlockMark.Crossing);
            TableMaker.BlockInf[,] blockInfs = new TableMaker.BlockInf[9, 10];
            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    if (x == 0) blockInfs[x, y] = left;
                    else if (x == 8) blockInfs[x, y] = right;
                    else if (y == 0 || y == 5) blockInfs[x, y] = up;
                    else if (y == 4 || y == 9) blockInfs[x, y] = down;
                    else blockInfs[x, y] = con;
                }
            }
            blockInfs[0, 0] = new TableMaker.BlockInf(TableMaker.BlockMark.South | TableMaker.BlockMark.East);
            blockInfs[8, 0] = new TableMaker.BlockInf(TableMaker.BlockMark.South | TableMaker.BlockMark.West);
            blockInfs[0, 9] = new TableMaker.BlockInf(TableMaker.BlockMark.North | TableMaker.BlockMark.East);
            blockInfs[8, 9] = new TableMaker.BlockInf(TableMaker.BlockMark.North | TableMaker.BlockMark.West);
            blockInfs[4, 1] = blockInfs[4, 8] = new TableMaker.BlockInf(TableMaker.BlockMark.Crossing | TableMaker.BlockMark.Cross | TableMaker.BlockMark.MinusCross);

            blockInfs[3, 0] = new TableMaker.BlockInf(up.mark | TableMaker.BlockMark.SE);
            blockInfs[5, 0] = new TableMaker.BlockInf(up.mark | TableMaker.BlockMark.SW);
            blockInfs[3, 2] = new TableMaker.BlockInf(con.mark | TableMaker.BlockMark.NE);
            blockInfs[5, 2] = new TableMaker.BlockInf(con.mark | TableMaker.BlockMark.NW);

            blockInfs[3, 9] = new TableMaker.BlockInf(down.mark | TableMaker.BlockMark.NE);
            blockInfs[5, 9] = new TableMaker.BlockInf(down.mark | TableMaker.BlockMark.NW);
            blockInfs[3, 7] = new TableMaker.BlockInf(con.mark | TableMaker.BlockMark.SE);
            blockInfs[5, 7] = new TableMaker.BlockInf(con.mark | TableMaker.BlockMark.SW);
            return TableMaker.GetTable(DEFBlockSizeR, blockInfs, DEFDrawIv.X);
        }
    }
    public class THEChess : ChessGame, IAIControl
    {
        public static readonly Dictionary<int, Image> id2img = new Dictionary<int, Image> { [1] = IconMaker.DEFChess(DEFChessSize, Color.Green).bitmap, [2] = IconMaker.DEFChess(DEFChessSize, Color.Red).bitmap };
        public static readonly Dictionary<int, string> id2str = new Dictionary<int, string>() { [1] = "绿子", [2] = "红子" };
        public static readonly Point[] Centres = new Point[] { new Point(1, 1), new Point(4, 1), new Point(7, 1),
            new Point(1, 4), new Point(4, 4), new Point(7, 4),
            new Point(1, 7), new Point(4, 7), new Point(7, 7)
    };
    public static readonly int[][] LLines = new int[][]
            {
                new int[]{0,4,8},
                new int[]{2,4,6},
                new int[]{3,4,5},
                new int[]{1,4,7},
                new int[]{0,1,2},
                
                new int[]{6,7,8},

                new int[]{0,3,6},
                
                new int[]{2,5,8},
            };
        public static readonly Point[] dirs = new Point[] { new Point(-1, -1), new Point(0, -1), new Point(1, -1), new Point(-1, 0),new Point(0,0), new Point(1, 0), new Point(-1, 1), new Point(0, 1), new Point(1, 1) };
        public THEChess():base("THEChess",9,9,dirs,TableMaker.GetStandredTable(9,9,DEFBlockSize,new TableMaker.BlockInf(TableMaker.BlockMark.Box,Brushes.WhiteSmoke))
            ,DEFBlockSize,id2img,new int[] {1,2},DEFDrawIv,id2str)
        {
        }
        private static int GetDir(Point point)
        {
            int x=point.X%3,y=point.Y%3;
            return x + y * 3;
        }


        private static int[] GetDominated(ChessTable chessTable)
        {
            int[] Dominated = new int[9];
            for (int i = 0; i < 9; i++)
            {
                ChessBlock cb = chessTable[Centres[i]];
                    if (cb.Id!=0&&CheckId(cb.Id,cb,-1))
                    {
                        Dominated[i] = cb.Id;
                    continue;
                    }
                
            }
            return Dominated;
        }

        private static int IsWin(int[] Dominated)
        {
            if (Dominated[4] != 0)
            {
                for (int i = 0; i < 4; i++)
                    if ( Dominated[LLines[i][0]] == Dominated[LLines[i][2]] && Dominated[LLines[i][2]] == Dominated[4])
                        return Dominated[4];
            }
            return 0;
        }

        public int Envaluate(ChessTable chessTable, Player player)
        {
            int re = 0;
            foreach(ChessBlock cb in chessTable.id2chess[1])
            {
                re += GetDir(cb.Loc) == 4 ? 5 : 1;
            }
            foreach (ChessBlock cb in chessTable.id2chess[2])
            {
                re -= GetDir(cb.Loc) == 4 ? 5 : 1;
            }
            int[] d = GetDominated(chessTable);
            for(int i = 0; i < 9; i++)
            {
                if (i == 4)
                {
                    if (d[4] == 1) re += 20;
                    else if (d[4] == -1) re -= 20;
                }
                else
                {
                    if (d[i] == 1) re += 4;
                    else if (d[i] == -1) re -= 4;
                }
            }
            if (PlayerId[player] == 1) return re;
            return -re;
        }

        public IEnumerable<CAction[]> GetLimitedActions(ChessTable chessTable, Player player)
        {
            LinkedList<CAction[]> re = new LinkedList<CAction[]>();
            int[] Dominated = GetDominated(chessTable);
            if (IsWin(Dominated) != 0)
                return re;
            if (chessTable.actionLast != null)
            {
                int d = GetDir(chessTable.actionLast[0].Loc);
                if (Dominated[d]==0)
                {
                    foreach(ChessBlock cb in chessTable[Centres[d]].Surround)
                    {
                        if (cb.Id == ChessBlock.DEFID)
                            re.AddLast(CAction.Add(cb, PlayerId[player]));
                    }
                    if(re.Count!= 0)
                        return re;
                }
            }
            foreach (ChessBlock cb in chessTable.id2chess[ChessBlock.DEFID])
                re.AddLast(CAction.Add(cb, PlayerId[player]));
            return re;
        }
        protected override void AfterApplied(CAction[] action)
        {
            int w = IsWin(GetDominated(ChessTable));
            if (w != 0) FinishGame(NowPlayer);
            base.AfterApplied(action);
        }
        public override IEnumerable<CAction[]> GetLegalActions(Player player)
        {
            return GetLimitedActions(ChessTable, player);
        }
        public static bool CheckId(int id,ChessBlock chessBlock, int x)
        {
            for(int idx = 0; idx < LLines.Length; idx++)
            {
                bool f = true;
                for (int i = 0; i < 3; i++)
                {
                    if (chessBlock.Surround[LLines[idx][i]].Id != id && LLines[idx][i] != x)
                    {
                        f= false;
                        break;
                    }
                }
                if(f)
                    return true;
            }
            return false;
        }
        public override CAction[] BeforeApply(ChessTable chessTable, CAction[] action)
        {
            int k = GetDir(action[0].Loc);
            ChessBlock centre = chessTable[action[0].Loc].Surround[8 - k];
            if (CheckId(action[0].to,centre,k))
                    {
                        CAction[] re = new CAction[9];
                        re[0] = action[0];
                        int l = 1;
                        for(int j = 0; j < 9; j++)
                        {
                            if (j != k)
                                re[l++] = new CAction(centre.Surround[j], action[0].to);
                        }
                        return re;
                    }
            return action;
        }
        public override string GetActionInf(CAction[] cActions)
        {
            return base.GetActionInf(new CAction[] { cActions[0] });
        }
    }
}

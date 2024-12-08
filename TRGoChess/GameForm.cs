using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static TRGoChess.ChessTable;

namespace TRGoChess
{
    public partial class GameForm : Form
    {
        public readonly ChessGame ChessGame;
        private GUIImg gUIImg;
        private BackgroundWorker BackgroundWorker;
        public bool saveImg = false, autoNxt=false;
        public GameForm(ChessGame chessGame, int[] ai)
        {
            InitializeComponent();
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            ChessGame = chessGame;
            Text= chessGame.Name;
            foreach(int i in ai)
            {
                if (i == -1) {
                    ChessGame.AddPlayer(new HumanPlayer(pictureBox1, ChessGame is RuledChessGame ? 2 : 1, this, ChessGame));
                    autoNxt = true;
                } 
                else ChessGame.AddPlayer(new UniversalAI(ChessGame as IAIControl, i));
            }
            gUIImg = new GUIImg(ChessGame.Width, ChessGame.Height, ChessGame.IconSize, Color.Red, chessGame.DrawIv, chessGame.ImageSize);
            UpdateTable();
            Size = new Size( pictureBox1.Width+pictureBox1.Location.X*2+20,Size.Height+10);
            chessGame.StartGame();
            if (saveImg)
            {
                dir = "./" + ChessGame.Name + DateTime.Now.ToString( "_MM;dd HH;mm;ss");
                Directory.CreateDirectory(dir);
            }
            UpdateTable();
            if (!(ChessGame.NowPlayer is HumanPlayer) && ChessGame.Winner == null && autoNxt)
                NextT();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            timer1.Stop();
            if ((bool)e.Result)
            {
                ChessGame.LastPlayer?.TurnEnded(ticks);
                UpdateTable();
                toolStripStatusLabel1.Text =ChessGame.HistoryCount.ToString()+": "+ ChessGame.LastPlayer.Name +" ♟️   "+ ChessGame.GetActionInf(ChessGame.LastAction());
            }
            if (ChessGame.Winner != null)
            {
                playerLabel.Enabled = false;
                MessageBox.Show(ChessGame.Winner.Name + " win!", "Game over!",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            else
            {
                playerLabel.Enabled = true;
                ChessGame.PlayerPrepare();
            }
            pictureBox1.Enabled = playerLabel.Enabled;
            withdrawButton.Enabled=playerLabel.Enabled;
            Update();
            ticks = 0;
            if (!(ChessGame.NowPlayer is HumanPlayer)&& ChessGame.Winner == null&&autoNxt)
                NextT();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = ChessGame.NextTurn();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            NextT();
        }

        private void NextT()
        {
            playerLabel.Enabled = false;
            pictureBox1.Enabled = false;
            withdrawButton.Enabled= false;
            Update();
            Nexturn();
        }

        public void Nexturn()
        {
            if (!BackgroundWorker.IsBusy)
            {
                timer1.Start();
                BackgroundWorker.RunWorkerAsync();
            }
        }
        private int picI = 0;
        private string dir;
        public bool Reversed=false;
        private void UpdateTable()
        {
            if (ChessGame.NowPlayer is HumanPlayer&&ChessGame is RuledChessGame)
            {
                bool rev = ChessGame.players[ChessGame.players.Count - 1] == ChessGame.NowPlayer;
                Reversed = rev;
            }
            pictureBox1.BackgroundImage = ChessGame.GetImage(Reversed);
            if(saveImg)
                pictureBox1.BackgroundImage.Save(dir + "/" + (++picI).ToString() + ".bmp");
            playerLabel.Image = ChessGame.GetPlayerIcon(ChessGame.NowPlayer);
            playerLabel.Text = ChessGame.NowPlayer.Name + "  ⌛";
            LinkedList<Point> points = new LinkedList<Point>();
            CAction[] ca = ChessGame.LastAction();
            if (ca != null)
            {
                foreach (CAction c in ca)
                    points.AddLast(c.Loc);
            }
            SetImage(gUIImg.GetGUI(points,ChessGame is RuledChessGame), "GameForm lastAction");
        }
        private Dictionary<string, Image> o2images=new Dictionary<string, Image>();
        public void SetImage(Image image, string c)
        {
            o2images[c]=image;
            ApplyImage(o2images.Values, pictureBox1.BackgroundImage.Size);
        }
        public void ClearImage()
        {
            o2images.Clear();
            ApplyImage(o2images.Values, pictureBox1.BackgroundImage.Size);
        }
        public void ClearImage(string name)
        {
            o2images.Remove(name);
            ApplyImage(o2images.Values, pictureBox1.BackgroundImage.Size);
        }
        private void ApplyImage(IEnumerable<Image> images, Size size)
        {
            Image image= new Bitmap(size.Width, size.Height);
            Graphics g=Graphics.FromImage(image);
            foreach(Image image1 in images)
                g.DrawImage(image1, 0,0);
            pictureBox1.Image= image;
            if (Reversed) pictureBox1.Image.RotateFlip(RotateFlipType.RotateNoneFlipY);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            WZChess wZChess = ChessGame as WZChess;
            int[] r = wZChess.powerCounter.CountPatterns(wZChess.GetChessTable());
            string re = "";
            for (int i = 0; i < r.Length; i++)
            {
                re += i.ToString() + ":" + r[i].ToString() + " ";
            }
            MessageBox.Show(re);
        }
        private int ticks = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            ticks++;
        }

        private void button1_EnabledChanged(object sender, EventArgs e)
        {
        }

        private void toolStripDropDownButton1_Click(object sender, EventArgs e)
        {
            ChessGame.WithDraw();
            UpdateTable();
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            ChessGame.WithDraw();
            UpdateTable();
        }
    }
    public class HumanPlayer : Player
    {
        public const int MARKCOUNT = 100;
        private Queue<Point> points = new Queue<Point>();
        private int maxp;
        private GameForm gameForm;
        private GUIImg gUI;
        private IActionInf actionInf;
        private ContextMenuStrip menuStrip;
        private Dictionary<string, CAction[]> cas;
        private IEnumerable<CAction[]> caa;
        private Control pictureBox;
        public static HumanPlayer NowPlayer;
        public HumanPlayer(PictureBox pictureBox, int maxp, GameForm gameForm, IActionInf actionInf) : base("Human")
        {
            this.pictureBox = pictureBox;
            menuStrip = new ContextMenuStrip();
            pictureBox.MouseClick += PictureBox_MouseClick;
            menuStrip.ItemClicked += MenuStrip_ItemClicked;
            this.maxp = maxp;
            this.actionInf = actionInf;
            this.gameForm = gameForm;
            gUI = new GUIImg(gameForm.ChessGame.Width, gameForm.ChessGame.Height, gameForm.ChessGame.IconSize, new Dictionary<int, Color>()
            {
                [1] = Color.Blue,
                [2] = Color.Green,
                [3] = Color.GreenYellow
            }, gameForm.ChessGame.DrawIv, gameForm.ChessGame.ImageSize);
        }

        private void MenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            action = cas[e.ClickedItem.Text];
            Nxt();
        }
        public override void PrePare(ChessGame game, IEnumerable<CAction[]> legalActions)
        {
            if (points.Count == 1)
                MarkMoves(points.Peek());
            else
                MarkMoves(legalActions);
            NowPlayer = this;
            base.PrePare(game, legalActions);
        }
        private void PictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (NowPlayer != this) return;
            if (points.Count == maxp) points.Dequeue();
            points.Enqueue(gameForm.ChessGame.GetClick(e.Location));
            if (e.Button == MouseButtons.Left)
            {
                if (maxp == 1)
                {
                    Nxt();
                }
                else if (maxp == 2)
                {
                    if (points.Count == 1)
                    {
                        MarkMoves(gameForm.ChessGame.GetClick(e.Location));
                    }
                    else
                    {
                        Nxt();
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                MarkMoves(gameForm.ChessGame.GetClick(e.Location));
                menuStrip.Items.Clear();
                cas = new Dictionary<string, CAction[]>();
                if (caa != null)
                {
                    foreach (CAction[] ca in caa)
                    {
                        string s = actionInf.GetActionInf(ca);
                        menuStrip.Items.Add(s);
                        cas[s] = ca;
                    }
                }
                if (menuStrip.Items.Count > 0)
                    menuStrip.Show(pictureBox, e.Location);
            }

        }

        private void Nxt()
        {
            gameForm.Nexturn();
            gameForm.ClearImage(Name + " moves");
            gameForm.ClearImage(Name + " canMoves");
        }
        private void MarkMoves(Point chessLoc)
        {
            if (actionInf != null)
            {
                caa = actionInf.GetMoves(chessLoc, this);
                if (caa.Any())
                {
                    Dictionary<Point, int> map = new Dictionary<Point, int>();
                    map[chessLoc] = 1;
                    foreach (CAction[] ca in caa)
                    {
                        if (ca.Length == 2)
                            map[ca[0].Loc] = 2;
                    }
                    gameForm.SetImage(gUI.GetGUI(map), Name + " moves");
                    return;
                }
            }
            gameForm.SetImage(gUI.GetGUI(new Point[] { chessLoc }), Name + " moves");
        }
        private void MarkMoves(IEnumerable<CAction[]> legalActions)
        {
            Dictionary<Point, int> map = new Dictionary<Point, int>();
            foreach (CAction[] ca in legalActions)
            {
                if (ca.Length > 1)
                    map[ca[1].Loc] = 3;
                else map[ca[0].Loc] = 3;
            }
            if (map.Count < MARKCOUNT)
                gameForm.SetImage(gUI.GetGUI(map), Name + " canMoves");
            return;
        }
        private CAction[] action;
        public override void Go(ChessGame game, IEnumerable<CAction[]> legalActions)
        {
            if (points.Count == 1 || points.Count == 2 || action != null)
            {
                if (action == null)
                {
                    Point[] ps = points.ToArray();
                    action = game.GetAction(ps, this);
                }
                if (game.ApplyAction(action))
                {
                    points.Clear();
                }
                else
                {
                    points.Dequeue();
                }
                action = null;
            }
        }
    }
    public class GUIImg : IGrid
    {
        public int[,] points;
        public int this[int x, int y] => points[x, y];
        public int width, height;
        private GridDrawer GridDrawer;

        public int Width => width;

        public int Height => height;

        public GUIImg(int width, int height, Size iconSize, Dictionary<int, Color> colors, Point DrawIv, Size backgroundSize)
        {
            this.width = width;
            this.height = height;
            Dictionary<int, Image> images = new Dictionary<int, Image>();
            foreach (int k in colors.Keys)
            {
                images[k] = IconMaker.DEFChess(iconSize, Color.Transparent, colors[k]).bitmap;
            }
            GridDrawer = new GridDrawer(this, new Bitmap(backgroundSize.Width, backgroundSize.Height), iconSize, images, DrawIv);
        }
        public GUIImg(int width, int height, Size iconSize, Color color, Point DrawIv, Size backgroundSize) : this(width, height, iconSize, new Dictionary<int, Color>() { [1] = color }, DrawIv, backgroundSize) { }
        public Image GetGUI(IEnumerable<Point> d,bool line=false, int t = 1)
        {
            points = new int[width, height];
            foreach (Point point in d)
                points[point.X, point.Y] = t;
            Image image = GridDrawer.GetImage();
            if (line)
            {
                GridDrawer.DrawLines(d, new Pen(Color.OrangeRed, 2));
            }
            return image;
        }
        public Image GetGUI(Dictionary<Point, int> p2t)
        {
            points = new int[width, height];
            foreach (Point p in p2t.Keys)
                points[p.X, p.Y] = p2t[p];
            return GridDrawer.GetImage();
        }
    }
}

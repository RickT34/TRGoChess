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
        public class HumanPlayer : Player
        {
            private Queue<Point> points = new Queue<Point>();
            private int maxp;
            private GameForm gameForm;
            private GUIImg gUI;
            private IActionInf actionInf;
            private ContextMenuStrip menuStrip;
            private Dictionary<string, CAction[]> cas;
            private IEnumerable<CAction[]> caa;
            private Control pictureBox;
            public HumanPlayer(PictureBox pictureBox, int maxp, GameForm gameForm,IActionInf actionInf) : base("Human")
            {
                this.pictureBox = pictureBox;
                menuStrip= new ContextMenuStrip();
                pictureBox.MouseClick += PictureBox_MouseClick;
                menuStrip.ItemClicked += MenuStrip_ItemClicked;
                this.maxp = maxp;
                this.actionInf= actionInf;
                this.gameForm = gameForm;
                gUI = new GUIImg(gameForm.ChessGame.Width, gameForm.ChessGame.Height, gameForm.ChessGame.IconSize,new Dictionary<int, Color>() {
                    [1] = Color.Blue,
                    [2] = Color.Green,
                    [3] = Color.GreenYellow
                },gameForm.ChessGame.DrawIv,gameForm.ChessGame.ImageSize);
            }

            private void MenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
            {
                action = cas[e.ClickedItem.Text];
                Nxt();
            }
            public void EndTurn()
            {
                if (points.Count == 1)
                    MarkMoves(points.Peek());
            }
            public override void PrePare(ChessGame game, IEnumerable<CAction[]> legalActions)
            {
                if (points.Count == 1)
                    MarkMoves(points.Peek());
                MarkMoves(legalActions);
                base.PrePare(game, legalActions);
            }
            private void PictureBox_MouseClick(object sender, MouseEventArgs e)
            {
                if (gameForm.ChessGame.NowPlayer != this) return;
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
                        if(points.Count == 1)
                        {
                            MarkMoves(gameForm.ChessGame.GetClick(e.Location));
                        }
                        else 
                        {
                            Nxt();
                        }
                    }
                }
                else if(e.Button == MouseButtons.Right)
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
                    if(menuStrip.Items.Count>0)
                        menuStrip.Show(pictureBox,e.Location);
                }
                
            }

            private void Nxt()
            {
                gameForm.ClearImage(Name + " moves");
                gameForm.ClearImage(Name + " canMoves");
                gameForm.Nexturn();
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
                if(map.Count<30)
                    gameForm.SetImage(gUI.GetGUI(map), Name + " canMoves");
                return;
            }
            private CAction[] action;
            public override void Go(ChessGame game, IEnumerable<CAction[]> legalActions)
            {
                if (points.Count == 1 || points.Count == 2||action!=null)
                {
                    if (action == null)
                    {
                        Point[] ps = points.ToArray();
                        action = game.GetAction(ps, this);
                    }
                    if( game.ApplyAction(action))
                        points.Clear();
                    else
                    {
                        points.Dequeue();
                    }
                    action= null;
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
                foreach(int k in colors.Keys)
                {
                    images[k] = IconMaker.DEFChess(iconSize, Color.Transparent, colors[k]).bitmap;
                }
                GridDrawer = new GridDrawer(this, new Bitmap(backgroundSize.Width, backgroundSize.Height), iconSize, images,DrawIv);
            }
            public GUIImg(int width, int height, Size iconSize, Color color, Point DrawIv, Size backgroundSize):this(width,height, iconSize, new Dictionary<int, Color>() { [1]=color}, DrawIv, backgroundSize) { }
            public Image GetGUI(IEnumerable<Point> d, int t=1)
            {
                points = new int[width, height];
                foreach (Point point in d)
                    points[point.X, point.Y] = t;
                return GridDrawer.GetImage();
            }
            public Image GetGUI(Dictionary<Point, int> p2t)
            {
                points=new int[width, height];
                foreach(Point p in p2t.Keys)
                    points[p.X, p.Y] = p2t[p];
                return GridDrawer.GetImage();
            }
        }
        public readonly ChessGame ChessGame;
        private GUIImg gUIImg;
        private BackgroundWorker BackgroundWorker;
        private UniversalAI uai;
        private HumanPlayer hum;
        public bool saveImg = false;
        public GameForm(ChessGame chessGame, int ai, bool firsth)
        {
            InitializeComponent();
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            ChessGame = chessGame;
            Text= chessGame.Name;
            uai = new UniversalAI(ChessGame as IAIControl, ai);
            hum = new HumanPlayer(pictureBox1, ChessGame is RuledChessGame ? 2 : 1, this, ChessGame as IActionInf);
            if (firsth)
            {
                ChessGame.AddPlayer(hum);
                ChessGame.AddPlayer(uai);
            }
            else
            {
                ChessGame.AddPlayer(uai);
                ChessGame.AddPlayer(hum);
            }
            gUIImg = new GUIImg(ChessGame.Width, ChessGame.Height, ChessGame.IconSize, Color.Red, chessGame.DrawIv, chessGame.ImageSize);
            UpdateTable();
            Size = new Size( pictureBox1.Width+pictureBox1.Location.X*2,Size.Height+10);
            chessGame.StartGame();
            if (saveImg)
            {
                dir = "./" + ChessGame.Name + DateTime.Now.ToString( "_MM;dd HH;mm;ss");
                Directory.CreateDirectory(dir);
            }
            
            UpdateTable();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((bool)e.Result)
            {
                UpdateTable();
            }
            if (ChessGame.Winner != null)
            {
                button1.Enabled = false;
                MessageBox.Show(ChessGame.Winner.Name + " win!");
            }
            else
            {
                button1.Enabled = true;
                ChessGame.PlayerPrepare();
            }
            pictureBox1.Enabled = button1.Enabled;
            button2.Enabled=button1.Enabled;
            Update();
            if (!(ChessGame.NowPlayer is HumanPlayer)&& ChessGame.Winner == null)
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
            button1.Enabled = false;
            pictureBox1.Enabled = false;
            button2.Enabled= false;
            Update();
            Nexturn();
        }

        public void Nexturn()
        {
            if (!BackgroundWorker.IsBusy)
            {
                BackgroundWorker.RunWorkerAsync();
            }
        }
        private int picI = 0;
        private string dir;
        private void UpdateTable()
        {
            pictureBox1.BackgroundImage = ChessGame.GetImage(uai.data);
            if(saveImg)
                pictureBox1.BackgroundImage.Save(dir + "/" + (++picI).ToString() + ".bmp");
            button1.BackgroundImage = ChessGame.GetPlayerIcon(ChessGame.NowPlayer);
            LinkedList<Point> points = new LinkedList<Point>();
            CAction[] ca = ChessGame.LastAction();
            if (ca != null)
            {
                foreach (CAction c in ca)
                    points.AddLast(c.Loc);

            }
            SetImage(gUIImg.GetGUI(points), "GameForm lastAction");
            Text = ChessGame.Name +":  " + ChessGame.NowPlayer.Name + "  ⌛";
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

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateTable();
        }

        private void button1_EnabledChanged(object sender, EventArgs e)
        {
            if (button1.Enabled == false) { button1.Text = "⌛"; }
            else button1.Text = "";
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            ChessGame.WithDraw();
            UpdateTable();
        }
    }
}

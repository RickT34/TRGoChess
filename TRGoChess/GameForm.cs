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

namespace TRGoChess
{
    public partial class GameForm : Form
    {
        internal class HumanPlayer : Player
        {
            private Queue<Point> points = new Queue<Point>();
            private int maxp;
            private GameForm gameForm;
            private GUIImg gUI;
            private PictureBox pictureBox;
            public HumanPlayer(PictureBox pictureBox, int maxp, GameForm gameForm) : base("Human")
            {
                this.pictureBox = pictureBox;
                pictureBox.MouseClick += PictureBox_MouseClick;
                this.maxp = maxp;
                this.gameForm = gameForm;
                gUI= new GUIImg(gameForm.ChessGame.Width, gameForm.ChessGame.Height, gameForm.ChessGame.IconSize, Color.Blue);
            }

            private void PictureBox_MouseClick(object sender, MouseEventArgs e)
            {
                if(e.Button== MouseButtons.Left)
                {
                    if (gameForm.ChessGame.NowPlayer != this) return;
                    if (points.Count == maxp) points.Dequeue();
                    points.Enqueue(e.Location);
                    if (points.Count == maxp)
                    {
                        gameForm.Nexturn();
                    }
                }
                LinkedList<Point> list = new LinkedList<Point>();
                foreach (Point p in points)
                {
                    list.AddLast(gameForm.ChessGame.GetClick(p));
                }
                pictureBox.Image = gUI.GetGUI(list);
            }

            public override void Go(ChessGame game, IEnumerable<CAction[]> legalActions)
            {
                CAction[] action;
                if (points.Count == 1 || points.Count == 2)
                {
                    Point[] ps = points.ToArray();
                    action = game.GetAction(ps, this);
                    game.ApplyAction(action);
                }
                points.Clear();
            }
        }
        internal class GUIImg : IGrid
        {
            public int[,] points;
            public int this[int x, int y] => points[x,y];
            public int width, height;
            private GridDrawer GridDrawer;

            public int Width => width;

            public int Height => height;

            public GUIImg(int width, int height, Size iconSize, Color color)
            {
                this.width = width;
                this.height = height;
                GridDrawer = new GridDrawer(this, new Bitmap(width * iconSize.Width, height * iconSize.Height), iconSize, new Dictionary<int, Image> { [1] = IconMaker.DEFChess(iconSize, Color.Transparent,color).bitmap });
            }
            public Image GetGUI(IEnumerable<Point> d)
            {
                points = new int[width, height];
                foreach (Point point in d)
                    points[point.X, point.Y] = 1;
                return GridDrawer.GetImage(false, false, false);
            }
        }
       internal ChessGame ChessGame;
        private GUIImg gUIImg;
        private BackgroundWorker BackgroundWorker;
        UniversalAI uai;
        public GameForm()
        {
            InitializeComponent();
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            ChessGame = new InternationalChess();
            ChessGame.AddPlayer(new HumanPlayer(pictureBox1, 2, this));
            //ChessGame.AddPlayer(new UniversalAI(ChessGame as IAIControl, 5));
            uai = new UniversalAI(ChessGame as IAIControl, 4);
            ChessGame.AddPlayer(uai);
            //ChessGame.AddPlayer(new HumanPlayer(pictureBox1, 1, this));
            dir = "./" + DateTime.Now.ToString("MM;dd;HH mm;ss");
            Directory.CreateDirectory(dir);
            gUIImg = new GUIImg(ChessGame.Width, ChessGame.Height, ChessGame.IconSize,Color.Red);
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
            else button1.Enabled = true;
            pictureBox1.Enabled = button1.Enabled;
            Update();
            if (!(ChessGame.NowPlayer is HumanPlayer))
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
        int picI = 0;
        string dir;
        private void UpdateTable()
        {
            pictureBox1.BackgroundImage = ChessGame.GetImage(uai.data);
            pictureBox1.BackgroundImage.Save(dir + "/" + (++picI).ToString() + ".bmp");
            button1.BackgroundImage = ChessGame.GetPlayerIcon(ChessGame.NowPlayer);
            LinkedList<Point> points = new LinkedList<Point>();
            CAction[] ca = ChessGame.LastAction();
            if (ca != null)
            {
                foreach (CAction c in ca)
                    points.AddLast(c.Loc);
                
            }
            pictureBox1.Image = gUIImg.GetGUI(points);
            label1.Text = ChessGame.NowPlayer.Name;
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
    }
}

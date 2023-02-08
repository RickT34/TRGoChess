using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TRGoChess
{
    public partial class StartForm : Form
    {
        public StartForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ChessGame chessGame;
            switch(comboBox1.SelectedIndex)
            {
                case 0:
                    chessGame = new WZChess();
                    break;
                case 1:
                    chessGame=new BWChess();
                    break;
                case 2:
                    chessGame=new InternationalChess();
                    break;
                case 3:
                    chessGame = new ChineseChess();
                    break;
                default:
                    return;
            }
            GameForm gameForm = new GameForm(chessGame,new int[] { radioButton2.Checked?4:-1, radioButton3.Checked ? 4: -1 } );
            Hide();
            gameForm.ShowDialog();
            Show();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
        }
    }
}

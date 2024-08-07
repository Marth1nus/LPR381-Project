using LPR381.LP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace LPR381
{
    public partial class Form1 : Form
    {
        private static readonly Dictionary<string, Solver> AlgorithmDict = new Dictionary<string, Solver>
        {
            { "Branch and Bound", BranchAndBound.Solve },
            { "Branch and Bound Knapsack", BranchAndBoundKnapsack.Solve },
            { "Cutting Plane", CuttingPlane.Solve },
            { "Primal Simplex", PrimalSimplex.Solve },
        };

        private Solver GetSolver() => comboBox1.SelectedValue as Solver;

        public Form1()
        {
            InitializeComponent();
            comboBox1.DataSource = new BindingSource(AlgorithmDict, null);
            comboBox1.DisplayMember = "Key";
            comboBox1.ValueMember = "Value";
        }


        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFileResult = openFileDialog1.ShowDialog(this);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var saveFileResult = saveFileDialog1.ShowDialog(this);
            richTextBox1.Text = saveFileResult.ToString();
        }
    }
}

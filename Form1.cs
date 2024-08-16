using LPR381.LP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
        private Tableau tableau;

        private Solver Solver => comboBox1.SelectedValue as Solver;
        private string SolverName => AlgorithmDict.FirstOrDefault(kv => kv.Value == Solver).Key;

        public Form1()
        {
            InitializeComponent();
            comboBox1.DataSource = new BindingSource(AlgorithmDict, null);
            comboBox1.DisplayMember = "Key";
            comboBox1.ValueMember = "Value";
            comboBox1.SelectedValue = AlgorithmDict["Primal Simplex"];
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) => openFileDialog1.ShowDialog(this);
        
        private void saveToolStripMenuItem_Click(object sender, EventArgs e) => saveFileDialog1.ShowDialog(this);
        
        private void solveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var newTableau = tableau.Copy();
                var steps = Solver(newTableau);
                richTextBox1.Text += $"# Solve Using {SolverName}\n\n{string.Join("\n\n", steps)}\n\n";
                tableau = newTableau;
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }
        }

        private void sensitivityAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            {
                var analysis = SensitivityAnalysis.Analise(tableau.Copy());
                richTextBox1.Text += $"{analysis}\n\n";
            }
            try { }
            catch (Exception err) 
            { 
                Console.WriteLine(err.ToString()); 
            }
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                tableau = Tableau.FromFile(openFileDialog1.FileName);
                textBox1.Text = openFileDialog1.FileName.Split('\\').Last();
                richTextBox1.Text = $"{Tableau.FromFileCanonicalForm(openFileDialog1.FileName)}\n\n# Tableau\n\n{tableau}\n\n";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                textBox1.Text = "Failed";
            }
        }
        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                File.WriteAllText(saveFileDialog1.FileName, richTextBox1.Text);
                textBox2.Text = saveFileDialog1.FileName.Split('\\').Last();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                textBox2.Text = "Failed";
            }
        }

        private void clearOutputToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = "";
        }
    }
}

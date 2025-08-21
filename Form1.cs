using LPR381.LP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;



namespace LPR381
{
    public partial class Form1 : Form
    {
        private static readonly Dictionary<string, Solver> AlgorithmDict = new Dictionary<string, Solver>
        {
            { "Branch and Bound", /*          */ BranchAndBound /*         */ .Solve },
            { "Branch and Bound Knapsack", /* */ BranchAndBoundKnapsack /* */ .Solve },
            { "Cutting Plane", /*             */ CuttingPlane /*           */ .Solve },
            { "Dual Simplex", /*              */ DualSimplex /*            */ .Solve },
            { "Primal Simplex", /*            */ PrimalSimplex /*          */ .Solve },
        };
        private Tableau tableau;

        private void errorMessageNoTableau()
        {
            // Set the color of the selected text to red
            richTextBox1.SelectionColor = Color.Red;

            richTextBox1.AppendText("> No tableau loaded.\n\n");

            // Reset the color for subsequent text
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
        }

        private Solver Solver => comboBox1.SelectedValue as Solver;
        private string SolverName => AlgorithmDict.FirstOrDefault(kv => kv.Value == Solver).Key;

        public Form1()
        {
            InitializeComponent();
            comboBox1.DataSource = new BindingSource(AlgorithmDict, null);
            comboBox1.DisplayMember = "Key";
            comboBox1.ValueMember = "Value";
            comboBox1.SelectedValue = AlgorithmDict["Primal Simplex"];
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) => openFileDialog1.ShowDialog(this);
        
        private void saveToolStripMenuItem_Click(object sender, EventArgs e) => saveFileDialog1.ShowDialog(this);
        
        private void solveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                label4.Text = "";
                label4.ForeColor = System.Drawing.Color.Black;
                if (tableau == null)
                {
                    errorMessageNoTableau();
                    return;
                }
                var newTableau = tableau.Copy();
                var steps = Solver(newTableau);
                var stepString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                richTextBox1.Text += $"# Solve Using {SolverName}\n\n{stepString}\n\n";
                tableau = newTableau;
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
                label4.Text = "Error";
                label4.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void sensitivityAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            label5.Text = "";
            label5.ForeColor = System.Drawing.Color.Black;
            try
            {
                if (tableau == null)
                {
                    errorMessageNoTableau();
                    return;
                }
                var analysis = SensitivityAnalysis.Analise(tableau.Copy());
                richTextBox1.Text += $"# Sensitivity Analysis\n\n{analysis}\n\n";
            }
            catch (Exception err) 
            { 
                Console.WriteLine(err.ToString());
                label5.Text = "Error";
                label5.ForeColor = System.Drawing.Color.Red;
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

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }

        private void sensitivityAnalysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //code to go to new tab
            label5.Text = "";
            label5.ForeColor = System.Drawing.Color.Black;
            try
            {
                if (tableau == null)
                {
                    // Set the color of the selected text to red
                    richTextBox1.SelectionColor = Color.Red;

                    richTextBox1.Text += "> No tableau loaded.\n\n";

                    // Reset the color for subsequent text
                    richTextBox1.SelectionColor = richTextBox1.ForeColor;
                    return;
                }
                var analysis = SensitivityAnalysis.Analise(tableau.Copy());
                richTextBox1.Text += $"# Sensitivity Analysis\n\n{analysis}\n\n";
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
                label5.Text = "Error";
                label5.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = "";
        }
    }
}

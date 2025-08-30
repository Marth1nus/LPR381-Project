using LPR381.LP;
using Markdig;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
            { "Primal Simplex" /*         */, PrimalSimplex /*                         */ .Solve },
            { "Primal Simplex Revised" /* */, PrimalSimplex /*                         */ .SolveRevised },
            { "Dual Simplex" /*           */, DualSimplex /*                           */ .Solve },
            { "Cutting Plane" /*          */, CuttingPlane /*                          */ .Solve },
            { "Branch&Bound" /*           */, BranchAndBound /*                        */ .Solve },
            { "Branch&Bound-Knapsack" /*  */, BranchAndBoundKnapsack /*                */ .Solve },
            { "Sensitivity Analysis" /*   */, SensitivityAnalysis /*                   */ .Solve },
        }; 
        private Solver Solver => AlgorithmDict[comboBox1.SelectedItem.ToString()];
        private string SolverName => comboBox1.SelectedItem.ToString();

        private string _SolutionText = "";
        private string SolutionText
        {
            get => _SolutionText; 
            set { _SolutionText = value; DisplaySolutionText(); }
        }

        private Tableau tableau;

        private static readonly MarkdownPipeline Pileline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()   // includes footnotes, tables, definition lists, etc.
                .UsePipeTables()           // Enables pipe tables like in Pandoc
                .UseGridTables()           // Optional: enables grid tables
                .UseAutoLinks()            // convert URLs to links
                .UseEmphasisExtras()       // allows underscore inside words
                .UseTaskLists()            // GitHub-style task lists
                .UseYamlFrontMatter()      // front matter support
                .UseEmojiAndSmiley()       // emoji like :smile:
                .UseGenericAttributes()    // allows {#id .class} attributes
                .Build();

        public Form1()
        {
            InitializeComponent();
            comboBox1.Items.Clear();
            foreach (var kv in AlgorithmDict)
                comboBox1.Items.Add(kv.Key);
            comboBox1.SelectedIndex = 4;
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            UpdateSensitivityAnalysisOptions();
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
                    SolutionText = "**No Tablueau**\n";
                    return;
                }
                var newTableau = tableau.Copy();
                var steps = Solver(newTableau);
                var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                SolutionText += $"# Algorithim Selected: {SolverName}\n\n{stepsString}\n\n";
                tableau = newTableau;
            }
            catch (Exception err)
            {
                MessageBox.Show($"{err.Message}\n\n{err}", "Solve Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label4.Text = "Error";
                label4.ForeColor = System.Drawing.Color.Red;
            }
                UpdateSensitivityAnalysisOptions();
        }

        private void clearOutputToolStripMenuItem_Click(object sender, EventArgs e) => SolutionText = "";

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                tableau = Tableau.FromFile(openFileDialog1.FileName);
                textBox1.Text = openFileDialog1.FileName.Split('\\').Last();
                SolutionText = $"{Tableau.FromFileCanonicalForm(openFileDialog1.FileName)}\n\n# Tableau\n\n{tableau}\n\n";
                UpdateSensitivityAnalysisOptions();
            }
            catch (Exception err)
            {
                MessageBox.Show($"{err.Message}\n\n{err}", "File Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBox1.Text = "Failed";
            }
        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                File.WriteAllText(saveFileDialog1.FileName, SolutionText);
                textBox2.Text = saveFileDialog1.FileName.Split('\\').Last();
            }
            catch (Exception err)
            {
                MessageBox.Show($"{err.Message}\n\n{err}", "File Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBox2.Text = "Failed";
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                label13.Text = "";
                label13.ForeColor = System.Drawing.Color.Black;

                var steps = (List<string>)null;
                var sensitivityAnalysisName = "";
                var rowName = comboBox2.SelectedText;
                var colName = comboBox3.SelectedText;
                var newValue = (double)numericUpDown1.Value;

                for (bool singleIteration = true; singleIteration; singleIteration = false)
                {
                    var i = tableau.RowNames.ToList().IndexOf(rowName);
                    if (i < 0)
                    {
                        label13.Text = "Row not found";
                        label13.ForeColor = System.Drawing.Color.Red;
                        break;
                    }

                    var j = tableau.ColumnNames.ToList().IndexOf(colName);
                    if (j < 0)
                    {
                        label13.Text = "Column not found";
                        label13.ForeColor = System.Drawing.Color.Red;
                        break;
                    }

                    if /**/ (i == 0)
                    {
                        sensitivityAnalysisName = "Objective Coefficient Change";
                        steps = SensitivityAnalysis.SolveApplyChangeObjectiveRow(tableau, j, newValue);
                    }
                    else if (j == tableau.Width - 1)
                    {
                        sensitivityAnalysisName = "Right Hand Side Change";
                        steps = SensitivityAnalysis.SolveApplyChangeRHSColumn(tableau, i, newValue);
                    }
                    else if (tableau.IsNonBasicVariable(j))
                    {
                        sensitivityAnalysisName = "Non-Basic Variable Value Change";
                        steps = SensitivityAnalysis.SolveApplyChangeNonBasicColumn(tableau, i, j, newValue);
                    }
                    else
                    {
                        steps = new List<string>() { $"Changin row:{rowName}, col:{colName} would require full resolve." };
                        break;
                    }
                }

                if (steps != null)
                {
                    var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                    SolutionText += $"# Sensitivity Based Change {sensitivityAnalysisName}\n\n{stepsString}\n\n";
                }
            }
            catch (Exception err)
            {
                MessageBox.Show($"{err.Message}\n\n{err}", "File Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label13.Text = "Failed";
                label13.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void DisplaySolutionText()
        {
            var htmlBodyContent = Markdown.ToHtml(SolutionText, Pileline);

            richTextBox1.Text = SolutionText;
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();

            richTextBox2.Text = htmlBodyContent;
            richTextBox2.SelectionStart = richTextBox2.Text.Length;
            richTextBox2.ScrollToCaret();

            webBrowser1.DocumentText = $@"
                <!DOCTYPE html>
                <html lang=""en"">
                    <head>
                        <meta charset=""UTF-8"">
                        <title>LPR381-Project</title>
                        <style>
                            body {{
                                background-color: #1e2125;
                                font-family: 'Courier New', Courier, monospace;
                                color: #f0f0f0;
                                margin: 40px;
                            }}
                            blockquote {{
                                border-left: 4px solid #555b6e;
                                padding: 4px 32px;
                            }}
                            table {{
                                border-collapse: collapse;
                                color: #e0e2e8;
                                background-color: #2c3044; /* Dark background for table rows */
                                border-radius: 8px;
                                overflow: hidden; /* Clips content to match the border-radius */
                                box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2);
                                font-size: 16px;
                            }}
                            thead tr {{
                                background-color: #353a50; /* A slightly lighter shade for the main header */
                            }}
                            tr {{
                                border-bottom: 1px solid #353a50;
                            }}
                            th, td {{
                                padding: 4px;
                                width: 96px;
                                white-space: nowrap;
                            }}
                            th {{
                                font-weight: 700;
                                font-size: 14px;
                                color: #a0a5b5; /* Muted color for header text */
                            }}
                        </style>
                    </head>
                    <body>
                        {htmlBodyContent}
                    </body>
                </html>
            ";
        }


        private void UpdateSensitivityAnalysisOptions()
        {
            var previousRowName = comboBox2.SelectedItem?.ToString() ?? "";
            comboBox2.Enabled = false;
            comboBox2.Items.Clear();

            var previousColName = comboBox3.SelectedItem?.ToString() ?? "";
            comboBox3.Enabled = false;
            comboBox3.Items.Clear();

            numericUpDown1.Enabled = false;
            numericUpDown1.Value = 0;

            button5.Enabled = false;

            if (tableau == null || !tableau.IsOptimal)
                return;

            comboBox2.Enabled = true;
            foreach (var rowName in tableau.RowNames)
                comboBox2.Items.Add(rowName);
            var previousRowNameIndex = comboBox2.Items.IndexOf(previousRowName);
            var rowNameIndex = comboBox2.SelectedIndex = previousRowNameIndex < 0 ? 0 : previousRowNameIndex;

            comboBox3.Enabled = true;
            foreach (var colName in tableau.ColumnNames)
                comboBox3.Items.Add(colName);
            var previousColNameIndex = comboBox3.Items.IndexOf(previousColName);
            var colNameIndex = comboBox3.SelectedIndex = previousColNameIndex < 0 ? 0 : previousColNameIndex;

            numericUpDown1.Enabled = true;
            numericUpDown1.Value = (decimal)tableau[rowNameIndex, colNameIndex];

            button5.Enabled = true;
        }

        private void comboBox2or3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tableau == null)
                return;
            var rowNameIndex = tableau?.RowNames /*    */?.ToList().IndexOf(comboBox2.SelectedItem as string ?? "") ?? -1;
            var colNameIndex = tableau?.ColumnNames /* */?.ToList().IndexOf(comboBox3.SelectedItem as string ?? "") ?? -1;
            if (rowNameIndex < 0 || colNameIndex < 0)
                return;
            numericUpDown1.Value = (rowNameIndex < 0 || colNameIndex < 0)
                ? 0 
                : (decimal)tableau[rowNameIndex, colNameIndex];
        }
    }
}




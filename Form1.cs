using LPR381.LP;
using Markdig;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private Solver Solver => comboBox1.SelectedValue as Solver;
        private string SolverName => AlgorithmDict.FirstOrDefault(kv => kv.Value == Solver).Key;

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
            comboBox1.DataSource = new BindingSource(AlgorithmDict, null);
            comboBox1.DisplayMember = "Key";
            comboBox1.ValueMember = "Value";
            comboBox1.SelectedValue = AlgorithmDict["Primal Simplex"];
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) => openFileDialog1.ShowDialog(this);

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) => saveFileDialog1.ShowDialog(this);

        private void clearOutputToolStripMenuItem_Click(object sender, EventArgs e) => SolutionText = "";

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
                SolutionText += $"# Solve Using {SolverName}\n\n{stepsString}\n\n";
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
                    SolutionText = "**No Tablueau**\n";
                    return;
                }
                var analysis = SensitivityAnalysis.Analise(tableau.Copy());
                SolutionText += $"# Sensitivity Analysis\n\n{analysis}\n\n";
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
                SolutionText = $"{Tableau.FromFileCanonicalForm(openFileDialog1.FileName)}\n\n# Tableau\n\n{tableau}\n\n";
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
                File.WriteAllText(saveFileDialog1.FileName, SolutionText);
                textBox2.Text = saveFileDialog1.FileName.Split('\\').Last();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                textBox2.Text = "Failed";
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
                                width: 64px;
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
    }
}

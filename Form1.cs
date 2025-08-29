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

        private static readonly Dictionary<string, Solver> SensitivityDict = new Dictionary<string, Solver>
        {
            { "Add a new activity to an optimal solution" /*                                                    */, SensitivityAnalysis /*                   */ .Solve },
            { "Add a new constraint to an optimal solution" /*                                                  */, SensitivityAnalysis /*                   */ .Solve },
            { "Display the shadow prices" /*                                                                    */, SensitivityAnalysis /*                   */ .SolveDisplayShadowPrices },
            { "Duality" /*                                                                                      */, SensitivityAnalysis /*                   */ .Solve },
        };
        private static readonly Dictionary<string, SolverWithTwoParams> SensitivityDictWithTwoParams = new Dictionary<string, SolverWithTwoParams>
        {
            { "Display the range of a selected Non-Basic Variable" /*                                           */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Apply and display a change of a selected Non-Basic Variable" /*                                  */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Display the range of a selected Basic Variable" /*                                               */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Apply and display a change of a selected Basic Variable" /*                                      */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Display the range of a selected constraint right-hand-side value" /*                             */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Apply and display a change of a selected constraint right-hand-side value" /*                    */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Display the range of a selected variable in a Non-Basic Variable column" /*                      */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
            { "Apply and display a change of a selected variable in a Non-Basic Variable column" /*             */, SensitivityAnalysis /*                   */ .SolveDisplayNonBasicRanges },
        };
        private Solver SolverSensitivity => SensitivityDict[comboBox2.SelectedItem.ToString()];
        private SolverWithTwoParams SolverSensitivityWithTwoParams => SensitivityDictWithTwoParams[comboBox2.SelectedItem.ToString()];
        private string SolverNameSensitivity => comboBox2.SelectedItem.ToString();

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
                MessageBox.Show(err.Message + $"\n\n{err}", "Solve Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label4.Text = "Error";
                label4.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void clearOutputToolStripMenuItem_Click(object sender, EventArgs e) => SolutionText = "";

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            try
            {
                tableau = Tableau.FromFile(openFileDialog1.FileName);
                textBox1.Text = openFileDialog1.FileName.Split('\\').Last();
                SolutionText = $"{Tableau.FromFileCanonicalForm(openFileDialog1.FileName)}\n\n# Tableau\n\n{tableau}\n\n";
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message + $"\n\n{err}", "File Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show(err.Message + $"\n\n{err}", "File Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void applySensitivityAnalysis(object sender, EventArgs e)
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
    
                if (SensitivityDict.ContainsKey(SolverNameSensitivity))
                {
                    var steps = SolverSensitivity(newTableau);
                    var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                    SolutionText += $"# Algorithim Selected: {SolverNameSensitivity}\n\n{stepsString}\n\n";
                    tableau = newTableau;
                }
                else if (SensitivityDictWithTwoParams.ContainsKey(SolverNameSensitivity))
                {
                    string var = "";
                    if (comboBox3.SelectedIndex != -1 && comboBox4.SelectedIndex != -1)
                    {
                        var = comboBox3.SelectedItem.ToString() + comboBox4.SelectedItem.ToString();
                    } else
                    {
                        MessageBox.Show(
                            "Please select an item from both the Prefix and Suffix combo boxes.",
                            "Missing Selection",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                        return;
                    }
                    var steps = SolverSensitivityWithTwoParams(newTableau, new[] { Array.IndexOf(tableau.ColumnNames, var) });
                    var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                    SolutionText += $"# Algorithim Selected: {SolverNameSensitivity}\n\n{stepsString}\n\n";
                    tableau = newTableau;
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message + $"\n\n{err}", "Solve Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label4.Text = "Error";
                label4.ForeColor = System.Drawing.Color.Red;
            }
            label4.ForeColor = System.Drawing.Color.Red;
            }
        }
    }




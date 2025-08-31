using LPR381.LP;
using Markdig;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
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

        // ARMAND Tasks 9, 10, and 12 of sensistivity analysis.
        private Label lblColumn;
        private TextBox txtColumn;
        private Label lblCost;
        private TextBox txtCost;
        private Button btnAddActivity;
        private Label lblRow;
        private TextBox txtRow;
        private Label lblRhs;
        private TextBox txtRhs;
        private Button btnAddConstraint;
        private Button btnSolveDual;
        private Control[] armandComponents;

        public Form1()
        {
            InitializeComponent();
            InitializeNewComponents();
            comboBox1.Items.Clear();
            foreach (var kv in AlgorithmDict)
                comboBox1.Items.Add(kv.Key);
            comboBox1.SelectedIndex = Math.Max(0, comboBox1.Items.IndexOf("Primal Simplex"));
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            UpdateSensitivityAnalysisOptions();
        }

        // ARMAND
        private void InitializeNewComponents()
        {
            // Task 9: Add Activity
            lblColumn = new Label
            {
                Text = "New Column Coefficients (comma-separated):",
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            };
            txtColumn = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            lblCost = new Label
            {
                Text = "Cost/Profit:",
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            };
            txtCost = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            btnAddActivity = new Button
            {
                Text = "Add Activity",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            btnAddActivity.Click += BtnAddActivity_Click;

            // Task 10: Add Constraint
            lblRow = new Label
            {
                Text = "New Row Coefficients (comma-separated):",
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            };
            txtRow = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            lblRhs = new Label
            {
                Text = "RHS:",
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            };
            txtRhs = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            btnAddConstraint = new Button
            {
                Text = "Add Constraint",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            btnAddConstraint.Click += BtnAddConstraint_Click;

            // Task 12: Solve Dual
            btnSolveDual = new Button
            {
                Text = "Solve Dual",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            btnSolveDual.Click += BtnSolveDual_Click;

            armandComponents = new Control[]
            {
                lblColumn,
                txtColumn,
                lblCost,
                txtCost,
                new Label(),
                btnAddActivity,
                new Label(),
                new Label(),
                lblRow,
                txtRow,
                lblRhs,
                txtRhs,
                new Label(),
                btnAddConstraint,
                new Label(),
                new Label(),
                new Label(),
                btnSolveDual,
                new Label(),
                new Label(),
            };
            tableLayoutPanel2.RowCount = 30;
            foreach (var component in armandComponents)
            {
                component.Dock = DockStyle.Fill;
                component.Margin = new Padding(0);
                tableLayoutPanel2.Controls.Add(component);
            }
        }

        private void BtnAddActivity_Click(object sender, EventArgs e)
        {
            try
            {
                label13.Text = "";
                label13.ForeColor = System.Drawing.Color.Black;
                if (tableau == null)
                {
                    SolutionText = "**No Tableau**\n";
                    return;
                }

                // Parse column coefficients
                double[] column = txtColumn.Text.Split(',')
                    .Select(s => double.Parse(s.Trim()))
                    .ToArray();
                if (column.Length != tableau.Height - 1)
                {
                    label13.Text = $"Column must have {tableau.Height - 1} coefficients.";
                    label13.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                // Parse cost
                if (!double.TryParse(txtCost.Text, out double cost))
                {
                    label13.Text = "Invalid cost value.";
                    label13.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                // Call SolveAddActivity
                var newTableau = tableau.Copy(); // Copy to avoid modifying original
                var steps = SensitivityAnalysis.SolveAddActivity(newTableau, column, cost);
                var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                SolutionText += $"# Add New Activity\n\n{stepsString}\n\n";
                tableau = newTableau; // Update tableau with new solution
                UpdateSensitivityAnalysisOptions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Add Activity Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label13.Text = "Failed";
                label13.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void BtnAddConstraint_Click(object sender, EventArgs e)
        {
            try
            {
                label13.Text = "";
                label13.ForeColor = System.Drawing.Color.Black;
                if (tableau == null)
                {
                    SolutionText = "**No Tableau**\n";
                    return;
                }

                // Parse row coefficients
                double[] row = txtRow.Text.Split(',')
                    .Select(s => double.Parse(s.Trim()))
                    .ToArray();
                int expectedCoefficients = tableau.Width - 1; // Should be 13 based on the error
                if (row.Length != expectedCoefficients)
                {
                    label13.Text = $"Row must have {expectedCoefficients} values excluding RHS.";
                    label13.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                // Parse RHS
                if (!double.TryParse(txtRhs.Text, out double rhs))
                {
                    label13.Text = "Invalid RHS value.";
                    label13.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                // Call SolveAddConstraint
                var newTableau = tableau.Copy(); // Copy to avoid modifying original
                var steps = SensitivityAnalysis.SolveAddConstraint(newTableau, row, rhs);
                var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                SolutionText += $"# Add New Constraint\n\n{stepsString}\n\n";
                tableau = newTableau; // Update tableau with new solution
                UpdateSensitivityAnalysisOptions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Add Constraint Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label13.Text = "Failed";
                label13.ForeColor = System.Drawing.Color.Red;
            }
        }

        private void BtnSolveDual_Click(object sender, EventArgs e)
        {
            try
            {
                label13.Text = "";
                label13.ForeColor = System.Drawing.Color.Black;
                if (tableau == null)
                {
                    SolutionText = "**No Tableau**\n";
                    return;
                }

                // Call SolveDual
                var newTableau = tableau.Copy(); // Copy to avoid modifying original
                var steps = SensitivityAnalysis.SolveDual(newTableau);
                var stepsString = string.Join("\n\n", steps.Select(step => "> " + step.Replace("\n", "\n> ")));
                SolutionText += $"# Solve Dual\n\n{stepsString}\n\n";
                tableau = newTableau; // Update tableau
                UpdateSensitivityAnalysisOptions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Solve Dual Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                label13.Text = "Failed";
                label13.ForeColor = System.Drawing.Color.Red;
            }
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
                UpdateSensitivityAnalysisOptions();
            }
            catch (Exception err)
            {
                MessageBox.Show($"{err.Message}\n\n{err}", "Solve Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                var rowName = comboBox2.SelectedItem?.ToString() ?? "0. ";
                var colName = comboBox3.SelectedItem?.ToString() ?? "0. ";
                var newValue = (double)numericUpDown1.Value;
                rowName = rowName.Substring(rowName.IndexOf('.') + 1).Trim();
                colName = colName.Substring(colName.IndexOf('.') + 1).Trim();

                for (bool singleIteration = true; singleIteration; singleIteration = false)
                {
                    var i = tableau.RowNames.ToList().IndexOf(rowName);
                    if (i < 0)
                    {
                        label13.Text = $"Row not found \"${rowName}\"";
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

        private void comboBox2or3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tableau == null)
                return;

            var rowName = comboBox2.SelectedItem?.ToString() ?? "0. ";
            rowName = rowName.Substring(rowName.IndexOf('.') + 1).Trim();
            var rowNameIndex = tableau?.RowNames /*    */?.ToList().IndexOf(rowName) ?? -1;

            var colName = comboBox3.SelectedItem?.ToString() ?? "0. ";
            colName = colName.Substring(colName.IndexOf('.') + 1).Trim();
            var colNameIndex = tableau?.ColumnNames /* */?.ToList().IndexOf(colName) ?? -1;

            var newValue = numericUpDown1.Value =
                (rowNameIndex >= 0 && colNameIndex >= 0)
                    ? (decimal)(tableau.InitialTableau ?? tableau)[rowNameIndex, colNameIndex]
                    : 0;
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
            var previousRowName = comboBox2.SelectedItem?.ToString() ?? "0. ";
            previousRowName = previousRowName.Substring(previousRowName.IndexOf('.') + 1).Trim();
            comboBox2.Enabled = false;
            comboBox2.Items.Clear();

            var previousColName = comboBox3.SelectedItem?.ToString() ?? "0. ";
            previousColName = previousColName.Substring(previousColName.IndexOf('.') + 1).Trim();
            comboBox3.Enabled = false;
            comboBox3.Items.Clear();

            numericUpDown1.Enabled = false;
            numericUpDown1.Value = 0;

            button5.Enabled = false;

            foreach (var component in armandComponents)
                if (!(component is Label))
                    component.Enabled = false;

            if (tableau == null || !tableau.IsOptimal)
                return;

            comboBox2.Enabled = true;
            foreach (var rowName in tableau.RowNames.Select((s, i) => $"{1 + i,3}. {s,6}"))
                comboBox2.Items.Add(rowName);
            var previousRowNameIndex = comboBox2.Items.IndexOf(previousRowName);
            var rowNameIndex = comboBox2.SelectedIndex = previousRowNameIndex < 0 ? 0 : previousRowNameIndex;

            comboBox3.Enabled = true;
            foreach (var colName in tableau.ColumnNames.Select((s, i) => $"{1 + i,3}. {s,6}"))
                comboBox3.Items.Add(colName);
            var previousColNameIndex = comboBox3.Items.IndexOf(previousColName);
            var colNameIndex = comboBox3.SelectedIndex = previousColNameIndex < 0 ? 0 : previousColNameIndex;

            numericUpDown1.Enabled = true;
            // Value Implied updated by combox2&3 // numericUpDown1.Value = (decimal)tableau[rowNameIndex, colNameIndex];

            button5.Enabled = true;

            foreach (var component in armandComponents)
                if (!(component is Label))
                    component.Enabled = true;
        }

    }
}




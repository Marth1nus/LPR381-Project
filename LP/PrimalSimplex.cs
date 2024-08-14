using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LPR381.LP
{
    // Delegate solver function
    public delegate List<string> Solver(ref Tableu tableu);

    // Struct representing Simplex tableau
    public struct Tableu
    {
        // Properties for row, column names, restrictions, and values
        public string[] RowNames { get; set; }
        public string[] ColumnNames { get; set; }
        public string[] ColumnRestrictions { get; set; }
        public double[,] Values { get; set; }
        public int TableIteration { get; set; }
        public int Height { get { return Values.GetLength(0); } }
        public int Width { get { return Values.GetLength(1); } }
        public double this[int i, int j] { get { return Values[i, j]; } set { Values[i, j] = value; } }

        // Constructor initializing the tableau
        public Tableu(int height = 2, int width = 3)
        {
            RowNames = Enumerable.Range(0, height).Select(i => $"c{i}").ToArray();
            RowNames[0] = "max z";
            ColumnNames = Enumerable.Range(0, width).Select(j => $"x{1 + j}").ToArray();
            ColumnNames[ColumnNames.Length - 1] = "rhs";
            ColumnRestrictions = Enumerable.Repeat("+", width).ToArray();
            Values = new double[height, width];
            TableIteration = 0;
        }

        // Returns the indices of basic variables
        public int[] BasicVariableIndices() => ColumnNames.Select((_, j) => j).Where(IsBasicVariable).ToArray();

        // Checks if a column is a basic variable
        public bool IsBasicVariable(int j) => BasicVariableValue(j) != null;

        // Returns the value of a basic variable if it exists
        public double? BasicVariableValue(int j) => BasicVariableValue(j, out int _);

        // Returns the value of a basic variable if it exists, and its row index
        public double? BasicVariableValue(int j, out int oneI)
        {
            int zeros = 0, ones = 0; oneI = 0;
            for (int i = 0; i < Height; i++)
                if (Values[i, j] == 0) zeros += 1;
                else if (Values[i, j] == 1) { ones += 1; oneI = i; }
                else return null;
            if (!(zeros == Height - 1 && ones == 1))
                return null;
            return Values[oneI, Width - 1];
        }

        // Validates the dimensions and consistency of the tableau
        public void ValidateLengths()
        {
            if (!(Height >= 2 && Width >= 3))
                throw new Exception("Table must be at least 2x3");
            if (!(RowNames.Length == Height))
                throw new Exception("Row Length inconsistent");
            if (!(ColumnNames.Length == Width))
                throw new Exception("Column Length inconsistent");
            if (!(ColumnRestrictions.Length <= Width))
                throw new Exception("Column Restriction Length too long");
        }

        // Performs a pivot operation on the tableau
        public string Pivot(int rowI, int colI)
        {
            double pivot = Values[rowI, colI];
            // Normalize the pivot row
            for (int j = 0; j < Values.GetLength(1); j++)
            {
                Values[rowI, j] /= pivot;
            }
            // Update the other rows
            for (int i = 0; i < Values.GetLength(0); i++)
            {
                if (i == rowI)
                    continue;
                double factor = Values[i, colI];
                for (int j = 0; j < Values.GetLength(1); j++)
                {
                    Values[i, j] -= factor * Values[rowI, j];
                }
            }
            TableIteration++;
            return $"Pivot on {RowNames[rowI]}, {ColumnNames[colI]}\n\n{this}";
        }

        // Adds a new row to the tableau
        public void AddRow(double[] newRow, string name = null)
        {
            if (newRow.Length != Values.GetLength(0))
                throw new ArgumentException($"New row must have {Values.GetLength(0)} values");
            var oldValues = Values;
            Values = new double[Height + 1, Width];
            int i = 0;
            for (; i < Height - 1; i++)
                for (int j = 0; i < Width; j++)
                    Values[i, j] = oldValues[i, j];
            for (; i < Height; i++)
                for (int j = 0; i < Width; j++)
                    Values[i, j] = newRow[j];
            RowNames.Append(name ?? $"c{RowNames.Length}");
        }

        // Removes a row from the tableau
        public void RemoveRow(int rowI)
        {
            if (rowI < 0 || Height <= rowI)
                throw new ArgumentException($"Out of range rowI:{rowI} parameter");
            var oldValues = Values;
            Values = new double[Height - 1, Width];
            int i = 0;
            for (; i < rowI; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i, j];
            for (; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i + 1, j];
        }

        // Adds a new column to the tableau
        public void AddColumn(double[] newColumn, string name = null, string restriction = "urs")
        {
            if (newColumn.Length != Height)
                throw new ArgumentException($"New row must have {Height} values");
            var oldValues = Values;
            Values = new double[Height, Width + 1];
            int j = 0;
            for (; j < Width - 2; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = oldValues[i, j];
            for (; j < Width - 1; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = newColumn[i];
            for (; j < Width; j++)
                for (int i = 0; i < Height; i++)
                    Values[i, j] = oldValues[i, j];
            ColumnNames.Append(name ?? $"s{Height}");
            ColumnRestrictions.Append(restriction);
        }

        // Removes a column from the tableau
        public void RemoveColumn(int colI)
        {
            if (colI < 0 || Width <= colI)
                throw new ArgumentException($"Out of range colI:{colI} parameter");
            var oldValues = Values;
            Values = new double[Height, Width - 1];
            for (int i = 0; i < Height; i++)
            {
                int j = 0;
                for (; j < colI; j++)
                    Values[i, j] = oldValues[i, j];
                for (; j < Width; j++)
                    Values[i, j] = oldValues[i, j + 1];
            }
        }

        // Converts the tableau to a string representation
        public override string ToString()
        {
            const int colWidth = 6;
            StringBuilder sb = new StringBuilder();
            sb.Append($"| T{TableIteration,1 - colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {ColumnNames[j],colWidth} ");
            sb.AppendLine($"|");
            for (int j = 0; j < Width + 1; j++)
                sb.Append($"| {"-----:",colWidth} ");
            sb.AppendLine($"|");
            for (int i = 0; i < Height; i++)
            {
                sb.Append($"| {RowNames[i],colWidth} ");
                for (int j = 0; j < Width; j++)
                    sb.Append($"| {Values[i, j].ToString("F3", CultureInfo.InvariantCulture),colWidth} ");
                sb.AppendLine($"|");
            }
            sb.Append($"| {"",colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {(j < ColumnRestrictions.Length ? ColumnRestrictions[j] : ""),colWidth} ");
            sb.AppendLine($"|");
            return sb.ToString();
        }

        // Reads a tableau from a file
        public static Tableu FromFile(string filename)
        {
            var lines = File.ReadAllLines(filename, Encoding.UTF8)
                .Select(line => Regex.Split(line, @"\s+"))
                .ToArray();
            if (lines.Length < 2)
                throw new Exception("Too Few Rows");

            var objectiveLine = lines.First();
            var constraintLines = lines.Skip(1).Reverse().Skip(1).Reverse().ToArray();
            var restrictionsLine = lines.Last();
            var rowLength = objectiveLine.Length;
            lines = null;

            if (objectiveLine.Length < 2)
                throw new Exception("Objective Row has too few columns");
            if (!objectiveLine.Skip(1).All(col => Regex.IsMatch(col, @"^[+-]\d$")))
                throw new Exception("Objective Row contains invalid coefficient");
            if (!objectiveLine.Take(1).All(col => Regex.IsMatch(col, @"^(min|max)$")))
                throw new Exception("Objective Row must start with min/max");

            if (!constraintLines.All(line => line.Length == rowLength))
                throw new Exception("Constraint Rows have inconsistent lengths");
            if (!constraintLines.All(line => line.Reverse().Skip(1).All(col => Regex.IsMatch(col, @"^[+-]\d$"))))
                throw new Exception("Constraint Rows contains invalid coefficient");
            if (!constraintLines.All(line => line.Reverse().Take(1).All(col => Regex.IsMatch(col, @"^[><=]$"))))
                throw new Exception("Constraint Rows must have a valid condition");

            if (!Regex.IsMatch(restrictionsLine.First(), @"^b(v|v\+\d+)?$"))
                throw new Exception("Restriction Row has invalid first column");
            if (!restrictionsLine.Skip(1).All(col => Regex.IsMatch(col, @"^(urs|s\+|s\-)")))
                throw new Exception("Restriction Rows contains invalid restriction");

            var width = objectiveLine.Length - 1;
            var height = constraintLines.Length + 1;
            var tableu = new Tableu(height, width);
            tableu.ColumnNames = objectiveLine.Skip(1).ToArray();
            tableu.ColumnRestrictions = restrictionsLine.Skip(1).ToArray();
            tableu.Values[0, 0] = 1;
            tableu.ColumnNames.Append("rhs");
            for (int j = 0; j < width - 1; j++)
            {
                var val = objectiveLine[j + 1];
                tableu.Values[0, j] = double.Parse(val);
            }
            tableu.RowNames[0] = $"{objectiveLine[0]} z";
            for (int i = 1; i < height; i++)
            {
                tableu.RowNames[i] = $"c{i}";
                var line = constraintLines[i - 1];
                for (int j = 0; j < width; j++)
                {
                    var val = line[j];
                    tableu.Values[i, j] = double.Parse(val);
                }
            }

            return tableu;
        }
    }

    // Class for solving the linear programming problem using the primal simplex method
    public class PrimalSimplex
    {
        // Solves the linear programming problem
        public static List<string> Solve(ref Tableu tableu)
        {
            List<string> messages = new List<string>();
            while (true)
            {
                int pivotCol = ChooseEnteringColumn(ref tableu);
                if (pivotCol == -1)
                    break;

                int pivotRow = ChooseLeavingRow(ref tableu, pivotCol);
                if (pivotRow == -1)
                    break;

                string pivotMessage = tableu.Pivot(pivotRow, pivotCol);
                messages.Add(pivotMessage);
            }

            return messages;
        }

        // Chooses the entering column for the pivot operation
        private static int ChooseEnteringColumn(ref Tableu tableu)
        {
            int pivotCol = -1;
            double lowestValue = 0;
            for (int j = 0; j < tableu.Width - 1; j++)
            {
                if (tableu[0, j] < lowestValue)
                {
                    lowestValue = tableu[0, j];
                    pivotCol = j;
                }
            }
            return pivotCol;
        }

        // Chooses the leaving row for the pivot operation
        private static int ChooseLeavingRow(ref Tableu tableu, int pivotCol)
        {
            int pivotRow = -1;
            double minRatio = double.PositiveInfinity;

            for (int i = 1; i < tableu.Height; i++)
            {
                if (tableu[i, pivotCol] > 0)
                {
                    double ratio = tableu[i, tableu.Width - 1] / tableu[i, pivotCol];
                    if (ratio < minRatio)
                    {
                        minRatio = ratio;
                        pivotRow = i;
                    }
                }
            }

            return pivotRow;
        }
    }
}

namespace LPR381
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize the tableau with a specific problem
            var tableu = new LP.Tableu(4, 5)
            {
                RowNames = new[] { "max z", "c1", "c2", "c3" },
                ColumnNames = new[] { "x1", "x2", "s1", "s2", "rhs" },
                ColumnRestrictions = new[] { "+", "+", "+", "+", "+" },
                Values = new double[4, 5]
                {
                    { -3, -5, 0, 0, 0 },
                    { 1, 0, 1, 0, 4 },
                    { 0, 2, 0, 1, 12 },
                    { 3, 2, 0, 0, 18 }
                }
            };

            // Solve the problem using the primal simplex method
            var messages = LP.PrimalSimplex.Solve(ref tableu);

            // Print the pivot operations and final tableau
            foreach (var message in messages)
            {
                Console.WriteLine(message);
            }

            Console.WriteLine("Final Tableu:");
            Console.WriteLine(tableu);
        }
    }
}

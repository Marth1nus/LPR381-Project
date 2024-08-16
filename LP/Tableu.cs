using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPR381.LP
{
    public delegate List<string> Solver(Tableu tableu);

    public class Tableu
    {
        public string[] RowNames { get; set; } // example [max z, c1, c2]
        public string[] ColumnNames { get; set; } // example [x1, x2, s1, s2, rhs]
        public string[] ColumnRestrictions { get; set; } // one of [+, -, urs, int, bin]
        public double[,] Values { get; set; } // contains Width, Height, and the values in the tableu.
        public int TableIteration { get; set; } // Just keeps track of how many pivots have been done.
        public Tableu InitialTable { get; set; } // Tracks The intial table
        public int Height { get { return Values.GetLength(0); } }
        public int Width { get { return Values.GetLength(1); } }
        public double this[int i, int j] { get { return Values[i, j]; } set { Values[i, j] = value; } }

        // Assume Values[, Width-1] is the RHS column
        // Assume Values[0,] is the Objective row

        public Tableu(int height = 2, int width = 3)
        {
            RowNames /*           */ = Enumerable.Range(1, height).Select(i => $"c{i}").Prepend("max z").ToArray();
            ColumnNames /*        */ = Enumerable.Range(1, width).Select(j => $"x{j}").Append("rhs").ToArray();
            ColumnRestrictions /* */ = Enumerable.Repeat("+", width).ToArray();
            Values /*             */ = new double[height, width];
            TableIteration /*     */ = 0;
        }

        public Tableu Copy()
        {
            return new Tableu
            {
                RowNames /*           */ = RowNames.ToArray(),
                ColumnNames /*        */ = ColumnNames.ToArray(),
                ColumnRestrictions /* */ = ColumnRestrictions.ToArray(),
                Values /*             */ = Copy(Values),
                TableIteration /*     */ = TableIteration
            };
        }

        public int[] GetBasicVariableIndices() => ColumnNames.Select((_, j) => j).Where(IsBasicVariable).ToArray();

        public bool IsBasicVariable(int j) => BasicVariableValue(j) != null;
        public double? BasicVariableValue(int j) => BasicVariableValue(j, out int _);
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

        public string Pivot(int rowI, int colI)
        {
            InitialTable = InitialTable ?? Copy();
            TableIteration++;
            double pivot = Values[rowI, colI];
            for (int j = 0; j < Values.GetLength(1); j++)
            {
                Values[rowI, j] /= pivot;
            }
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
            return $"Pivot on {RowNames[rowI]}, {ColumnNames[colI]}\n\n{this}";
        }

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

        public override string ToString()
        {
            const int colWidth = 6; // "00.000".Length
            StringBuilder sb = new StringBuilder();
            /* | T1     |     x1 |     s1 |    rhs | */
            sb.Append($"| T{TableIteration,1 - colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {ColumnNames[j], colWidth} ");
            sb.AppendLine($"|");
            /* | -----: | -----: | -----: | -----: | */
            for (int j = 0; j < Width + 1; j++)
                sb.Append($"| {"-----:", colWidth} ");
            sb.AppendLine($"|");
            /* |  max Z | 00.000 | 00.000 | 00.000 | */
            /* |     C1 | 00.000 | 00.000 | 00.000 | */
            for (int i = 0; i < Height; i++)
            {
                sb.Append($"| {RowNames[i], colWidth} ");
                for (int j = 0; j < Width; j++)
                    sb.Append($"| {Values[i, j].ToString("F3", CultureInfo.InvariantCulture), colWidth} ");
                sb.AppendLine($"|");
            }
            /* |   Sign |    int |      + |        | */
            sb.Append($"| {"", colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {(j < ColumnRestrictions.Length ? ColumnRestrictions[j] : ""), colWidth} ");
            sb.AppendLine($"|");
            return sb.ToString();
        }

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
            lines = null; // use named variables instead
            var lastChecked = "";

            if (objectiveLine.Length < 2) // must include at least one decision variable
                throw new Exception("Objective Row has too few columns");
            if (!objectiveLine.Skip(1).All(col => Regex.IsMatch(lastChecked = col, @"^[+-]\d$"))) // validate coefficients
                throw new Exception($"Objective Row contains invalid coefficient. While checking: {lastChecked}");
            if (!objectiveLine.Take(1).All(col => Regex.IsMatch(lastChecked = col, @"^(min|max)$"))) // ojective row must start with min/max
                throw new Exception($"Objecive Row must start with min/max. While checking: {lastChecked}");

            if (!constraintLines.All(line => line.Length == rowLength)) // object and constaint rows must have the same width
                throw new Exception($"Constraint Rows have inconsistent lengths");
            if (!constraintLines.All(line => line.Reverse().Skip(1).All(col => Regex.IsMatch(lastChecked = col, @"^[+-]\d+$"))))
                throw new Exception($"Constraint Rows contains invalid coefficient. While checking: {lastChecked}");
            if (!constraintLines.All(line => line.Reverse().Take(1).All(col => Regex.IsMatch(lastChecked = col, @"^(=|<=|>=)\d+$"))))
                throw new Exception($"Constraint Rows contains invalid rhs. While checking: {lastChecked}");

            if (!(restrictionsLine.Length == rowLength - 1)) // object and constaint rows must have the same width
                throw new Exception("Restrictions Row have inconsistent lengths");
            if (!restrictionsLine.All(col => Regex.IsMatch(lastChecked = col, @"^(\+|-|urs|int|bin)$"))) // restrictions row must have valid restrictions
                throw new Exception($"Restrictions Row contains unknown symbol. While checking: {lastChecked}");

            // Row Names
            var rowNamesList = new List<string> { $"{objectiveLine.First()} z" };
            for (int i = 0; i < constraintLines.Length; i++)
                rowNamesList.Add($"c{1 + i}");
            var rowNames = rowNamesList.ToArray();
            rowNamesList = null;

            // Column Names
            var columnNamesList = new List<string>();
            for (int j = 0; j < objectiveLine.Length - 1; j++) // Decision variables
                columnNamesList.Add($"x{1 + j}");
            for (int i = 0; i < constraintLines.Length; i++) // Slack/Excess variables
                columnNamesList.AddRange(
                    constraintLines[i].Last().StartsWith("<=") ? new[] { $"s{1 + i}" } :
                    constraintLines[i].Last().StartsWith(">=") ? new[] { $"e{1 + i}" } :
                    constraintLines[i].Last().StartsWith("=") ? new[] { $"s{1 + i}", $"e{1 + i}" } :
                    throw new Exception("Unkown constraint ineqality"));
            columnNamesList.Add($"rhs"); // RHS
            var columnNames = columnNamesList.ToArray();
            columnNamesList = null;

            // Column Restructions
            var columnRestrictions = columnNames.Select((_, j) => j < restrictionsLine.Length ? restrictionsLine[j] : "+").ToArray();

            // Values
            var height = 1 + constraintLines.Length;
            var widthWithoutSlacks = constraintLines.First().Length;
            var widthOfSlacks = constraintLines.Sum(line => line.Last().StartsWith("=") ? 2 /*slack and excess*/ : 1 /* slack xor excess */);
            var width = widthWithoutSlacks + widthOfSlacks;
            var values = new double[height, width];
            {
                int i = 0;
                int jSlackOrExcess = widthWithoutSlacks;
                for (; i < 1; i++) // Objective Row
                {
                    int j = 0;
                    for (; j < objectiveLine.Length - 1; j++) // Decision Variables
                        values[i, j] = -double.Parse(objectiveLine[1 + j]);
                    for (; j < width; j++) // Slack|Excess Variables and RHS 
                        values[i, j] = 0;
                }
                for (; i < height; i++) // Constraint Rows
                {
                    var rhs = constraintLines[i - 1].Last();
                    var hasS = rhs.StartsWith("<=") || rhs.StartsWith("=");
                    var hasE = rhs.StartsWith(">=") || rhs.StartsWith("=");
                    rhs = rhs.Substring(rhs.StartsWith("=") ? 1 : 2);

                    int j = 0;
                    for (; j < constraintLines[i - 1].Length - 1; j++) // Decision Variables
                        values[i, j] = double.Parse(constraintLines[i - 1][j]);
                    for (; j < width - 1; j++) // Slack|excess Variables
                        values[i, j] = 0;
                    for (; j < width; j++) // RHS
                        values[i, j] = double.Parse(rhs);

                    if (hasS) values[i, jSlackOrExcess++] = 1;
                    if (hasE) values[i, jSlackOrExcess++] = -1;
                    if (hasE) for (j = 0; j < width; j++) values[i, j] *= -1;
                }
            }

            // return
            return new Tableu
            {
                RowNames = rowNames,
                ColumnNames = columnNames,
                ColumnRestrictions = columnRestrictions,
                Values = values,
                TableIteration = 0
            };
        }

        private static double[,] Copy(double[,] from, double[,] to = null,
            int /*     */ CountI = 0, int /*      */ CountJ = 0, // 0 means use `from`'s dimention
            int /* */ fromStartI = 0, int /*  */ fromStartJ = 0,
            int /*   */ toStartI = 0, int /*    */ toStartJ = 0)
        {
            to = to ?? new double[from.GetLength(0), from.GetLength(1)];
            CountI = CountI > 0 ? CountI : from.GetLength(0);
            CountJ = CountJ > 0 ? CountJ : from.GetLength(1);
            for (int i = 0; i < CountI; i++) 
                for (int j = 0; j <= CountJ; j++)
                    to[toStartI + i, toStartJ + j] = from[fromStartI + i, fromStartJ + j];
            return to;
        }
    }
}

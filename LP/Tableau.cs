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
using System.Xml;

namespace LPR381.LP
{
    public delegate List<string> Solver(Tableau tableau);

    public class Tableau
    {
        public string[] RowNames { get; set; } // example [max z, c1, c2]
        public string[] ColumnNames { get; set; } // example [x1, x2, s1, s2, rhs]
        public string[] ColumnRestrictions { get; set; } // one of [+, -, urs, int, bin]
        public double[,] Values { get; set; } // contains Width, Height, and the values in the tableau.
        public int TableIteration { get; set; } // Just keeps track of how many pivots have been done.
        public Tableau InitialTable { get; set; } // Tracks The intial table
        public int Height => Values.GetLength(0); 
        public int Width => Values.GetLength(1); 
        public double ObjectiveValue => Values[0, Width - 1]; 
        public double this[int i, int j] { get => Values[i, j]; set => Values[i, j] = value; }

        // Assume Values[, Width-1] is the RHS column
        // Assume Values[0,] is the Objective row

        public Tableau(int height = 2, int width = 3, bool maxElseMin = true)
        {
            RowNames /*           */ = Enumerable.Range(1, height).Select(i => $"c{i}").Prepend(maxElseMin ? "max z" : "min z").ToArray();
            ColumnNames /*        */ = Enumerable.Range(1, width).Select(j => $"x{j}").Append("rhs").ToArray();
            ColumnRestrictions /* */ = Enumerable.Repeat("+", width).ToArray();
            Values /*             */ = new double[height, width];
            TableIteration /*     */ = 0;
        }

        public Tableau Copy() => new Tableau
        {
            RowNames /*           */ = RowNames.ToArray(),
            ColumnNames /*        */ = ColumnNames.ToArray(),
            ColumnRestrictions /* */ = ColumnRestrictions.ToArray(),
            Values /*             */ = Copy(Values),
            TableIteration /*     */ = TableIteration,
            InitialTable /*       */ = InitialTable
        };

        public int GetBasicVariableI(int j)
        {
            if (!(0 <= j && j < Width - 1))
                throw new ArgumentOutOfRangeException($"{nameof(j)} must be in range [{0}..{Width - 2}]");
            int indexOf1 = -1;
            var column = Enumerable.Range(0, Height).Select(i => (v: Values[i, j], i)).Skip(1);
            return column.All(p =>
                p.v == 0.0 ||
                p.v == 1.0 && indexOf1 == -1 && (indexOf1 = p.i) != -1
            ) ? indexOf1 : -1;
        }

        public double GetVariableValue(int j) /*          */ => GetBasicVariableValue(j) ?? 0.0;
        public double? GetBasicVariableValue(int j) /*    */ { var i = GetBasicVariableI(j); return i < 0 ? (double?)null : Values[i, Width - 1]; }
        public double? GetNonBasicVariableValue(int j) /* */ => GetBasicVariableValue(j).HasValue ? (double?)null : 0.0;

        public bool IsVariable(int j) /*         */ => 0 <= j && j < Width - 1;
        public bool IsBasicVariable(int j) /*    */ => GetBasicVariableValue(j).HasValue;
        public bool IsNonBasicVariable(int j) /* */ => GetNonBasicVariableValue(j).HasValue;

        public IEnumerable<int> GetVariableIndices() /*         */ => Enumerable.Range(0, Width - 1);
        public IEnumerable<int> GetBasicVariableIndices() /*    */ => GetVariableIndices().Where(IsBasicVariable);
        public IEnumerable<int> GetNonBasicVariableIndices() /* */ => GetVariableIndices().Where(IsNonBasicVariable);

        public IEnumerable<double> GetVariableValues() /*         */ => GetVariableIndices().Select(GetVariableValue);
        public IEnumerable<double> GetBasicVariableValues() /*    */ => GetVariableIndices().Select(GetBasicVariableValue).Where(v => v.HasValue).Select(v => v.Value);
        public IEnumerable<double> GetNonBasicVariableValues() /* */ => GetVariableIndices().Select(GetNonBasicVariableValue).Where(v => v.HasValue).Select(v => v.Value);

        public Dictionary<string, double> GetVariableValuesNamed() /*         */ => GetVariableValues().Select((v, j) => (key: ColumnNames[j], value: v)).ToDictionary(p => p.key, p => p.value);
        public Dictionary<string, double> GetBasicVariableValuesNamed() /*    */ => GetBasicVariableValues().Select((v, j) => (key: ColumnNames[j], value: v)).ToDictionary(p => p.key, p => p.value);
        public Dictionary<string, double> GetNonBasicVariableValuesNamed() /* */ => GetNonBasicVariableValues().Select((v, j) => (key: ColumnNames[j], value: v)).ToDictionary(p => p.key, p => p.value);

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
            for (int j = 0; j < Width; j++)
            {
                Values[rowI, j] /= pivot;
            }
            for (int i = 0; i < Height; i++)
            {
                if (i == rowI)
                    continue;
                double factor = Values[i, colI];
                for (int j = 0; j < Width; j++)
                {
                    Values[i, j] -= factor * Values[rowI, j];
                }
            }
            return $"Pivot on {RowNames[rowI]}, {ColumnNames[colI]}\n\n{this}";
        }

        public void AddRow(double[] newRow, string name = null)
        {
            if (newRow.Length != Width)
                throw new ArgumentException($"New row must have {Width} values");
            var oldValues = Values;
            Values = new double[Height + 1, Width];
            int i = 0;
            for (; i < Height - 1; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = oldValues[i, j];
            for (; i < Height; i++)
                for (int j = 0; j < Width; j++)
                    Values[i, j] = newRow[j];
            RowNames = RowNames.Append(name ?? $"c{RowNames.Length}").ToArray();
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
            ColumnNames = ColumnNames.Append(name ?? $"s{Height}").ToArray();
            ColumnRestrictions = ColumnRestrictions.Append(restriction).ToArray();
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
                sb.Append($"| {ColumnNames[j],colWidth} ");
            sb.AppendLine($"|");
            /* | -----: | -----: | -----: | -----: | */
            for (int j = 0; j < Width + 1; j++)
                sb.Append($"| {"-----:",colWidth} ");
            sb.AppendLine($"|");
            /* |  max Z | 00.000 | 00.000 | 00.000 | */
            /* |     C1 | 00.000 | 00.000 | 00.000 | */
            for (int i = 0; i < Height; i++)
            {
                sb.Append($"| {RowNames[i],colWidth} ");
                for (int j = 0; j < Width; j++)
                    sb.Append($"| {Values[i, j].ToString("F3", CultureInfo.InvariantCulture),colWidth} ");
                sb.AppendLine($"|");
            }
            /* |   Sign |    int |      + |        | */
            sb.Append($"| {"",colWidth} ");
            for (int j = 0; j < Width; j++)
                sb.Append($"| {(j < ColumnRestrictions.Length ? ColumnRestrictions[j] : ""),colWidth} ");
            sb.AppendLine($"|");
            return sb.ToString();
        }

        private static (string[] objectiveLine, string[][] constraintLines, string[] restrictionsLine) FromFileValidateFile(string filename)
        {
            // TODO: canonical form out param
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
            return (objectiveLine, constraintLines, restrictionsLine);
        }

        public static Tableau FromFile(string filename)
        {
            var (objectiveLine, constraintLines, restrictionsLine) = FromFileValidateFile(filename);

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
                int jSlackOrExcess = widthWithoutSlacks - 1;
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

            var res = new Tableau
            {
                RowNames /*           */ = rowNames,
                ColumnNames /*        */ = columnNames,
                ColumnRestrictions /* */ = columnRestrictions,
                Values /*             */ = values,
                TableIteration /*     */ = 0
            };
            res.InitialTable = res.Copy();
            return res;
        }

        public static string FromFileCanonicalForm(string filename)
        {
            var canonicalForm = "";
            var (objectiveLine, constraintLines, restrictionsLine) = FromFileValidateFile(filename);
            canonicalForm += $"# Canonical Form";
            canonicalForm += $"\n\n## Objective\n\n{objectiveLine[0]} z = {string.Join(" + ", objectiveLine.Skip(1).Select((col, j) => $"{-double.Parse(col)}x{1 + j}"))}";
            canonicalForm += $"\n\n## Constraints\n\n" + string.Join("\n", constraintLines.Select((line, i) =>
                string.Join(" + ", line.Take(line.Length - 1).Select((col, j) => $"{double.Parse(col)}x{1 + j}")) +
                (line.Last().StartsWith("=") || line.Last().StartsWith("<=") ? $" + s{1 + i}" : "") +
                (line.Last().StartsWith("=") || line.Last().StartsWith(">=") ? $" + -e{1 + i}" : "") +
                $" = {double.Parse(line.Last().Substring(line.Last().StartsWith("=") ? 1 /* = */ : 2 /* <= or >= */))}"));
            canonicalForm += $"\n\n## Restrictions\n\n{string.Join(", ", restrictionsLine.Select((v, j) => $"x{1 + j}:{v}"))}";
            return canonicalForm;
        }

        private static double[,] Copy(double[,] from, double[,] to = null,
            int /*     */ CountI = 0, int /*      */ CountJ = 0, // 0 means use `from`'s dimention
            int /* */ fromStartI = 0, int /*  */ fromStartJ = 0,
            int /*   */ toStartI = 0, int /*    */ toStartJ = 0)
        {
            to = to ?? new double[from.GetLength(0), from.GetLength(1)];
            CountI = CountI > 0 ? CountI : from.GetLength(0);
            CountJ = CountJ > 0 ? CountJ : from.GetLength(1);
            if (!(to.GetLength(0) <= toStartI + CountI)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(to.GetLength(1) <= toStartJ + CountJ)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(from.GetLength(0) <= fromStartI + CountI)) throw new ArgumentOutOfRangeException($"Out of Range");
            if (!(from.GetLength(1) <= fromStartJ + CountJ)) throw new ArgumentOutOfRangeException($"Out of Range");
            for (int i = 0; i < CountI; i++) 
                for (int j = 0; j < CountJ; j++)
                    to[toStartI + i, toStartJ + j] = from[fromStartI + i, fromStartJ + j];
            return to;
        }
    }
}

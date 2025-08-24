using Markdig;
using System.Collections.Generic;
using System.Text;

namespace LPR381.LP
{
    public static class PrimalSimplex
    {
        // Solve method implements Primal Simplex algorithm
        public static List<string> Solve(Tableau tableau)
        {
            var steps = new List<string> { "Start Primal Simplex" };
            for (int iteration = 0; iteration < 128; iteration++)
            {
                //  Check for optimality by looking at the objective row
                int pivotColumn = -1;
                for (int j = 0; j < tableau.Width - 1; j++)
                {
                    if (tableau[0, j] < 0)
                    {
                        pivotColumn = j;
                        break;
                    }
                }
                if (pivotColumn == -1)
                {
                    // If no negative entries in objective row, optimal solution is found
                    steps.Add(ConstructSolution(tableau));
                    break;
                }

                // Determine the pivot row using the minimum ratio test
                double minRatio = double.PositiveInfinity;
                int pivotRow = -1;
                for (int i = 1; i < tableau.Height; i++)
                {
                    if (tableau[i, pivotColumn] > 0)
                    {
                        double numerator = tableau[i, tableau.Width - 1],
                               demoninator = tableau[i, pivotColumn],
                               ratio = numerator / demoninator;
                        if (ratio < 0) continue;
                        if (ratio == 0 && (numerator < 0) != (demoninator < 0)) continue;
                        if (ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }
                if (pivotRow == -1)
                {
                    // If no valid pivot row is found, the solution is unbounded
                    steps.Add("Unbounded solution");
                    break;
                }

                // Perform the pivot operation
                steps.Add(tableau.Pivot(pivotRow, pivotColumn));
            }
            steps.Add("End Primal Simplex");
            return steps;
        }

        // Constructs and returns a string representation of the optimal solution
        private static string ConstructSolution(Tableau tableau)
        {
            var result = new StringBuilder();

            // Optimal value is typically in the last column of the objective function row (row 0)
            double optimalValue = tableau[0, tableau.Width - 1];

            // Append a complete HTML structure with embedded dark-theme styles
            result.AppendLine(@"
<html>
<head>
    <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
    <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
    <link href=""https://fonts.googleapis.com/css2?family=Lato:wght@400;700&display=swap"" rel=""stylesheet"">
    <style>
        body {
            background-color: #1e2125;
            font-family: 'Lato', sans-serif;
            color: #f0f0f0;
            margin: 40px;
        }
        h3 {
            color: #e0e2e8;
            font-weight: 700;
        }
        p {
            color: #a0a5b5;
            font-size: 18px;
        }
        p span {
            color: #28a745; /* A vibrant green for the optimal value */
            font-weight: bold;
        }
        table {
            width: 30%; /* Make the solution table narrower */
            max-width: 450px; /* Set a maximum width for readability */
            margin: 30px auto; /* Center the table with space above and below */
            border-collapse: collapse;
            color: #e0e2e8;
            background-color: #2c3044;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2);
            font-size: 16px;
        }
        thead tr {
            background-color: #353a50;
        }
        th, td {
            padding: 16px 24px;
            border-bottom: 1px solid #353a50;
            text-align: left;
        }
        th {
            text-align: left;
            font-weight: 700;
            font-size: 14px;
            color: #a0a5b5;
        }
        tbody tr:last-child th,
        tbody tr:last-child td {
            border-bottom: none; /* Remove border from the final row */
        }
        td:last-child, th:last-child {
            text-align: right; /* Right-align the 'Value' column */
        }
    </style>
</head>
<body>
    <div class='solution-container'>
");

            // Add the title and optimal value
            result.AppendLine("<h3>Optimal Solution</h3>");
            result.AppendLine($"<p>Optimal Value: <span>{optimalValue:0.###}</span></p>");

            // Start the table structure
            result.AppendLine("<table>");
            result.AppendLine("<thead><tr><th>Variable</th><th>Value</th></tr></thead>");

            // Extract values of decision variables from the final tableau
            for (int j = 0; j < tableau.Width - 1; j++)
            {
                string varName = tableau.ColumnNames[j];

                // Skip slack variables from the solution table for clarity
                if (varName.StartsWith("s")) continue;

                double varValue = 0;
                bool isBasic = false;

                // Find the value if it's a basic variable (in a pivot column)
                for (int i = 1; i < tableau.Height; i++)
                {
                    // A basic variable has a single '1' in its column (and zeros elsewhere)
                    if (tableau[i, j] == 1)
                    {
                        varValue = tableau[i, tableau.Width - 1];
                        isBasic = true;
                        break;
                    }
                }

                // Non-basic variables are zero. Only add if it's a decision variable.
                if (!isBasic)
                {
                    varValue = 0;
                }

                result.AppendLine($"<tr><td>{varName}</td><td>{varValue:0.###}</td></tr>");
            }

            // Close all HTML tags
            result.AppendLine(@"
    
</table>
</div>
</body>
</html>
");

            return result.ToString();
        }
    }
}

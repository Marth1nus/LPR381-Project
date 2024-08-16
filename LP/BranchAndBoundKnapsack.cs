using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381.LP
{
    public static class BranchAndBoundKnapsack
    {
        public static List<string> Solve(Tableu tableu)
        {
            var steps = new List<string>();

            // Convert Tableu to List of Items and capacity
            var items = new List<Item>();
            for (int j = 0; j < tableu.Height; j++)
            {
                items.Add(new Item(tableu[0, j], tableu[1, j]));
            }
            double knapsackCapacity = tableu[0, tableu.Width - 1];

            // Sort items by value/weight ratio
            items = items.OrderByDescending(item => (float)item.Value / item.Weight).ToList();

            // Create a priority queue to store nodes
            var priorityQueue = new SortedSet<Node>(Comparer<Node>.Create((a, b) => b.Bound.CompareTo(a.Bound)));

            // Create a dummy node at level -1
            Node currentNode = new Node(-1, 0, 0);
            currentNode.Bound = CalculateBound(currentNode, knapsackCapacity, items);

            priorityQueue.Add(currentNode);
            steps.Add($"Initial node added with Bound: {currentNode.Bound}");

            double maxProfit = 0;

            while (priorityQueue.Any())
            {
                currentNode = priorityQueue.First();
                priorityQueue.Remove(currentNode);
                steps.Add($"Exploring node at level {currentNode.Level} with Profit: {currentNode.Profit}, Weight: {currentNode.Weight}, Bound: {currentNode.Bound}");

                if (currentNode.Bound > maxProfit)
                {
                    // Branch to include the next item
                    Node nextNode = new Node(currentNode.Level + 1, currentNode.Profit, currentNode.Weight);

                    // Include the item
                    if (nextNode.Level < items.Count)
                    {
                        nextNode.Weight += items[nextNode.Level].Weight;
                        nextNode.Profit += items[nextNode.Level].Value;
                        steps.Add($"Including item {nextNode.Level} -> New Profit: {nextNode.Profit}, New Weight: {nextNode.Weight}");

                        if (nextNode.Weight <= knapsackCapacity && nextNode.Profit > maxProfit)
                        {
                            maxProfit = nextNode.Profit;
                            steps.Add($"New max profit found: {maxProfit}");
                        }

                        nextNode.Bound = CalculateBound(nextNode, knapsackCapacity, items);
                        steps.Add($"Calculated Bound for included item {nextNode.Level}: {nextNode.Bound}");

                        if (nextNode.Bound > maxProfit)
                        {
                            priorityQueue.Add(nextNode);
                            steps.Add($"Node added to queue with Bound: {nextNode.Bound}");
                        }

                        // Branch to exclude the next item
                        nextNode = new Node(currentNode.Level + 1, currentNode.Profit, currentNode.Weight);
                        nextNode.Bound = CalculateBound(nextNode, knapsackCapacity, items);
                        steps.Add($"Calculated Bound for excluded item {nextNode.Level}: {nextNode.Bound}");

                        if (nextNode.Bound > maxProfit)
                        {
                            priorityQueue.Add(nextNode);
                            steps.Add($"Node added to queue with Bound: {nextNode.Bound}");
                        }
                    }
                }
            }

            steps.Add($"Maximum Profit: {maxProfit}");
            return steps;
        }

        private static double CalculateBound(Node node, double knapsackCapacity, List<Item> items)
        {
            if (node.Weight >= knapsackCapacity)
                return 0;

            double upperBound = node.Profit;
            double totalWeight = node.Weight;
            int index = node.Level + 1;

            while (index < items.Count && totalWeight + items[index].Weight <= knapsackCapacity)
            {
                totalWeight += items[index].Weight;
                upperBound += items[index].Value;
                index++;
            }

            if (index < items.Count)
                upperBound += (knapsackCapacity - totalWeight) * (float)items[index].Value / items[index].Weight;

            return upperBound;
        }

        public struct Item
        {
            public double Value { get; set; }
            public double Weight { get; set; }

            public Item(double value, double weight)
            {
                Value = value;
                Weight = weight;
            }
        }

        public class Node
        {
            public int Level { get; set; }
            public double Profit { get; set; }
            public double Weight { get; set; }
            public double Bound { get; set; }

            public Node(int level, double profit, double weight)
            {
                Level = level;
                Profit = profit;
                Weight = weight;
            }
        }
    }
}
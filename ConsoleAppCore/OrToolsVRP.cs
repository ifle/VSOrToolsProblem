﻿using System;
using Google.OrTools.ConstraintSolver;

namespace ConsoleAppCore
{
    public class OrToolsVRP
    {
        class DataProblem
        {
            private int[,] locations_;

            // Constructor:
            public DataProblem()
            {
                locations_ = new int[,]
                {
                    {4, 4},
                    {2, 0}, {8, 0},
                    {0, 1}, {1, 1},
                    {5, 2}, {7, 2},
                    {3, 3}, {6, 3},
                    {5, 5}, {8, 5},
                    {1, 6}, {2, 6},
                    {3, 7}, {6, 7},
                    {0, 8}, {7, 8}
                };

                // Compute locations in meters using the block dimension defined as follow
                // Manhattan average block: 750ft x 264ft -> 228m x 80m
                // here we use: 114m x 80m city block
                // src: https://nyti.ms/2GDoRIe "NY Times: Know Your distance"
                int[] cityBlock = { 228 / 2, 80 };
                for (int i = 0; i < locations_.GetLength(0); i++)
                {
                    locations_[i, 0] = locations_[i, 0] * cityBlock[0];
                    locations_[i, 1] = locations_[i, 1] * cityBlock[1];
                }
            }

            public int GetVehicleNumber()
            {
                return 4;
            }

            public ref readonly int[,] GetLocations()
            {
                return ref locations_;
            }

            public int GetLocationNumber()
            {
                return locations_.GetLength(0);
            }

            public int GetDepot()
            {
                return 0;
            }
        };


        /// <summary>
        ///   Manhattan distance implemented as a callback. It uses an array of
        ///   positions and computes the Manhattan distance between the two
        ///   positions of two different indices.
        /// </summary>
        class ManhattanDistance : NodeEvaluator2
        {
            private int[,] distances_;

            public ManhattanDistance(in DataProblem data)
            {
                // precompute distance between location to have distance callback in O(1)
                distances_ = new int[data.GetLocationNumber(), data.GetLocationNumber()];
                for (int fromNode = 0; fromNode < data.GetLocationNumber(); fromNode++)
                {
                    for (int toNode = 0; toNode < data.GetLocationNumber(); toNode++)
                    {
                        if (fromNode == toNode)
                            distances_[fromNode, toNode] = 0;
                        else
                            distances_[fromNode, toNode] =
                                Math.Abs(data.GetLocations()[toNode, 0] - data.GetLocations()[fromNode, 0]) +
                                Math.Abs(data.GetLocations()[toNode, 1] - data.GetLocations()[fromNode, 1]);
                    }
                }
            }

            /// <summary>
            ///   Returns the manhattan distance between the two nodes
            /// </summary>
            public override long Run(int FromNode, int ToNode)
            {
                return distances_[FromNode, ToNode];
            }
        };

        /// <summary>
        ///   Add distance Dimension
        /// </summary>
        static void AddDistanceDimension(
            in DataProblem data,
            in RoutingModel routing)
        {
            String distance = "Distance";
            routing.AddDimension(
                new ManhattanDistance(data),
                0, // null slack
                3000, // maximum distance per vehicle
                true, // start cumul to zero
                distance);
            RoutingDimension distanceDimension = routing.GetDimensionOrDie(distance);
            // Try to minimize the max distance among vehicles.
            // /!\ It doesn't mean the standard deviation is minimized
            distanceDimension.SetGlobalSpanCostCoefficient(100);
        }

        /// <summary>
        ///   Print the solution
        /// </summary>
        static void PrintSolution(
            in DataProblem data,
            in RoutingModel routing,
            in Assignment solution)
        {
            Console.WriteLine("Objective: {0}", solution.ObjectiveValue());
            // Inspect solution.
            for (int i = 0; i < data.GetVehicleNumber(); ++i)
            {
                Console.WriteLine("Route for Vehicle " + i + ":");
                long distance = 0;
                var index = routing.Start(i);
                while (routing.IsEnd(index) == false)
                {
                    Console.Write("{0} -> ", routing.IndexToNode(index));
                    var previousIndex = index;
                    index = solution.Value(routing.NextVar(index));
                    distance += routing.GetArcCostForVehicle(previousIndex, index, i);
                }

                Console.WriteLine("{0}", routing.IndexToNode(index));
                Console.WriteLine("Distance of the route: {0}m", distance);
            }
        }

        /// <summary>
        ///   Solves the current routing problem.
        /// </summary>
        public static void Solve()
        {
            // Instantiate the data problem.
            DataProblem data = new DataProblem();

            // Create Routing Model
            RoutingModel routing = new RoutingModel(
                data.GetLocationNumber(),
                data.GetVehicleNumber(),
                data.GetDepot());

            // Define weight cost of each edge
            NodeEvaluator2 distanceEvaluator = new ManhattanDistance(data);
            //protect callbacks from the GC
            GC.KeepAlive(distanceEvaluator);
            routing.SetArcCostEvaluatorOfAllVehicles(distanceEvaluator);
            AddDistanceDimension(data, routing);

            // Setting first solution heuristic (cheapest addition).
            RoutingSearchParameters searchParameters = RoutingModel.DefaultSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;

            Assignment solution = routing.SolveWithParameters(searchParameters);
            PrintSolution(data, routing, solution);
        }
    }
}
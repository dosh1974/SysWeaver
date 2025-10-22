using System;
using System.Collections.Generic;
using System.Linq;

namespace SysWeaver.Geometry
{


    /// <summary>
    /// Represents a KD-Tree. KD-Trees are used for fast spatial searches. Searching in a
    /// balanced KD-Tree is O(log n) where linear search is O(n). Points in the KD-Tree are
    /// equi-length arrays of type <typeparamref name="TDimension"/>. The node objects associated
    /// with the points is an array of type <typeparamref name="TNode"/>.
    /// </summary>
    /// <remarks>
    /// KDTrees can be fairly difficult to understand at first. The following references helped me
    /// understand what exactly a KDTree is doing and the contain the best descriptions of searches in a KDTree.
    /// Samet's book is the best reference of multidimensional data structures I have ever seen. Wikipedia is also a good starting place.
    /// References:
    /// <ul style="list-style-type:none">
    /// <li> <a href="http://store.elsevier.com/product.jsp?isbn=9780123694461">Foundations of Multidimensional and Metric Data Structures, 1st Edition, by Hanan Samet. ISBN: 9780123694461</a> </li>
    /// <li> <a href="https://en.wikipedia.org/wiki/K-d_tree"> https://en.wikipedia.org/wiki/K-d_tree</a> </li>
    /// </ul>
    /// </remarks>
    /// <typeparam name="TDimension">The type of the dimension.</typeparam>
    /// <typeparam name="TNode">The type representing the actual node objects.</typeparam>
    [Serializable]
    public class KDTree<TDimension, TNode>
        where TDimension : IComparable<TDimension>
    {
        /// <summary>
        /// The number of points in the KDTree
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// The numbers of dimensions that the tree has.
        /// </summary>
        public int Dimensions { get; }

        /// <summary>
        /// The array in which the binary tree is stored. Enumerating this array is a level-order traversal of the tree.
        /// </summary>
        public TDimension[][] InternalPointArray { get; }

        /// <summary>
        /// The array in which the node objects are stored. There is a one-to-one correspondence with this array and the <see cref="InternalPointArray"/>.
        /// </summary>
        public TNode[] InternalNodeArray { get; }

        /// <summary>
        /// The metric function used to calculate distance between points.
        /// </summary>
        public Func<TDimension[], TDimension[], double> Metric { get; set; }

        /// <summary>
        /// Gets a <see cref="BinaryTreeNavigator{TPoint,TNode}"/> that allows for manual tree navigation,
        /// </summary>
        public BinaryTreeNavigator<TDimension[], TNode> Navigator
            => new BinaryTreeNavigator<TDimension[], TNode>(this.InternalPointArray, this.InternalNodeArray);

        /// <summary>
        /// The maximum value along any dimension.
        /// </summary>
        private TDimension MaxValue { get; }

        /// <summary>
        /// The minimum value along any dimension.
        /// </summary>
        private TDimension MinValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KDTree{TDimension,TNode}"/> class.
        /// </summary>
        /// <param name="dimensions">The number of dimensions in the data set.</param>
        /// <param name="points">The points to be constructed into a <see cref="KDTree{TDimension,TNode}"/></param>
        /// <param name="nodes">The nodes associated with each point.</param>
        /// <param name="metric">A function that should returned the squared distance between two points. This should satisfy the triangle inequality.</param>
        /// <param name="searchWindowMinValue">The minimum value to be used in node searches. If null, we assume that <typeparamref name="TDimension"/> has a static field named "MinValue". All numeric structs have this field.</param>
        /// <param name="searchWindowMaxValue">The maximum value to be used in node searches. If null, we assume that <typeparamref name="TDimension"/> has a static field named "MaxValue". All numeric structs have this field.</param>
        public KDTree(
            int dimensions,
            TDimension[][] points,
            TNode[] nodes,
            Func<TDimension[], TDimension[], double> metric,
            TDimension searchWindowMinValue = default(TDimension),
            TDimension searchWindowMaxValue = default(TDimension))
        {
            // Attempt find the Min/Max value if null.
            if (searchWindowMinValue.Equals(default(TDimension)))
            {
                var type = typeof(TDimension);
                this.MinValue = (TDimension)type.GetField("MinValue").GetValue(type);
            }
            else
            {
                this.MinValue = searchWindowMinValue;
            }

            if (searchWindowMaxValue.Equals(default(TDimension)))
            {
                var type = typeof(TDimension);
                this.MaxValue = (TDimension)type.GetField("MaxValue").GetValue(type);
            }
            else
            {
                this.MaxValue = searchWindowMaxValue;
            }

            // Calculate the number of nodes needed to contain the binary tree.
            // This is equivalent to finding the power of 2 greater than the number of points
            var elementCount = (int)Math.Pow(2, (int)(Math.Log(points.Length) / Math.Log(2)) + 1);
            this.Dimensions = dimensions;
            this.InternalPointArray = Enumerable.Repeat(default(TDimension[]), elementCount).ToArray();
            this.InternalNodeArray = Enumerable.Repeat(default(TNode), elementCount).ToArray();
            this.Metric = metric;
            this.Count = points.Length;
            this.GenerateTree(0, 0, points, nodes);
        }

        /// <summary>
        /// The signature of the custom function 
        /// </summary>
        /// <param name="searchPoint">The point whose neighbors we search for.</param>
        /// <param name="nodePoint">The point of the node that we consider</param>
        /// <param name="distanceSquared">The squared distance between the node and the search point</param>
        /// <param name="node">The node that we consider to add</param>
        /// <returns>True to keep this node (add to results) or false to ignore this</returns>
        public delegate bool ValidateNodeDelagate(TDimension[] searchPoint, TDimension[] nodePoint, double distanceSquared, TNode node);

        /// <summary>
        /// Finds the nearest neighbors in the <see cref="KDTree{TDimension,TNode}"/> of the given <paramref name="searchPoint"/>.
        /// </summary>
        /// <param name="searchPoint">The point whose neighbors we search for.</param>
        /// <param name="maxDist">The maximum distance to search for, zero or less for unlimited radius</param>
        /// <param name="maxCount">The maximum number of nodes to return, if zero or less all nodes are returned (that satisfy other constraints such as maxDist and the keep function)</param>
        /// <param name="validateNode">Optional custom function that is executed to determine if a node should be included or not (useful for filtering), see ValidateNodeDelagate for details</param>
        /// <returns>The nodes found</returns>
        public Tuple<TDimension[], TNode>[] Search(TDimension[] searchPoint, int maxCount = 0, double maxDist = 0, Func<TDimension[], TDimension[], double, TNode, bool> validateNode = null)
        {
            var state = new State(searchPoint,
                maxCount <= 0 ?
                    new BoundedPriorityList<int, double>(this.Count)
                    :
                    new BoundedPriorityList<int, double>(maxCount, true),
                maxDist <= 0 ? double.MaxValue : (maxDist * maxDist),  
                validateNode);
            SearchForNearestNeighbors(ref state, HyperRect<TDimension>.Infinite(this.Dimensions, this.MaxValue, this.MinValue));
            return state.nearestNeighbors.ToResultSet(this);
        }

        /// <summary>
        /// Grows a KD tree recursively via median splitting. We find the median by doing a full sort.
        /// </summary>
        /// <param name="index">The array index for the current node.</param>
        /// <param name="dim">The current splitting dimension.</param>
        /// <param name="points">The set of points remaining to be added to the kd-tree</param>
        /// <param name="nodes">The set of nodes RE</param>
        private void GenerateTree(
            int index,
            int dim,
            IReadOnlyCollection<TDimension[]> points,
            IEnumerable<TNode> nodes)
        {
            if (points.Count <= 0)
                return;

            // See wikipedia for a good explanation kd-tree construction.
            // https://en.wikipedia.org/wiki/K-d_tree

            // zip both lists so we can sort nodes according to points
            var zippedList = points.Zip(nodes, (p, n) => new { Point = p, Node = n });

            // sort the points along the current dimension
            var sortedPoints = zippedList.OrderBy(z => z.Point[dim]).ToArray();

            // get the point which has the median value of the current dimension.
            var medianPoint = sortedPoints[points.Count / 2];
            var medianPointIdx = sortedPoints.Length / 2;

            // The point with the median value all the current dimension now becomes the value of the current tree node
            // The previous node becomes the parents of the current node.
            this.InternalPointArray[index] = medianPoint.Point;
            this.InternalNodeArray[index] = medianPoint.Node;

            // We now split the sorted points into 2 groups
            // 1st group: points before the median
            var leftPoints = new TDimension[medianPointIdx][];
            var leftNodes = new TNode[medianPointIdx];
            Array.Copy(sortedPoints.Select(z => z.Point).ToArray(), leftPoints, leftPoints.Length);
            Array.Copy(sortedPoints.Select(z => z.Node).ToArray(), leftNodes, leftNodes.Length);

            // 2nd group: Points after the median
            var rightPoints = new TDimension[sortedPoints.Length - (medianPointIdx + 1)][];
            var rightNodes = new TNode[sortedPoints.Length - (medianPointIdx + 1)];
            Array.Copy(
                sortedPoints.Select(z => z.Point).ToArray(),
                medianPointIdx + 1,
                rightPoints,
                0,
                rightPoints.Length);
            Array.Copy(sortedPoints.Select(z => z.Node).ToArray(), medianPointIdx + 1, rightNodes, 0, rightNodes.Length);

            // We new recurse, passing the left and right arrays for arguments.
            // The current node's left and right values become the "roots" for
            // each recursion call. We also forward cycle to the next dimension.
            var nextDim = (dim + 1) % this.Dimensions; // select next dimension

            // We only need to recurse if the point array contains more than one point
            // If the array has no points then the node stay a null value
            if (leftPoints.Length <= 1)
            {
                if (leftPoints.Length == 1)
                {
                    this.InternalPointArray[BinaryTreeNavigation.LeftChildIndex(index)] = leftPoints[0];
                    this.InternalNodeArray[BinaryTreeNavigation.LeftChildIndex(index)] = leftNodes[0];
                }
            }
            else
            {
                this.GenerateTree(BinaryTreeNavigation.LeftChildIndex(index), nextDim, leftPoints, leftNodes);
            }

            // Do the same for the right points
            if (rightPoints.Length <= 1)
            {
                if (rightPoints.Length == 1)
                {
                    this.InternalPointArray[BinaryTreeNavigation.RightChildIndex(index)] = rightPoints[0];
                    this.InternalNodeArray[BinaryTreeNavigation.RightChildIndex(index)] = rightNodes[0];
                }
            }
            else
            {
                this.GenerateTree(BinaryTreeNavigation.RightChildIndex(index), nextDim, rightPoints, rightNodes);
            }
        }

        static readonly Func<TDimension[], TDimension[], double, TNode, bool> KeepAll = (a, b, c, d) => true;


        struct State
        {
            public readonly TDimension[] target;
            public readonly BoundedPriorityList<int, double> nearestNeighbors;
            public readonly double maxSearchRadiusSquared;
            public readonly Func<TDimension[], TDimension[], double, TNode, bool> keep;

            public State(TDimension[] target, BoundedPriorityList<int, double> nearestNeighbors, double maxSearchRadiusSquared, Func<TDimension[], TDimension[], double, TNode, bool> keep)
            {
                this.target = target;
                this.nearestNeighbors = nearestNeighbors;
                this.maxSearchRadiusSquared = maxSearchRadiusSquared;
                this.keep = keep ?? KeepAll;
            }
        }


        private void SearchForNearestNeighbors(
            ref State state,
            HyperRect<TDimension> rect,
            int nodeIndex = 0,
            int dimension = 0
            )
        {
            if (this.InternalPointArray.Length <= nodeIndex || nodeIndex < 0
                || this.InternalPointArray[nodeIndex] == null)
            {
                return;
            }

            // Work out the current dimension
            var dim = dimension % this.Dimensions;

            // Split our hyper-rectangle into 2 sub rectangles along the current
            // node's point on the current dimension
            var leftRect = rect.Clone();
            leftRect.MaxPoint[dim] = this.InternalPointArray[nodeIndex][dim];

            var rightRect = rect.Clone();
            rightRect.MinPoint[dim] = this.InternalPointArray[nodeIndex][dim];

            // Determine which side the target resides in
            var target = state.target;
            var compare = target[dim].CompareTo(this.InternalPointArray[nodeIndex][dim]);

            var nearerRect = compare <= 0 ? leftRect : rightRect;
            var furtherRect = compare <= 0 ? rightRect : leftRect;

            var nearerNode = compare <= 0 ? BinaryTreeNavigation.LeftChildIndex(nodeIndex) : BinaryTreeNavigation.RightChildIndex(nodeIndex);
            var furtherNode = compare <= 0 ? BinaryTreeNavigation.RightChildIndex(nodeIndex) : BinaryTreeNavigation.LeftChildIndex(nodeIndex);

            // Move down into the nearer branch
            this.SearchForNearestNeighbors(
                ref state,
                nearerRect,
                nearerNode,
                dimension + 1);

            // Walk down into the further branch but only if our capacity hasn't been reached
            // OR if there's a region in the further rectangle that's closer to the target than our
            // current furtherest nearest neighbor
            var closestPointInFurtherRect = furtherRect.GetClosestPoint(target);
            var distanceSquaredToTarget = this.Metric(closestPointInFurtherRect, target);
            var maxSearchRadiusSquared = state.maxSearchRadiusSquared;
            if (distanceSquaredToTarget.CompareTo(maxSearchRadiusSquared) <= 0)
            {
                if (state.nearestNeighbors.IsFull)
                {
                    if (distanceSquaredToTarget.CompareTo(state.nearestNeighbors.MaxPriority) < 0)
                    {
                        this.SearchForNearestNeighbors(
                            ref state,
                            furtherRect,
                            furtherNode,
                            dimension + 1);
                    }
                }
                else
                {
                    this.SearchForNearestNeighbors(
                        ref state,
                        furtherRect,
                        furtherNode,
                        dimension + 1);
                }
            }

            // Try to add the current node to our nearest neighbors list
            var dataPoint = this.InternalPointArray[nodeIndex];
            distanceSquaredToTarget = this.Metric(dataPoint, target);
            if (distanceSquaredToTarget.CompareTo(maxSearchRadiusSquared) <= 0)
            {
                if (state.keep(target, dataPoint, distanceSquaredToTarget, this.InternalNodeArray[nodeIndex]))
                    state.nearestNeighbors.Add(nodeIndex, distanceSquaredToTarget);
            }
        }

    }

}

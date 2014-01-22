﻿/*
Copyright (c) 2013, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    public class UnboundCollection : IRayTraceable
    {
        IRayTraceable[] items;

        public UnboundCollection(IEnumerable<IRayTraceable> traceableItems)
        {
            items = traceableItems.ToArray();
        }

        public RGBA_Floats GetColor(IntersectInfo info)
        {
            throw new NotImplementedException("You should not get a color directly from a BoundingVolumeHierarchy.");
        }

        public IMaterial Material
        {
            get
            {
                throw new Exception("You should not get a material from an UnboundCollection.");
            }
            set
            {
                throw new Exception("You can't set a material on an UnboundCollection.");
            }
        }

        public IntersectInfo GetClosestIntersection(Ray ray)
        {
            IntersectInfo bestInfo = null;
            foreach (IRayTraceable item in items)
            {
                IntersectInfo info = item.GetClosestIntersection(ray);
                if (info != null && info.hitType != IntersectionType.None && info.distanceToHit >= 0)
                {
                    if (bestInfo == null || info.distanceToHit < bestInfo.distanceToHit)
                    {
                        bestInfo = info;
                    }
                }
            }

            return bestInfo;
        }

        public bool GetContained(List<IRayTraceable> results, AxisAlignedBoundingBox subRegion)
        {
            bool foundItem = false;
            foreach (IRayTraceable item in items)
            {
                foundItem |= item.GetContained(results, subRegion);
            }

            return foundItem;
        }

        public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
        {
            throw new NotImplementedException();
        }

        public void GetClosestIntersections(RayBundle ray, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
        {
            throw new NotImplementedException();
        }

        public IEnumerable IntersectionIterator(Ray ray)
        {
            foreach (IRayTraceable item in items)
            {
                foreach (IntersectInfo info in item.IntersectionIterator(ray))
                {
                    yield return info;
                }
            }
        }

        public double GetSurfaceArea()
        {
            double totalSurfaceArea = 0;
            foreach (IRayTraceable item in items)
            {
                totalSurfaceArea += item.GetSurfaceArea();
            }

            return totalSurfaceArea;
        }

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            AxisAlignedBoundingBox totalBounds = items[0].GetAxisAlignedBoundingBox();
            for (int i = 1; i < items.Length; i++)
            {
                totalBounds += items[i].GetAxisAlignedBoundingBox();
            }

            return totalBounds;
        }

        /// <summary>
        /// This is the computation cost of doing an intersection with the given type.
        /// Attempt to give it in average CPU cycles for the intersecton.
        /// It really does not need to be a member variable as it is fixed to a given
        /// type of object.  But it needs to be virtual so we can get to the value
        /// for a given class. (If only there were class virtual functions :) ).
        /// </summary>
        /// <returns></returns>
        public double GetIntersectCost()
        {
            double totalIntersectCost = 0;
            foreach (IRayTraceable item in items)
            {
                totalIntersectCost += item.GetIntersectCost();
            }

            return totalIntersectCost;
        }
    }

    public class BoundingVolumeHierarchy : IRayTraceable
    {
        internal AxisAlignedBoundingBox Aabb;
        IRayTraceable nodeA;
        IRayTraceable nodeB;
        int splitingPlane;

        public BoundingVolumeHierarchy()
        {
        }

        public BoundingVolumeHierarchy(IRayTraceable nodeA, IRayTraceable nodeB, int splitingPlane)
        {
            this.splitingPlane = splitingPlane;
            this.nodeA = nodeA;
            this.nodeB = nodeB;
            this.Aabb = nodeA.GetAxisAlignedBoundingBox() + nodeB.GetAxisAlignedBoundingBox(); // we can cache this because it is not allowed to change.
        }

        public RGBA_Floats GetColor(IntersectInfo info)
        {
            throw new NotImplementedException("You should not get a color directly from a BoundingVolumeHierarchy.");
        }

        public IMaterial Material
        {
            get
            {
                throw new Exception("You should not get a material from a BoundingVolumeHierarchy.");
            }
            set
            {
                throw new Exception("You can't set a material on a BoundingVolumeHierarchy.");
            }
        }

        public bool GetContained(List<IRayTraceable> results, AxisAlignedBoundingBox subRegion)
        {
            AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
            if (bounds.Contains(subRegion))
            {
                bool resultA = this.nodeA.GetContained(results, subRegion);
                bool resultB = this.nodeB.GetContained(results, subRegion);
                return resultA | resultB;
            }

            return false;
        }

        public double GetIntersectCost()
        {
            return AxisAlignedBoundingBox.GetIntersectCost();
        }

        public IntersectInfo GetClosestIntersection(Ray ray)
        {
            if (ray.Intersection(Aabb))
            {
                IRayTraceable checkFirst = nodeA;
                IRayTraceable checkSecond = nodeB;
                if (ray.direction[splitingPlane] < 0)
                {
                    checkFirst = nodeB;
                    checkSecond = nodeA;
                }

                IntersectInfo infoFirst = checkFirst.GetClosestIntersection(ray);
                if (infoFirst != null && infoFirst.hitType != IntersectionType.None)
                {
                    if (ray.isShadowRay)
                    {
                        return infoFirst;
                    }
                    else
                    {
                        ray.maxDistanceToConsider = infoFirst.distanceToHit;
                    }
                }
                if (checkSecond != null)
                {
                    IntersectInfo infoSecond = checkSecond.GetClosestIntersection(ray);
                    if (infoSecond != null && infoSecond.hitType != IntersectionType.None)
                    {
                        if (ray.isShadowRay)
                        {
                            return infoSecond;
                        }
                        else
                        {
                            ray.maxDistanceToConsider = infoSecond.distanceToHit;
                        }
                    }
                    if (infoFirst != null && infoFirst.hitType != IntersectionType.None && infoFirst.distanceToHit >= 0)
                    {
                        if (infoSecond != null && infoSecond.hitType != IntersectionType.None && infoSecond.distanceToHit < infoFirst.distanceToHit && infoSecond.distanceToHit >= 0)
                        {
                            return infoSecond;
                        }
                        else
                        {
                            return infoFirst;
                        }
                    }

                    return infoSecond; // we don't have to test it because it didn't hit.
                }
                return infoFirst;
            }

            return null;
        }

        public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
        {
            // check if first ray hits bounding box
            if (rayBundle.rayArray[rayIndexToStartCheckingFrom].Intersection(Aabb))
            {
                return rayIndexToStartCheckingFrom;
            }

            int count = rayBundle.rayArray.Length; 
            // check if all bundle misses
            if (!rayBundle.CheckIfBundleHitsAabb(Aabb))
            {
                return count;
            }

            // check each ray until one hits or all miss
            for (int i = rayIndexToStartCheckingFrom + 1; i < count; i++)
            {
                if (rayBundle.rayArray[i].Intersection(Aabb))
                {
                    return i;
                }
            }

            return count;
        }

        public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
        {
            int startRayIndex = FindFirstRay(rayBundle, rayIndexToStartCheckingFrom);
            IRayTraceable checkFirst = nodeA;
            IRayTraceable checkSecond = nodeB;
            if (rayBundle.rayArray[startRayIndex].direction[splitingPlane] < 0)
            {
                checkFirst = nodeB;
                checkSecond = nodeA;
            }

            checkFirst.GetClosestIntersections(rayBundle, startRayIndex, intersectionsForBundle);
            if (checkSecond != null)
            {
                checkSecond.GetClosestIntersections(rayBundle, startRayIndex, intersectionsForBundle);
            }
        }

        public IEnumerable IntersectionIterator(Ray ray)
        {
            if (ray.Intersection(Aabb))
            {
                IRayTraceable checkFirst = nodeA;
                IRayTraceable checkSecond = nodeB;
                if (ray.direction[splitingPlane] < 0)
                {
                    checkFirst = nodeB;
                    checkSecond = nodeA;
                }

                foreach (IntersectInfo info in checkFirst.IntersectionIterator(ray))
                {
                    if (info != null && info.hitType != IntersectionType.None)
                    {
                        yield return info;
                    }
                }

                if (checkSecond != null)
                {
                    foreach (IntersectInfo info in checkSecond.IntersectionIterator(ray))
                    {
                        if (info != null && info.hitType != IntersectionType.None)
                        {
                            yield return info;
                        }
                    }
                }
            }
        }

        public double GetSurfaceArea()
        {
            return Aabb.GetSurfaceArea();
        }

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            return Aabb;
        }

        static int nextAxisForBigGroups = 0;
        public static IRayTraceable CreateNewHierachy(List<IRayTraceable> traceableItems)
        {
            int numItems = traceableItems.Count;

            if (numItems == 0)
            {
                return null;
            }

            if (numItems == 1)
            {
                return traceableItems[0];
            }

            int bestAxis = -1;
            int bestIndexToSplitOn = -1;
            CompareCentersOnAxis axisSorter = new CompareCentersOnAxis(0);
            if (numItems > 100)
            {
                bestAxis = nextAxisForBigGroups++;
                if (nextAxisForBigGroups >= 3) nextAxisForBigGroups = 0;
                bestIndexToSplitOn = numItems / 2;
            }
            else
            {
                double totalIntersectCost = 0;
                int skipInterval = 1;
                for (int i = 0; i < numItems; i += skipInterval)
                {
                    IRayTraceable item = traceableItems[i];
                    totalIntersectCost += item.GetIntersectCost();
                }

                // get the bounding box of all the items we are going to consider.
                AxisAlignedBoundingBox OverallBox = traceableItems[0].GetAxisAlignedBoundingBox();
                for (int i = skipInterval; i < numItems; i += skipInterval)
                {
                    OverallBox += traceableItems[i].GetAxisAlignedBoundingBox();
                }
                double areaOfTotalBounds = OverallBox.GetSurfaceArea();

                double bestCost = totalIntersectCost;

                Vector3 totalDeviationOnAxis = new Vector3();
                double[] surfaceArreaOfItem = new double[numItems - 1];
                double[] rightBoundsAtItem = new double[numItems - 1];

                for (int axis = 0; axis < 3; axis++)
                {
                    double intersectCostOnLeft = 0;

                    axisSorter.WhichAxis = axis;
                    traceableItems.Sort(axisSorter);

                    // Get all left bounds
                    AxisAlignedBoundingBox currentLeftBounds = traceableItems[0].GetAxisAlignedBoundingBox();
                    surfaceArreaOfItem[0] = currentLeftBounds.GetSurfaceArea();
                    for (int i = 1; i < numItems - 1; i += skipInterval)
                    {
                        currentLeftBounds += traceableItems[i].GetAxisAlignedBoundingBox();
                        surfaceArreaOfItem[i] = currentLeftBounds.GetSurfaceArea();

                        totalDeviationOnAxis[axis] += Math.Abs(traceableItems[i].GetAxisAlignedBoundingBox().GetCenter()[axis] - traceableItems[i - 1].GetAxisAlignedBoundingBox().GetCenter()[axis]);
                    }

                    // Get all right bounds
                    if (numItems > 1)
                    {
                        AxisAlignedBoundingBox currentRightBounds = traceableItems[numItems - 1].GetAxisAlignedBoundingBox();
                        rightBoundsAtItem[numItems - 2] = currentRightBounds.GetSurfaceArea();
                        for (int i = numItems - 1; i > 1; i -= skipInterval)
                        {
                            currentRightBounds += traceableItems[i - 1].GetAxisAlignedBoundingBox();
                            rightBoundsAtItem[i - 2] = currentRightBounds.GetSurfaceArea();
                        }
                    }

                    // Sweep from left
                    for (int i = 0; i < numItems - 1; i += skipInterval)
                    {
                        double thisCost = 0;

                        {
                            // Evaluate Surface Cost Equation
                            double costOfTwoAABB = 2 * AxisAlignedBoundingBox.GetIntersectCost(); // the cost of the two children AABB tests

                            // do the left cost
                            intersectCostOnLeft += traceableItems[i].GetIntersectCost();
                            double leftCost = (surfaceArreaOfItem[i] / areaOfTotalBounds) * intersectCostOnLeft;

                            // do the right cost
                            double intersectCostOnRight = totalIntersectCost - intersectCostOnLeft;
                            double rightCost = (rightBoundsAtItem[i] / areaOfTotalBounds) * intersectCostOnRight;

                            thisCost = costOfTwoAABB + leftCost + rightCost;
                        }

                        if (thisCost < bestCost + .000000001) // if it is less within some tiny error
                        {
                            if (thisCost > bestCost - .000000001)
                            {
                                // they are the same within the error
                                if (axis > 0 && bestAxis != axis) // we have changed axis since last best and we need to decide if this is better than the last axis best
                                {
                                    if (totalDeviationOnAxis[axis] > totalDeviationOnAxis[axis - 1])
                                    {
                                        // this new axis is better and we'll switch to it.  Otherwise don't switch.
                                        bestCost = thisCost;
                                        bestIndexToSplitOn = i;
                                        bestAxis = axis;
                                    }
                                }
                            }
                            else // this is just better
                            {
                                bestCost = thisCost;
                                bestIndexToSplitOn = i;
                                bestAxis = axis;
                            }
                        }
                    }
                }
            }

            if (bestAxis == -1)
            {
                // No better partition found
                return new UnboundCollection(traceableItems);
            }
            else
            {
                axisSorter.WhichAxis = bestAxis;
                traceableItems.Sort(axisSorter);
                List<IRayTraceable> leftItems = new List<IRayTraceable>(bestIndexToSplitOn + 1);
                List<IRayTraceable> rightItems = new List<IRayTraceable>(numItems - bestIndexToSplitOn + 1);
                for (int i = 0; i <= bestIndexToSplitOn; i++)
                {
                    leftItems.Add(traceableItems[i]);
                }
                for (int i = bestIndexToSplitOn + 1; i < numItems; i++)
                {
                    rightItems.Add(traceableItems[i]);
                }
                BoundingVolumeHierarchy newBVHNode = new BoundingVolumeHierarchy(CreateNewHierachy(leftItems), CreateNewHierachy(rightItems), bestAxis);
                return newBVHNode;
            }
        }
    }
}
